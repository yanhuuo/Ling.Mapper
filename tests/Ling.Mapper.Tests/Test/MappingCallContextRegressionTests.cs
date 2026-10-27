using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>Per-call parameters, service resolution and logical-flow scope isolation.</summary>
public static class MappingCallContextRegressionTests
{
    public static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        int assertions = 0;
        void Equal(object? expected, object? actual)
        { Interlocked.Increment(ref assertions); if (!Equals(expected, actual)) throw new Exception($"Context: expected {expected ?? "null"}, actual {actual ?? "null"}."); }
        void Throws<T>(Action action) where T : Exception
        { Interlocked.Increment(ref assertions); try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        var initial = MappingCallContext.Current;
        Equal(0, initial.Parameters.Count); Equal(null, initial.Services);
        var mutable = new Dictionary<string, object?> { ["offset"] = 10, ["label"] = "first", ["null"] = null };
        var services = new Services(100);
        var first = new MappingCallContext(mutable, services);
        mutable["offset"] = 999;
        Equal(10, first.GetParameter<int>("offset")); Equal(null, first.GetParameter<string?>("null"));
        Equal(true, first.TryGetParameter<int>("offset", out var offset)); Equal(10, offset);
        Equal(false, first.TryGetParameter<string>("offset", out _)); Equal(false, first.TryGetParameter<int>("missing", out _));
        Throws<KeyNotFoundException>(() => first.GetParameter<int>("missing"));
        Throws<InvalidCastException>(() => first.GetParameter<string>("offset"));
        Throws<InvalidCastException>(() => first.GetParameter<int>("null"));
        Throws<NotSupportedException>(() => ((IDictionary<string, object?>)first.Parameters).Add("new", 1));
        var second = Context(20, "second", new Services(200));
        using (first.BeginScope())
        {
            Equal(first, MappingCallContext.Current); await Task.Yield(); Equal(first, MappingCallContext.Current);
            try { using var scope = second.BeginScope(); Equal(second, MappingCallContext.Current); throw new Deliberate(); }
            catch (Deliberate) { }
            Equal(first, MappingCallContext.Current);
            await Task.Run(async () => { Equal(first, MappingCallContext.Current); using (second.BeginScope()) { await Task.Yield(); Equal(second, MappingCallContext.Current); } Equal(first, MappingCallContext.Current); });
            Equal(first, MappingCallContext.Current);
        }
        Equal(initial, MappingCallContext.Current);
        var outer = first.BeginScope(); var inner = second.BeginScope();
        Throws<InvalidOperationException>(() => outer.Dispose()); Equal(second, MappingCallContext.Current);
        inner.Dispose(); Equal(first, MappingCallContext.Current); inner.Dispose(); Equal(first, MappingCallContext.Current);
        // A copied async flow must not share a mutable disposed flag with the owner's scope.
        await Task.Run(() => { outer.Dispose(); Equal(initial, MappingCallContext.Current); });
        Equal(first, MappingCallContext.Current); outer.Dispose(); outer.Dispose(); Equal(initial, MappingCallContext.Current);

        var configuration = new MapperConfiguration(); configuration.AddProfile(new Rules());
        var mapper = configuration.CreateMapper();
        var input = new Input { Id = 3 };
        var one = mapper.MapWithContext<Output>(input, first)!;
        Equal(13, one.Value); Equal("first", one.Label); Equal(103, one.FromService); Equal(initial, MappingCallContext.Current);
        var two = mapper.MapWithContext<Output>(input, second)!;
        Equal(23, two.Value); Equal("second", two.Label); Equal(203, two.FromService); Equal(1, services.Resolutions);
        Equal(13, ((Output)mapper.MapWithContext(input, typeof(Input), typeof(Output), first)!).Value);
        Equal(13, ((Output)mapper.MapWithContext(input, typeof(Input), typeof(Output), first, AdaptOptions.Strict)!).Value);
        Equal(13, mapper.MapWithContext<Immutable>(input, first)!.Value);
        using (first.BeginScope())
        {
            Equal(13, mapper.Map<Output>(input)!.Value);
            Equal(23, mapper.MapWithContext<Output>(input, second)!.Value); Equal(first, MappingCallContext.Current);
            Throws<MappingException>(() => mapper.MapWithContext<Output>(input, new MappingCallContext()));
            Equal(first, MappingCallContext.Current);
            Throws<MappingException>(() => mapper.MapWithContext<Output>(input, second, AdaptOptions.Default, (_, _) => throw new Deliberate()));
            Equal(first, MappingCallContext.Current);
        }
        Equal(initial, MappingCallContext.Current);
        var callbacks = 0;
        mapper.MapWithContext<Output>(input, second, AdaptOptions.Default, (_, _) => { callbacks++; Equal(second, MappingCallContext.Current); });
        Equal(1, callbacks); Equal(initial, MappingCallContext.Current);
        Equal(null, mapper.MapWithContext<Output>(null, first)); Equal(initial, MappingCallContext.Current);
        var ignored = mapper.MapWithContext<Output>(input, new MappingCallContext(), AdaptOptions.Default, ignoreNames: new[] { "Value", "Label", "FromService" });
        Equal(0, ignored!.Value);
        var existing = new Output { Value = -1, Label = "existing", FromService = -1 };
        Equal(existing, mapper.MapIntoWithContext(input, existing, first));
        Equal(13, existing.Value); Equal("first", existing.Label); Equal(103, existing.FromService);
        Equal(existing, mapper.MapIntoWithContext(input, existing, second, AdaptOptions.Strict, ignoreNames: new[] { "Label" }));
        Equal(23, existing.Value); Equal("first", existing.Label); Equal(203, existing.FromService);
        Equal(initial, MappingCallContext.Current);
        Equal(existing, mapper.MapIntoWithContext(input, existing, typeof(Input), typeof(Output), first)); Equal(13, existing.Value);
        Equal(existing, mapper.MapIntoWithContext(input, existing, typeof(Input), typeof(Output), second, AdaptOptions.Strict)); Equal(23, existing.Value);
        var previous = MapperProvider.Current;
        try
        {
            MapperProvider.SetCurrent(mapper);
            Equal(13, input.AdaptWithContext<Output>(first)!.Value);
            Equal(23, input.AdaptWithContext<Output>(second, AdaptOptions.Strict)!.Value);
            Equal(existing, input.AdaptIntoWithContext(existing, first)); Equal(13, existing.Value);
            Equal(existing, input.AdaptIntoWithContext(existing, second, AdaptOptions.Strict)); Equal(23, existing.Value);
        }
        finally { MapperProvider.SetCurrent(previous); }
        var tasks = Enumerable.Range(0, 32).Select(index => Task.Run(async () =>
        {
            var service = new Services(index * 1000); var context = Context(index, "worker" + index, service);
            using (context.BeginScope())
            {
                await Task.Yield();
                for (int iteration = 0; iteration < 8; iteration++)
                {
                    var output = mapper.Map<Output>(input)!;
                    Equal(index + 3, output.Value); Equal("worker" + index, output.Label); Equal(index * 1000 + 3, output.FromService);
                    var nested = mapper.MapWithContext<Output>(input, first)!; Equal(13, nested.Value); Equal(context, MappingCallContext.Current);
                    await Task.Yield();
                }
            }
            Equal(initial, MappingCallContext.Current); Equal(8, service.Resolutions); Equal(false, service.Disposed);
        }));
        await Task.WhenAll(tasks);
        Equal(initial, MappingCallContext.Current); Equal(false, services.Disposed);
        Equal(10, new ContextAwareCustomMapper().MapWithContext<int>(input, first));
        Equal(initial, MappingCallContext.Current); Equal(false, services.Disposed);
        Console.WriteLine($"Mapping call context regression: {assertions} assertions passed.");
    }

    private static MappingCallContext Context(int offset, string label, Services services) => new(new Dictionary<string, object?> { ["offset"] = offset, ["label"] = label }, services);
    private sealed class Deliberate : Exception { }
    public sealed class Input { public int Id { get; set; } }
    public sealed class Output { public int Value { get; set; } public string? Label { get; set; } public int FromService { get; set; } }
    public sealed class Immutable { public int Value { get; } public Immutable(int value) => Value = value; }
    private interface IIncrement { int Apply(int value); }
    private sealed class Services(int offset) : IServiceProvider, IIncrement, IDisposable
    {
        public int Resolutions; public bool Disposed;
        public object? GetService(Type serviceType) { Interlocked.Increment(ref Resolutions); return serviceType == typeof(IIncrement) ? this : null; }
        public int Apply(int value) => value + offset;
        public void Dispose() => Disposed = true;
    }
    private sealed class Rules : MapperProfile
    {
        public Rules()
        {
            CreateMap<Input, Output>().ForMember(d => d.Value, o => o.MapFrom((s, c) => s.Id + c.GetParameter<int>("offset")))
                .ForMember(d => d.Label, o => o.MapFrom((s, c) => c.GetParameter<string>("label")))
                .ForMember(d => d.FromService, o => o.MapFrom((s, c) => c.GetRequiredService<IIncrement>().Apply(s.Id)));
            CreateMap<Input, Immutable>().ForMember(d => d.Value, o => o.MapFrom((s, c) => s.Id + c.GetParameter<int>("offset")));
        }
    }
    private sealed class ContextAwareCustomMapper : IMapper
    {
        public TDestination? Map<TDestination>(object? source) => (TDestination)(object)MappingCallContext.Current.GetParameter<int>("offset");
        public TDestination? Map<TDestination>(object? source, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null) => Map<TDestination>(source);
        public object? Map(object? source, Type sourceType, Type destType) => MappingCallContext.Current.GetParameter<int>("offset");
        public object? Map(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null) => Map(source, sourceType, destType);
    }
}
