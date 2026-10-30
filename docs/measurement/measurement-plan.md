# Measurement plan: baseline before the on-call rotation

**Status:** Draft, written by an AI agent from queries run on 2026-10-30. The owner must re-run the commands and check the numbers. **Owner:** Service owner (role chosen by the owner, 2026-10-30).

## 1. Scope and honest limits

- Source repo: `VarunSemwal/ai-in-sdlc-assignment-test`, branch `main`.
- The history is 10 commits by one author from 2026-10-21 to 2026-10-30. It is assignment work (documents, diagrams, evals), not service changes. All changes were pushed directly to `main`; there are no pull requests, tags or other branches.
- The real CI ([ci.yml](../../.github/workflows/ci.yml)) builds and tests on push and pull request to `main`. It has no deploy step, and no deployment exists.
- [ops/pipeline/run-timings.csv](../../ops/pipeline/run-timings.csv) and the incident packs describe a *planned* deployment. They are **not** used as measured history here.
- Because of the above, the two captured numbers are **proxies**, and the third metric cannot be captured today.

## 2. The three baseline metrics

| Metric | Definition to use once the rotation exists | Exact source / query | Who runs it, when | Status today |
| --- | --- | --- | --- | --- |
| Lead time for changes | Time from the first commit of a change to it running in production. | First commit: `git log --format="%h\|%P\|%aI\|%cI\|%s" main` (per branch). Deploy time: the deployment record, which **does not exist yet**. | Service owner, weekly from the start of the rotation. | Proxy captured (section 3). |
| Change failure rate | Deployments that cause a failure needing a rollback, hotfix or incident, divided by all deployments. | Failed deploys and rollbacks from the deployment record, which **does not exist yet**. Proxy: `GET /repos/VarunSemwal/ai-in-sdlc-assignment-test/actions/runs?per_page=100`, count `conclusion` other than `success`. | Service owner, weekly. | Proxy captured (section 3). |
| Review turnaround | Time from a pull request being opened to its first review. | `GET /repos/VarunSemwal/ai-in-sdlc-assignment-test/pulls?state=all`, then `GET /repos/.../pulls/{n}/reviews`; first review `submitted_at` minus PR `created_at`. | Service owner, weekly. | **Not capturable** (section 4). |

## 3. Captured numbers (2026-10-30)

Extraction: GitHub Actions workflow runs through the GitHub MCP tool `actions_list` (`list_workflow_runs`), which maps to the REST query above, plus `git log -1 --format=%cI <sha>` for each commit time. Git timestamps (+05:30) were converted to UTC before subtracting.

### 3.1 Change failure rate, as CI failure rate on `main` (proxy)

| Item | Value |
| --- | --- |
| Sample | 9 workflow runs (run numbers 1 to 9), all `event=push`, `branch=main`, `status=completed` |
| Date range | 2026-10-21T06:18:29Z to 2026-10-30T01:51:51Z (run creation) |
| Failures | 0 (all 9 `conclusion=success`) |
| Arithmetic | 0 / 9 = **0%** |

Limits:
- This is a **CI failure rate, not a deployment change failure rate**. There is no deployment, so a true rate has a denominator of 0.
- 9 runs in 9 days is too few to say anything about reliability. The changes are documentation, so a 0% result is expected.
- 10 commits produced 9 runs. Commit `7ff4fab` was pushed together with the merge `ec1e7fb` and has no run of its own.

### 3.2 Lead time for changes, as commit-to-CI-complete time (proxy)

There are no pull requests or feature branches, so "first commit on a branch" and "deployed" do not exist. The only available interval is commit time to CI completion on `main` (run `updated_at`).

| Commit | Commit time (UTC) | CI finished (UTC) | Seconds |
| --- | --- | --- | --- |
| 87998d8 | 2026-10-21 06:18:15 | 06:18:55 | 40 |
| 8ef689f | 2026-10-22 01:38:38 | 01:39:06 | 28 |
| 7d88492 | 2026-10-22 01:48:43 | 01:49:15 | 32 |
| 0869343 | 2026-10-23 01:54:34 | 01:55:45 | 71 |
| 236abf4 | 2026-10-23 02:18:29 | 02:19:15 | 46 |
| 5342a9a | 2026-10-24 12:22:17 | 12:23:08 | 51 |
| fe149b5 | 2026-10-24 12:30:24 | 12:31:15 | 51 |
| ec1e7fb | 2026-10-25 10:25:21 | 10:26:03 | 42 |
| a53d93b | 2026-10-30 01:51:47 | 01:52:37 | 50 |

Arithmetic: n = 9; min 28 s; **median 46 s**; max 71 s; mean = (40+28+32+71+46+51+51+42+50) / 9 = 411 / 9 = 45.7 s.

Limits:
- This measures how fast CI runs after a push, **not** how long a change takes to reach users.
- Commit `7ff4fab` has no run of its own and is excluded. Commit `ec1e7fb` is a merge commit and is included; excluding it gives n = 8.
- One author, direct pushes, no review step, so no waiting time is measured.
- Report the median and the raw values; the sample is too small for a mean to carry weight.

## 4. Source limitation: review turnaround

- **Query run:** list pull requests with `state=all` on the repository (GitHub MCP `list_pull_requests`). **Result:** an empty list (`[]`), so 0 pull requests.
- **Consequence:** no review turnaround can be computed. Git history alone cannot supply it. The merge commit `ec1e7fb` is a pull from `origin/main`, not a reviewed pull request.
- **What must be resolved before the baseline can be captured:**
  1. Changes go through pull requests, with review required on `main`.
  2. A fixed definition of "first review" (first `submitted_at` from a reviewer other than the author), applied to `/pulls/{n}/reviews`.
  3. A minimum sample before a baseline is stated, set by the owner on 2026-10-30: at least 20 pull requests and 20 deployments, over at least 4 weeks.

## 5. Other gaps that must be resolved

- **Deployment record.** Lead time and change failure rate as defined need a deployment event with a timestamp and an outcome. None exists in the repo; `ops/` describes a planned production deployment only.
- **Rollback and incident link.** Change failure rate needs a way to tie an incident or rollback to a change. This is not defined anywhere in the evidence.
- **Representative sample.** The current commits are not service changes. The baseline should be re-captured on real service changes before the rotation starts.

## 6. Re-running the capture

1. Commit times: `git log --format="%h|%P|%aI|%cI|%s" main`.
2. CI runs: `GET /repos/VarunSemwal/ai-in-sdlc-assignment-test/actions/runs?per_page=100` (note `run_number`, `head_sha`, `conclusion`, `created_at`, `updated_at`).
3. Pull requests: `GET /repos/VarunSemwal/ai-in-sdlc-assignment-test/pulls?state=all`.
4. Convert all times to UTC, then compute the figures as in section 3.

## 7. Evidence basis

- All numbers come from the git history and the GitHub Actions and pull-request listings read on 2026-10-30. Nothing was estimated.
- The figures will change as new commits and runs are added.
