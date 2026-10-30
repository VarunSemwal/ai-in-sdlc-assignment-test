# AI augmentation log

**Status:** Draft, written by an AI agent from files in this repo and the working session. Columns marked `[OWNER: ...]` are for the owner to complete; the agent cannot know what the owner changed or rejected. Nothing here was reconstructed from memory: where a prompt is not in the repo or in the session history, it is marked "not recoverable".

## How AI was used

- **Tool / model.** An AI coding assistant in VS Code (Copilot SDK). Model setting: **Auto, not recorded**. The session could not see which model ran, and it may have varied between turns. Subagents used for the eval reviews were general-purpose agents on the same setting.
- **Role of the agent.** Adviser and drafter. The owner supplied the answer key, the two added eval cases, the rulings on disputed verdicts, the provenance statements for those cases and the model setting. The owner makes every decision recorded as `[OWNER DECISION]`.
- **Not independent.** Several prompts ran in one chat session, so earlier answers were in context for later ones. See the decision record, "Weaknesses in how the test was run".

## Per deliverable

### 1. Diagrams and architecture test (assignment items 1 and 2)

| Field | Entry |
| --- | --- |
| Output | [mermaid-diagram.md](./architecture/mermaid-diagram.md): as-built container view and sequence diagram, target-state diagram, citation list, inferred-items list. Test: `ArchitectureTests` in `backend/tests/InvoiceApp.Tests`. |
| Prompts | Not recoverable from the repo. The session history for this step was summarised and the prompts were not saved. |
| What the agent produced | The diagram sources, the citations and inferred-items list, and an architecture test. |
| Verification | The test command is written in the diagram document: `dotnet test backend/tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj --filter FullyQualifiedName~ArchitectureTests`. It passed when run in the session (`Domain_DoesNotReferenceApi`). |
| Agent limitation, disclosed in the session | The Mermaid MCP server was not configured when the diagrams were written, so they were hand-authored and not produced by the MCP, which the assignment asks for. The server is now listed as available; the diagrams have not been regenerated or validated with it. |
| What I changed or rejected | `[OWNER: ...]` |

### 2. Exactly-once decision record (item 3)

| Field | Entry |
| --- | --- |
| Output | [decision-record.md](./decisions/exactly-once-submission/decision-record.md) |
| Prompts | Five prompts, verbatim, with their outputs, in [README.md](./decisions/exactly-once-submission/README.md): (1) baseline grounded in the facts, (2) trade-off comparison, (3) stress test with a stated preference for Option C, (4) 3 a.m. month-end failure, (5) cost to build and run. |
| What the agent produced | The option trade-offs, the stress-test answers and the cost estimate. The record itself is written by the owner. |
| Where the agent was wrong or useless | Twelve specific items in the record's section "Where the agent was wrong or useless" (e.g. self-contradiction on Option C, unsupported "Two"/"Several" crash windows, invented state names, unanchored cost figures, dismissed Service Bus without evidence). |
| Test-method weakness | All five prompts ran in one session, so the stress tests were not independent. The record states this. |
| What I changed or rejected | `[OWNER: ...]` |

### 3. Autonomy policy (item 4)

| Field | Entry |
| --- | --- |
| Output | [autonomy-policy.md](./decisions/autonomy-policy.md) (draft) |
| Prompts | Session prompts, in order. (1) A read-only pass over the `ops/` incident material for the chosen workflow (month-end log triage), producing a task list, a lethal-trifecta check and a missing-facts list. (2) A drafting prompt to write the policy from that output. The exact wording of these is not saved in the repo; the full text is in the session history only. |
| Not run | The "skeptical SRE" attack prompt and the "tighten to one page" prompt were **not run**. The policy shows a "Review disposition" section, so a review step happened at some point; its prompt is not recoverable here. |
| Agent behaviour | The agent labelled thresholds as assumptions and did not invent role names, the log platform or the redaction tooling. It left them as TBD. |
| What I changed or rejected | `[OWNER: day-one level decision, approver roles, ...]` |

### 4. Review evaluation (item 5)

| Field | Entry |
| --- | --- |
| Output | [evals/README.md](./evals/README.md), [results.csv](./evals/results.csv), raw outputs in [raw-outputs-run-1.md](./evals/raw-outputs-run-1.md), [raw-outputs-runs-2-3.md](./evals/raw-outputs-runs-2-3.md), [raw-outputs-run-4.md](./evals/raw-outputs-run-4.md) |
| Prompts | Review prompt v1 (from `ops/evals/README.md`) and v2 (v1 plus one sentence), both quoted in the eval README. The extension prompt for `results.csv` is also quoted there. 48 reviews were run by subagents, one per case per run. |
| What the agent produced | The 48 reviewer outputs, draft verdicts for all 48 rows and the README text. |
| Owner input | The answer key, the two added cases (`case-11`, `case-12`) and their provenance, the choice of v2 as the changed variable, rulings on `case-04` run 4 (not a false alarm) and `case-09` runs 2 and 3 (missed), and the `Auto` model setting. |
| Where the agent was wrong or useless | Eight items in the eval README, section "Where the agent was wrong or useless". Highlights: scoring was not blind, line numbers varied between runs, hedged findings, unverified extra findings on `case-01`, no model identity, and the agent's v2 prediction partly missed. |
| Correction made | The `case-02` run 4 note had line numbers off by four against the key; the agent corrected it after re-checking the diff. |
| What I changed or rejected | `[OWNER: ...]` |

### 5. Risk audit (item 6)

| Field | Entry |
| --- | --- |
| Output | [risk-audit.md](./risk/risk-audit.md) (draft; decision recorded as Option B, accepted by the Service owner on 2026-10-30) |
| Prompts | (1) Evidence-gathering prompt: read-only, audit the month-end log triage agent per the autonomy policy against the lethal trifecta, with a path and line for each claim, "unknown" when unsupported, no `ops/` as current-state evidence, no decision. (2) Draft prompt: write `docs/risk/risk-audit.md` from only that evidence table. Both were given by the owner in the session. |
| Agent limitation | The assignment refers to an agent setup at the end of `ops/README.md`; that file contains none. The audit uses the autonomy-policy setup instead, and says so. |
| Not run | The "argue it is wrong" attack prompt in a fresh session. |
| What I changed or rejected | `[OWNER: ...]` (decision supplied in the session: Option B, Service owner, four revisit triggers) |

### 6. Measurement plan (item 7)

| Field | Entry |
| --- | --- |
| Output | [measurement-plan.md](./measurement/measurement-plan.md) |
| Prompts | (1) Read-only source inventory for lead time, change failure rate and review turnaround. (2) Extraction of the two supportable metrics with exact command, raw output, sample size, date range and arithmetic. (3) Draft the plan with the numbers and the source limitation. All given by the owner. |
| Tools used | `git log`, and the GitHub MCP tools for Actions runs and pull requests (`gh` was not installed). |
| Results | CI failure rate 0 of 9 runs (proxy); commit-to-CI-complete median 46 s, n = 9 (proxy). Review turnaround: not capturable, 0 pull requests. |
| Agent limitation | The agent stated that both numbers are proxies, that no deployment exists, and that commit `7ff4fab` has no run of its own. It first guessed the pair of metrics before checking GitHub; the result came from the queries, not the guess. |
| What I changed or rejected | `[OWNER: ...]` |

## Cross-cutting agent misses

1. Overstated claims that had to be softened (Option C "in every case", A versus B distinction).
2. Numbers given more precision than the evidence supports (crash windows, storage rows, cost ranges).
3. Scoring done by the same agent that set up the runs, with the key visible.
4. Not verifying that subagents obeyed "read only your own diff, one call".
5. Using a tool that was supposed to be used (Mermaid MCP) by hand-authoring instead.
6. Being unable to name the model it was running on.

## Still to do

- Fill in each `[OWNER: ...]` entry.
- Add prompts for deliverable 1, and the policy prompts, if the owner has a saved copy.
- Link or append the demo video (item 8) when recorded.
