using System.Runtime.CompilerServices;
using System.Text;
using Ling.Mapper.Models;

namespace Ling.Mapper.Mapper;

internal sealed class MappingContext
{
    // sourceScope is an interned token for the effective source-plan/destination
    // pair, so different explicit rules cannot share results. The destination
    // parameter remains explicit at call sites but is already encoded in scope.
    // Flat/identity maps never allocate this holder. Projection entries remain
    // separate so an unfiltered instance can never satisfy a filtered request.
    private ReferenceCache? _references;
    public bool IsUpdating { get; set; }
    public object? ExistingDestination { get; set; }
    private sealed class ReferenceCache
    {
        internal Dictionary<(object, object), object>? Objects;
        internal Dictionary<(object, object, AdaptOptionsKey), object>? ProjectedObjects;
    }
    private List<(string? Name, int Index, bool Sensitive)>? _path;
    private HashSet<(object, object)>? _pendingCollections;
    public void EnterCollection(object source, object sourceScope, Type destination)
    {
        if (!(_pendingCollections ??= new(IdentityComparer.Instance)).Add((source, sourceScope)))
            throw new InvalidOperationException("This collection cannot preserve a circular reference. Register an explicit converter.");
    }
    public void ExitCollection(object source, object sourceScope, Type destination) => _pendingCollections!.Remove((source, sourceScope));
    private HashSet<(object, object, AdaptOptionsKey)>? _pendingConstruction;
    public void EnterConstruction(object source, object sourceScope, Type destination, string[]? ignoreNames)
    {
        var projection = new AdaptOptionsKey(AdaptOptions.Strict, ignoreNames is { Length: > 0 } ? ignoreNames : null);
        if (!(_pendingConstruction ??= new(ProjectionComparer.Instance)).Add((source, sourceScope, projection)))
            throw new InvalidOperationException("This constructor cannot preserve a circular reference before its destination exists. Register an explicit factory or break the cycle.");
    }
    public void ExitConstruction(object source, object sourceScope, Type destination, string[]? ignoreNames)
        => _pendingConstruction!.Remove((source, sourceScope, new AdaptOptionsKey(AdaptOptions.Strict, ignoreNames is { Length: > 0 } ? ignoreNames : null)));
    private object? _origin;
    public object Origin => _origin ??= new object();
    public bool HasReferences => _references != null;
    public void Register(object source, object sourceScope, Type destination, object value, string[]? ignores)
    {
        var references = _references ??= new ReferenceCache();
        if (ignores == null || ignores.Length == 0)
            (references.Objects ??= new(IdentityComparer.Instance))[(source, sourceScope)] = value;
        else
            (references.ProjectedObjects ??= new(ProjectionComparer.Instance))[(source, sourceScope, new AdaptOptionsKey(AdaptOptions.Strict, ignores))] = value;
    }
    public bool TryGet(object source, object sourceScope, Type destination, out object value, string[]? ignores)
    {
        var references = _references;
        if (ignores == null || ignores.Length == 0)
        {
            if (references?.Objects != null) return references.Objects.TryGetValue((source, sourceScope), out value!);
        }
        else if (references?.ProjectedObjects != null)
            return references.ProjectedObjects.TryGetValue((source, sourceScope, new AdaptOptionsKey(AdaptOptions.Strict, ignores)), out value!);
        value = null!;
        return false;
    }
    private sealed class ProjectionComparer : IEqualityComparer<(object, object, AdaptOptionsKey)>
    {
        internal static readonly ProjectionComparer Instance = new();
        public bool Equals((object, object, AdaptOptionsKey) x, (object, object, AdaptOptionsKey) y)
            => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2) && x.Item3 == y.Item3;
        public int GetHashCode((object, object, AdaptOptionsKey) value)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(value.Item1), RuntimeHelpers.GetHashCode(value.Item2), value.Item3);
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
    private sealed class IdentityComparer : IEqualityComparer<(object, object)>
    {
        internal static readonly IdentityComparer Instance = new();
        public bool Equals((object, object) x, (object, object) y) => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
        public int GetHashCode((object, object) value) => HashCode.Combine(RuntimeHelpers.GetHashCode(value.Item1), RuntimeHelpers.GetHashCode(value.Item2));
    }
}
