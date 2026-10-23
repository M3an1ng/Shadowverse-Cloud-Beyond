import importlib.util
from pathlib import Path
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / 'check-docs.py'
SPEC = importlib.util.spec_from_file_location('check_docs', SCRIPT)
check_docs = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(check_docs)


class DocumentationParserTests(unittest.TestCase):
    def test_local_links_include_unicode_and_parentheses(self):
        text = '[归档](目录/方案(AutoMapper).md) [索引](../README.md)'
        self.assertEqual(list(check_docs.local_links(text)), ['目录/方案(AutoMapper).md', '../README.md'])

    def test_angle_links_and_optional_titles(self):
        text = '[说明](<目录/文件 名.md>) [标题](README.md "文档")'
        self.assertEqual(list(check_docs.local_links(text)), ['目录/文件 名.md', 'README.md'])

    def test_external_links_are_not_local_files(self):
        text = '[a](https://example.com) [b](mailto:user@example.com) [c](//example.com/a)'
        self.assertEqual(list(check_docs.local_links(text)), [])

    def test_code_examples_are_ignored(self):
        text = '```md\n[示例](missing.md)\n```\n`[示例](missing.md)`\n[实际](README.md)'
        self.assertEqual(list(check_docs.local_links(text)), ['README.md'])

    def test_long_outer_fence_keeps_inner_example_hidden(self):
        text = '````\n```\n[示例](missing.md)\n```\n````\n[实际](README.md)'
        self.assertEqual(list(check_docs.local_links(text)), ['README.md'])

    def test_chinese_anchors_and_duplicates(self):
        text = '# 当前文档\n## API 与数据\n## API 与数据\n'
        self.assertEqual(check_docs.anchors(text), {'当前文档', 'api-与数据', 'api-与数据-1'})

    def test_code_headings_are_ignored(self):
        self.assertEqual(check_docs.anchors('~~~\n# example\n~~~\n# Actual'), {'actual'})


if __name__ == '__main__':
    unittest.main()
