using System.Diagnostics;
using Ling.Mapper.Extensions;

namespace TestConsole.Utils;

/// <summary>Fixed workloads, checked results, no console I/O inside measured loops.</summary>
internal static class CleanBenchmark
{
    private static object? sink;
    public static void Run()
    {
        Console.WriteLine($"CLEAN BENCHMARK runtime={Environment.Version}; processors={Environment.ProcessorCount}; iterations=100000; rounds=5");
        var source = new Item { Id = 42, Name = "benchmark", Value = 123.5m };
        var collection = Enumerable.Range(0, 100).Select(i => new Item { Id = i, Name = "item", Value = i }).ToList();
        var failures = new List<Exception>();
        RunCase("same-type object", () => Measure("same-type object", () => source.Adapt<Item>(), x => x.Id == 42 && x.Name == "benchmark" && x.Value == 123.5m));
        RunCase("numeric property", () => Measure("numeric property", () => source.Adapt<NumericItem>(), x => x.Id == 42L && x.Value == 123.5m));
        RunCase("object collection (100 items)", () =>
        {
            // Validate independent root operations BEFORE warmup and timing. A stale
            // child-object cache can look fast while returning an earlier mapping.
            // Keep the measured workload unchanged after restoring the source.
            var first = collection.Adapt<List<NumericItem>>();
            if (first.Count != 100 || first[0].Id != 0 || first[99].Id != 99)
                throw new InvalidOperationException("Initial collection result is incorrect.");
            int originalId = collection[0].Id;
            try
            {
                collection[0].Id = 123456;
                var second = collection.Adapt<List<NumericItem>>();
                if (second.Count != 100 || second[0].Id != 123456 || second[99].Id != 99)
                    throw new InvalidOperationException("A second root map returned stale child data after the source changed.");
            }
            finally
            {
                collection[0].Id = originalId;
            }
            Measure("object collection (100 items)", () => collection.Adapt<List<NumericItem>>(),
                x => x.Count == 100 && x[0].Id == originalId && x[99].Id == 99, 1000);
        });
        var numbers = new[] { 1, 2, 3 };
        RunCase("root numeric collection", () => Measure("root numeric collection", () => numbers.Adapt<List<long>>(), x => x.SequenceEqual(new long[] { 1, 2, 3 })));
        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"BENCHMARK FAILED: {failures.Count} case(s). Failed cases are not performance-comparable; a failing baseline must be classified BASELINE_INCORRECT, not faster.");
            throw new AggregateException("Benchmark correctness/execution failures; successful case timings above remain available independently.", failures);
        }

        void RunCase(string name, Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                failures.Add(new InvalidOperationException($"Benchmark case failed: {name}", error));
                Console.Error.WriteLine($"BENCHMARK_CASE_FAILED {name}: {error.Message}; timing comparison invalid for this case.");
            }
        }
    }

    /// <summary>
    /// Shared mapper, dedicated workers, independently owned mutable source DTOs.
    /// Uses only the original public mapper API so the baseline DLL can run it too.
    /// </summary>
    public static void RunConcurrent()
    {
        const int iterations = 50_000;
        const int rounds = 3;
        var mapper = Ling.Mapper.Provider.MapperProvider.Current;
        Console.WriteLine($"CONCURRENT BENCHMARK runtime={Environment.Version}; processors={Environment.ProcessorCount}; per_worker={iterations}; rounds={rounds}; degrees=1,2,8");
        Console.WriteLine("Timing includes per-operation result validation, gate release, scheduling and task completion. Bytes/op uses process-wide GC.GetTotalAllocatedBytes(true), including measured scheduling costs and any other process allocations, not per-thread allocations.");
        var failures = new List<Exception>();
        foreach (int degree in new[] { 1, 2, 8 })
        {
            try
            {
                var rates = new List<double>();
                var allocations = new List<double>();
                for (int round = 0; round < rounds; round++)
                {
                    using var ready = new CountdownEvent(degree);
                    using var startGate = new ManualResetEventSlim(false);
                    var errors = new Exception?[degree];
                    var tasks = new Task[degree];
                    for (int worker = 0; worker < degree; worker++)
                    {
                        int index = worker;
                        // Task construction and per-worker source construction are not timed.
                        tasks[index] = Task.Factory.StartNew(() =>
                        {
                            var source = new Item { Name = "worker-" + index, Value = index + 0.5m };
                            try
                            {
                                for (int warmup = 0; warmup < 2000; warmup++) MapAndCheck(warmup);
                            }
                            catch (Exception ex) { errors[index] = ex; }
                            finally { ready.Signal(); }
                            startGate.Wait();
                            if (errors[index] != null) return;
                            try
                            {
                                for (int iteration = 0; iteration < iterations; iteration++) MapAndCheck(iteration);
                            }
                            catch (Exception ex) { errors[index] = ex; }

                            void MapAndCheck(int iteration)
                            {
                                // Vary the source to catch stale-result caches, not just crashes.
                                source.Id = index * iterations + iteration;
                                var result = mapper.Map<NumericItem>(source);
                                if (result == null || result.Id != source.Id || result.Name != source.Name || result.Value != source.Value)
                                    throw new InvalidOperationException($"Concurrent numeric mapping returned an incorrect result for worker {index}, iteration {iteration}.");
                            }
                        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    }
                    ready.Wait(); // All workers have warmed up and reached the start gate.
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    long allocated = GC.GetTotalAllocatedBytes(precise: true);
                    long start = Stopwatch.GetTimestamp();
                    startGate.Set();
                    Task.WaitAll(tasks);
                    long end = Stopwatch.GetTimestamp();
                    long bytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
                    var workerErrors = errors.Where(error => error != null).Cast<Exception>().ToArray();
                    if (workerErrors.Length != 0) throw new AggregateException("Concurrent benchmark correctness failed.", workerErrors);
                    long operations = (long)degree * iterations;
                    rates.Add(operations * (double)Stopwatch.Frequency / (end - start));
                    allocations.Add((double)bytes / operations);
                }
                rates.Sort(); allocations.Sort();
                Console.WriteLine($"numeric DTO degree={degree}: median_ops/sec={rates[1]:F2}; median_process_bytes/op={allocations[1]:F2}; min_ops/sec={rates[0]:F2}; max_ops/sec={rates[2]:F2}; validated_ops={degree * iterations * rounds}");
            }
            catch (Exception error)
            {
                failures.Add(new InvalidOperationException($"Concurrent benchmark degree {degree} failed.", error));
                Console.Error.WriteLine($"BENCHMARK_CASE_FAILED concurrent degree={degree}: {error.Message}; performance comparison invalid.");
            }
        }
        if (failures.Count != 0)
            throw new AggregateException("Concurrent benchmark failed; invalid results are not performance-comparable.", failures);
    }

    private static void Measure<T>(string name, Func<T> action, Func<T, bool> validate, int iterations = 100_000)
    {
        var sample = action();
        if (!validate(sample)) throw new InvalidOperationException($"Benchmark correctness failed: {name}");
        for (int i = 0; i < 2000; i++) sink = action();
        var times = new List<double>();
        var allocations = new List<double>();
        for (int round = 0; round < 5; round++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++) sink = action();
            long end = Stopwatch.GetTimestamp();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            times.Add((end - start) * 1_000_000_000.0 / Stopwatch.Frequency / iterations);
            allocations.Add((double)bytes / iterations);
        }
        times.Sort(); allocations.Sort();
        Console.WriteLine($"{name}: median_ns/op={times[2]:F2}; median_bytes/op={allocations[2]:F2}; min_ns/op={times[0]:F2}; max_ns/op={times[4]:F2}");
        GC.KeepAlive(sink);
    }
    public sealed class Item { public int Id { get; set; } public string? Name { get; set; } public decimal Value { get; set; } }
    public sealed class NumericItem { public long Id { get; set; } public string? Name { get; set; } public decimal Value { get; set; } }
}
