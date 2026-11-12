#!/usr/bin/env python3
"""Compare compiler-confirmed dispatch with the explicitness CLI for issue 250."""
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile

repo = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
fixtures = Path(__file__).resolve().parent / "fixtures"
expectations = {
    "NativeSilent": ("consume", "no invented input/output; unknown allowed"),
    "NativeNarrowing": ("consume", "no invented input/output; unknown allowed"),
    "ParamsMismatch": ("consume", "no invented input/output; unknown allowed"),
    "EnumMismatch": ("consume", "no invented input/output; unknown allowed"),
    "ConstantLocal": ("custom", "custom"),
}
pattern = re.compile(r":\d+\s+(\w+)\s+The method (\w+)\(\)")
failures = []
subprocess.run(["scripts/dotnet.sh", "build", "-c", "Release"], cwd=repo, check=True, stdout=subprocess.DEVNULL)
with tempfile.TemporaryDirectory(prefix=".verify250-matrix-", dir=repo) as scratch:
    root = Path(scratch)
    for name, (action_expected, pure_expected) in expectations.items():
        case = root / name
        case.mkdir()
        for filename in ("Probe.cs", "Program.cs", "Verify.csproj"):
            shutil.copyfile(fixtures / name / filename, case / filename)
        project = str((case / "Verify.csproj").relative_to(repo))
        source = str((case / "Probe.cs").relative_to(repo))
        subprocess.run(["scripts/dotnet.sh", "build", project], cwd=repo, check=True, stdout=subprocess.DEVNULL)
        runtime = subprocess.run(["scripts/dotnet.sh", "run", "--no-build", "--project", project], cwd=repo, text=True, capture_output=True, check=True)
        cli = subprocess.run(["scripts/dotnet.sh", "src/MessSharp/bin/Release/net8.0/messharp.dll", source, "text", "explicitness", "--strict"], cwd=repo, text=True, capture_output=True)
        if cli.returncode != 2 or cli.stderr:
            failures.append(f"{name}: CLI exit {cli.returncode}, stderr={cli.stderr!r}")
        records = {"Action": [], "Pure": []}
        for line in cli.stdout.splitlines():
            match = pattern.search(line)
            if match and match.group(2) in records:
                records[match.group(2)].append(match.group(1))
        for owner, expected in (("Action", action_expected), ("Pure", pure_expected)):
            actual = records[owner]
            if expected == "consume":
                valid = ("ImplicitInput" in actual or "UnknownEffect" in actual) and "ImplicitOutput" not in actual
            elif expected == "custom":
                valid = ("ImplicitOutput" in actual or "UnknownEffect" in actual) and "ImplicitInput" not in actual
            else:
                valid = "ImplicitInput" not in actual and "ImplicitOutput" not in actual
            print(f"{name} {owner}: expected {expected}, got {actual}")
            if not valid:
                failures.append(f"{name} {owner}: expected {expected}, got {actual}")
        print(f"  runtime: {runtime.stdout.strip()}")
if failures:
    print("\n".join(failures), file=sys.stderr)
    raise SystemExit(1)
print("Compiler-confirmed dispatch matrix passed.")
