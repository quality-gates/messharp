# Workspace handoff snapshot

This branch is a backup, not a pull request or a merge candidate. The files under `issue-169`, `issue-219`, `issue-220`, and `issue-260` preserve changes that were uncommitted in existing worktrees when the run stopped. They have not been verified by this snapshot.

Each directory records the original branch and base commit. `worktree.patch.gz` contains the combined tracked changes against that commit. `index.patch.gz` and `unstaged.patch.gz` preserve the separate index and working-tree states. Decompress a patch with `gzip -dc` before applying it. Files under `untracked/` have `.txt` appended to their original paths.

For #220, `local-commits.patch.gz` also records the commits present in the local branch but absent from current `main`. The existing remote PR branch has different history. Do not force-push it without checking ownership and history.

The #219 changes have already been merged in PR #269. Its original dirty worktree is included only as a precaution.

The #250 implementation is pushed separately in draft PR #271. Its directory here preserves the binding design, last review, handoff, and a compiler-backed matrix. To run the matrix against the PR worktree, use:

```sh
python3 .handoff/issue-250/binding-matrix.py /absolute/path/to/issue-250-worktree
```

The matrix permits `UnknownEffect` for calls whose overload cannot be resolved. Independent review must decide whether that precision tradeoff satisfies #250 before the PR lands.
