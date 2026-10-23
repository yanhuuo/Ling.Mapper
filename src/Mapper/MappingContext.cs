using System.Runtime.CompilerServices;
using System.Text;

namespace Ling.Mapper.Mapper;

internal sealed class MappingContext
{
    private Dictionary<(object, Type), object>? _objects;
    private List<(string? Name, int Index, bool Sensitive)>? _path;
    private HashSet<(object, Type)>? _pendingCollections;
    public void EnterCollection(object source, Type destination)
    {
        if (!(_pendingCollections ??= new(new IdentityComparer())).Add((source, destination)))
            throw new InvalidOperationException("This collection cannot preserve a circular reference. Register an explicit converter.");
    }
    public void ExitCollection(object source, Type destination) => _pendingCollections!.Remove((source, destination));
    private object? _origin;
    public object Origin => _origin ??= new object();
    public bool HasReferences => _objects != null;
    public void Register(object source, Type destination, object value) => (_objects ??= new(new IdentityComparer()))[(source, destination)] = value;
    public bool TryGet(object source, Type destination, out object value)
    {
        if (_objects != null) return _objects.TryGetValue((source, destination), out value!);
        value = null!;
        return false;
    }
    public bool IsSensitive => _path?.Any(segment => segment.Sensitive) == true;
    public void Push(string name, bool sensitive = false) => (_path ??= new()).Add((name, 0, sensitive));
    public void Push(int index) => (_path ??= new()).Add((null, index, false));
    public void Pop() => _path!.RemoveAt(_path.Count - 1);
    public string GetPath()
    {
        var result = new StringBuilder("$");
        foreach (var segment in _path ?? Enumerable.Empty<(string? Name, int Index, bool Sensitive)>())
            if (segment.Name != null) result.Append('.').Append(segment.Name);
            else result.Append('[').Append(segment.Index).Append(']');
        return result.ToString();
    }
    private sealed class IdentityComparer : IEqualityComparer<(object, Type)>
    {
        public bool Equals((object, Type) x, (object, Type) y) => ReferenceEquals(x.Item1, y.Item1) && x.Item2 == y.Item2;
        public int GetHashCode((object, Type) value) => HashCode.Combine(RuntimeHelpers.GetHashCode(value.Item1), value.Item2);
    }
}
