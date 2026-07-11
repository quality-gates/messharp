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

# When the host routes HTTPS through an egress proxy (corporate networks,
# remote agent sandboxes), the container needs the host network to reach a
# loopback proxy, the proxy env vars, and the proxy's CA bundle for TLS.
PROXY_ARGS=()
if [[ -n "${HTTPS_PROXY:-}" ]]; then
  PROXY_ARGS+=(--network host -e HTTPS_PROXY -e NO_PROXY)
  if [[ -n "${SSL_CERT_FILE:-}" && -f "${SSL_CERT_FILE}" ]]; then
    PROXY_ARGS+=(-v "${SSL_CERT_FILE}:/tmp/host-ca-bundle.pem:ro" -e SSL_CERT_FILE=/tmp/host-ca-bundle.pem)
  fi
fi

exec docker run --rm \
  ${PROXY_ARGS[@]+"${PROXY_ARGS[@]}"} \
  -v "$REPO_ROOT":/src \
  -v messharp-nuget:/root/.nuget \
  -v messharp-dotnet:/root/.dotnet \
  -w /src \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet "$@"
