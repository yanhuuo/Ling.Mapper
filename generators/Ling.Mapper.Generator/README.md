# Ling.Mapper.Generator

This is a real, explicit-pair Roslyn incremental source generator. It emits typed
DTO construction/member assignment and module-initializer registration for the
reflection-free `GeneratedMapperRegistry`. It does **not** infer every pair from
`Adapt<T>()` calls and does not interpret runtime Profiles.

## Consumer setup

The generator is a **separate analyzer package**, version 1.3.1, kept in sync
with the runtime release. This patch does not add generator capabilities or
interpret runtime Profiles. Version 1.3.0 was published and independently
consumed from NuGet; the 1.3.1 source version alone is not publication evidence.
Both packages are released manually. No active GitHub Actions workflow remains;
see [manual release](../../docs/manual-release/README.md).

The generator is a **separate analyzer package**. Reference both `Ling.Mapper`
and `Ling.Mapper.Generator`; installing only the runtime package does not enable
generation. For a source checkout, use a normal project reference to the runtime
and an analyzer-only reference to this project:

```xml
<ProjectReference Include="path/to/src/Ling.Mapper.csproj" />
<ProjectReference Include="path/to/generators/Ling.Mapper.Generator/Ling.Mapper.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

For packaged consumption, use a normal `PackageReference` to `Ling.Mapper` and
an analyzer `PackageReference` to `Ling.Mapper.Generator` with `PrivateAssets="all"`.
The generator package contains its DLL under `analyzers/dotnet/cs`; Roslyn loads
it automatically. The analyzer targets netstandard2.0 and uses Roslyn 4.14 APIs;
the runtime source currently requires a C# 14-capable SDK. Runtime package target
frameworks remain net6.0/net8.0/net9.0/net10.0. The native smoke project targets
net10.0; no claim is made that every library target supports Native AOT publishing.

```csharp
using Ling.Mapper;
using Ling.Mapper.Extensions;
using Ling.Mapper.Provider;
[assembly: GeneratedMap(typeof(Source), typeof(Destination))]

MapperProvider.SetCurrent(GeneratedMapperRegistry.CreateMapper());
var mapped = new Source { Id = "42" }.Adapt<Destination>();
public sealed class Source { public string Id { get; set; } = ""; }
public sealed class Destination { public int Id { get; set; } }
```

On Native AOT, default `MapperConfiguration.CreateMapper()` uses generated
dispatch. On JIT, explicitly select `GeneratedMapperRegistry.CreateMapper()` to
compare generated behavior with the runtime mapper. The object overload uses the
actual source type, so a registered type boxed as object works; an unknown pair
throws a safe `MappingException` identifying `NotSupportedException`, without
falling back to reflection.

## Generated contract and deliberate boundaries

Supported: concrete DTOs with public parameterless constructors and public
read/write properties; explicit scalar pairs; matching options, ignore names,
null policy, callbacks; registered nested pairs; arrays/lists (including common
read-only/enumerable destination interfaces); per-root cycle/shared-reference
context; checked scalar errors with safe member/index paths. Numeric conversion
uses the shared runtime scalar conversion semantics, including midpoint-to-even
rounding, not truncating casts. Enum destinations use typed `Enum.Parse<TEnum>`
or the declared enum underlying numeric type.

Each nested object pair must also have a `GeneratedMap` declaration. Declared
pairs automatically register array/list collection shapes. Nullable source pairs
account for CLR boxing of non-null nullable values. Getter, constructor and setter
exceptions are wrapped without retaining unsafe original exceptions.

Dotted ignored-property paths are explicitly rejected in generated mode, rather
than silently ignored. Top-level masks are preserved per collection item and
reference-cache projection.

Not interpreted: runtime Profiles, ForMember expressions, ReverseMap, arbitrary
conventions, and TypeConverterRegistry registrations. These must not silently
change generated behavior. Unsupported destination construction (including
required/init-only DTOs) is a build error `LMG001`; unsupported matched member
conversions such as undeclared nested pairs or dictionaries produce `LMG002`.
Immutable-constructor/dictionary capabilities of the runtime mapper must not be
presented as automatically supported by this generated subset. A deliberately
hand-written AOT-safe mapping can be registered with the typed
`GeneratedMapperRegistry.Register<TSource, TDestination>` API.

## Native verification

The executable test is `tests/Ling.Mapper.AotSmoke`, not the interactive legacy
console. It checks generated execution, dynamic object dispatch, null policy,
rounding, enums, collections, cycles, context isolation, concurrent roots and
safe diagnostic paths. EmitCompilerGeneratedFiles preserves the generated code
under its obj/generated directory.

```sh
dotnet run --project tests/Ling.Mapper.AotSmoke -c Release
dotnet publish tests/Ling.Mapper.AotSmoke -c Release -r linux-x64 \
  -p:NativeSmoke=true -p:CppCompilerAndLinker=gcc
./tests/Ling.Mapper.AotSmoke/bin/Release/net10.0/linux-x64/publish/Ling.Mapper.AotSmoke --require-aot
```

A successful build alone is not an AOT validation. Run the native executable and
check `dynamic-code-supported=False` and its assertion summary. Compiler/linker
prerequisites and the matching official ILCompiler package must be installed.

### Historical 1.3.0 native build environment

The following records historical 1.3.0 validation only. Native AOT execution was
not rerun or verified for 1.3.1; see [1.3.1 validation](../../docs/1.3.1-validation.md).

The validation workspace prohibits local IPC sockets, and its escalation launcher
also failed before executing the compiler. Native publishing was completed using
MSBuild's documented `UsingTask Override="true"` and `AssemblyTaskFactory` to run
the **unmodified official** `ComputeManagedAssemblies` ILLink task in the current
process, instead of its socket-dependent TaskHost. This was a temporary build-only
target, not a modification to ILCompiler, generated source, runtime code, or OS
security settings. Standard machines do not need this adaptation. The native
binary still reports `RuntimeFeature.IsDynamicCodeSupported == false`.
