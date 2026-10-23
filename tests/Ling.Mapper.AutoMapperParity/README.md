# AutoMapper date-conversion parity

This isolated executable compares **actual AutoMapper** results with Ling.Mapper. AutoMapper is a **test-only dependency**; the production library does not reference it. The default package is AutoMapper 16.2.0. The test configures only generic DTO container maps, with no custom date converters, formatting, or culture overrides inside AutoMapper.

## Matrix

- Current cultures: en-US, de-DE, fr-FR, zh-CN, tr-TR.
- Entries: root, DTO member, array item, nested DTO member.
- Values: DateTime, DateTimeOffset, DateOnly, TimeOnly, TimeSpan; nullable date values.
- Parsing: absent offset, Z, +08:00, -05:00, US DST gap/overlap dates, local date formats, date-only/time-only text, fractional time span, invalid and empty input.
- Equality includes DateTime ticks **and Kind**, and DateTimeOffset ticks **and Offset**. The process timezone is not changed in-process; run separate processes below.
- Every case continues after failures. Any parity failure throws an aggregate and exits unsuccessfully.

### Deliberate compatibility boundaries

AutoMapper 13.0.1 and 16.2.0 do not natively support string-to-DateOnly/TimeOnly. Ling keeps these existing extensions using current-culture Parse. Those rows are observations, not claims of AutoMapper parity; `DateCultureRegressionTests` asserts the extension contracts.

AutoMapper's non-generic root `Map(null, typeof(string), typeof(DateTime))` returns null, whereas Ling's documented null policy returns the default non-nullable value (or throws when configured). These root null-policy rows are excluded from date-behavior parity. Member and collection null/default behavior and nullable null behavior are compared normally.

Numeric/money conversion remains invariant in Ling. This harness is not an attempt to change numeric behavior to match AutoMapper.

## Run

From the repository root, with the .NET 10 SDK available:

```sh
dotnet build tests/Ling.Mapper.AutoMapperParity/AutoMapperParity.csproj -c Release
for zone in UTC Asia/Shanghai America/New_York; do
  TZ="$zone" dotnet tests/Ling.Mapper.AutoMapperParity/bin/Release/net10.0/AutoMapperParity.dll
done
```

The separate `DateCultureRegressionTests` suite checks the same default BCL date contracts, including the public `ScalarConversion.Convert` entry and the retained DateOnly/TimeOnly extensions, without requiring AutoMapper.

### Historical .NET 6 baseline (explicit opt-in only)

AutoMapper 13.0.1 supports .NET 6 but has a reported high-severity vulnerability: [GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x). It is **not** the recommended/default version. Use it only as an isolated historical compatibility test, with these fixed local fixtures and no untrusted input. Do not suppress the NuGet vulnerability warning or introduce it into the production dependency graph.

```sh
dotnet build tests/Ling.Mapper.AutoMapperParity/AutoMapperParity.csproj -c Release -p:TargetFramework=net6.0 -p:AutoMapperVersion=13.0.1
for zone in UTC Asia/Shanghai America/New_York; do
  TZ="$zone" dotnet tests/Ling.Mapper.AutoMapperParity/bin/Release/net6.0/AutoMapperParity.dll
done
```

A .NET 6 runtime and targeting pack are required for this optional run. Rebuild with default properties afterward to restore the safe default package graph.

## Established default date behavior

All five date/time types convert to string using their parameterless `ToString()`, hence the current culture. DateTime formatting can lose fractional precision and Kind; TimeOnly default formatting can omit seconds. Round-trip serialization is not promised by this default.

String-to-DateTime uses `Convert.ToDateTime(string)`: absent offsets produce Unspecified DateTime; explicit offsets/Z produce local-time DateTime. String-to-DateTimeOffset uses parameterless Parse: absent offsets use the local timezone and explicit offsets are retained. TimeSpan uses parameterless Parse. DateOnly/TimeOnly parsing remains a Ling extension as described above.
