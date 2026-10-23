using System.Runtime.CompilerServices;
using System.Text;
using Ling.Mapper.Models;

namespace Ling.Mapper.Mapper;

internal sealed class MappingContext
{
    // Flat/identity maps never allocate this holder. Projection entries remain
    // separate so an unfiltered instance can never satisfy a filtered request.
    private ReferenceCache? _references;
    private sealed class ReferenceCache
    {
        internal Dictionary<(object, Type), object>? Objects;
        internal Dictionary<(object, Type, AdaptOptionsKey), object>? ProjectedObjects;
    }
    private List<(string? Name, int Index, bool Sensitive)>? _path;
    private HashSet<(object, Type)>? _pendingCollections;
    public void EnterCollection(object source, Type destination)
    {
        if (!(_pendingCollections ??= new(IdentityComparer.Instance)).Add((source, destination)))
            throw new InvalidOperationException("This collection cannot preserve a circular reference. Register an explicit converter.");
    }
    public void ExitCollection(object source, Type destination) => _pendingCollections!.Remove((source, destination));
    private object? _origin;
    public object Origin => _origin ??= new object();
    public bool HasReferences => _references != null;
    public void Register(object source, Type destination, object value) => Register(source, destination, value, null);
    public void Register(object source, Type destination, object value, string[]? ignores)
    {
        var references = _references ??= new ReferenceCache();
        if (ignores == null || ignores.Length == 0)
            (references.Objects ??= new(IdentityComparer.Instance))[(source, destination)] = value;
        else
            (references.ProjectedObjects ??= new(ProjectionComparer.Instance))[(source, destination, new AdaptOptionsKey(AdaptOptions.Strict, ignores))] = value;
    }
    public bool TryGet(object source, Type destination, out object value) => TryGet(source, destination, out value, null);
    public bool TryGet(object source, Type destination, out object value, string[]? ignores)
    {
        var references = _references;
        if (ignores == null || ignores.Length == 0)
        {
            if (references?.Objects != null) return references.Objects.TryGetValue((source, destination), out value!);
        }
        else if (references?.ProjectedObjects != null)
            return references.ProjectedObjects.TryGetValue((source, destination, new AdaptOptionsKey(AdaptOptions.Strict, ignores)), out value!);
        value = null!;
        return false;
    }
    private sealed class ProjectionComparer : IEqualityComparer<(object, Type, AdaptOptionsKey)>
    {
        internal static readonly ProjectionComparer Instance = new();
        public bool Equals((object, Type, AdaptOptionsKey) x, (object, Type, AdaptOptionsKey) y)
            => ReferenceEquals(x.Item1, y.Item1) && x.Item2 == y.Item2 && x.Item3 == y.Item3;
        public int GetHashCode((object, Type, AdaptOptionsKey) value)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(value.Item1), value.Item2, value.Item3);
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
        internal static readonly IdentityComparer Instance = new();
        public bool Equals((object, Type) x, (object, Type) y) => ReferenceEquals(x.Item1, y.Item1) && x.Item2 == y.Item2;
        public int GetHashCode((object, Type) value) => HashCode.Combine(RuntimeHelpers.GetHashCode(value.Item1), value.Item2);
    }
}
