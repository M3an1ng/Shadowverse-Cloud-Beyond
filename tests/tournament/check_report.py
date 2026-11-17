"""Verify the workbook layout and statistics downloaded from the tournament tests."""

from pathlib import Path
import json
import sys
import warnings
from zipfile import ZipFile

from openpyxl import load_workbook

warnings.filterwarnings("ignore", message=".*extension is not supported.*", category=UserWarning)
TABS = ["选手卡组（初赛）", "选手卡组 (决赛)", "职业统计", "卡组统计", "对阵表"]
METRICS = ["卡组名", "初赛数量", "决赛数量", "对局数", "胜场数", "胜率", "大局数", "ban次数", "ban率"]


def data_rows(sheet):
    headings = [cell.value for cell in sheet[1]]
    return [dict(zip(headings, row)) for row in sheet.iter_rows(min_row=2, values_only=True)]


def read_report(path):
    with ZipFile(path) as package:
        assert package.testzip() is None
    workbook = load_workbook(path)
    assert workbook.sheetnames == TABS
    for sheet in workbook:
        assert sheet.freeze_panes is None
        assert sheet.sheet_view.showGridLines is None
        assert sheet["A1"].alignment.horizontal == "center"
        assert all(cell.data_type not in ("e", "f") for row in sheet for cell in row), sheet.title
    return workbook


def main(folder):
    early = read_report(folder / "report-open-series.xlsx")
    assert early["对阵表"].max_row == 1
    assert early[TABS[1]].max_row == 1
    formula_label = next(cell for row in early["卡组统计"] for cell in row if cell.value == "=1+1")
    assert formula_label.data_type == "s"
    early.close()

    report = read_report(folder / "report-sample.xlsx")
    for lineup_tab in TABS[:2]:
        sheet = report[lineup_tab]
        assert [cell.value for cell in sheet[1]] == ["选手", "卡组1", "卡组2", "卡组3", "卡组列表"]
        lineups = data_rows(sheet)
        assert len(lineups) == 3 and all(row["卡组1"] for row in lineups)
        assert sheet["B2"].fill.fgColor.rgb == "FF92D050"
        assert sheet.auto_filter.ref is None
        assert all(row["卡组列表"] == "、".join(row[key] for key in ("卡组1", "卡组2", "卡组3") if row[key]) for row in lineups)
    assert all(row["卡组2"] is None and row["卡组3"] is None for row in data_rows(report[TABS[1]]))

    metrics = report["卡组统计"]
    assert metrics["A1"].fill.fgColor.type == "theme" and metrics["A1"].fill.fgColor.theme == 1
    assert [cell.value for cell in metrics[1]] == [*METRICS, "可 Ban 大局"]
    assert metrics.auto_filter.ref == f"A1:I{metrics.max_row}"
    summary = data_rows(metrics)
    assert [sum(row[key] for row in summary) for key in ("初赛数量", "决赛数量", "对局数", "胜场数", "ban次数")] == [9, 3, 8, 4, 2]
    assert all(row["ban率"] == row["ban次数"] / row["可 Ban 大局"] for row in summary if row["可 Ban 大局"])
    rate_cells = [cell for (cell,) in metrics.iter_rows(min_row=2, min_col=6, max_col=6) if isinstance(cell.value, float)]
    assert rate_cells and all(cell.number_format == "0.00%" for cell in rate_cells)

    pairings = report["对阵表"]
    assert [cell.value for cell in pairings[1]] == [
        "玩家1", "卡组1", "卡组2", "玩家2", "胜利卡组", "失败卡组", "禁用卡组1", "禁用卡组2", "玩家1卡组列表", "玩家2卡组列表", "备注",
    ]
    assert pairings.auto_filter.ref is None
    assert set(map(str, pairings.merged_cells.ranges)) == {f"{column}2:{column}3" for column in "ADGHIJK"}
    assert pairings["G2"].value in pairings["I2"].value.split("、")
    assert pairings["H2"].value in pairings["J2"].value.split("、")
    assert pairings["B3"].value and pairings["C3"].value and pairings["E3"].value
    assert pairings["B2"].border.top.style == "medium"
    assert pairings["B3"].border.top.style is None
    assert pairings["B3"].border.bottom.style == "medium"
    assert len({pairings.row_dimensions[row].height for row in range(2, pairings.max_row + 1)}) == 1
    assert len(data_rows(pairings)) == 4
    report.close()

    same = read_report(folder / "report-both-ban.xlsx")
    assert [cell.value for cell in same["卡组统计"][1]] == METRICS
    assert same["职业统计"].max_column == 9
    assert sum(row["ban次数"] for row in data_rows(same["卡组统计"])) == 4
    assert all(row["ban率"] == row["ban次数"] / row["大局数"] for row in data_rows(same["卡组统计"]) if row["大局数"])
    games = same["对阵表"]
    assert len(data_rows(games)) == 4
    expected = json.loads((folder / "report-matchup-expected.json").read_text(encoding="utf-8"))
    assert (games["A2"].value, games["D2"].value) == (expected["winner"], expected["loser"])
    assert (games["G2"].value, games["H2"].value) == (expected["winnerBan"], expected["loserBan"])
    assert games["A2"].font.color.rgb == "FF00B050"
    assert games["K2"].value == "疾驰妖内战" + expected["winner"] + "胜"
    assert len({games.row_dimensions[row].height for row in range(2, games.max_row + 1)}) == 1
    same.close()
    print("Exact reference sheets, conditional ban denominator, grouped matches and typed Excel percentages passed.")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
