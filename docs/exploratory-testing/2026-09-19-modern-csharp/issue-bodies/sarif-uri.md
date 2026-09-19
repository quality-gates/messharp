## Summary

The SARIF renderer writes the raw file path into `artifactLocation.uri`. When a path contains a space, `&`, or non-ASCII characters, the result is not a valid URI reference. The output then fails validation against the OASIS SARIF 2.1.0 schema, which declares `uri` as `format: uri-reference`.

## Reproduction

Repository: `quality-gates/messharp`
Commit: `627bd26b5cf96b4e1cf6e7911dad57707c6e2383`

```console
scripts/dotnet.sh build -c Release
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/ci sarif naming,controversial > ci.sarif
python3 -m venv /tmp/v && /tmp/v/bin/pip install jsonschema rfc3987
curl -sL -o /tmp/sarif-schema.json https://json.schemastore.org/sarif-2.1.0.json
/tmp/v/bin/python docs/exploratory-testing/2026-09-19-modern-csharp/validate_sarif.py /tmp/sarif-schema.json ci.sarif
```

The fixture path is `fixtures/ci/My Project & Co/Café Box.cs`.

Actual result:

```
ci.sarif: 4 schema error(s)
   [..., 'artifactLocation', 'uri'] '.../fixtures/ci/My Project & Co/Café Box.cs' is not a 'uri-reference'
```

Control: the same source copied to `fixtures/ci-plain/CafeBox.cs` produces 0 schema errors.

## Expected

`artifactLocation.uri` should be a percent-encoded URI reference, for example `.../My%20Project%20%26%20Co/Caf%C3%A9%20Box.cs`, using `/` separators on every OS.

## Impact

SARIF consumers that validate URIs may reject the file or fail to map results back to source files. Examples include GitHub code scanning, SARIF viewers and Microsoft's SARIF SDK. The problem only affects repositories with such characters in their paths.

## Evidence

`docs/exploratory-testing/2026-09-19-modern-csharp/out/ci.sarif`, `docs/exploratory-testing/2026-09-19-modern-csharp/out/ci-plain.sarif`, `docs/exploratory-testing/2026-09-19-modern-csharp/validate_sarif.py`.
