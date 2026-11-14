using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace ElectronicSchedule;

/// <summary>不依赖第三方库的最小 xlsx 读取器：取第一个工作表，返回按行列排好的文本。</summary>
internal static class XlsxReader
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static List<List<string>> ReadFirstSheet(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        var shared = ReadSharedStrings(zip);

        var sheet = zip.Entries
            .Where(e => Normalize(e.FullName).StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                        && Normalize(e.FullName).EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => Normalize(e.FullName), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? throw new InvalidDataException("找不到工作表，这个文件可能不是 .xlsx 格式。");

        XDocument document;
        using (var stream = sheet.Open())
            document = XDocument.Load(stream);

        var rows = new List<List<string>>();

        foreach (var rowElement in document.Descendants(Ns + "row"))
        {
            var cells = new List<string>();

            foreach (var cell in rowElement.Elements(Ns + "c"))
            {
                int column = ColumnIndex((string?)cell.Attribute("r") ?? string.Empty);
                while (cells.Count <= column)
                    cells.Add(string.Empty);

                cells[column] = CellText(cell, shared);
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static string CellText(XElement cell, IReadOnlyList<string> shared)
    {
        string type = (string?)cell.Attribute("t") ?? string.Empty;

        if (type == "s")
        {
            string raw = cell.Element(Ns + "v")?.Value ?? string.Empty;
            return int.TryParse(raw, out int index) && index >= 0 && index < shared.Count
                ? shared[index].Trim()
                : string.Empty;
        }

        if (type == "inlineStr")
            return string.Concat(cell.Element(Ns + "is")?.Elements(Ns + "t").Select(x => x.Value) ?? Enumerable.Empty<string>()).Trim();

        return (cell.Element(Ns + "v")?.Value ?? string.Empty).Trim();
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.Entries.FirstOrDefault(e =>
            Normalize(e.FullName).Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return new List<string>();

        XDocument document;
        using (var stream = entry.Open())
            document = XDocument.Load(stream);

        return document.Descendants(Ns + "si")
            .Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value)))
            .ToList();
    }

    /// <summary>部分工具生成的 zip 条目用反斜杠，统一成正斜杠再匹配。</summary>
    private static string Normalize(string name) => name.Replace('\\', '/');

    /// <summary>"B12" → 1</summary>
    private static int ColumnIndex(string reference)
    {
        int index = 0;
        int letters = 0;

        foreach (char c in reference)
        {
            if (c < 'A' || c > 'Z')
                break;

            index = index * 26 + (c - 'A' + 1);
            letters++;
        }

        return letters == 0 ? 0 : index - 1;
    }
}
