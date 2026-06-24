#!/usr/bin/env bash
# Run coverage-guided fuzzing for MessSharp inside Docker.
#
# Usage:
#   FUZZ_SECONDS=60 scripts/fuzz.sh source
#   FUZZ_SECONDS=60 scripts/fuzz.sh ruleset
set -euo pipefail

TARGET="${1:-source}"
FUZZ_SECONDS="${FUZZ_SECONDS:-60}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

case "$TARGET" in
  source|ruleset) ;;
  *)
    echo "usage: scripts/fuzz.sh <source|ruleset>" >&2
    exit 2
    ;;
esac

exec docker run --rm \
  -v "$REPO_ROOT":/src \
  -v messharp-nuget:/root/.nuget \
  -w /src \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -e TARGET="$TARGET" \
  -e FUZZ_SECONDS="$FUZZ_SECONDS" \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  bash -lc '
    set -euo pipefail
    export PATH="$PATH:/root/.dotnet/tools"
    export AFL_I_DONT_CARE_ABOUT_MISSING_CRASHES=1
    export AFL_SKIP_BIN_CHECK=1
    export AFL_SKIP_CPUFREQ=1

    apt-get update >/dev/null
    apt-get install -y --no-install-recommends afl++ >/dev/null

    if ! command -v sharpfuzz >/dev/null 2>&1; then
      dotnet tool install --global SharpFuzz.CommandLine --version 2.3.0 >/dev/null
    fi

    output_dir="fuzz/MessSharp.Fuzz/bin/fuzz"
    findings_dir="fuzz/findings/$TARGET"

    rm -rf "$output_dir" "$findings_dir"
    mkdir -p "$(dirname "$findings_dir")"
    dotnet publish fuzz/MessSharp.Fuzz/MessSharp.Fuzz.csproj -c Release -o "$output_dir" >/dev/null
    sharpfuzz "$output_dir/messharp.dll"

    afl-fuzz \
      -V "$FUZZ_SECONDS" \
      -i "fuzz/corpus/$TARGET" \
      -o "$findings_dir" \
      -t 10000 \
      -m none \
      -- dotnet "$output_dir/MessSharp.Fuzz.dll" "$TARGET"
  '
