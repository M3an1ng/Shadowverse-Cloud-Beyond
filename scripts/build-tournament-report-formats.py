"""Refresh the formatting-only styles used by tournament exports from the historical report folder."""

from copy import deepcopy
from pathlib import Path
from zipfile import ZipFile
import json
import sys
import warnings
import xml.etree.ElementTree as ET
from openpyxl import load_workbook
from openpyxl.utils.cell import get_column_letter, range_boundaries

warnings.simplefilter('ignore', UserWarning)
main = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
drawing = 'http://schemas.openxmlformats.org/drawingml/2006/main'
spreadsheet_extensions = 'http://schemas.microsoft.com/office/spreadsheetml/2009/9/main'
excel = 'http://schemas.microsoft.com/office/excel/2006/main'
ET.register_namespace('', main)
ET.register_namespace('a', drawing)
def tag(name):
    return '{' + main + '}' + name

if len(sys.argv) != 2:
    raise SystemExit("usage: python scripts/build-tournament-report-formats.py <historical-report-directory>")

repo = Path(__file__).resolve().parent.parent
references = Path(sys.argv[1])
output = repo / 'Server/Services/TournamentReportFormats.json'
with ZipFile(references / 'EOT大赛.xlsx') as archive:
    styles = ET.fromstring(archive.read('xl/styles.xml'))
styles.attrib.clear()
primary_sections = {'fonts', 'fills', 'borders', 'cellStyleXfs', 'cellXfs', 'cellStyles'}
for section in list(styles):
    if section.tag not in {tag(name) for name in primary_sections}:
        styles.remove(section)
for name, keep in [('fonts', 1), ('fills', 2), ('borders', 1), ('cellStyleXfs', 1), ('cellXfs', 1), ('cellStyles', 1)]:
    section = styles.find(tag(name))
    for extra in list(section)[keep:]:
        section.remove(extra)
    section.set('count', str(keep))

def nodes(root, name):
    return list(root.find(tag(name)))

def add_style_part(name, part):
    section = styles.find(tag(name))
    key = signature(part)
    for index, existing in enumerate(section):
        if signature(existing) == key:
            return index
    index = len(section)
    section.append(deepcopy(part))
    section.set('count', str(index + 1))
    return index

# Ignore serialization-only whitespace while preserving every formatting attribute.
def signature(node):
    return (node.tag, tuple(sorted(node.attrib.items())), (node.text or '').strip(), tuple(map(signature, node)))

templates = [
    ('Plaza', 'EOT小赛.xlsx', [('Lineups','选手卡组',8), ('Class','职业统计',5), ('Games','对阵表',5)]),
    ('Major', 'EOT大赛.xlsx', [('Lineups','选手卡组（初赛）',5), ('FinalLineups','选手卡组 (决赛)',5), ('Class','职业统计',9), ('Archetype','卡组统计',9), ('Games','对阵表',11)]),
    ('Final', 'SOJ大赛-决赛.xlsx', [('Lineups','选手卡组 ',5), ('Class','职业统计',8), ('Archetype','卡组统计',8), ('Games','对阵表',11)]),
]
catalog = {}
shared_theme = None
colors = ['妖精','皇家护卫','巫师','龙族','梦魇','主教','超越者']
for format_key, filename, layouts in templates:
    path = references / filename
    book = load_workbook(path, data_only=False)
    with ZipFile(path) as archive:
        theme = archive.read('xl/theme/theme1.xml')
        if shared_theme is None:
            shared_theme = theme
        assert shared_theme == theme, 'Expected the same formatting theme across the three examples'
        source = ET.fromstring(archive.read('xl/styles.xml'))
        assert source.find(tag('numFmts')) is None or not list(source.find(tag('numFmts'))), 'Unexpected custom source number formats'
        maps = {name: [add_style_part(name, x) for x in nodes(source, name)] for name in ('fonts','fills','borders')}
        def import_xf(xf):
            result = deepcopy(xf)
            for attr, name in [('fontId','fonts'),('fillId','fills'),('borderId','borders')]:
                result.set(attr, str(maps[name][int(xf.get(attr,'0'))]))
            return result
        defaults = [add_style_part('cellStyleXfs', import_xf(xf)) for xf in nodes(source,'cellStyleXfs')]
        source_styles = nodes(source, 'cellXfs')
        raw_sheets = {name: ET.fromstring(archive.read(f'xl/worksheets/sheet{ix}.xml'))
                      for ix, name in enumerate(book.sheetnames,1)}
        raw_cell_styles = {
            name: {cell.get('r'): int(cell.get('s','0')) for cell in data.iter(tag('c'))}
            for name, data in raw_sheets.items()
        }
        def style_of(name, coordinate):
            return source_styles[raw_cell_styles[name].get(coordinate,0)]

        palette = {
            cell.value: int(style_of('职业统计', cell.coordinate).get('fillId','0'))
            for cell in book['职业统计']['A'][1:] if cell.value in colors
        }
        assert all(color in palette for color in colors), filename
        marks = {}
        if format_key == 'Plaza':
            for row in book['选手卡组'].iter_rows(min_row=2, min_col=3, max_col=6):
                for cell in row:
                    if cell.value in ('○','×') and cell.value not in marks:
                        marks[cell.value] = int(style_of('选手卡组', cell.coordinate).get('fontId','0'))
            assert len(marks) == 2
        else:
            marks['○'] = int(style_of('对阵表','A2').get('fontId','0'))

        for role, name, count in layouts:
            sheet = book[name]
            raw = raw_sheets[name]
            headers, widths = [], []
            data_columns = next(row for row in raw.iter(tag('row')) if row.get('r') == '2')
            default_height = sheet.sheet_format.defaultRowHeight

            def format_style(xf, fill=None, font=None, closing=None):
                result = import_xf(xf)
                result.set('xfId', str(defaults[int(xf.get('xfId','0'))]))
                if fill is not None:
                    result.set('fillId', str(maps['fills'][fill]))
                    result.set('applyFill','1')
                if font is not None:
                    result.set('fontId', str(maps['fonts'][font]))
                    result.set('applyFont','1')
                if closing is not None:
                    borders = nodes(source,'borders')
                    edge = borders[int(closing.get('borderId','0'))].find(tag('bottom'))
                    if edge is not None:
                        border = deepcopy(borders[int(xf.get('borderId','0'))])
                        previous = border.find(tag('bottom'))
                        position = list(border).index(previous) if previous is not None else len(border)
                        if previous is not None:
                            border.remove(previous)
                        border.insert(position, deepcopy(edge))
                        result.set('borderId', str(add_style_part('borders', border)))
                        result.set('applyBorder','1')
                alignment = result.find(tag('alignment'))
                if alignment is None:
                    alignment = ET.Element(tag('alignment'))
                    result.insert(0, alignment)
                alignment.set('wrapText','1')
                result.set('applyAlignment','1')
                return add_style_part('cellXfs', result)

            def row_format(row, end=None):
                cells, palette_styles = [], []
                for column in range(1, count + 1):
                    letter = get_column_letter(column)
                    base = style_of(name, f'{letter}{row}')
                    closing = style_of(name, f'{letter}{end}') if end is not None else None
                    fill = int(base.get('fillId','0'))
                    # The base style may have been colored for the example player's profession.
                    neutral_fill = 0 if fill in palette.values() else None
                    neutral_font = None
                    if role == 'Games' and format_key != 'Plaza' and column in (1,4):
                        neutral_font = int(style_of(name,'D2').get('fontId','0'))
                    cells.append(format_style(base, neutral_fill, neutral_font, closing))
                    palette_styles.append([
                        *(format_style(base, palette[color], neutral_font, closing) for color in colors),
                        format_style(base, neutral_fill, marks.get('○',neutral_font), closing),
                        format_style(base, neutral_fill, marks.get('×',neutral_font), closing),
                    ])
                return {'Cells':cells, 'Colors':palette_styles}

            for column in range(1, count + 1):
                letter = get_column_letter(column)
                sizing = next((dim.width for dim in sheet.column_dimensions.values() if dim.min is not None
                               and dim.min <= column <= dim.max), sheet.sheet_format.defaultColWidth or 8.43)
                widths.append(sizing)
                headers.append(format_style(style_of(name, f'{letter}1')))

            series = role == 'Games' and format_key != 'Plaza'
            first = min((cell.min_row for cell in sheet.merged_cells.ranges
                         if cell.min_col == cell.max_col == 1 and cell.max_row - cell.min_row == 2), default=2)
            bar = next((rule for rule in raw.iter(tag('cfRule')) if rule.get('type') == 'dataBar'), None)
            bar_extension = None
            if bar is not None:
                extension = next((element for element in raw.findall(f'./{tag("extLst")}/{tag("ext")}')
                                  if element.find(f'{{{spreadsheet_extensions}}}conditionalFormattings') is not None), None)
                if extension is not None:
                    bar_extension = ET.Element(tag('extLst'))
                    bar_extension.append(deepcopy(extension))
                    for target in bar_extension.iter(f'{{{excel}}}sqref'):
                        target.text = "__WIN_COUNT_RANGE__"
            catalog[format_key + role] = {
                'SheetName':name,
                'Widths':widths, 'Headers':headers,
                'DefaultRow':row_format(first),
                'MiddleRow':row_format(first + 1) if series else None,
                'LastRow':row_format(first + 2) if series else None,
                'SingleRow':row_format(first, end=first + 2) if series else None,
                'HeaderHeight':sheet.row_dimensions[1].height or default_height,
                'RowHeight':float(data_columns.get('ht', default_height)),
                'FilterColumns':range_boundaries(sheet.auto_filter.ref)[2] if sheet.auto_filter.ref else 0,
                'ShowGridLines':sheet.sheet_view.showGridLines,
                'DataBarRule':ET.tostring(bar, encoding='unicode') if bar is not None else None,
                'DataBarExtension':ET.tostring(bar_extension, encoding='unicode') if bar_extension is not None else None,
            }
    book.close()

style_xml = ET.tostring(styles, encoding='unicode')
payload = {'Styles':style_xml, 'Theme':shared_theme.decode('utf-8-sig'), 'Tables':catalog}
output.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Extracted {len(catalog)} original table formats and {len(nodes(styles,"cellXfs"))} cell styles. {output.stat().st_size} bytes.')
