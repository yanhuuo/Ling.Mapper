#!/usr/bin/env bash
# Compile fixed consumer calls against a real 1.3.1 baseline and the candidate DLL.
# Usage: DOTNET=/path/to/dotnet ./verify.sh BASELINE_DLL CANDIDATE_DLL [OUTPUT_DIR]
# Requires .NET 10 SDK / C# 14. Does not build or modify either library checkout.
set -euo pipefail
if [[ $# -lt 2 || $# -gt 3 ]]; then
  echo "Usage: $0 BASELINE_DLL CANDIDATE_DLL [OUTPUT_DIR]" >&2
  exit 2
fi
baseline=$(realpath "$1")
candidate=$(realpath "$2")
[[ -f "$baseline" && -f "$candidate" ]] || { echo 'Both DLLs must exist.' >&2; exit 2; }
here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
output=${3:-"$here/.work"}
mkdir -p "$output"
output=$(realpath "$output")
dotnet=${DOTNET:-dotnet}
mkdir -p "$output/home" "$output/xdg/"{run,data,config,cache,state}
# Keep first-use files and named-mutex paths inside this validation directory.
export HOME="$output/home" DOTNET_CLI_HOME="$output/home" NUGET_PACKAGES="$output/nuget" DOTNET_NOLOGO=1
export XDG_RUNTIME_DIR="$output/xdg/run" XDG_DATA_HOME="$output/xdg/data" XDG_CONFIG_HOME="$output/xdg/config" XDG_CACHE_HOME="$output/xdg/cache" XDG_STATE_HOME="$output/xdg/state"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"$dotnet" --info > "$output/dotnet-info.txt"
failed=0
: > "$output/summary.txt"
for version in BASE NEW; do
  library=$baseline
  [[ $version == NEW ]] && library=$candidate
  for scenario in POSITIVE NULL_ONLY NULL_STRING NULL_ARRAY OPTIONS_NULL; do
    definitions=$scenario
    [[ $version/$scenario == NEW/POSITIVE ]] && definitions=NEW
    expected=PASS
    case "$version/$scenario" in
      */NULL_ONLY|NEW/NULL_STRING|NEW/NULL_ARRAY|NEW/OPTIONS_NULL) expected=CS0121 ;;
      BASE/OPTIONS_NULL) expected=CS1503 ;;
    esac
    log="$output/$version-$scenario.log"
    if "$dotnet" build "$here/Consumer.csproj" -c Release --no-incremental --disable-build-servers -m:1 -p:UseSharedCompilation=false \
      "-p:LibraryPath=$library" "-p:DefineConstants=$definitions" \
      "-p:BaseIntermediateOutputPath=$output/obj/" "-p:OutputPath=$output/bin/" > "$log" 2>&1; then
      actual=PASS
    else
      actual=$(grep -o -m1 'error CS[0-9]*' "$log" | head -1 | sed 's/error //' || true)
      [[ -n $actual ]] || actual=BUILD_ERROR
    fi
    if [[ $actual != "$expected" ]]; then
      echo "FAIL $version/$scenario expected=$expected actual=$actual ($log)" | tee -a "$output/summary.txt"
      failed=1
    else
      echo "PASS $version/$scenario expected=$expected" | tee -a "$output/summary.txt"
    fi
    # Invalid-null source forms compiled in 1.3.1 but are not successful runtime calls.
    # Execute only the positive fixture; never execute a stale binary after failure.
    if [[ $scenario == POSITIVE && $actual == PASS ]]; then
      if "$dotnet" "$output/bin/Consumer.dll" > "$output/$version-runtime.log" 2>&1; then
        echo "PASS $version/POSITIVE runtime" | tee -a "$output/summary.txt"
      else
        echo "FAIL $version/POSITIVE runtime ($output/$version-runtime.log)" | tee -a "$output/summary.txt"
        failed=1
      fi
      if [[ $version == BASE ]]; then
        # Run the unchanged baseline-built consumer with only the library replaced.
        cp "$candidate" "$output/bin/Ling.Mapper.dll"
        if "$dotnet" "$output/bin/Consumer.dll" > "$output/BASE-binary-compatibility.log" 2>&1; then
          echo "PASS BASE/POSITIVE candidate DLL binary compatibility" | tee -a "$output/summary.txt"
        else
          echo "FAIL BASE/POSITIVE candidate DLL binary compatibility" | tee -a "$output/summary.txt"
          failed=1
        fi
      fi
    fi
  done
done
exit "$failed"
