using Ling.Mapper;
[assembly: GeneratedMap(typeof(FieldOnly), typeof(FieldOnly))]
[assembly: GeneratedMap(typeof(Opaque), typeof(Opaque))]
[assembly: GeneratedMap(typeof(ReadOnlyState), typeof(ReadOnlyState))]
[assembly: GeneratedMap(typeof(DictionaryGraph), typeof(DictionaryGraph))]
[assembly: GeneratedMap(typeof(ResourceGraph), typeof(ResourceGraph))]
[assembly: GeneratedMap(typeof(int[,]), typeof(int[,]))]
[assembly: GeneratedMap(typeof(GenericGrowth<int>), typeof(GenericGrowth<int>))]
[assembly: GeneratedMap(typeof(UserStream), typeof(OtherUserStream))]
[assembly: GeneratedMap(typeof(Plain), typeof(AsyncResource))]

public struct FieldOnly { public List<int>? Values; }
public sealed class Opaque { private int state; public int Value { get => state; set => state = value; } }
public sealed class ReadOnlyState { public List<int> Values { get; } = new(); }
public sealed class DictionaryGraph { public Dictionary<string, List<int>>? Values { get; set; } }
public sealed class ResourceGraph { public MemoryStream? Resource { get; set; } }

public sealed class GenericGrowth<T> { public GenericGrowth<List<T>>? Child { get; set; } }

public sealed class UserStream : MemoryStream { }
public sealed class OtherUserStream : MemoryStream { }
public sealed class Plain { public int Value { get; set; } }
public sealed class AsyncResource : IAsyncDisposable { public int Value { get; set; } public ValueTask DisposeAsync() => default; }
