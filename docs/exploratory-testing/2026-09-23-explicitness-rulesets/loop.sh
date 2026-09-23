#!/usr/bin/env bash
# Red while the CLI output for the minimal fixtures differs from the expected findings.
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../../.."
E=docs/exploratory-testing/2026-09-23-explicitness-rulesets
scripts/dotnet.sh build src/MessSharp -c Release -v q -nologo 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error"
{ $E/ms.sh $E/fixtures/arrow text explicitness; $E/ms.sh $E/fixtures/generic text explicitness; } > $E/out/loop-actual.txt 2>&1
if diff <(sed 's/  */ /g' $E/expected-loop.txt) <(sed 's/  */ /g' $E/out/loop-actual.txt); then echo GREEN; else echo RED; exit 1; fi
