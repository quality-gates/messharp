#!/usr/bin/env bash
# Replay helper: run the Release-built MessSharp CLI through the Docker wrapper.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
exec "$ROOT/scripts/dotnet.sh" src/MessSharp/bin/Release/net8.0/messharp.dll "$@"
