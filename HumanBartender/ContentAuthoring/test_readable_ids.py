import unittest

import readable_ids

HASH_PARENT = 'shared_1111111111111111'
HASH_CHILD = 'shared_2222222222222222'
HASH_TEMPLATE_PARENT = 'shared_animationcharacters_aaaaaaaaaaaaaaaa'
HASH_TEMPLATE_CHILD = 'shared_expressions_bbbbbbbbbbbbbbbb'


def csv_bytes(rows, bom=True):
    return readable_ids.write(bom, rows)


def sample(extra_child=None):
    children = [['context', 'expression', 'dataset_id', 'row_id', 'parent_id', 'parent_field', 'source_order', 'template_id'],
                ['', 'default', 'character_anim', HASH_CHILD, HASH_PARENT, '\\N', '0', HASH_TEMPLATE_CHILD]]
    if extra_child:
        children.append(extra_child)
    return {
        'system/table_index.csv': csv_bytes([['table', 'path'], ['AnimationCharacters', 'characters/a.csv'],
                                             ['Expressions', 'characters/e.csv']]),
        'system/row_formats.csv': csv_bytes([
            ['template_id', 'node_id', 'row_id', 'owner'],
            [HASH_TEMPLATE_PARENT, '0', '', 'AnimationCharacters'],
            [HASH_TEMPLATE_CHILD, '0', '', 'Expressions'],
            ['source_x', '0', HASH_PARENT, '']], bom=False),
        'system/data_sets.csv': csv_bytes([['dataset_id', 'template_id'], ['character_anim', 'source_x']]),
        'characters/a.csv': csv_bytes([['character', 'dataset_id', 'row_id', 'parent_id', 'source_order', 'template_id'],
                                       ['tom', 'character_anim', HASH_PARENT, '@character_anim', '0', HASH_TEMPLATE_PARENT]]),
        'characters/e.csv': csv_bytes(children),
    }


def rows(files, path):
    return readable_ids.read(files[path])[1]


class ReadableIdTests(unittest.TestCase):
    def test_hash_ids_get_table_parent_and_key_names_and_context_is_filled(self):
        out, names, _ = readable_ids.normalize(sample())
        child = rows(out, 'characters/e.csv')[1]
        self.assertEqual(child[0], 'tom')
        self.assertEqual(child[3], 'expressions_tom_default')
        self.assertEqual(child[4], 'animation_characters_tom')
        self.assertEqual(child[7], 'expressions_format_01')
        # 다른 표가 참조하는 행 ID도 함께 바뀐다.
        self.assertEqual(rows(out, 'system/row_formats.csv')[3][2], 'animation_characters_tom')
        self.assertFalse(any(value.startswith('shared_') for f in out.values() for value in f.decode('utf-8-sig').split('"')))

    def test_csv_formatting_and_non_id_values_are_preserved(self):
        source = sample()
        out, _, _ = readable_ids.normalize(source)
        self.assertTrue(out['characters/e.csv'].startswith(b'\xef\xbb\xbf'))
        self.assertFalse(out['system/row_formats.csv'].startswith(b'\xef\xbb\xbf'))
        self.assertEqual(rows(out, 'characters/a.csv')[1][0], 'tom')
        self.assertEqual(rows(out, 'system/data_sets.csv'), rows(source, 'system/data_sets.csv'))

    def test_known_hashes_keep_their_names_when_new_rows_arrive(self):
        _, first, _ = readable_ids.normalize(sample())
        extra = ['', 'default', 'character_anim', 'shared_3333333333333333', HASH_PARENT, '\\N', '1', HASH_TEMPLATE_CHILD]
        out, second, _ = readable_ids.normalize(sample(extra), first)
        self.assertEqual(second['rows'][HASH_CHILD], first['rows'][HASH_CHILD])
        self.assertEqual(rows(out, 'characters/e.csv')[2][3], 'expressions_tom_default_2')

    def test_readable_web_ids_are_left_untouched(self):
        files = sample()
        text = files['characters/e.csv'].decode('utf-8-sig').replace(HASH_CHILD, 'already_readable')
        files['characters/e.csv'] = b'\xef\xbb\xbf' + text.encode('utf-8')
        out, names, _ = readable_ids.normalize(files)
        self.assertEqual(rows(out, 'characters/e.csv')[1][3], 'already_readable')
        self.assertNotIn('already_readable', names['rows'])

    def test_author_context_is_not_overwritten(self):
        files = sample()
        text = files['characters/e.csv'].decode('utf-8-sig').replace('"","default"', '"custom","default"')
        files['characters/e.csv'] = b'\xef\xbb\xbf' + text.encode('utf-8')
        out, _, _ = readable_ids.normalize(files)
        self.assertEqual(rows(out, 'characters/e.csv')[1][0], 'custom')


if __name__ == '__main__':
    unittest.main()
