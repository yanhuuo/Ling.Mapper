using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;

// Isolated from the regression harness's mandatory JSON-converter registration.
// This checks generated-backend guards under JIT; it is not a NativeAOT execution test.
int assertions = 0;
void Reject(Action action, string reason)
{
    assertions++;
    try { action(); }
    catch (NotSupportedException error) when (error.Message.Contains(reason, StringComparison.OrdinalIgnoreCase)) { return; }
    throw new Exception("Expected generated-backend rejection: " + reason);
}
void Equal(object? expected, object? actual)
{ assertions++; if (!Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}."); }
var initial = MappingCallContext.Current;
var mapper = GeneratedMapperRegistry.CreateMapper();
var context = new MappingCallContext(new Dictionary<string, object?> { ["id"] = 1 });
var services = new MappingCallContext(services: new Services());
Reject(() => mapper.MapWithContext<object>(null, context), "per-call");
Reject(() => mapper.MapWithContext<object>(null, services), "per-call");
Reject(() => mapper.MapWithContext(null, typeof(object), typeof(object), context, AdaptOptions.Default), "per-call");
Equal(null, mapper.MapWithContext<object>(null, new MappingCallContext()));
Equal(initial, MappingCallContext.Current);
Reject(() => mapper.GetProjection<object, object>(), "projection");
Reject(() => Array.Empty<object>().AsQueryable().ProjectTo<object>(mapper), "projection");
using (context.BeginScope())
{
    Reject(() => mapper.Map<object>(null), "per-call");
    Reject(() => mapper.Map(null, typeof(object), typeof(object)), "per-call");
    // The proxy forwards to this declared-type boundary; invoking a delegate must not bypass the guard.
    Func<object, object?> generatedDelegate = value => mapper.Map(value, typeof(object), typeof(object));
    Reject(() => generatedDelegate(new object()), "per-call");
}
Equal(initial, MappingCallContext.Current);
var profileConfiguration = new MapperConfiguration(); profileConfiguration.AddProfile(new Profile());
Reject(() => GeneratedMapperRegistry.CreateMapper(profileConfiguration), "Profiles");
Console.WriteLine($"Projection/context generated guards: {assertions} assertions passed; JIT only.");

sealed class Services : IServiceProvider { public object? GetService(Type serviceType) => null; }
sealed class Profile : MapperProfile { public Profile() => CreateMap<Input, Output>(); }
sealed class Input { public int Id { get; set; } }
sealed class Output { public int Id { get; set; } }
