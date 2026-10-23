// This project is an intentional negative compile test, excluded from the solution.
using Ling.Mapper;
[assembly: GeneratedMap(typeof(Source), typeof(ImmutableTarget))]
[assembly: GeneratedMap(typeof(Source), typeof(MutableTarget))]
public sealed class Child { public int Value { get; set; } }
public sealed class ChildTarget { public int Value { get; set; } }
public sealed class Source { public Child Child { get; set; } = new Child(); }
public sealed record ImmutableTarget(int Value);
public sealed class MutableTarget { public ChildTarget Child { get; set; } = new ChildTarget(); }
