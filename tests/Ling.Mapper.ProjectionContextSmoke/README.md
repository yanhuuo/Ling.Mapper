# Projection/context generated guard smoke

```sh
dotnet run --project tests/Ling.Mapper.ProjectionContextSmoke/Ling.Mapper.ProjectionContextSmoke.csproj -c Release
```

This isolated process has no runtime profiles or converter registrations. The ordinary
regression runner registers a JSON converter before every suite, so it cannot reach
these generated-backend guard boundaries through `GeneratedMapperRegistry.CreateMapper`.

Checks: nonempty parameters/services are rejected through the context wrappers and
through direct generic/declared-Type Map under an ambient scope; wrapping the declared-Type
entry in a delegate cannot bypass its guard; empty context preserves null mapping;
projection capability and runtime profiles are rejected; scopes restore after exceptions.

This is a **JIT smoke**, not a NativeAOT publish/execution test. The internal
`GeneratedMapperFactoryProxy` only selects its forwarding delegate when dynamic code is
unavailable, so that branch itself is not executed here. Source inspection confirms it
forwards to the same guarded declared-Type Map entry; that does not substitute for AOT testing.
