#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
dotnet="${DOTNET:-dotnet}"
"$dotnet" publish "$root/tests/Ling.Mapper.AotSmoke/Ling.Mapper.AotSmoke.csproj" -c Release -r linux-x64 -p:NativeSmoke=true -p:CppCompilerAndLinker="${AOT_COMPILER:-gcc}"
"$root/tests/Ling.Mapper.AotSmoke/bin/Release/net10.0/linux-x64/publish/Ling.Mapper.AotSmoke" --require-aot
