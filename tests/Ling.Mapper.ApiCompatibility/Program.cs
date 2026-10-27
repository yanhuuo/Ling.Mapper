using Ling.Mapper.Extensions;
using Ling.Mapper.Models;
var source = new Source { Value = 9, Label = "src" };
var many = new[] { source };
string[] ignores = ["Value"];
Action<Destination> result = d => {};
Action<Destination, Source> item = (d, s) => {};
source.Adapt<Destination>();
source.Adapt<Destination>(AdaptOptions.Strict);
source.Adapt<Destination>("Value", "Label");
source.Adapt<Destination>(firstIgnore: "Value", otherIgnores: ["Label"]);
source.Adapt<Destination>(afterMap: result);
AdaptExtensions.Adapt<Destination>(source, afterMap: result);
Func<object?, Action<Destination>, Destination> resultMethod = AdaptExtensions.Adapt<Destination>;
resultMethod(source, result);
Func<Source, Action<Destination, Source>, Destination> objectMethod = AdaptExtensions.Adapt<Destination, Source>;
objectMethod(source, item);
Func<IEnumerable<Source>, Action<Destination, Source>, List<Destination>> itemMethod = AdaptExtensions.Adapt<Destination, Source>;
itemMethod(many, item);
source.Adapt<Destination, Source>(afterMapItem: item);
many.Adapt<Destination, Source>(afterMapItem: item);
#if NEW
source.Adapt<Destination>(ignores);
source.Adapt<Destination>(ignoreNames: ignores);
source.Adapt<Destination>(["Value"]);
source.Adapt<Destination>(Array.Empty<string>());
source.Adapt<Destination>(AdaptOptions.Strict, ignores);
source.Adapt<Destination>(options: AdaptOptions.Strict, ignoreNames: ignores);
source.Adapt<Destination>(result, ignores);
source.Adapt<Destination>(result, "Value", "Label");
source.Adapt<Destination>(AdaptOptions.Strict, result);
source.Adapt<Destination>(AdaptOptions.Strict, result, ignores);
source.Adapt<Destination>(ignoreNames: ignores, afterMap: result, options: AdaptOptions.Strict);
source.Adapt<Destination, Source>(item, ignores);
source.Adapt<Destination, Source>(AdaptOptions.Strict, item);
source.Adapt<Destination, Source>(AdaptOptions.Strict, item, ignores);
source.Adapt<Destination, Source>(ignoreNames: ignores, afterMapItem: item, options: AdaptOptions.Strict);
many.Adapt<Destination, Source>(item, ignores);
many.Adapt<Destination, Source>(AdaptOptions.Strict, item);
many.Adapt<Destination, Source>(AdaptOptions.Strict, item, ignores);
many.Adapt<Destination, Source>(ignoreNames: ignores, afterMapItem: item, options: AdaptOptions.Strict);
source.Adapt((Destination d) => {}, ignores);
source.Adapt((Destination d, Source s) => {}, ignores);
many.Adapt((Destination d, Source s) => {}, ignores);
source.Adapt(AdaptOptions.Strict, (Destination d) => {}, ignores);
source.Adapt(AdaptOptions.Strict, (Destination d, Source s) => {}, ignores);
many.Adapt(AdaptOptions.Strict, (Destination d, Source s) => {}, ignores);
#endif
#if NULL_ONLY
source.Adapt<Destination>(null);
#endif
#if NULL_STRING
source.Adapt<Destination>(null, "Value");
#endif
#if NULL_ARRAY
source.Adapt<Destination>(null, ignores);
#endif
#if OPTIONS_NULL
try { source.Adapt<Destination>(AdaptOptions.Strict, null); }
catch (ArgumentException e) { Console.WriteLine($"options-null: {e.GetType().Name}/{e.ParamName}"); }
#endif
Console.WriteLine("consumer ok");
public sealed class Source { public int Value { get; set; } public string? Label { get; set; } }
public sealed class Destination { public int Value { get; set; } public string? Label { get; set; } }
