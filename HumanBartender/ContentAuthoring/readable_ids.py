#!/usr/bin/env python3
"""Give web-exported structural IDs readable names before the CSVs are published to Unity.

웹 내보내기는 행 연결 ID(row_id·parent_id)와 행 형식 ID(template_id)를 `shared_<해시>`로 만들고 `context`를 비운다.
게임은 이 ID를 연결 키로만 쓰므로 이름을 바꿔도 동작은 같다. 사람이 CSV를 읽고 고칠 수 있도록 반입 때만
`expressions_tom_default`처럼 표·부모·키로 이름을 만들고, 비어 있는 `context`에 부모 경로를 채운다.

- 이름 규칙: `<표 이름>_<부모 경로>_<행 키>`. 행 키 열은 TABLE_KEYS가 소유한다.
- 같은 해시는 항상 같은 이름이 되도록 대응표(web_id_map.json)를 유지한다. 새 해시만 새 이름을 받는다.
- 행 ID는 16자리 해시가 들어 있는 `shared_…`만, 행 형식 ID는 `shared_…` 전부를 바꾼다.
  `shared_neighborhood_home_cat`처럼 웹이 사람이 읽게 지은 행 ID는 그대로 둔다.
- 값·순서·행 수·CSV 서식(BOM, 따옴표, 줄바꿈)은 바꾸지 않는다. 바꾸는 열은 row_id·parent_id·template_id·context뿐이다.
"""
import csv
import io
import re

HASH_ROW = re.compile(r'^shared_(?:.*_)?[0-9a-f]{16}(?:_.*)?$')
WEB_TEMPLATE = re.compile(r'^shared_')
ID_COLUMNS = ('row_id', 'parent_id', 'template_id')

# 표마다 행을 구별하는 열. '#'은 같은 부모 안의 저장 순서(source_order), 'a|b'는 앞의 값이 비면 뒤의 값을 쓴다.
# 여기에 없는 새 표는 id가 있으면 id, 없으면 저장 순서를 쓴다.
TABLE_KEYS = {
    'BalanceRoot': [], 'BalanceConfig': ['setting'], 'GradeCuts': ['grade'], 'SettlementRules': ['setting'],
    'ScoreBands': ['band_type', '#'], 'AffinityMatrix': ['taste_tier'],
    'BarkSituations': ['situation'], 'Barks': ['situation', '#'],
    'AnimationCharacters': ['character'], 'Expressions': ['expression'], 'ExpressionParts': ['part'],
    'Characters': ['id'], 'CharacterExpressions': ['#'], 'Dossier': ['character_id', '#'],
    'FieldAnimations': ['character_id', 'action'],
    'Cocktails': ['id'], 'CocktailTags': ['id|#'], 'Recipes': ['ingredient|#'], 'ShelfItems': ['id'],
    'Tastes': ['character_id', 'seq'], 'OrderRules': ['seq'],
    'Cutscenes': ['id'], 'Days': ['day'], 'Endings': ['id'], 'Transitions': ['id'], 'TextTags': ['tag'],
    'UIStrings': ['string_id'], 'PresentationValues': ['#'],
    'GuestSettings': [], 'GuestParts': ['id'], 'GuestPersonalities': ['#'], 'GuestExclusions': ['a', 'b'],
    'Personalities': ['id'], 'RandomWaves': ['day', 'seq'], 'RegularSlots': ['#'],
    'InteractPoints': ['id'], 'DialogueFlows': ['flow_seq|#'], 'Spots': ['id'],
    'ScriptFiles': ['dataset_id'], 'Scenes': ['id'], 'Steps': ['seq|#'],
    'BarChoiceGroups': ['choice_group'], 'BarChoices': ['seq|#'], 'StreetChoices': ['id|seq|#'],
}
# 게임 전체에서 고유한 id를 키로 쓰는 표는 부모 경로를 다시 시작한다(예: 장면 id 아래의 대사는 `steps_d0_open_5`).
PATH_ROOTS = {'Scenes', 'StreetChoices'}


def snake(name):
    return re.sub(r'(?<=[a-z0-9])(?=[A-Z])', '_', name).lower()


def clean(value):
    value = (value or '').strip()
    if value in ('', '\\N', '\\E'):
        return ''
    return re.sub(r'_+', '_', re.sub(r'[^0-9A-Za-z가-힣]+', '_', value)).strip('_')


def read(data):
    bom = data.startswith(b'\xef\xbb\xbf')
    rows = list(csv.reader(io.StringIO(data.decode('utf-8-sig'), newline='')))
    return bom, rows


def write(bom, rows):
    out = io.StringIO(newline='')
    csv.writer(out, quoting=csv.QUOTE_ALL, lineterminator='\n').writerows(rows)
    return (b'\xef\xbb\xbf' if bom else b'') + out.getvalue().encode('utf-8')


class _Table:
    def __init__(self, path, data):
        self.path = path
        self.bom, rows = read(data)
        if write(self.bom, rows) != data:
            raise ValueError('CSV 서식을 그대로 다시 쓸 수 없습니다: ' + path)
        self.header, self.rows = (rows[0], rows[1:]) if rows else ([], [])
        self.col = {name: i for i, name in enumerate(self.header)}

    def get(self, row, name):
        i = self.col.get(name)
        return row[i] if i is not None and i < len(row) else ''

    def set(self, row, name, value):
        row[self.col[name]] = value

    def dump(self):
        return write(self.bom, [self.header] + self.rows)


def _row_key(table, name, row):
    spec = TABLE_KEYS.get(name, ['id|#'] if 'id' in table.col else ['#'])
    parts = []
    for item in spec:
        for choice in item.split('|'):
            value = clean(table.get(row, 'source_order')) if choice == '#' else clean(table.get(row, choice))
            if value:
                parts.append(value)
                break
    return parts


def normalize(files, previous=None):
    """files: {상대 경로: 웹 원본 바이트}. 반환: (게시할 바이트 사전, 갱신된 대응표, 바뀐 ID 수)."""
    tables = {path: _Table(path, data) for path, data in files.items()}
    index = tables['system/table_index.csv']
    table_paths = {index.get(r, 'table'): index.get(r, 'path') for r in index.rows}
    formats = tables['system/row_formats.csv']
    data_sets = tables['system/data_sets.csv']

    previous = previous or {}
    names = {'templates': dict(previous.get('templates', {})), 'rows': dict(previous.get('rows', {}))}
    used = set(names['templates'].values()) | set(names['rows'].values())

    holders = {}  # 행 이름 → 그 이름을 가진 행의 데이터 묶음

    def claim(kind, old, wanted, qualified=None, dataset=None):
        """wanted를 다른 데이터 묶음의 행이 쓰고 있으면 묶음을 넣은 qualified를, 그래도 겹치면 번호를 붙인다."""
        if old in names[kind]:
            return names[kind][old]
        if wanted in used and qualified and holders.get(wanted) != dataset:
            wanted = qualified
        name, n = wanted, 2
        while name in used:
            name = f'{wanted}_{n}'
            n += 1
        used.add(name)
        names[kind][old] = name
        if dataset is not None:
            holders[name] = dataset
        return name

    # 행 형식: 표 이름별로 처음 나온 순서대로 `<표>_format_NN`, 데이터 묶음 최상위 형식은 `source_<묶음>`.
    owners, order = {}, []
    for row in formats.rows:
        template = formats.get(row, 'template_id')
        if template not in owners:
            owners[template] = ''
            order.append(template)
        if formats.get(row, 'node_id') == '0' and formats.get(row, 'owner'):
            owners[template] = formats.get(row, 'owner')
    first_dataset = {}
    for row in data_sets.rows:
        first_dataset.setdefault(data_sets.get(row, 'template_id'), data_sets.get(row, 'dataset_id'))
    counters = {}
    for template in order:
        if not WEB_TEMPLATE.match(template) or template in names['templates']:
            continue
        owner = owners[template]
        if owner:
            # 대응표에 이미 있는 번호는 건너뛴다. 새 형식이 기존 이름 뒤에 `_2`를 달지 않게 한다.
            counters[owner] = counters.get(owner, 0) + 1
            while f'{snake(owner)}_format_{counters[owner]:02d}' in used:
                counters[owner] += 1
            wanted = f'{snake(owner)}_format_{counters[owner]:02d}'
        elif template in first_dataset:
            wanted = 'source_' + clean(first_dataset[template]).lower()
        else:
            wanted = 'format'
        claim('templates', template, wanted)

    # 행: 먼저 모든 행의 키를 모은 뒤 부모를 따라 경로를 만든다(부모가 다른 표에 있어도 된다).
    rows = {}
    for name, path in table_paths.items():
        table = tables.get(path)
        if table is None or 'row_id' not in table.col:
            continue
        for row in table.rows:
            rows[table.get(row, 'row_id')] = (name, table, row)

    paths = {}

    def path_of(row_id, depth=0):
        if row_id in paths:
            return paths[row_id]
        if depth > 64 or row_id not in rows:
            return []
        name, table, row = rows[row_id]
        parent = table.get(row, 'parent_id')
        prefix = [] if name in PATH_ROOTS or not parent or parent.startswith('@') else path_of(parent, depth + 1)
        paths[row_id] = prefix + _row_key(table, name, row)
        return paths[row_id]

    # 같은 키가 다른 데이터 묶음에도 있으면(예: 같은 캐릭터가 두 묶음에 있음) 묶음 이름으로 구별한다.
    for row_id, (name, table, row) in rows.items():
        if row_id in names['rows']:
            holders[names['rows'][row_id]] = clean(table.get(row, 'dataset_id')).lower()
    for row_id, (name, table, row) in rows.items():
        if HASH_ROW.match(row_id):
            path = path_of(row_id)
            dataset = clean(table.get(row, 'dataset_id')).lower()
            claim('rows', row_id, '_'.join([snake(name)] + path), '_'.join([snake(name), dataset] + path), dataset)

    # 비어 있는 context에 부모 경로를 채운다. 작성자가 넣은 값은 덮어쓰지 않는다(이름을 바꾸기 전의 부모로 계산).
    contexts = []
    for row_id, (name, table, row) in rows.items():
        parent = table.get(row, 'parent_id')
        if 'context' in table.col and not table.get(row, 'context') and parent in rows:
            context = '/'.join(path_of(parent))
            if context:
                contexts.append((table, row, context))

    def rename(value):
        return names['rows'].get(value) or names['templates'].get(value) or value

    changed = 0
    for table in tables.values():
        for row in table.rows:
            for column in ID_COLUMNS:
                if column in table.col:
                    old = table.get(row, column)
                    new = rename(old)
                    if new != old:
                        table.set(row, column, new)
                        changed += 1
    for table, row, context in contexts:
        table.set(row, 'context', context)

    output = {path: table.dump() for path, table in tables.items()}
    leftovers = sorted({table.get(row, column) for table in tables.values() for row in table.rows
                        for column in ID_COLUMNS if column in table.col
                        and (HASH_ROW.match(table.get(row, column)) or column == 'template_id' and WEB_TEMPLATE.match(table.get(row, column)))})
    if leftovers:
        raise ValueError('이름을 붙이지 못한 해시 ID가 남았습니다: ' + ', '.join(leftovers[:5]))
    return output, names, changed
