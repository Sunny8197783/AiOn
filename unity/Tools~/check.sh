#!/usr/bin/env bash
# Compiles the Unity project's C# without opening Unity: csc against the installed
# editor's reference DLLs, the same .NET Standard 2.1 profile Unity uses, then runs
# the Logic tests on .NET. Usage: bash "Tools~/check.sh" [logic|runtime|editor|tests]
# ponytail: URP/ and InputSys/ are not compiled here (their packages are source-only
# until Unity resolves them); keep them small and check them in the editor.
set -euo pipefail
cd "$(dirname "$0")/.."
UNITY="${UNITY_EDITOR_DATA:-/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data}"
CSC="${CSC:-/c/Program Files/dotnet/sdk/10.0.300/Roslyn/bincore/csc.dll}"
NETREF="${NETREF:-$(ls -d "/c/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/"*/ref/net*.0 | tail -1)}"
OUT="Tools~/out"; mkdir -p "$OUT"
win() { cygpath -w "$1" 2>/dev/null || echo "$1"; }
DEFS="UNITY_6000_0_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_2022_3_OR_NEWER;ENABLE_LEGACY_INPUT_MANAGER;UNITY_STANDALONE;UNITY_STANDALONE_WIN"
WARN=(-nologo -nostdlib -utf8output -langversion:9.0 -nowarn:1701,1702,0649,0414)
NETSTD="-r:$(win "$UNITY/NetStandard/ref/2.1.0/netstandard.dll")"
ENGINE=(); for f in "$UNITY/Managed/UnityEngine/"UnityEngine.*Module.dll; do ENGINE+=("-r:$(win "$f")"); done
EDITOR=(); for f in "$UNITY/Managed/UnityEngine/"UnityEditor.*Module.dll; do EDITOR+=("-r:$(win "$f")"); done
NEWTON="-r:$(win "$UNITY/Managed/Newtonsoft.Json.dll")"
LOGIC="-r:$(win "$OUT/Gyeongbo.Logic.dll")"
RUNTIME="-r:$(win "$OUT/Gyeongbo.Runtime.dll")"

# compile <name> <exe|library> <colon-separated source dirs> <args...>
compile() {
  local name=$1 kind=$2 dirs=$3; shift 3
  local rsp="$OUT/$name.rsp" dll="$OUT/$name.dll"; rm -f "$dll"
  IFS=: read -ra d <<< "$dirs"
  { printf '"%s"\n' "${WARN[@]}" "-target:$kind" "-out:$(win "$dll")" "$@"
    find "${d[@]}" -name '*.cs' 2>/dev/null | while read -r f; do printf '"%s"\n' "$(win "$f")"; done; } > "$rsp"
  echo "== $name"
  dotnet "$CSC" -noconfig "@$(win "$rsp")" | grep -v '^$' || true
  [ -f "$dll" ] || { echo "FAILED: $name"; exit 1; }
}

step=${1:-all}
case $step in all|logic|runtime|editor) compile Gyeongbo.Logic library Assets/Scripts/Logic "$NETSTD" -define:"$DEFS";; esac
case $step in all|runtime|editor) compile Gyeongbo.Runtime library Assets/Scripts/Runtime "$NETSTD" -define:"$DEFS" "${ENGINE[@]}" "$NEWTON" "$LOGIC";; esac
case $step in all|editor) compile Gyeongbo.Editor library Assets/Editor "$NETSTD" -define:"$DEFS;UNITY_EDITOR;UNITY_EDITOR_WIN" "${ENGINE[@]}" "${EDITOR[@]}" "$NEWTON" "$LOGIC" "$RUNTIME";; esac
case $step in all|logic|tests)
  # Logic sources + EditMode tests + a tiny NUnit stand-in, run as a .NET console app.
  REFS=(); for f in "$NETREF"/*.dll; do REFS+=("-r:$(win "$f")"); done
  compile LogicTests exe "Assets/Scripts/Logic:Assets/Tests/EditMode:Tools~/nunit" "${REFS[@]}"
  ver=$(basename "$(dirname "$(dirname "$NETREF")")"); tfm=$(basename "$NETREF")
  printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$tfm" "$ver" > "$OUT/LogicTests.runtimeconfig.json"
  dotnet "$OUT/LogicTests.dll";;
esac
echo "check ok"
