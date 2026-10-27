// NEW concurrency probe. Prepared source only; not a historical benchmark recovery.
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ReconstructedMapperParity;
using LM=Ling.Mapper.Mapper.IMapper;
using Ling.Mapper;
using Ling.Mapper.Models;
namespace MapperConcurrencyProbe;
public static class Program {
 const int BatchSize=64;
 const long ExpectedChecksum=14950; // sum(3*i+1), i=0..99, checked fully on EVERY mapped result.
 sealed class WorkerState {
  public required LM Mapper;
  public required List<CustomSource> Source;
  public long Operations,Checksum,AllocatedBytes,StartTimestamp,EndTimestamp;
  public string? Error;
 }
 static LM NewMapper(){var c=new Ling.Mapper.Configuration.MapperConfiguration{DefaultAdaptOptions=AdaptOptions.Strict};c.AddProfile(new Profile());return c.CreateMapper();}
 public static int Main(string[] args){
  CultureInfo.DefaultThreadCurrentCulture=CultureInfo.InvariantCulture;CultureInfo.DefaultThreadCurrentUICulture=CultureInfo.InvariantCulture;
  if(args.Contains("--validate")){Validation.Run();var input=Data.Custom100();var sharedProbe=NewMapper();
   foreach(var mapper in new[]{sharedProbe,sharedProbe,sharedProbe,sharedProbe,NewMapper(),NewMapper(),NewMapper(),NewMapper()}){
    var state=new WorkerState{Mapper=mapper,Source=input};if(MapAndChecksum(state)!=ExpectedChecksum||MapAndChecksum(state)!=ExpectedChecksum)throw new InvalidOperationException("preflight measured checksum contract");
   }
   if(input.Where((s,i)=>s.Id!=i).Any()||UserWork.Trace!=null)throw new InvalidOperationException("preflight source/trace");
   Console.WriteLine("PASS exact14950 checksum on shared/private mapper instances and one shared readonly input; concurrency timing NOT_RUN");return 0;}
  if(Environment.GetEnvironmentVariable("MAPPER_CONCURRENCY_TIMING_GO")!="1")throw new InvalidOperationException("Requires separate source/correctness/CPU GO; no automatic timing.");
  string mode=Get(args,"--mode")??throw new ArgumentException("--mode shared-1|shared-2|shared-4|private-4 required");
  bool shared=mode.StartsWith("shared-");int workers=mode switch{"shared-1"=>1,"shared-2"=>2,"shared-4"=>4,"private-4"=>4,_=>throw new ArgumentException("unsupported scope")};
  const double warmSeconds=2,measureSeconds=3;
  // Validate once before any worker exists; Trace remains null during all concurrent work.
  Validation.Run();if(UserWork.Trace!=null)throw new InvalidOperationException("audit trace still enabled");
  LM? sharedMapper=shared?NewMapper():null;
  var sharedReadOnlySource=Data.Custom100();
  var states=Enumerable.Range(0,workers).Select(_=>new WorkerState{Mapper=sharedMapper??NewMapper(),Source=sharedReadOnlySource}).ToArray();
  // Compile/warm every actual mapper instance on the measuring thread only for preflight.
  foreach(var state in states){var a=state.Mapper.Map<List<CustomDestination>>(state.Source)!;var b=state.Mapper.Map<List<CustomDestination>>(state.Source)!;CheckOne(state.Source,a);CheckOne(state.Source,b);GraphAssert.Disjoint(a,b);}
  using var ready=new CountdownEvent(workers);using var warmStart=new ManualResetEventSlim();using var warmDone=new CountdownEvent(workers);
  using var measureStart=new ManualResetEventSlim();using var measureDone=new CountdownEvent(workers);
  long warmDeadline=0,measureDeadline=0;int failed=0;
  void Worker(object? obj){var s=(WorkerState)obj!;ready.Signal();warmStart.Wait();
   try{while(Stopwatch.GetTimestamp()<Volatile.Read(ref warmDeadline)){for(int n=0;n<BatchSize;n++)MapAndChecksum(s);if(Volatile.Read(ref failed)!=0)break;}}
   catch(Exception e){s.Error=e.ToString();Volatile.Write(ref failed,1);}finally{warmDone.Signal();}
   measureStart.Wait();
   try{if(s.Error!=null)return;
    long allocated=GC.GetAllocatedBytesForCurrentThread();s.StartTimestamp=Stopwatch.GetTimestamp();long ops=0,checksum=0;
    while(Stopwatch.GetTimestamp()<Volatile.Read(ref measureDeadline)){
     for(int n=0;n<BatchSize;n++){checksum+=MapAndChecksum(s);ops++;}
     if(Volatile.Read(ref failed)!=0)break;
    }
    s.EndTimestamp=Stopwatch.GetTimestamp();s.AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;s.Operations=ops;s.Checksum=checksum;
    if(ops==0||checksum!=checked(ops*ExpectedChecksum))throw new InvalidOperationException("worker checksum/operation mismatch");
   }catch(Exception e){s.Error=e.ToString();Volatile.Write(ref failed,1);}finally{measureDone.Signal();}
  }
  var threads=states.Select((s,i)=>new Thread(Worker){Name="mapper-long-worker-"+i,IsBackground=true}).ToArray();
  for(int i=0;i<workers;i++)threads[i].Start(states[i]);
  if(!ready.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("worker startup");
  Volatile.Write(ref warmDeadline,Stopwatch.GetTimestamp()+(long)(warmSeconds*Stopwatch.Frequency));warmStart.Set();
  if(!warmDone.Wait(TimeSpan.FromSeconds(warmSeconds+60)))throw new TimeoutException("warmup");
  using var process=Process.GetCurrentProcess();process.Refresh();var cpuBefore=process.TotalProcessorTime;
  int[] gcBefore={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
  long wallStart=Stopwatch.GetTimestamp();Volatile.Write(ref measureDeadline,wallStart+(long)(measureSeconds*Stopwatch.Frequency));measureStart.Set();
  if(!measureDone.Wait(TimeSpan.FromSeconds(measureSeconds+60)))throw new TimeoutException("measurement");
  long wallEnd=Stopwatch.GetTimestamp();process.Refresh();var cpuAfter=process.TotalProcessorTime;
  int[] gcAfter={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
  foreach(var thread in threads)thread.Join();
  // Postflight outside the timed region; sources must still be untouched.
  foreach(var s in states)for(int i=0;i<100;i++)if(s.Source[i].Id!=i){s.Error="source mutated";Volatile.Write(ref failed,1);}
  long operations=states.Sum(s=>s.Operations),bytes=states.Sum(s=>s.AllocatedBytes);double wall=(wallEnd-wallStart)/(double)Stopwatch.Frequency;
  var assembly=typeof(LM).Assembly;string assemblyHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant();
  long? affinity=null;try{affinity=process.ProcessorAffinity.ToInt64();}catch(PlatformNotSupportedException){}
  var result=new {schema="reconstructed-c100-concurrency-v1",sourceSha=Environment.GetEnvironmentVariable("MAPPER_SOURCE_SHA"),assemblyHash,assembly.Location,
   mode,sharedMapper=shared,sourceSharing="one-readonly-C100-source-across-all-workers",workers,warmSeconds,requestedMeasureSeconds=measureSeconds,wallSeconds=wall,
   processCpuSeconds=(cpuAfter-cpuBefore).TotalSeconds,cpuSecondsPerWallSecond=(cpuAfter-cpuBefore).TotalSeconds/wall,
   processorCount=Environment.ProcessorCount,affinityMask=affinity,runtime=Environment.Version.ToString(),tieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
   gcServer=System.Runtime.GCSettings.IsServerGC,gcLatencyMode=System.Runtime.GCSettings.LatencyMode.ToString(),
   operations,operationsPerSecond=operations/wall,threadAllocatedBytes=bytes,bytesPerOperation=operations==0?(double?)null:bytes/(double)operations,
   gcCollections=gcAfter.Zip(gcBefore,(a,b)=>a-b).ToArray(),batchSize=BatchSize,checksumReadsPerMap=100,expectedChecksumPerMap=ExpectedChecksum,
   traceEnabled=UserWork.Trace!=null,success=Volatile.Read(ref failed)==0,
   workerResults=states.Select((s,i)=>new {worker=i,s.Operations,s.Checksum,s.AllocatedBytes,
    startOffsetMs=(s.StartTimestamp-wallStart)*1000.0/Stopwatch.Frequency,endOffsetMs=(s.EndTimestamp-wallEnd)*1000.0/Stopwatch.Frequency,s.Error}).ToArray()};
  Console.WriteLine("RESULT_JSON "+JsonSerializer.Serialize(result));return failed==0?0:2;
 }
 static long MapAndChecksum(WorkerState s){var d=s.Mapper.Map<List<CustomDestination>>(s.Source)!;if(d.Count!=100)throw new InvalidOperationException("result count");long sum=0;for(int i=0;i<100;i++)sum+=d[i].Value;return sum;}
 static void CheckOne(List<CustomSource> s,List<CustomDestination> d){if(d.Count!=100)throw new InvalidOperationException("count");for(int i=0;i<100;i++)if(d[i].Value!=3*i+1)throw new InvalidOperationException("value");GraphAssert.Disjoint(s,d);}
 static string? Get(string[] args,string name){int p=Array.IndexOf(args,name);return p>=0&&p+1<args.Length?args[p+1]:null;}
}
