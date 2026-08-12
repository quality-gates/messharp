#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "usage: $0 INPUT_ARCHIVE OUTPUT_ARCHIVE EXECUTABLE SOURCE_DATE_EPOCH" >&2
  exit 64
fi

input_archive="$1"
output_archive="$2"
executable_name="$3"
source_date_epoch="$4"
working_directory="$(mktemp -d)"
trap 'rm -rf "$working_directory"' EXIT

if [[ ! "$source_date_epoch" =~ ^[0-9]+$ ]]; then
  echo "SOURCE_DATE_EPOCH must be a non-negative integer." >&2
  exit 1
fi

printf 'LICENSE\n%s\n' "$executable_name" | LC_ALL=C sort > "$working_directory/expected"
tar -tzf "$input_archive" | LC_ALL=C sort > "$working_directory/actual"
if ! diff -u "$working_directory/expected" "$working_directory/actual"; then
  echo "The unsigned archive has an unexpected payload." >&2
  exit 1
fi

tar -xzf "$input_archive" -C "$working_directory"
executable="$working_directory/$executable_name"
test -x "$executable"
codesign --force --sign - "$executable"
codesign --verify --strict "$executable"

mkdir -p "$(dirname "$output_archive")"
SOURCE_DATE_EPOCH="$source_date_epoch" python3 - \
  "$working_directory" "$output_archive" "$executable_name" <<'PY'
import gzip
import os
import pathlib
import tarfile
import sys

source = pathlib.Path(sys.argv[1])
output = pathlib.Path(sys.argv[2])
executable = sys.argv[3]
with output.open("wb") as destination:
    with gzip.GzipFile(fileobj=destination, mode="wb", mtime=0) as compressed:
        with tarfile.open(fileobj=compressed, mode="w") as archive:
            for name, mode in (("LICENSE", 0o644), (executable, 0o755)):
                path = source / name
                info = archive.gettarinfo(str(path), arcname=name)
                info.uid = info.gid = 0
                info.uname = "root"
                info.gname = "wheel"
                info.mtime = int(os.environ["SOURCE_DATE_EPOCH"])
                info.mode = mode
                with path.open("rb") as payload:
                    archive.addfile(info, payload)
PY

mkdir "$working_directory/verify"
tar -xzf "$output_archive" -C "$working_directory/verify"
codesign --verify --strict "$working_directory/verify/$executable_name"
