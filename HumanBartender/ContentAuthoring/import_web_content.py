#!/usr/bin/env python3
"""Publish a pinned, validated web CSV snapshot to Unity.

Web bytes are verified against the pinned Git commit as-is. Only the published copy gets readable structural IDs
(readable_ids.py); values, order and CSV formatting stay identical, and the assembled game data must match exactly."""
import argparse
import concurrent.futures
import csv
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import tempfile
import urllib.request

import export_content
import readable_ids

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent
REPOSITORY = 'junseo874/junseo874.github.io'
SOURCE_PATH = 'bar-playtest/data/shared-csv'
LOCK = HERE / 'web_content_source.json'
CONTRACT = HERE / 'web_runtime_contract.json'
ID_MAP = HERE / 'web_id_map.json'
TARGET = PROJECT / 'Assets/StreamingAssets/csv'


def safe_path(value):
    path = PurePosixPath(value)
    if (not value or path.is_absolute() or '..' in path.parts or
            '\\' in value or ':' in value or not value.endswith('.csv')):
        raise ValueError('Invalid source CSV path: ' + value)
    return path


def snapshot(folder):
    paths = ['system/table_index.csv', 'system/columns.csv',
             'system/data_sets.csv', 'system/row_formats.csv']
    with (folder / paths[0]).open(encoding='utf-8-sig', newline='') as stream:
        paths += [str(safe_path(row['path'])) for row in csv.DictReader(stream)]
    return {path: (folder / path).read_bytes() for path in dict.fromkeys(paths)}


def validate(folder):
    manifest = export_content.load_manifest(folder / 'system')
    tables = export_content.read_csv_tables(folder, manifest)
    documents = export_content.assemble(manifest, tables)
    for dataset, document in documents.items():
        if isinstance(document, list):
            ids = [row['id'] for row in document if isinstance(row, dict) and row.get('id')]
            if len(ids) != len(set(ids)):
                raise ValueError('Duplicate game id in ' + dataset)
        if not dataset.startswith('script/'):
            continue
        scenes = document.get('scenes', [])
        ids = [scene['id'] for scene in scenes]
        if len(ids) != len(set(ids)):
            raise ValueError('Duplicate scene id in ' + dataset)
        for scene in scenes:
            seq = [step.get('seq', 0) for step in scene.get('steps', [])]
            if len([n for n in seq if n]) != len(set(n for n in seq if n)):
                raise ValueError('Duplicate step seq: ' + dataset + '/' + scene['id'])
            if dataset.startswith('script/bar/') and seq != sorted(seq):
                raise ValueError('Bar source_order and seq disagree: ' + scene['id'])
        if dataset.startswith('script/bar/'):
            groups = document.get('choices', {})
            for scene in scenes:
                for step in scene.get('steps', []):
                    if step['type'] == 'choice' and step.get('arg') not in groups:
                        raise ValueError('Missing choice group: ' + str(step.get('arg')))
            for options in groups.values():
                for option in options:
                    if option.get('goto') and option['goto'] not in ids:
                        raise ValueError('Missing goto scene: ' + option['goto'])
    return documents


def readable(raw, previous=None):
    """Rename web hash IDs for the Unity copy and prove the assembled game data did not change."""
    published, names, _ = readable_ids.normalize(raw, previous)
    with tempfile.TemporaryDirectory(prefix='bartender_readable_csv_') as work:
        source, renamed = Path(work) / 'source', Path(work) / 'renamed'
        for root, files in ((source, raw), (renamed, published)):
            for name, data in files.items():
                (root / name).parent.mkdir(parents=True, exist_ok=True)
                (root / name).write_bytes(data)
        if not export_content.same_json(validate(source), validate(renamed)):
            raise ValueError('Readable ID conversion changed the assembled game data')
    return published, names


def load_id_map(path=ID_MAP):
    return json.loads(path.read_text(encoding='utf-8')) if path.exists() else None


def download(commit, folder):
    base = f'https://raw.githubusercontent.com/{REPOSITORY}/{commit}/{SOURCE_PATH}/'

    def fetch(path):
        dest = folder / path
        dest.parent.mkdir(parents=True, exist_ok=True)
        with urllib.request.urlopen(base + path, timeout=45) as response:
            dest.write_bytes(response.read())

    fetch('system/table_index.csv')
    with (folder / 'system/table_index.csv').open(encoding='utf-8-sig', newline='') as stream:
        paths = [str(safe_path(row['path'])) for row in csv.DictReader(stream)]
    paths += ['system/columns.csv', 'system/data_sets.csv', 'system/row_formats.csv']
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        list(pool.map(fetch, dict.fromkeys(paths)))
    # Retain projection references as source metadata, never as a second game-state owner.
    fetch('web-views.json')


def verify_runtime_contract(tree, contract):
    actual = {entry['path']: entry['sha'] for entry in tree['tree'] if entry['type'] == 'blob'}
    changed = [name for name, blob in contract['files'].items() if actual.get(name) != blob]
    if changed:
        raise ValueError('Web execution contract changed; review native Unity handlers before importing: ' + ', '.join(changed))


def require_compatibility(report, incoming):
    expected = {name: hashlib.sha256(data).hexdigest() for name, data in incoming.items()}
    if report.get('success') is not True or report.get('files') != expected:
        raise ValueError('Missing or stale Unity compatibility report; validate these exact CSV bytes before --apply')


def verify_revision(folder, commit):
    """An --input directory must not be able to claim another commit's provenance."""
    url = f'https://api.github.com/repos/{REPOSITORY}/git/trees/{commit}?recursive=1'
    with urllib.request.urlopen(url, timeout=45) as response:
        tree = json.load(response)
    if tree.get('truncated'):
        raise ValueError('Cannot verify a truncated source tree')
    verify_runtime_contract(tree, json.loads(CONTRACT.read_text(encoding='utf-8')))
    expected = {entry['path'][len(SOURCE_PATH) + 1:]: entry['sha'] for entry in tree['tree']
                if entry['type'] == 'blob' and entry['path'].startswith(SOURCE_PATH + '/')}
    for name, data in snapshot(folder).items():
        blob = hashlib.sha1(b'blob ' + str(len(data)).encode('ascii') + b'\0' + data).hexdigest()
        if expected.get(name) != blob:
            raise ValueError('Staged CSV does not match pinned Git commit: ' + name)


def publish(folder, target, commit, lock_path=LOCK, id_map_path=None):
    """Write the Unity copy. With id_map_path, IDs become readable and the hash-to-name map is kept beside the lock."""
    documents = validate(folder)
    source = snapshot(folder)
    if id_map_path is None:
        incoming, names = source, None
    else:
        incoming, names = readable(source, load_id_map(id_map_path))
    target = target.resolve()
    target.mkdir(parents=True, exist_ok=True)
    old_lock = json.loads(lock_path.read_text(encoding='utf-8')) if lock_path.exists() else None
    if old_lock:
        for name, expected in old_lock['files'].items():
            current = target / safe_path(name)
            if not current.is_file() or hashlib.sha256(current.read_bytes()).hexdigest() != expected:
                raise ValueError('Local published CSV changed; reconcile before import: ' + name)
    # Keep a rollback copy until every CSV and the lock have been replaced successfully.
    previous = {name: (target / name).read_bytes() if (target / name).exists() else None
                for name in incoming}
    old_manifest = lock_path.read_bytes() if lock_path.exists() else None
    old_map = id_map_path.read_bytes() if id_map_path is not None and id_map_path.exists() else None
    written = []
    manifest_written = False
    manifest_temp = lock_path.with_name(lock_path.name + '.import-tmp')
    try:
        for name, data in incoming.items():
            dest = target / name
            actual = dest.read_bytes() if dest.exists() else None
            if actual != previous[name]:
                raise ValueError('Concurrent edit during publication: ' + name)
            dest.parent.mkdir(parents=True, exist_ok=True)
            temp = dest.with_suffix('.csv.import-tmp')
            temp.write_bytes(data)
            os.replace(temp, dest)
            written.append(name)
        record = {'repository': REPOSITORY, 'commit': commit, 'source_path': SOURCE_PATH,
                  'direction': 'web-to-unity',
                  'files': {name: hashlib.sha256(data).hexdigest() for name, data in incoming.items()}}
        if names is not None:
            # files: Unity에 게시한 파일(읽기 쉬운 ID). web_files: 같은 커밋의 웹 원본. 둘의 조립 결과는 같다.
            record['ids'] = 'readable'
            record['web_files'] = {name: hashlib.sha256(data).hexdigest() for name, data in source.items()}
            id_map_path.write_text(json.dumps(names, ensure_ascii=False, indent=1, sort_keys=True) + '\n', encoding='utf-8')
        lock_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_temp.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        os.replace(manifest_temp, lock_path)
        manifest_written = True
        if snapshot(target) != incoming:
            raise ValueError('Publication verification failed')
    except BaseException:
        for name in reversed(written):
            data = previous[name]
            dest = target / name
            if data is None:
                dest.unlink(missing_ok=True)
            else:
                dest.write_bytes(data)
        if manifest_written:
            if old_manifest is None:
                lock_path.unlink(missing_ok=True)
            else:
                lock_path.write_bytes(old_manifest)
        if id_map_path is not None:
            if old_map is None:
                id_map_path.unlink(missing_ok=True)
            else:
                id_map_path.write_bytes(old_map)
        raise
    finally:
        manifest_temp.unlink(missing_ok=True)
        for name in incoming:
            (target / name).with_suffix('.csv.import-tmp').unlink(missing_ok=True)
    return len(incoming), len(documents)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--commit', help='Full immutable Git commit SHA; defaults to the pinned lock')
    parser.add_argument('--input', type=Path, help='Already downloaded shared-csv directory')
    parser.add_argument('--stage', type=Path, help='Write the Unity-ready CSVs (readable IDs) here for Unity validation')
    parser.add_argument('--out', type=Path, default=TARGET)
    parser.add_argument('--compatibility-report', type=Path, help='Successful CsvContentValidator.ValidateBatch receipt for the exact input files')
    parser.add_argument('--apply', action='store_true', help='Publish after validation (otherwise read-only)')
    args = parser.parse_args()
    commit = args.commit or (json.loads(LOCK.read_text(encoding='utf-8'))['commit'] if LOCK.exists() else '')
    if not re.fullmatch(r'[0-9a-f]{40}', commit):
        parser.error('--commit must be a full immutable Git commit SHA')
    if args.apply and not args.compatibility_report:
        parser.error('--apply requires --compatibility-report; use CsvContentValidator.ValidateBatch with -csvValidationReport')
    if args.stage:
        for target in (TARGET.resolve(), args.out.resolve()):
            if args.stage.resolve().is_relative_to(target) or target.is_relative_to(args.stage.resolve()):
                parser.error('--stage must be separate from the publication directory')
    default_out = args.out.resolve() == TARGET.resolve()
    lock_path = LOCK if default_out else args.out / 'web_content_source.json'
    id_map_path = ID_MAP if default_out else args.out / 'web_id_map.json'
    with tempfile.TemporaryDirectory(prefix='bartender_web_csv_') as work:
        folder = args.input.resolve() if args.input else Path(work)
        if not args.input:
            download(commit, folder)
        verify_revision(folder, commit)
        documents = validate(folder)
        files, _ = readable(snapshot(folder), load_id_map(id_map_path))
        changed = [p for p, data in files.items()
                   if not (args.out / p).is_file() or (args.out / p).read_bytes() != data]
        print(f'{commit}: {len(documents)} datasets, {len(files)} CSVs, {len(changed)} changed files')
        if args.stage:
            for name, data in files.items():
                (args.stage / name).parent.mkdir(parents=True, exist_ok=True)
                (args.stage / name).write_bytes(data)
        if args.apply:
            require_compatibility(json.loads(args.compatibility_report.read_text(encoding='utf-8')), files)
            print('Published:', publish(folder, args.out, commit, lock_path, id_map_path))
        else:
            print('Read-only validation; use --apply after Unity compatibility checks.')
            if args.stage:
                print('Unity -csvContentPath:', args.stage.resolve())


if __name__ == '__main__':
    main()
