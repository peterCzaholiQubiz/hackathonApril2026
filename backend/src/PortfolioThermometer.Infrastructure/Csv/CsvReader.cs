using System.Text;

namespace PortfolioThermometer.Infrastructure.Csv;

public static class CsvReader
{
    public static async Task<List<Dictionary<string, string>>> ReadAllAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return [];

        var rows = new List<Dictionary<string, string>>();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
            return rows;

        var headers = ParseLine(headerLine);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = ParseLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
                row[headers[i]] = values[i];

            rows.Add(row);
        }

        return rows;
    }

    public static async Task<List<Dictionary<string, string>>> ReadMultipleAsync(
        IEnumerable<string> filePaths,
        CancellationToken ct = default)
    {
        var all = new List<Dictionary<string, string>>();

        foreach (var path in filePaths)
        {
            var rows = await ReadAllAsync(path, ct);
            all.AddRange(rows);
        }

        return all;
    }

    public static async Task<string[]> ReadHeaderAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return [];

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        var headerLine = await reader.ReadLineAsync(ct);
        return headerLine is null ? [] : ParseLine(headerLine);
    }

    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}
