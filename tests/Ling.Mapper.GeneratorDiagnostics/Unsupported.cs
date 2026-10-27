// This project is an intentional negative compile test, excluded from the solution.
using Ling.Mapper;
[assembly: GeneratedMap(typeof(Source), typeof(ImmutableTarget))]
[assembly: GeneratedMap(typeof(Source), typeof(MutableTarget))]
[assembly: GeneratedMap(typeof(AmbiguousOperatorSource), typeof(AmbiguousOperatorTarget))]
[assembly: GeneratedMap(typeof(OperatorHolderSource), typeof(OperatorHolderTarget))]
[assembly: GeneratedMap(typeof(ReferenceOperatorSource), typeof(ReferenceOperatorTarget))]
[assembly: GeneratedMap(typeof(int?), typeof(NullableChoice))]
[assembly: GeneratedMap(typeof(int?), typeof(NullableChoice?))]
[assembly: GeneratedMap(typeof(AbstractOperatorHolderSource), typeof(AbstractOperatorHolderTarget))]
[assembly: GeneratedMap(typeof(RefLikeHolderSource), typeof(RefLikeHolderTarget))]
public sealed class Child { public int Value { get; set; } }
public sealed class ChildTarget { public int Value { get; set; } }
public sealed class Source { public Child Child { get; set; } = new Child(); }
public sealed record ImmutableTarget(int Value);
public sealed class MutableTarget { public ChildTarget Child { get; set; } = new ChildTarget(); }

// Each declaration is legal C#, but together the exact pair is ambiguous.
// The generator must diagnose it instead of silently falling back to matching properties.
public sealed class AmbiguousOperatorSource
{
    public int Value { get; set; }
    public static implicit operator AmbiguousOperatorTarget(AmbiguousOperatorSource source) => new();
}
public sealed class AmbiguousOperatorTarget
{
    public int Value { get; set; }
    public static explicit operator AmbiguousOperatorTarget(AmbiguousOperatorSource source) => new();
}
public sealed class OperatorHolderSource { public AmbiguousOperatorSource Value { get; set; } = new(); }
public sealed class OperatorHolderTarget { public AmbiguousOperatorTarget Value { get; set; } = new(); }

public sealed class ReferenceOperatorSource
{
    public int Value { get; set; }
    public static explicit operator ReferenceOperatorTarget(ReferenceOperatorSource source) => new() { Value = source.Value + 100 };
}
public sealed class ReferenceOperatorTarget { public int Value { get; set; } }

// Runtime generic roots see boxed int while nullable members retain int?.
// Root aliases cannot choose both declared operators faithfully.
public readonly record struct NullableChoice(int Value)
{
    public static explicit operator NullableChoice(int source) => new(source + 100);
    public static explicit operator NullableChoice(int? source) => new(source.GetValueOrDefault() + 200);
}

public abstract class AbstractOperatorSource
{
    public int Value { get; set; }
    public static explicit operator AbstractOperatorTarget(AbstractOperatorSource source) => new() { Value = source.Value + 100 };
}
public sealed class AbstractOperatorTarget { public int Value { get; set; } }
public sealed class AbstractOperatorHolderSource { public AbstractOperatorSource Value { get; set; } = null!; }
public sealed class AbstractOperatorHolderTarget { public AbstractOperatorTarget Value { get; set; } = new(); }

public ref struct RefLikeValue { public static explicit operator int(RefLikeValue source) => 1; }
public sealed class RefLikeHolderSource { public RefLikeValue Value => default; }
public sealed class RefLikeHolderTarget { public int Value { get; set; } }
