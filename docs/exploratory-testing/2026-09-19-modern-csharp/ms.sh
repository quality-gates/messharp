#!/usr/bin/env bash
# Replay helper: run the Release-built MessSharp CLI through the Docker wrapper.
# Usage (from repo root): docs/exploratory-testing/2026-09-19-modern-csharp/ms.sh <cli args...>
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
exec "$ROOT/scripts/dotnet.sh" src/MessSharp/bin/Release/net8.0/messharp.dll "$@"
