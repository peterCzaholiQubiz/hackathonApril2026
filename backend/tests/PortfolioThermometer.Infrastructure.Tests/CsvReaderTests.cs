using System.Text;
using PortfolioThermometer.Infrastructure.Csv;
using Xunit;

namespace PortfolioThermometer.Infrastructure.Tests;

public sealed class CsvReaderTests : IDisposable
{
    private readonly string _tempDir;

    public CsvReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteCsv(string content, bool withBom = false)
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid()}.csv");
        if (withBom)
        {
            using var fs = File.OpenWrite(path);
            // Write UTF-8 BOM
            fs.Write([0xEF, 0xBB, 0xBF]);
            var bytes = Encoding.UTF8.GetBytes(content);
            fs.Write(bytes);
        }
        else
        {
            File.WriteAllText(path, content, Encoding.UTF8);
        }
        return path;
    }

    [Fact]
    public async Task ReadAllAsync_SimpleCsv_ParsesHeaderAndRows()
    {
        var path = WriteCsv("Id,Name,Value\n1,Alice,100\n2,Bob,200");

        var rows = await CsvReader.ReadAllAsync(path);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice", rows[0]["Name"]);
        Assert.Equal("200", rows[1]["Value"]);
    }

    [Fact]
    public async Task ReadAllAsync_StripsBom_ParsesHeaderCorrectly()
    {
        var path = WriteCsv("Id,Name\n1,Alice", withBom: true);

        var rows = await CsvReader.ReadAllAsync(path);

        Assert.Single(rows);
        Assert.True(rows[0].ContainsKey("Id"), "BOM should be stripped so 'Id' is first column");
        Assert.Equal("Alice", rows[0]["Name"]);
    }

    [Fact]
    public async Task ReadAllAsync_HandlesQuotedFieldsWithCommas()
    {
        var path = WriteCsv("Id,Description\n1,\"Value,With,Commas\"\n2,Plain");

        var rows = await CsvReader.ReadAllAsync(path);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Value,With,Commas", rows[0]["Description"]);
        Assert.Equal("Plain", rows[1]["Description"]);
    }

    [Fact]
    public async Task ReadAllAsync_HandlesEscapedQuotesInsideFields()
    {
        var path = WriteCsv("Id,Notes\n1,\"He said \"\"hello\"\"\"");

        var rows = await CsvReader.ReadAllAsync(path);

        Assert.Single(rows);
        Assert.Equal("He said \"hello\"", rows[0]["Notes"]);
    }

    [Fact]
    public async Task ReadAllAsync_SkipsBlankLines()
    {
        var path = WriteCsv("Id,Name\n1,Alice\n\n2,Bob\n");

        var rows = await CsvReader.ReadAllAsync(path);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task ReadAllAsync_MissingFile_ReturnsEmpty()
    {
        var rows = await CsvReader.ReadAllAsync("/nonexistent/path/file.csv");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ReadAllAsync_HeaderOnlyFile_ReturnsEmpty()
    {
        var path = WriteCsv("Id,Name,Value");

        var rows = await CsvReader.ReadAllAsync(path);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ReadAllAsync_IsCaseInsensitiveOnHeaders()
    {
        var path = WriteCsv("ID,name,VALUE\n1,Alice,100");

        var rows = await CsvReader.ReadAllAsync(path);

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0]["NAME"]);
        Assert.Equal("Alice", rows[0]["name"]);
    }

    [Fact]
    public async Task ReadMultipleAsync_CombinesRowsFromAllFiles()
    {
        var path1 = WriteCsv("Id,Name\n1,Alice");
        var path2 = WriteCsv("Id,Name\n2,Bob");

        var rows = await CsvReader.ReadMultipleAsync([path1, path2]);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice", rows[0]["Name"]);
        Assert.Equal("Bob", rows[1]["Name"]);
    }

    [Fact]
    public async Task ReadMultipleAsync_SkipsMissingFiles_ReturnsOnlyExisting()
    {
        var path1 = WriteCsv("Id,Name\n1,Alice");
        var missingPath = "/nonexistent/file.csv";

        var rows = await CsvReader.ReadMultipleAsync([path1, missingPath]);

        Assert.Single(rows);
    }

    [Fact]
    public async Task ReadHeaderAsync_ReturnsColumnNames()
    {
        var path = WriteCsv("OrganizationId,Name,OrganizationTypeId\n1,Alice,2");

        var headers = await CsvReader.ReadHeaderAsync(path);

        Assert.Equal(["OrganizationId", "Name", "OrganizationTypeId"], headers);
    }

    [Fact]
    public async Task ReadHeaderAsync_MissingFile_ReturnsEmpty()
    {
        var headers = await CsvReader.ReadHeaderAsync("/nonexistent/file.csv");

        Assert.Empty(headers);
    }
}
