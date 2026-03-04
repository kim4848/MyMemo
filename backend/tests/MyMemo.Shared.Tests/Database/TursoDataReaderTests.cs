using FluentAssertions;
using MyMemo.Shared.Database.Turso;

namespace MyMemo.Shared.Tests.Database;

public class TursoDataReaderTests
{
    private static TursoDataReader MakeReader(
        string[] colNames,
        object?[][] rows)
    {
        var cols = colNames.Select(n => new TursoCol(n, "TEXT")).ToList();
        var tursoRows = rows.Select(row =>
            row.Select(v => v is null
                ? new TursoValue("null", null)
                : new TursoValue("text", v.ToString()!)).ToArray()
        ).ToList();
        return new TursoDataReader(cols, tursoRows);
    }

    [Fact]
    public void FieldCount_ReturnsColumnCount()
    {
        var reader = MakeReader(["id", "title"], []);
        reader.FieldCount.Should().Be(2);
    }

    [Fact]
    public void GetName_ReturnsColumnName()
    {
        var reader = MakeReader(["id", "title"], []);
        reader.GetName(0).Should().Be("id");
        reader.GetName(1).Should().Be("title");
    }

    [Fact]
    public void GetOrdinal_ReturnsColumnIndex()
    {
        var reader = MakeReader(["id", "title"], []);
        reader.GetOrdinal("title").Should().Be(1);
        reader.GetOrdinal("TITLE").Should().Be(1);
        reader.GetOrdinal("Title").Should().Be(1);
    }

    [Fact]
    public void Read_ReturnsTrueWhileRowsAvailable()
    {
        var reader = MakeReader(["id"], [["abc"], ["def"]]);
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeFalse();
    }

    [Fact]
    public void GetValue_ReturnsCorrectValue()
    {
        var reader = MakeReader(["id", "title"], [["abc123", "My Session"]]);
        reader.Read();
        reader.GetValue(0).Should().Be("abc123");
        reader.GetValue(1).Should().Be("My Session");
    }

    [Fact]
    public void IsDBNull_ReturnsTrueForNullValue()
    {
        var reader = MakeReader(["id", "title"], [["abc", null]]);
        reader.Read();
        reader.IsDBNull(1).Should().BeTrue();
    }

    [Fact]
    public void GetValue_ReturnsDBNull_ForNullTursoValue()
    {
        var reader = MakeReader(["title"], [[null]]);
        reader.Read();
        reader.GetValue(0).Should().Be(DBNull.Value);
    }
}
