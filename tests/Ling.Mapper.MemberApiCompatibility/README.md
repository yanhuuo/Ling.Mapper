# ForMember consumer compatibility fixture

Build `Consumer.csproj` with `-p:MapperAssembly=/absolute/path/Ling.Mapper.dll`.
Run the resulting `Consumer.dll`. The same source works against the old
expression-only API and the candidate options API, including a legitimate source
method named `Ignore`, named arguments, and typed/untyped-null argument checks.
The fixture targets .NET 10 and uses the installed compiler's `latest` language mode.

```sh
dotnet build Consumer.csproj -c Release -p:MapperAssembly=/path/Ling.Mapper.dll
dotnet bin/Release/net10.0/Consumer.dll
```

For a binary check, copy the complete baseline-built output to a separate folder,
replace **only** its `Ling.Mapper.dll` with the candidate DLL, and run the unchanged
consumer there. Keep all three build/run outputs separate.

## Null-selector behavior

The candidate's `ForMember(destination, null)` still resolves to the original
expression overload in the tested compiler, then throws `ArgumentNullException`
with `ParamName == "srcExpr"`, just as the baseline does. The fixture checks this
at runtime rather than assuming that the new options overload creates an ambiguity.

Use an explicit `Expression<Func<Source, Member>>` cast or the named `srcExpr`
argument for intentional source-expression null validation. A typed-null options
callback still throws `ArgumentNullException` and is covered by
`MemberConfigurationRegressionTests`.

These checks cover the listed call forms and the old binary method signature;
they are not a claim about every possible call under every compiler version.
