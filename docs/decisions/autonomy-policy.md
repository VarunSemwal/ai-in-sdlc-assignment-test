# Autonomy policy: month-end log triage

**Status:** Draft; not approved for day one. **Workflow:** month-end production-log and metric triage. **Levels:** 1 Read, 2 Propose, 3 Act with approval, 4 Act alone. [INC-2041](../../ops/incidents/INC-2041/README.md) and [INC-2047](../../ops/incidents/INC-2047/README.md) describe a planned deployment, not the current code.

## Task boundaries

| Task | Day one / ceiling | Boundary and required control |
|---|---|---|
| Summarize logs; build timeline | 1 / 1 | Read-only access; redact; cite lines. No causal claims or recommendations. |
| Rank hypotheses; propose mitigations | 1, shadow / 2 | Store, hide from on-call; experienced engineer reviews all. Include disconfirming evidence; state mitigation change, cost, undo. |
| Run inspection commands | Disabled / 3 | Before enabling: allow-list commands and constrain arguments/target; approve each run; audit command, target, approver, result. |
| Draft incident/paging messages | Disabled / 2 | No send permission; human reviews/posts. Pattern-check customer addresses. |
| Execute mitigation | Disabled / 3 | Before enabling: permission scoped to approved action/target; incident commander approves each; audit approval/result; test non-prod rollback and kill switch. Replay recovery needs separate approval. |

At levels 1–2, logs are untrusted; deny egress/write tools in the tool layer. Before day one, verify permissions and test redaction for emails/user names on the configured path. Platform, retention, redaction, and permission setup are undefined: **no task is enabled until checked**. Exclude level 4 because month-end rollback affects the wider release (INC-2047).

## Promotion and ownership

For tasks 2 and 4: assess two pack replays and two real shadow windows. The 4-of-5 threshold has only four specified cases; define a fifth case or approve a revised threshold before measuring. No promotion until resolved. Record expected causes before replay; preselect 20 cited claims per case by rule. Require 95% supported citations, zero fabrication/redaction failures, and valid undo for every mitigation. Assumptions, not results.

**No level-3 promotion path:** tasks 3/6 lack a level-2 stage and evidence procedure; keep disabled pending staged path and task-specific tests. Three windows and 80% unedited proposal acceptance do not prove safe execution. Task 5 stays disabled without evidence.

Service owner approves promotions in writing; SOC 2 owner co-signs level 3 (audit due in six months). Incident commander may demote one level immediately and approves level-3 actions out of hours. Name roles before enabling. Failed/lost control evidence disables the task.

## Review disposition

**Accepted:** require verifiable controls, pre-set sampling, bounded tasks, experienced review, and approval bound to the action. Citations can be accurate but incomplete; four cases cannot meet a five-case denominator; tasks 3/6 lack a usable level-3 gate. Requirements above do not claim controls exist.

**Rejected, with limits:** human posting alone does not exceed the agent’s formal level if it lacks send permission; it is still a review risk, so posting stays human-controlled. Actual permissions, redaction coverage, audit integrity, rollback/kill switch, and thresholds cannot be judged; these remain launch blockers.
