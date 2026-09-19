"""Validate SARIF files against the OASIS 2.1.0 schema with URI format checks.
Usage: python validate_sarif.py <schema.json> <file.sarif>... (needs jsonschema + rfc3987)."""
import json, sys
import jsonschema

schema = json.load(open(sys.argv[1]))
validator = jsonschema.Draft7Validator(schema, format_checker=jsonschema.Draft7Validator.FORMAT_CHECKER)
for path in sys.argv[2:]:
    errors = list(validator.iter_errors(json.load(open(path))))
    print(f"{path}: {len(errors)} schema error(s)")
    for e in errors:
        print("  ", list(e.absolute_path), e.message)
