import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import import_web_content as importer


class PublicationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'
        self.target = self.root / 'target'
        self.lock = self.root / 'source.json'
        for folder in (self.source, self.target):
            (folder / 'system').mkdir(parents=True)
            for name in ('columns', 'data_sets', 'row_formats'):
                (folder / f'system/{name}.csv').write_bytes(b'header\r\n')
            (folder / 'system/table_index.csv').write_bytes(b'table,path\r\nValues,values.csv\r\n')
            (folder / 'values.csv').write_bytes(b'id,value\r\n1,old\r\n')
        (self.source / 'values.csv').write_bytes(b'\xef\xbb\xbfid,value\r\n1,"latest"\r\n')

    def tearDown(self):
        self.temp.cleanup()

    def test_published_csv_is_byte_identical_and_lock_covers_every_file(self):
        with patch.object(importer, 'validate', return_value={'values': []}):
            importer.publish(self.source, self.target, 'a' * 40, self.lock)
        self.assertEqual(importer.snapshot(self.source), importer.snapshot(self.target))
        lock = json.loads(self.lock.read_text())
        for name, value in importer.snapshot(self.source).items():
            self.assertEqual(lock['files'][name], hashlib.sha256(value).hexdigest())

    def test_local_edits_are_not_overwritten(self):
        with patch.object(importer, 'validate', return_value={'values': []}):
            importer.publish(self.source, self.target, 'a' * 40, self.lock)
            (self.target / 'values.csv').write_bytes(b'local changes')
            before = importer.snapshot(self.target)
            with self.assertRaisesRegex(ValueError, 'Local published CSV changed'):
                importer.publish(self.source, self.target, 'b' * 40, self.lock)
            self.assertEqual(before, importer.snapshot(self.target))

    def test_invalid_input_cannot_change_target(self):
        before = importer.snapshot(self.target)
        with patch.object(importer, 'validate', side_effect=ValueError('invalid parent')):
            with self.assertRaisesRegex(ValueError, 'invalid parent'):
                importer.publish(self.source, self.target, 'a' * 40, self.lock)
        self.assertEqual(before, importer.snapshot(self.target))
        self.assertFalse(self.lock.exists())

    def test_mid_publication_io_error_restores_previous_contents_and_lock(self):
        before = importer.snapshot(self.target)
        original = importer.os.replace
        calls = 0

        def fail_second(source, target):
            nonlocal calls
            calls += 1
            if calls == 2:
                raise OSError('injected disk error')
            return original(source, target)

        with patch.object(importer, 'validate', return_value={}), patch.object(importer.os, 'replace', side_effect=fail_second):
            with self.assertRaisesRegex(OSError, 'injected disk error'):
                importer.publish(self.source, self.target, 'a' * 40, self.lock)
        self.assertEqual(before, importer.snapshot(self.target))
        self.assertFalse(self.lock.exists())

    def test_metadata_cannot_escape_source_directory(self):
        for path in ('../values.csv', '/values.csv', 'C:/values.csv', 'a\\values.csv'):
            with self.assertRaises(ValueError):
                importer.safe_path(path)

    def test_manifest_replace_error_preserves_existing_lock_and_csv(self):
        with patch.object(importer, 'validate', return_value={}):
            importer.publish(self.source, self.target, 'a' * 40, self.lock)
            before = importer.snapshot(self.target)
            old_lock = self.lock.read_bytes()
            (self.source / 'values.csv').write_bytes(b'id,value\r\n1,new\r\n')
            original = importer.os.replace

            def fail_manifest(source, target):
                if Path(target) == self.lock:
                    raise OSError('injected manifest error')
                return original(source, target)

            with patch.object(importer.os, 'replace', side_effect=fail_manifest):
                with self.assertRaisesRegex(OSError, 'injected manifest error'):
                    importer.publish(self.source, self.target, 'b' * 40, self.lock)
            self.assertEqual(before, importer.snapshot(self.target))
            self.assertEqual(old_lock, self.lock.read_bytes())
            self.assertFalse(self.lock.with_name(self.lock.name + '.import-tmp').exists())

    def test_changed_web_execution_rules_cannot_silently_reuse_native_handlers(self):
        contract = {'files': {'events/shop.js': 'old'}}
        tree = {'tree': [{'type': 'blob', 'path': 'events/shop.js', 'sha': 'new'}]}
        with self.assertRaisesRegex(ValueError, 'execution contract changed'):
            importer.verify_runtime_contract(tree, contract)

    def test_content_only_commit_can_reuse_an_unchanged_execution_contract(self):
        contract = {'files': {'events/shop.js': 'same'}}
        tree = {'tree': [{'type': 'blob', 'path': 'events/shop.js', 'sha': 'same'},
                         {'type': 'blob', 'path': 'story/steps.csv', 'sha': 'new-content'}]}
        importer.verify_runtime_contract(tree, contract)

    def test_changed_csv_cannot_reuse_a_previous_unity_receipt(self):
        data = {'story/steps.csv': b'validated'}
        receipt = {'success': True, 'files': {'story/steps.csv': hashlib.sha256(b'validated').hexdigest()}}
        importer.require_compatibility(receipt, data)
        with self.assertRaisesRegex(ValueError, 'stale Unity compatibility report'):
            importer.require_compatibility(receipt, {'story/steps.csv': b'edited after validation'})


if __name__ == '__main__':
    unittest.main()
