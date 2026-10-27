# Reconstructed registry-publication contract smoke

New cloud fixture reconstructed 2026-10-27; **not** the lost historical 6,927-check fixture. Public APIs only, fresh process per run, no reflection resetting the registry version, no timing. Generic tag types isolate test registrations.

`LibraryPath` selects an explicit frozen normal DLL; otherwise the project uses a project reference. `TestTargetFrameworks` selects a target framework. Baseline and candidate must run the identical fixture against their own frozen DLLs; actual loaded DLL copies must be verified.

## Contracts tested

- Initial runtime-registry version zero: manually registered generated mapping delegate succeeds. After first runtime registration, both creation of a new generated mapper and use of the existing mapper reject. This is **JIT dispatch/guard** evidence only, not Source Generator compilation or Native AOT execution.
- Same-thread completed registration refreshes previously warmed root/list/array/dictionary mappings and invalidates a cached projection request (runtime converters reject projection). Lists and arrays have exact counts and distinct ordered values 7/8; dictionary count and keys 1/2 are checked, so empty containers cannot pass vacuously.
- Four readers warm shared caches, rendezvous with a writer, then wait for an Event explicitly set after registration completes. Each reader's next 32 shape sets must observe the new converter, including coherent numeric checksum, stamp and text; projection and generated guards are checked too.
- Four readers plus one writer execute 16 Barrier rounds. Within the unsynchronized update window, every individual output must be one complete old/new value. The next barrier completes after registration, followed by exact-new checks; a third barrier prevents the subsequent update from overlapping those convergence checks. Final completed-registration checks run 32 more shape sets. Timeouts and cancellation prevent silent deadlock on assertion failure.
- First registration from source getter and destination constructor is observed by the current child field; immediate reentry and subsequent roots see the new rule. Collection callback registration is observed by reentry and the next item without retroactively changing the first item; exact callback count and item order are asserted. A user converter registers another pair and immediately reenters it.

This tests atomic/acquire publication visibility after explicit synchronization and permits coherent old/new concurrent observations. It does **not** assert that an acquire read equals an RMW full fence, that unsynchronized wall-clock-latest reads are guaranteed, or that a whole mapped graph is one registry-version transaction. The member-plan smoke separately covers profiles, initialization/parallel mappings and the existing pre-registered inline-converter capture behavior.

Example:

```sh
dotnet build RegistryPublicationSmoke.csproj -c Release -p:TestTargetFrameworks=net10.0 -p:LibraryPath=/absolute/frozen/Ling.Mapper.dll
dotnet bin/Release/net10.0/RegistryPublicationSmoke.dll
```

Baseline validation of this new fixture: net6.0/net8.0/net9.0/net10.0 each passed **4,953 checks**, `IntPtr.Size=8`. All four loaded library copies matched their frozen Interlocked.Read baseline DLLs byte-for-byte. New logs are under recovery `version-publication-evidence/publication-baseline-*.log`; these results do not reissue the historical fixture evidence.
