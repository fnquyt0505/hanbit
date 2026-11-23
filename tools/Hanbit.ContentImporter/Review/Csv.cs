using System.Text;

namespace Hanbit.ContentImporter.Review;

/// <summary>
/// CSV tối giản theo RFC 4180 (không thêm package): ghi UTF-8 có BOM + dấu phẩy (Excel/Google Sheets hiện đúng chữ Việt/Hàn);
/// đọc tự nhận dấu phân cách <c>,</c> / <c>;</c> / tab từ dòng tiêu đề (Excel ở vùng Việt Nam thường lưu bằng <c>;</c>).
/// </summary>
public static class Csv
{
    public static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static string Write(IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendJoin(',', row.Select(Quote));
            builder.Append("\r\n");
        }

        return builder.ToString();
    }

    public static List<List<string>> Parse(string text)
    {
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var delimiter = DetectDelimiter(text);
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                field.Clear();
                AddRow(rows, row);
                row = [];
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            AddRow(rows, row);
        }

        return rows;
    }

    private static void AddRow(List<List<string>> rows, List<string> row)
    {
        if (row.Any(f => f.Length > 0))
        {
            rows.Add(row);
        }
    }

    private static char DetectDelimiter(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        var header = end < 0 ? text : text[..end];
        return new[] { ',', ';', '\t' }.MaxBy(d => header.Count(c => c == d));
    }

    private static string Quote(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', ';', '"', '\r', '\n', '\t']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
