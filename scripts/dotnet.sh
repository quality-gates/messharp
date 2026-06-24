#!/usr/bin/env bash
# Run the dotnet CLI inside Docker. All C# tooling in this repo goes through
# this wrapper -- never a host dotnet install.
#
# Usage: scripts/dotnet.sh <dotnet args...>
#   e.g. scripts/dotnet.sh build
#        scripts/dotnet.sh test
#        scripts/dotnet.sh run --project src/MessSharp -- ./src text csharp
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

exec docker run --rm \
  -v "$REPO_ROOT":/src \
  -v messharp-nuget:/root/.nuget \
  -w /src \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet "$@"
