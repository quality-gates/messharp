## FAIL — `b68ed29d2aaede967613b5000d4f72704c9f0760`

**Do not land this head.** The earlier blockers are fixed, but new compiler-valid overload cases still violate #250’s contract.

### Reproducible blockers

Under `.independent250-b68ed29/new-challenges/`:

| Fixture | Compiler/runtime behavior | CLI result |
|---|---|---|
| `NativeSilent/Probe.cs:14` | A `nint` variable cannot implicitly convert to the custom overload’s `int` parameter. Standard LINQ `Contains` enumerates the effectful iterator. | **Caller silent**—neither input nor unknown. |
| `ParamsMismatch/Probe.cs:14,18` | Custom `params string[]` overload cannot accept the `int` argument. Standard `Contains` executes; no custom output occurs. | Missed caller input and **false `ImplicitOutput`**, including on the pure twin. |
| `ConstantLocal/Probe.cs:15,20` | `const int constant = 1` validly converts to the custom overload’s `short` parameter. Custom method writes output without enumerating. | **False iterator input**, lost custom output; pure caller silent despite writing output. |

Native-integer and enum variants independently reproduce incompatible custom-effect transfers. These blockers build without warnings/errors and pass runtime assertions confirming actual dispatch.

Reproduce:
```sh
P=.independent250-b68ed29/new-challenges/NativeSilent
scripts/dotnet.sh build "$P/Verify.csproj"
scripts/dotnet.sh run --no-build --project "$P/Verify.csproj"
scripts/dotnet.sh run --no-build --project src/MessSharp -- \
  "$P/Probe.cs" text explicitness --strict
```
Exits: **0 / 0 / 2**.

### Passing verification

- `scripts/dotnet.sh build --no-restore`: passed.
- `scripts/dotnet.sh test --no-build --logger 'console;verbosity=minimal'`: **1,270 passed**, none failed/skipped.
- CLI self-analysis on `./src`, `text csharp --ignore-tests`: exit **0**.
- All seven prior challenge projects: compile/runtime checks pass; previous narrowing and incompatible-extension-effect failures are fixed.
- Retained reproductions: **160/160 assertions pass**, covering standard consumers, pure twins, unknown sources, aliases, custom dispatch, and execution sites.
- Earlier-unknown attribution is **not a blocker**, per #251 and the PR disclosure.

Full commands, results, blockers, and limits: **`.independent250-b68ed29/REPORT.md`**.

All **1,882 prior evidence files remain unchanged**. Tracked files and GitHub state are unchanged. No prohibited XML scan or equivalent retry, host `dotnet`, or agents. Verification is not exhaustive C# binding coverage; no claim that every remaining defect is newly introduced.

**Stopped; nothing landed.**
