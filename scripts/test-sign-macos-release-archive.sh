#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
working_directory="$(mktemp -d)"
trap 'rm -rf "$working_directory"' EXIT

mkdir "$working_directory/input"
cp "$repository_root/LICENSE" "$working_directory/input/LICENSE"
cat > "$working_directory/fixture.c" <<'C'
#include <stdio.h>
int main(void) {
    puts("messharp fixture");
    return 0;
}
C
cc "$working_directory/fixture.c" -o "$working_directory/input/messharp"
tar -czf "$working_directory/unsigned.tar.gz" -C "$working_directory/input" LICENSE messharp

"$repository_root/scripts/sign-macos-release-archive.sh" \
  "$working_directory/unsigned.tar.gz" \
  "$working_directory/signed.tar.gz" \
  messharp 1

mkdir "$working_directory/output"
tar -xzf "$working_directory/signed.tar.gz" -C "$working_directory/output"
printf 'LICENSE\nmessharp\n' > "$working_directory/expected"
tar -tzf "$working_directory/signed.tar.gz" | LC_ALL=C sort > "$working_directory/actual"
diff -u "$working_directory/expected" "$working_directory/actual"
codesign --verify --strict "$working_directory/output/messharp"
test "$("$working_directory/output/messharp")" = "messharp fixture"
