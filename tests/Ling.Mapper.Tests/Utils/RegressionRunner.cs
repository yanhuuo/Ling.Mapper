using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace TestConsole.Utils;

/// <summary>Runs every suite in a fresh process so static mapper state cannot leak.</summary>
internal static class RegressionRunner
{
    private static MethodInfo[] Suites => typeof(RegressionRunner).Assembly.GetTypes()
        .Where(t => t.Namespace == "TestConsole.Test")
        .Select(t => t.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes))
        .Where(m => m != null && m.ReturnType == typeof(void)).Cast<MethodInfo>()
        .OrderBy(m => m.DeclaringType!.Name, StringComparer.Ordinal).ToArray();

    public static int Run(string[] args, Action initialize)
    {
        if (args.Length == 1 && args[0] == "--list")
        {
            foreach (var suite in Suites) Console.WriteLine(suite.DeclaringType!.Name);
            return 0;
        }
        if (args.Length == 1 && args[0] == "--benchmark-clean")
        {
            initialize();
            CleanBenchmark.Run();
            return 0;
        }
        if (args.Length == 1 && args[0] == "--benchmark-concurrent")
        {
            initialize();
            CleanBenchmark.RunConcurrent();
            return 0;
        }
        if (args.Length == 2 && args[0] == "--suite")
        {
            var suite = Suites.SingleOrDefault(m => m.DeclaringType!.Name == args[1]);
            if (suite == null) { Console.Error.WriteLine($"Unknown suite: {args[1]}"); return 2; }
            TestHelper.ResetResults();
            initialize();
            var original = Console.Out;
            using var observer = new LegacyFailureObserver(original);
            Console.SetOut(observer);
            Exception? error = null;
            try { suite.Invoke(null, null); }
            catch (Exception ex) { error = ex is TargetInvocationException invocation ? invocation.InnerException ?? ex : ex; }
            finally { Console.SetOut(original); }
            var legacyCount = suite.DeclaringType!.GetField("_failedTests", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as int? ?? 0;
            var failed = error != null || legacyCount > 0 || TestHelper.FailedAssertions > 0 || observer.Failures > 0;
            if (error != null) Console.Error.WriteLine(error);
            Console.WriteLine($"RESULT {suite.DeclaringType.Name}: {(failed ? "FAIL" : "COMPLETED")}; assertions={TestHelper.Assertions}; assertionFailures={TestHelper.FailedAssertions}; legacyFailures={legacyCount}; diagnosticFailures={observer.Failures}");
            if (!suite.DeclaringType.Name.EndsWith("RegressionTests", StringComparison.Ordinal))
                Console.WriteLine("COVERAGE: legacy suite includes demonstration/diagnostic output; completion is not proof that every printed expectation was asserted.");
            return failed ? 1 : 0;
        }
        if (args.Length != 1 || args[0] != "--all")
        {
            Console.Error.WriteLine("Usage: --all | --list | --suite NAME | --benchmark-clean | --benchmark-concurrent");
            return 2;
        }
        var failures = new List<string>();
        foreach (var suite in Suites)
        {
            string name = suite.DeclaringType!.Name;
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(typeof(RegressionRunner).Assembly.Location);
            start.ArgumentList.Add("--suite");
            start.ArgumentList.Add(name);
            using var process = Process.Start(start)!;
            // A hung or stack-overflowing suite must not prevent other suites from executing.
            if (!process.WaitForExit(600_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                Console.Error.WriteLine($"TIMEOUT {name}");
                failures.Add(name);
            }
            else if (process.ExitCode != 0) failures.Add(name);
        }
        Console.WriteLine($"SUITES: {Suites.Length} executed; {failures.Count} failed.");
        Console.WriteLine("Legacy demonstrations remain explicitly unasserted; see the deterministic regression suites for checked contracts.");
        if (failures.Count > 0) Console.WriteLine("FAILED: " + string.Join(", ", failures));
        return failures.Count == 0 ? 0 : 1;
    }

    // Legacy tests sometimes swallow their own exceptions. Observe only explicit failure
    // diagnostics, not arbitrary mentions of exceptions, expected failures or summary icons.
    private sealed class LegacyFailureObserver(TextWriter inner) : TextWriter
    {
        private readonly StringBuilder line = new();
        public int Failures { get; private set; }
        public override Encoding Encoding => inner.Encoding;
        public override void Write(char value)
        {
            inner.Write(value);
            if (value == '\n') { Inspect(line.ToString()); line.Clear(); }
            else if (value != '\r') line.Append(value);
        }
        public override void Write(string? value) { if (value != null) foreach (char c in value) Write(c); }
        public override void WriteLine(string? value) { Write(value); Write('\n'); }
        private void Inspect(string value)
        {
            var text = value.Trim();
            if (text.Contains("测试统计:") || text.Contains("测试统计：")) return;
            if (text.StartsWith("❌", StringComparison.Ordinal)
                || Regex.IsMatch(text, @"^\?+\s.*(失败|意外异常|行为异常|处理异常|StackOverflow)")
                || Regex.IsMatch(text, @"^\?+\s*异常[:：]")
                || Regex.IsMatch(text, @"^(System|Ling\.Mapper)\.[\w.]*Exception:")) Failures++;
        }
    }
}
