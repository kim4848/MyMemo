using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public record TursoCol(string Name, string DeclType);
public record TursoValue(string Type, string? Value);

public sealed class TursoDataReader(List<TursoCol> cols, List<TursoValue[]> rows) : DbDataReader
{
    private int _rowIndex = -1;

    public override int FieldCount => cols.Count;
    public override bool HasRows => rows.Count > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => -1;
    public override int Depth => 0;

    public override bool Read()
    {
        _rowIndex++;
        return _rowIndex < rows.Count;
    }

    public override string GetName(int ordinal) => cols[ordinal].Name;

    public override int GetOrdinal(string name)
    {
        var idx = cols.FindIndex(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) throw new IndexOutOfRangeException($"Column '{name}' not found.");
        return idx;
    }

    public override bool IsDBNull(int ordinal)
    {
        var v = rows[_rowIndex][ordinal];
        return v.Type == "null" || v.Value is null;
    }

    public override object GetValue(int ordinal)
    {
        var v = rows[_rowIndex][ordinal];
        if (v.Type == "null" || v.Value is null) return DBNull.Value;
        return v.Type switch
        {
            "integer" => long.TryParse(v.Value, out var l) ? (object)l : v.Value,
            "real"    => double.TryParse(v.Value, System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var d) ? (object)d : v.Value,
            _         => v.Value
        };
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++) values[i] = GetValue(i);
        return count;
    }

    public override string GetString(int ordinal)    => rows[_rowIndex][ordinal].Value ?? "";
    public override long GetInt64(int ordinal)        => long.Parse(rows[_rowIndex][ordinal].Value!);
    public override int GetInt32(int ordinal)         => int.Parse(rows[_rowIndex][ordinal].Value!);
    public override double GetDouble(int ordinal)     => double.Parse(rows[_rowIndex][ordinal].Value!,
                                                             System.Globalization.CultureInfo.InvariantCulture);
    public override bool GetBoolean(int ordinal)      => rows[_rowIndex][ordinal].Value != "0";
    public override byte GetByte(int ordinal)         => byte.Parse(rows[_rowIndex][ordinal].Value!);
    public override char GetChar(int ordinal)         => rows[_rowIndex][ordinal].Value![0];
    public override Guid GetGuid(int ordinal)         => Guid.Parse(rows[_rowIndex][ordinal].Value!);
    public override short GetInt16(int ordinal)       => short.Parse(rows[_rowIndex][ordinal].Value!);
    public override float GetFloat(int ordinal)       => float.Parse(rows[_rowIndex][ordinal].Value!,
                                                             System.Globalization.CultureInfo.InvariantCulture);
    public override decimal GetDecimal(int ordinal)   => decimal.Parse(rows[_rowIndex][ordinal].Value!,
                                                             System.Globalization.CultureInfo.InvariantCulture);
    public override DateTime GetDateTime(int ordinal) => DateTime.Parse(rows[_rowIndex][ordinal].Value!);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;

    public override string GetDataTypeName(int ordinal) => cols[ordinal].DeclType;
    public override Type GetFieldType(int ordinal) => typeof(string);

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool NextResult() => false;
    public override System.Collections.IEnumerator GetEnumerator() =>
        new System.Data.Common.DbEnumerator(this);
}
