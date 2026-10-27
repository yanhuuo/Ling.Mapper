using System.Collections.ObjectModel;
using System.Threading;

namespace Ling.Mapper;

/// <summary>Immutable per-call parameter snapshot and optional caller-owned services.</summary>
/// <remarks>Only the parameter dictionary is copied; parameter values and services are not cloned or disposed.
/// Read <see cref="Current"/> during invocation, never while creating a cached mapping configuration.</remarks>
public sealed class MappingCallContext
{
    private static readonly MappingCallContext EmptyContext = new();

    /// <summary>Create a context, copying parameter keys and values using ordinal key comparison.</summary>
    public MappingCallContext(IReadOnlyDictionary<string, object?>? parameters = null, IServiceProvider? services = null)
    {
        var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters != null)
            foreach (var pair in parameters) snapshot.Add(pair.Key, pair.Value);
        Parameters = new ReadOnlyDictionary<string, object?>(snapshot);
        Services = services;
    }

    /// <summary>The current logical async-flow context, or an empty context outside a scope.</summary>
    public static MappingCallContext Current => MappingCallScope.CurrentContext ?? EmptyContext;

    /// <summary>Read-only snapshot of parameters; mutable parameter values remain caller-owned.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>Caller-owned service provider, resolved on demand and never globally cached or disposed.</summary>
    public IServiceProvider? Services { get; }

    /// <summary>Read a required parameter without conversion. Missing keys and incompatible types are errors.</summary>
    public T GetParameter<T>(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!Parameters.TryGetValue(name, out var value)) throw new KeyNotFoundException($"Mapping parameter '{name}' was not supplied.");
        if (value is T typed) return typed;
        if (value == null && default(T) == null) return default!;
        throw new InvalidCastException($"Mapping parameter '{name}' is not compatible with {typeof(T)}.");
    }

    /// <summary>Try to read a parameter of the requested type without conversion.</summary>
    public bool TryGetParameter<T>(string name, out T value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Parameters.TryGetValue(name, out var raw))
        {
            if (raw is T typed) { value = typed; return true; }
            if (raw == null && default(T) == null) { value = default!; return true; }
        }
        value = default!;
        return false;
    }

    /// <summary>Resolve a required service on each invocation. The mapper does not own the returned service.</summary>
    public T GetRequiredService<T>()
    {
        var service = Services?.GetService(typeof(T));
        return service is T result ? result : throw new InvalidOperationException($"Mapping service {typeof(T)} is not available.");
    }

    /// <summary>Enter this context in the current async flow. Dispose the returned scope in LIFO order.</summary>
    public MappingCallScope BeginScope() => MappingCallScope.Begin(this);
}

/// <summary>A nestable context scope that flows across awaits and restores the previous context on disposal.</summary>
/// <remarks>Use a using statement, including around calls that may throw. Scopes must be disposed in LIFO order
/// within each async flow. Child async flows can override their context without changing their parent's context.</remarks>
public sealed class MappingCallScope : IDisposable
{
    private static readonly AsyncLocal<MappingCallScope?> Ambient = new();
    private readonly MappingCallScope? _previous;
    private readonly MappingCallContext _context;
    private MappingCallScope(MappingCallContext context)
    {
        _context = context;
        _previous = Ambient.Value;
        Ambient.Value = this;
    }

    internal static MappingCallContext? CurrentContext => Ambient.Value?._context;

    /// <summary>Enter a caller-created context without owning its parameter values or services.</summary>
    public static MappingCallScope Begin(MappingCallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MappingCallScope(context);
    }

    /// <summary>Restore the prior context in this logical flow; repeated disposal is harmless.</summary>
    public void Dispose()
    {
        if (ReferenceEquals(Ambient.Value, this)) { Ambient.Value = _previous; return; }
        // No shared disposed flag: the same scope can be inherited by another async flow.
        // Disposing there must never prevent its owner from restoring its own prior context.
        for (var current = Ambient.Value; current != null; current = current._previous)
            if (ReferenceEquals(current, this)) throw new InvalidOperationException("Mapping context scopes must be disposed in reverse creation order.");
    }
}
