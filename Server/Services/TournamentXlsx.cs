using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace ShadowverseBeyond.Server.Services;

internal sealed record TournamentWorksheet(TournamentTableFormat Format, (string Heading, double Width)[] Columns,
    IReadOnlyList<object?[]> Rows, IReadOnlyList<TournamentRowMerge>? Merges = null, bool UniformRowHeight = false);

internal readonly record struct TournamentRowMerge(int Column, int FirstDataRow, int LastDataRow);
internal readonly record struct TournamentPercent(double Value);

// These values index the styles copied from each reference workbook's profession palette.
internal enum TournamentCellColor { Forest, Sword, Rune, Dragon, Abyss, Haven, Portal, Win, Loss }
internal readonly record struct TournamentColoredText(string Text, TournamentCellColor Color);

internal enum TournamentSeriesRow { Single, First, Middle, Last }

internal sealed record TournamentRowFormat(int[] Cells, int[][] Colors)
{
    public int CellStyle(int column, object? value)
    {
        if (column >= Cells.Length) return Cells[1];
        return value is TournamentColoredText text ? Colors[column][(int)text.Color] : Cells[column];
    }

    public TournamentRowFormat SelectColumns(int[] columns) =>
        new(columns.Select(column => Cells[column]).ToArray(), columns.Select(column => Colors[column]).ToArray());
}

internal sealed record TournamentTableFormat(string SheetName, double[] Widths, int[] Headers, TournamentRowFormat DefaultRow,
    TournamentRowFormat? MiddleRow, TournamentRowFormat? LastRow, TournamentRowFormat? SingleRow,
    double HeaderHeight, double RowHeight, int FilterColumns, bool? ShowGridLines, string? DataBarRule, string? DataBarExtension)
{
    public TournamentRowFormat RowFormat(TournamentSeriesRow row) => row switch
    {
        TournamentSeriesRow.Middle => MiddleRow ?? DefaultRow,
        TournamentSeriesRow.Last => LastRow ?? DefaultRow,
        TournamentSeriesRow.Single => SingleRow ?? DefaultRow,
        _ => DefaultRow
    };

    public TournamentTableFormat SelectColumns(int[] columns) => this with
    {
        Widths = columns.Select(column => Widths[column]).ToArray(),
        Headers = columns.Select(column => Headers[column]).ToArray(),
        DefaultRow = DefaultRow.SelectColumns(columns),
        MiddleRow = MiddleRow?.SelectColumns(columns),
        LastRow = LastRow?.SelectColumns(columns),
        SingleRow = SingleRow?.SelectColumns(columns)
    };
}

internal sealed record TournamentReportFormats(string Styles, string Theme, Dictionary<string, TournamentTableFormat> Tables)
{
    public static TournamentReportFormats Reference { get; } = Load();

    private static TournamentReportFormats Load()
    {
        using var stream = typeof(TournamentReportFormats).Assembly.GetManifestResourceStream("TournamentReportFormats.json")
            ?? throw new InvalidOperationException("Missing tournament report formats.");
        return JsonSerializer.Deserialize<TournamentReportFormats>(stream)
            ?? throw new InvalidOperationException("Invalid tournament report formats.");
    }
}

internal static class TournamentXlsx
{
    private const string Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string PackageRelations = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string OfficeRelations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] Write(IReadOnlyList<TournamentWorksheet> sheets, CancellationToken cancellationToken)
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            Part("[Content_Types].xml", "Types", ContentTypes, xml =>
            {
                Empty(xml, "Default", ("Extension", "rels"), ("ContentType", "application/vnd.openxmlformats-package.relationships+xml"));
                Empty(xml, "Default", ("Extension", "xml"), ("ContentType", "application/xml"));
                const string prefix = "application/vnd.openxmlformats-officedocument.spreadsheetml.";
                Empty(xml, "Override", ("PartName", "/xl/workbook.xml"), ("ContentType", prefix + "sheet.main+xml"));
                Empty(xml, "Override", ("PartName", "/xl/styles.xml"), ("ContentType", prefix + "styles+xml"));
                Empty(xml, "Override", ("PartName", "/xl/theme/theme1.xml"), ("ContentType", "application/vnd.openxmlformats-officedocument.theme+xml"));
                for (var index = 1; index <= sheets.Count; index++)
                    Empty(xml, "Override", ("PartName", $"/xl/worksheets/sheet{index}.xml"), ("ContentType", prefix + "worksheet+xml"));
            });
            Part("_rels/.rels", "Relationships", PackageRelations, xml =>
                Relation(xml, 1, "officeDocument", "xl/workbook.xml"));
            Part("xl/_rels/workbook.xml.rels", "Relationships", PackageRelations, xml =>
            {
                for (var index = 1; index <= sheets.Count; index++)
                    Relation(xml, index, "worksheet", $"worksheets/sheet{index}.xml");
                Relation(xml, sheets.Count + 1, "styles", "styles.xml");
                Relation(xml, sheets.Count + 2, "theme", "theme/theme1.xml");
            });
            Part("xl/workbook.xml", "workbook", Spreadsheet, xml =>
            {
                xml.WriteAttributeString("xmlns", "r", null, OfficeRelations);
                Start(xml, "sheets");
                for (var index = 0; index < sheets.Count; index++)
                {
                    Start(xml, "sheet", ("name", sheets[index].Format.SheetName), ("sheetId", index + 1));
                    xml.WriteAttributeString("r", "id", OfficeRelations, "rId" + (index + 1));
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            });
            using (var styles = new StreamWriter(zip.CreateEntry("xl/styles.xml").Open(), new UTF8Encoding(false)))
                styles.Write(TournamentReportFormats.Reference.Styles);
            using (var theme = new StreamWriter(zip.CreateEntry("xl/theme/theme1.xml").Open(), new UTF8Encoding(false)))
                theme.Write(TournamentReportFormats.Reference.Theme);

            for (var index = 0; index < sheets.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sheet = sheets[index];
                Part($"xl/worksheets/sheet{index + 1}.xml", "worksheet", Spreadsheet,
                    xml => WriteSheet(xml, sheet, cancellationToken));
            }

            void Part(string name, string root, string ns, Action<XmlWriter> body)
            {
                using var stream = zip.CreateEntry(name, CompressionLevel.Fastest).Open();
                using var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
                xml.WriteStartDocument();
                xml.WriteStartElement(root, ns);
                body(xml);
                xml.WriteEndElement();
                xml.WriteEndDocument();
            }
        }
        return bytes.ToArray();
    }

    private static void WriteSheet(XmlWriter xml, TournamentWorksheet sheet, CancellationToken cancellationToken)
    {
        var format = sheet.Format;
        var widths = sheet.Columns.Select((col, index) => index < format.Widths.Length ? format.Widths[index] : col.Width).ToArray();
        var lastRow = sheet.Rows.Count + 1;
        var positions = new TournamentSeriesRow[sheet.Rows.Count];
        var spans = new Dictionary<(int Row, int Column), int>();
        foreach (var merge in sheet.Merges ?? [])
        {
            var first = merge.FirstDataRow - 1;
            var last = merge.LastDataRow - 1;
            spans[(first, merge.Column - 1)] = last - first + 1;
            if (merge.Column != 1) continue;
            positions[first] = TournamentSeriesRow.First;
            positions[last] = TournamentSeriesRow.Last;
            Array.Fill(positions, TournamentSeriesRow.Middle, first + 1, last - first - 1);
        }

        var heights = sheet.Rows.Select(MeasureHeight).ToArray();
        var uniformHeight = sheet.UniformRowHeight && heights.Length > 0 ? heights.Max() : (double?)null;
        Empty(xml, "dimension", ("ref", $"A1:{Column(widths.Length)}{lastRow}"));
        Start(xml, "sheetViews");
        if (format.ShowGridLines is { } grid)
            Empty(xml, "sheetView", ("workbookViewId", 0), ("showGridLines", grid ? 1 : 0));
        else
            Empty(xml, "sheetView", ("workbookViewId", 0));
        xml.WriteEndElement();
        Empty(xml, "sheetFormatPr", ("defaultRowHeight", format.RowHeight));

        Start(xml, "cols");
        for (var index = 0; index < widths.Length; index++)
            Empty(xml, "col", ("min", index + 1), ("max", index + 1), ("width", widths[index]), ("customWidth", 1));
        xml.WriteEndElement();

        Start(xml, "sheetData");
        Start(xml, "row", ("r", 1), ("ht", format.HeaderHeight), ("customHeight", 1));
        for (var index = 0; index < widths.Length; index++)
            Cell(xml, index + 1, 1, sheet.Columns[index].Heading, format.Headers[index < format.Headers.Length ? index : 1]);
        xml.WriteEndElement();

        for (var index = 0; index < sheet.Rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = index + 2;
            var values = sheet.Rows[index];
            var rowFormat = format.RowFormat(positions[index]);
            Start(xml, "row", ("r", row), ("ht", uniformHeight ?? heights[index]), ("customHeight", 1));
            for (var col = 0; col < values.Length; col++)
                Cell(xml, col + 1, row, values[col], rowFormat.CellStyle(col, values[col]));
            xml.WriteEndElement();
        }
        xml.WriteEndElement();

        if (format.FilterColumns > 0)
            Empty(xml, "autoFilter", ("ref", $"A1:{Column(Math.Min(format.FilterColumns, widths.Length))}{lastRow}"));
        if (sheet.Merges is { Count: > 0 } merges)
        {
            Start(xml, "mergeCells", ("count", merges.Count));
            foreach (var merge in merges)
            {
                var column = Column(merge.Column);
                Empty(xml, "mergeCell", ("ref", $"{column}{1 + merge.FirstDataRow}:{column}{1 + merge.LastDataRow}"));
            }
            xml.WriteEndElement();
        }

        if (format.DataBarRule is { } dataBar && lastRow > 1)
        {
            var range = $"G2:G{lastRow}";
            Start(xml, "conditionalFormatting", ("sqref", range));
            xml.WriteRaw(dataBar);
            xml.WriteEndElement();
            if (format.DataBarExtension is { } extension)
                xml.WriteRaw(extension.Replace("__WIN_COUNT_RANGE__", range, StringComparison.Ordinal));
        }

        double MeasureHeight(object?[] values, int row)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lines = 1;
            for (var col = 0; col < values.Length; col++)
            {
                var text = values[col] is TournamentColoredText colored ? colored.Text : values[col] as string;
                if (text is null) continue;
                var width = Math.Max(1, widths[col] - 1);
                var wrapped = text.Split('\n').Sum(line => Math.Max(1,
                    (int)Math.Ceiling(line.Sum(ch => ch <= 0x7f ? 1 : 2) / width)));
                var span = spans.GetValueOrDefault((row, col), 1);
                lines = Math.Max(lines, (wrapped + span - 1) / span);
            }
            return Math.Min(150, format.RowHeight * lines);
        }
    }

    private static void Cell(XmlWriter xml, int column, int row, object? value, int style)
    {
        Start(xml, "c", ("r", Column(column) + row.ToString(CultureInfo.InvariantCulture)), ("s", style));
        if (value is TournamentColoredText styled) value = styled.Text;
        if (value is string text)
        {
            // User-provided labels must stay text, even if they start with Excel formula syntax.
            xml.WriteAttributeString("t", "inlineStr");
            Start(xml, "is");
            Start(xml, "t");
            xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            var safe = new StringBuilder();
            foreach (var rune in text.EnumerateRunes())
                safe.Append(rune.Value is 9 or 10 or 13 || rune.Value >= 32 && rune.Value is not (0xFFFE or 0xFFFF)
                    ? rune.ToString() : "\uFFFD");
            xml.WriteString(safe.ToString());
            xml.WriteEndElement();
            xml.WriteEndElement();
        }
        else if (value is not null)
        {
            var number = value is TournamentPercent percent ? percent.Value : value;
            xml.WriteElementString("v", Convert.ToString(number, CultureInfo.InvariantCulture));
        }
        xml.WriteEndElement();
    }

    private static string Column(int number)
    {
        Span<char> letters = stackalloc char[8];
        var start = letters.Length;
        while (number > 0)
        {
            number--;
            letters[--start] = (char)('A' + number % 26);
            number /= 26;
        }
        return new string(letters[start..]);
    }

    private static void Relation(XmlWriter xml, int id, string type, string target) =>
        Empty(xml, "Relationship", ("Id", "rId" + id), ("Type", OfficeRelations + "/" + type), ("Target", target));

    private static void Start(XmlWriter xml, string name, params (string Name, object Value)[] attributes)
    {
        xml.WriteStartElement(name);
        foreach (var (key, value) in attributes)
            xml.WriteAttributeString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static void Empty(XmlWriter xml, string name, params (string Name, object Value)[] attributes)
    {
        Start(xml, name, attributes);
        xml.WriteEndElement();
    }
}
