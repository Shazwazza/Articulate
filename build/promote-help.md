# Articulate promotion utility

`build/promote.cs` creates and validates a local candidate for an independent LTS/STS branch. It cherry-picks commits that already fit that lane; it does not port version or lock-file changes between lanes or update the target branch. For the dual-lane PR branch (`pr/v18-build`), merge or fast-forward source changes normally. It never pushes or opens a pull request. See [Updating branches](../BUILD.md#updating-branches).

```text
dotnet run --file build/promote.cs -- patch [options]
```

## patch

Create a candidate from a clean target branch and cherry-pick selected commits from a source ref:

```text
dotnet run --file build/promote.cs -- patch \
  --profile v17-lts \
  --base upstream/main \
  --source upstream/develop \
  --commits abc1234,def5678 \
  --manifest build/promotion.json
```

| Option | Required | Notes |
|---|---:|---|
| `--profile` | yes | `v17-lts` or `v18-sts`. Selects target checks and build profile. |
| `--base` | yes | Target branch or commit. The candidate starts here. |
| `--source` | yes | Side-by-side source branch or commit. |
| `--commits` | yes | Comma-separated commit SHAs, applied in the listed order. |
| `--branch` | no | Candidate branch. Defaults to `promote/<profile>/<source-short-sha>`. |
| `--worktree` | no | Candidate worktree. Defaults to a temporary directory. |
| `--manifest` | no | Path for the JSON evidence manifest, relative to the repository root. |
| `--docker` | no | Also run the target worktree's `docker-test`. Requires Docker; cannot be combined with `--skip-build`. |
| `--skip-build` | no | Run Git/reference checks only; skip builds and all runtime tests. Cannot be combined with `--docker`. Use only while preparing or debugging a target branch. |

The command resolves refs to SHAs, verifies that every selected commit belongs to the source ref, creates an isolated worktree from the target base, cherry-picks the commits, checks for forbidden platform references, and runs the target branch's clean build, unit tests and package smoke checks.

When the candidate contains `build/test.cs`, promotion also runs its `fresh` command against the built packages. A failed native E2E run stops promotion before the manifest is written. The manifest's `NativeE2ETested` field records whether that check passed. Older candidates without the runner retain the existing build checks; `--skip-build` also skips native E2E.

A cherry-pick conflict stops the promotion. The command does not resolve conflicts or mutate an upstream branch. Review the candidate worktree, port the change manually when required, and rerun with the resulting commit.

The candidate is local until explicitly pushed:

```text
git -C <candidate-worktree> push origin <candidate-branch>
```

The maintainer opens the pull request against the correct upstream target branch.
