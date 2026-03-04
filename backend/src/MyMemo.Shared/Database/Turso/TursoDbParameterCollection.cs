using System.Collections;
using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoDbParameterCollection : DbParameterCollection
{
    private readonly List<TursoDbParameter> _params = [];

    public override int Count => _params.Count;
    public override object SyncRoot => ((ICollection)_params).SyncRoot;

    public override int Add(object value)
    {
        _params.Add((TursoDbParameter)value);
        return _params.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var v in values) Add(v);
    }

    public override void Clear() => _params.Clear();

    public override bool Contains(object value) => _params.Contains((TursoDbParameter)value);
    public override bool Contains(string value) => _params.Any(p => p.ParameterName == value);

    public override void CopyTo(Array array, int index) => ((ICollection)_params).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _params.GetEnumerator();

    public override int IndexOf(object value) => _params.IndexOf((TursoDbParameter)value);
    public override int IndexOf(string parameterName) => _params.FindIndex(p => p.ParameterName == parameterName);

    public override void Insert(int index, object value) => _params.Insert(index, (TursoDbParameter)value);

    public override void Remove(object value) => _params.Remove((TursoDbParameter)value);
    public override void RemoveAt(int index) => _params.RemoveAt(index);
    public override void RemoveAt(string parameterName)
    {
        var idx = _params.FindIndex(p => p.ParameterName == parameterName);
        if (idx >= 0) _params.RemoveAt(idx);
    }

    protected override DbParameter GetParameter(int index) => _params[index];
    protected override DbParameter GetParameter(string parameterName)
    {
        var idx = _params.FindIndex(p => p.ParameterName == parameterName);
        if (idx < 0) throw new IndexOutOfRangeException($"Parameter '{parameterName}' not found.");
        return _params[idx];
    }

    protected override void SetParameter(int index, DbParameter value) => _params[index] = (TursoDbParameter)value;
    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var idx = _params.FindIndex(p => p.ParameterName == parameterName);
        if (idx < 0) throw new IndexOutOfRangeException($"Parameter '{parameterName}' not found.");
        _params[idx] = (TursoDbParameter)value;
    }

    public IReadOnlyList<TursoDbParameter> All => _params;
}
