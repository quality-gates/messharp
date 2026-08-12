# Homebrew release operations

A stable release starts from an exact `vMAJOR.MINOR.PATCH` tag. The release
workflow builds signed Intel and Apple Silicon archives and publishes an
immutable GitHub release. This event is the release commit point. The workflow
then starts tap publication in `quality-gates/homebrew-tap`.

## One-time repository setup

1. Enable immutable releases for `quality-gates/messharp`.
2. Protect stable release tags so that only release maintainers can create
   them.
3. Create an organization-owned GitHub App with only **Actions: write**. Install
   it only on `quality-gates/homebrew-tap`. Do not add it to a ruleset bypass
   list.
4. Create a protected `homebrew` environment in `messharp`. Add
   `HOMEBREW_TAP_APP_ID` and `HOMEBREW_TAP_APP_PRIVATE_KEY` as environment
   secrets. Permit stable release tags to use this environment.
5. Protect the `homebrew-tap` default branch. Allow Actions to create pull
   requests, and require the tap formula tests.

The source repository does not store a personal access token. The protected job
creates a short-lived GitHub App token that can start a workflow only in the tap
repository.

## Normal release

1. Merge all release changes to `main`.
2. Create and push an exact `vMAJOR.MINOR.PATCH` tag for that commit.
3. Monitor the `Release` workflow and the linked tap workflow.
4. Review and merge the formula candidate after all required tap checks pass.

The workflow validates the stable release identity. It runs the test suite and
self-analysis through `scripts/dotnet.sh`. It then builds self-contained Intel
and Apple Silicon executables. Matching macOS runners apply and verify native ad
hoc signatures. The workflow tests the exact signed archives before it
publishes the immutable stable release.

The stable release contains only these assets:

- `messharp_VERSION_darwin_arm64.tar.gz`
- `messharp_VERSION_darwin_amd64.tar.gz`
- `checksums.txt`

Each archive contains `messharp` and `LICENSE` at its top level. The tap checks
the release ID, tag, source commit, asset names, and SHA-256 values. It then
creates or updates one formula candidate. Tap checks install and test the
formula on Intel and Apple Silicon macOS runners.

## Recovery

Run the `Release` workflow manually. Select the same stable tag as the workflow
reference, and enter that tag in the `tag` input.

The workflow has state-aware retry behavior:

- It keeps a draft asset when its bytes match.
- It uploads only missing draft assets.
- It stops when a draft asset has different bytes or the draft has an extra
  asset.
- It verifies an existing immutable stable release and retries only tap
  publication.
- It stops when an existing published release is mutable or does not match the
  stable tag.
- It does not replace or delete a published asset.

A repeated tap request uses the same formula candidate branch and pull request.
If tap publication fails, keep the stable tag and immutable GitHub release.
Correct the tap workflow, branch policy, environment approval, credentials, or
formula check. Then retry the same stable release.
