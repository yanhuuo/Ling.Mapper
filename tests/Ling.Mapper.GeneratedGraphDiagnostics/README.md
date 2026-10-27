# Deliberate independent-graph compilation failures

```sh
dotnet build tests/Ling.Mapper.GeneratedGraphDiagnostics -c Release
```

Expected: build exit 1 and nine distinct `LMG001` errors, for field-only struct,
opaque private state, read-only state, dictionary graph, system resource and
multidimensional array, expanding generic recursion, a cross-pair of user-defined stream subclasses and an async-disposable destination. An unrelated restore/compilation failure is not a pass.

The generator cannot safely copy these shapes by returning the source or by constructing
an empty/default object and silently discarding its state. There is no arbitrary resource
reflection-copy fallback. This project is intentionally excluded from the solution.
