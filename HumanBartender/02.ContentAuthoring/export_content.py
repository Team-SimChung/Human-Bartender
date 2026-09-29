#!/usr/bin/env python3
"""Export the companion authoring workbooks to game-ready CSV tables.

Python 3.9+ standard library only. Use --out to choose the CSV output directory.
CSV schema files describe structure and types; content values come from the tables.
"""
import argparse
import csv
import math
from pathlib import Path
import re
import sys
import shutil
import xml.etree.ElementTree as ET
import zipfile

HERE = Path(__file__).resolve().parent
NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
META = {'context', 'dataset_id', 'row_id', 'parent_id', 'parent_field', 'source_order', 'template_id', 'value_type'}


def load_manifest(folder):
    names = {'_Tables.csv':'table_index.csv','_Columns.csv':'columns.csv','_Sources.csv':'data_sets.csv','_Schemas.csv':'row_formats.csv'}
    def rows(name):
        with (folder / names.get(name, name)).open(encoding='utf-8-sig', newline='') as f:
            return list(csv.DictReader(f))
    tables = {}
    internal_names = set()
    for r in rows('_Tables.csv'):
        tables[r['table']] = {'name': r['table'], 'book': r['book'], 'path':r['path'], 'columns': [], 'types': {}}
        if r['editable'].lower() != 'true':
            internal_names.add(r['table'])
    for r in sorted(rows('_Columns.csv'), key=lambda x: (x['table'], int(x['column_order']))):
        t = tables[r['table']]
        t['columns'].append(r['column'])
        t['types'][r['column']] = r['types'].split('|')
    flat = {}
    for row in rows('_Schemas.csv'):
        flat.setdefault(row['template_id'], []).append(row)
    templates = {}
    def shape(template, node_id='0'):
        data = next(r for r in flat[template] if r['node_id'] == node_id)
        result = {'kind': data['kind']}
        keys = {'cell': ['column','type'], 'record': ['table','row_id'],
                'children': ['table','slot','container','key_column','parent']}.get(data['kind'], [])
        for k in keys:
            if data[k] != '' or k == 'slot':
                result[k] = data[k]
        if data['kind'] == 'object':
            result['fields'] = {r['field']: shape(template, r['node_id']) for r in sorted(flat[template],key=lambda r:int(r['field_order'])) if r['parent_node'] == node_id}
        return result
    for key, nodes in flat.items():
        root = next(r for r in nodes if r['node_id'] == '0')
        if root['owner']:
            templates[key] = {'table': root['owner'], 'shape': shape(key), 'map_key': root['map_key'] or None}
    sources = {r['dataset_id']: {'book': r['book'], 'shape': shape(r['template_id'])} for r in rows('_Sources.csv')}
    internal = read_csv_tables(folder.parent, {'tables': {n: tables[n] for n in internal_names}})
    return {'version': 2, 'header_row': 5, 'books': {'C':'HumanBartender_C_Content.xlsx','Game':'HumanBartender_Game_Settings.xlsx'},
            'tables': {n:t for n,t in tables.items() if n not in internal_names},
            'internal': {n:{**tables[n],'rows':r} for n,r in internal.items()},
            'templates': templates, 'documents': sources}


def column_number(address):
    n = 0
    for c in re.match(r'[A-Z]+', address).group():
        n = n * 26 + ord(c) - 64
    return n - 1


def same_json(a, b):
    """Compare JSON types as well as values (Python otherwise treats True == 1)."""
    if type(a) is not type(b):
        return False
    if isinstance(a, dict):
        return a.keys() == b.keys() and all(same_json(a[k], b[k]) for k in a)
    if isinstance(a, list):
        return len(a) == len(b) and all(same_json(x, y) for x, y in zip(a, b))
    return a == b


def read_xlsx(path, sheets, header_row):
    result = {}
    with zipfile.ZipFile(path) as z:
        strings = []
        if 'xl/sharedStrings.xml' in z.namelist():
            strings = [''.join(n.itertext()) if not n.findall('.//s:t', NS) else ''.join(t.text or '' for t in n.findall('.//s:t', NS))
                       for n in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si', NS)]
        rels = {r.get('Id'): r.get('Target') for r in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
        wb = ET.fromstring(z.read('xl/workbook.xml'))
        paths = {}
        for sh in wb.findall('s:sheets/s:sheet', NS):
            target = rels[sh.get('{' + REL + '}id')]
            paths[sh.get('name')] = target.lstrip('/') if target.startswith('/') else 'xl/' + target
        for name, columns in sheets.items():
            if name not in paths:
                raise ValueError(f'{path.name}: 시트 없음: {name}')
            xml = ET.fromstring(z.read(paths[name]))
            rows = []
            header = None
            for rr in xml.findall('s:sheetData/s:row', NS):
                rn = int(rr.get('r'))
                if rn < header_row:
                    continue
                cells = {}
                for cell in rr.findall('s:c', NS):
                    ci = column_number(cell.get('r'))
                    if ci >= len(columns):
                        if cell.find('s:v', NS) is not None or cell.find('s:is', NS) is not None:
                            raise ValueError(f'{name}!{cell.get("r")}: 정의된 표 밖의 값. 필드를 임의로 추가할 수 없습니다.')
                        continue
                    if cell.find('s:f', NS) is not None:
                        raise ValueError(f'{name}!{cell.get("r")}: 데이터 표의 수식은 지원하지 않습니다. 값으로 입력하세요.')
                    cv = cell.find('s:v', NS)
                    raw = cv.text if cv is not None else None
                    typ = cell.get('t')
                    if typ == 's':
                        value = strings[int(raw)] if raw is not None else None
                    elif typ == 'inlineStr':
                        value = ''.join(t.text or '' for t in cell.findall('s:is//s:t', NS))
                    elif typ == 'b':
                        value = raw == '1'
                    elif typ in ('str', 'e'):
                        if typ == 'e':
                            raise ValueError(f'{name}!{cell.get("r")}: Excel 오류 {raw}')
                        value = raw
                    elif raw is None:
                        value = None
                    else:
                        value = float(raw)
                        if not math.isfinite(value):
                            raise ValueError(f'{name}!{cell.get("r")}: 유한수가 아닙니다.')
                        if value.is_integer():
                            value = int(value)
                    cells[ci] = value
                if rn == header_row:
                    header = [cells.get(i) for i in range(len(columns))]
                    if header != columns:
                        raise ValueError(f'{name}: {header_row}행 헤더가 바뀌었습니다. 원래 열 이름과 순서를 유지하세요.')
                    continue
                row = {c: cells.get(i) for i, c in enumerate(columns)}
                if any(v not in (None, '') for v in row.values()):
                    rows.append(row)
            if header is None:
                raise ValueError(f'{name}: 헤더를 찾지 못했습니다.')
            result[name] = rows
    return result


def csv_value(value):
    if value is None:
        return r'\N'
    if isinstance(value, bool):
        return 'true' if value else 'false'
    return str(value)


def read_csv_tables(folder, manifest):
    result = {}
    for name, spec in manifest['tables'].items():
        with (folder / spec['path']).open(encoding='utf-8-sig', newline='') as f:
            reader = csv.DictReader(f)
            if reader.fieldnames != spec['columns']:
                raise ValueError(f'{name}.csv: 헤더가 스키마와 다릅니다.')
            rows = []
            for r in reader:
                if None in r:
                    raise ValueError(f'{name}.csv: 열 수가 맞지 않습니다.')
                row = {}
                for key, value in r.items():
                    if value in (r'\N', ''):
                        value = None
                    elif spec['types'].get(key) == ['bool']:
                        if value.lower() not in ('true', 'false'):
                            raise ValueError(f'{name}.{key}: true/false가 필요합니다.')
                        value = value.lower() == 'true'
                    elif set(spec['types'].get(key, [])) <= {'int', 'float'}:
                        value = float(value)
                        if value.is_integer():
                            value = int(value)
                    row[key] = value
                rows.append(row)
            result[name] = rows
    return result


def assemble(manifest, data):
    tables = dict(data)
    tables.update({n: t['rows'] for n, t in manifest['internal'].items()})
    by_id = {}
    children = {}
    for name, rows in tables.items():
        for row in rows:
            rid = row.get('row_id')
            template = row.get('template_id')
            if not isinstance(rid, str) or not rid.strip():
                raise ValueError(f'{name}: row_id가 없는 행이 있습니다. 새 행은 기존 행을 복사하고 고유 ID를 입력하세요.')
            if rid in by_id:
                raise ValueError(f'중복 row_id: {rid}')
            if template not in manifest['templates'] or manifest['templates'][template]['table'] != name:
                raise ValueError(f'{name}/{rid}: template_id가 해당 표의 형식과 맞지 않습니다.')
            order = row.get('source_order')
            if isinstance(order, bool) or not isinstance(order, (int, float)) or order < 0 or order != int(order):
                raise ValueError(f'{name}/{rid}: source_order는 0 이상의 정수여야 합니다.')
            by_id[rid] = (name, row)
            key = (name, row.get('parent_id'), row.get('parent_field') or '')
            children.setdefault(key, []).append(row)
    for key, rows in children.items():
        orders = [r['source_order'] for r in rows]
        if len(orders) != len(set(orders)):
            raise ValueError(f'{key}: 같은 부모 아래 source_order가 중복됩니다.')
        rows.sort(key=lambda r: r['source_order'])

    visited = set()
    stack = set()

    def decode(value, kind, table, column):
        if value in (None, '', r'\N'):
            return None
        if value == r'\E':
            return ''
        if value == r'\M':
            raise ValueError('\\M은 지원하지 않습니다. 필드 삭제는 스키마 변경이 필요합니다.')
        if kind == 'NoneType':
            kinds = manifest['tables'].get(table, {}).get('types', {}).get(column, ['str'])
            kind = 'bool' if kinds == ['bool'] else 'float' if set(kinds) <= {'float', 'int'} else 'str'
        if kind == 'str':
            if not isinstance(value, str):
                raise ValueError(f'{table}.{column}: 텍스트 형식으로 입력하세요: {value!r}')
            return value[1:] if value.startswith('\\\\') else value
        if kind == 'bool':
            if isinstance(value, bool):
                return value
            if isinstance(value, str) and value.lower() in ('true', 'false'):
                return value.lower() == 'true'
            raise ValueError(f'{table}.{column}: true/false가 필요합니다.')
        if kind in ('int', 'float'):
            if isinstance(value, bool):
                raise ValueError(f'{table}.{column}: 숫자가 필요합니다.')
            v = float(value)
            if not math.isfinite(v) or (kind == 'int' and v != int(v)):
                raise ValueError(f'{table}.{column}: {kind} 값이 필요합니다.')
            return int(v) if kind == 'int' else v
        raise ValueError(f'지원하지 않는 타입: {kind}')

    def build_row(rid, source):
        if rid in stack:
            raise ValueError(f'부모 연결에 순환이 있습니다: {rid}')
        if rid not in by_id:
            raise ValueError(f'필수 루트 행이 없습니다: {rid}')
        name, row = by_id[rid]
        if row.get('dataset_id') != source:
            raise ValueError(f'{name}/{rid}: 부모와 dataset_id가 다릅니다.')
        spec = manifest['templates'][row['template_id']]
        allowed = set(META)
        if spec.get('map_key'):
            allowed.add(spec['map_key'])
        def collect(node):
            if node['kind'] == 'cell':
                allowed.add(node['column'])
            elif node['kind'] == 'object':
                for child in node['fields'].values():
                    collect(child)
        collect(spec['shape'])
        for col, value in row.items():
            if col not in allowed and value not in (None, ''):
                raise ValueError(f'{name}/{rid}: {col}은 현재 template_id에 없는 필드입니다. 같은 형식의 행을 복사하세요.')
        stack.add(rid)
        visited.add(rid)
        value = build(spec['shape'], rid, row, name, source)
        stack.remove(rid)
        return value

    def build(node, parent, row, table, source):
        kind = node['kind']
        if kind == 'empty_array':
            return []
        if kind == 'constant':
            return node['value']
        if kind == 'record':
            return build_row(node['row_id'], source)
        if kind == 'cell':
            return decode(row.get(node['column']), node['type'], table, node['column'])
        if kind == 'object':
            return {k: build(v, parent, row, table, source) for k, v in node['fields'].items()}
        if kind == 'children':
            key = (node['table'], node.get('parent', parent), node['slot'])
            matched = children.get(key, [])
            values = [build_row(r['row_id'], source) for r in matched]
            if node['container'] == 'list':
                return values
            keys = [r.get(node['key_column']) for r in matched]
            if any(not isinstance(k, str) or not k for k in keys) or len(keys) != len(set(keys)):
                raise ValueError(f'{node["table"]}/{parent}: 사전 키 누락 또는 중복입니다.')
            return dict(zip(keys, values))
        raise ValueError(kind)

    output = {source: build(doc['shape'], None, None, None, source) for source, doc in manifest['documents'].items()}
    orphans = set(by_id) - visited
    if orphans:
        raise ValueError('부모에 연결되지 않은 행: ' + ', '.join(sorted(orphans)[:8]))
    return output


def write_outputs(out, manifest, tables, schema_folder):
    out = out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    for name, spec in manifest['tables'].items():
        target = out / spec['path']
        target.parent.mkdir(parents=True, exist_ok=True)
        temp = target.with_suffix('.csv.tmp')
        with temp.open('w', encoding='utf-8-sig', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=spec['columns'], lineterminator='\r\n')
            writer.writeheader()
            for row in sorted(tables[name], key=lambda r: (r['dataset_id'], r['parent_id'], r.get('parent_field') or '', r['source_order'])):
                writer.writerow({k: csv_value(row.get(k)) for k in spec['columns']})
        temp.replace(target)
    for path in schema_folder.glob('*.csv'):
        target = out / 'system' / path.name
        target.parent.mkdir(parents=True, exist_ok=True)
        if path.resolve() != target.resolve():
            shutil.copyfile(path, target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', choices=['xlsx', 'csv'], default='xlsx')
    parser.add_argument('--input', type=Path, default=HERE, help='엑셀 파일들이 있는 폴더. CSV 모드에서는 CSV 폴더')
    parser.add_argument('--out', type=Path, default=HERE / 'csv')
    parser.add_argument('--validate-only', action='store_true')
    args = parser.parse_args()
    schema_folder = (args.input if args.source == 'csv' else HERE) / 'system'
    manifest = load_manifest(schema_folder)
    if args.source == 'xlsx':
        tables = {}
        for book, filename in manifest['books'].items():
            specs = {n: s['columns'] for n, s in manifest['tables'].items() if s['book'] == book}
            tables.update(read_xlsx(args.input / filename, specs, manifest['header_row']))
    else:
        tables = read_csv_tables(args.input, manifest)
    documents = assemble(manifest, tables)
    if not args.validate_only:
        write_outputs(args.out, manifest, tables, schema_folder)
        print(f'생성 완료: 편집 표 {len(tables)}개 + 구조용 CSV 5개 → {args.out.resolve()}')
    print(f'검증 완료: {len(documents)}개 데이터 묶음')
    print('구조·연결·타입을 검사했습니다. 게임 내 참조 ID·조건식·리소스의 실행 유효성은 별도 검증 대상입니다.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, FileNotFoundError) as error:
        print('내보내기 중단: ' + str(error), file=sys.stderr)
        sys.exit(1)
