# Risk audit: month-end log triage agent vs the lethal trifecta

**Status:** Draft, decision recorded. **Decision:** Option B, accept the risk in writing (section 4). **Owner role:** Service owner. **Date:** 2026-10-30. The decision was chosen by the owner in the session; the agent only recorded it.

## 1. Setup under audit

- **Source of the setup.** The assignment points at an agent setup "at the end of `ops/README.md`". That file contains no agent setup (it ends with the "No answers here" note), so this audit covers the setup defined in [autonomy-policy.md](../decisions/autonomy-policy.md): the month-end log triage agent. This is a choice made by the author, not text supplied by the assignment.
- **What exists.** A draft policy only. It names the tasks, the levels and the required controls. It does not name the tools, log platform, permissions, retention or redaction tooling (policy line 15: these are "undefined").
- **Tasks (policy lines 9-13).** Summarise logs and build a timeline (level 1); rank hypotheses and propose mitigations (level 1, shadow); run inspection commands, draft messages and execute mitigations (disabled on day one).
- **Planned, not built.** `ops/` describes a planned deployment (database, message topic, PayEx). Log samples under `ops/` show what such logs would contain; they are not evidence of what the current code does.
- **Separate from the above.** [.mcp.json](../../.mcp.json) configures the coding-agent tools used on this repo (Playwright and Mermaid). They are not part of the triage agent, and are listed only because a triage agent in the same environment could inherit them.

## 2. The three legs

| Leg | Status | Evidence |
| --- | --- | --- |
| Access to private data | **Present** | The task is summarising production logs and metrics (policy line 9). The sample incident logs contain recipient email addresses, e.g. `ap@harlowfreight.example`, `finance@castellanfoods.example` ([worker-logs.txt](../../ops/incidents/INC-2047/worker-logs.txt) lines 8-16; planned-deployment sample, so it shows the kind of content only). The policy itself expects emails and user names in the logs and requires redaction tests and an address pattern-check (policy lines 12, 15). The current code holds customer names as sample data ([InMemoryInvoiceRepository.cs](../../backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs) lines 13-17). Redaction is a requirement only; the policy says it is undefined. Whether real data reaches the agent depends on the log platform, which is undefined. |
| Exposure to untrusted content | **Present** | The policy says "logs are untrusted" (policy line 15). `CustomerName` comes from the request body and is only trimmed ([InvoicesController.cs](../../backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs) lines 20, 62). Alerts and user quotes also reach the agent (e.g. [alert.md](../../ops/incidents/INC-2047/alert.md) line 16). A keyword search found no injection-style text in the incident packs; this was not a full read, so the packs are not shown to be clean. |
| External communication / exfiltration | **Unknown** for the triage agent; **present** for the configured coding-agent tools | The policy requires "deny egress/write tools in the tool layer" at levels 1-2 (policy line 15), but this is a requirement, not evidence it is done. Message drafting has no send permission and a human posts (policy line 12). Tasks 3 and 5 would need write-capable rights (policy lines 11, 13) and are disabled. No tool list, network policy or egress control for the triage agent exists in the repo. [.mcp.json](../../.mcp.json) lines 3-6 configure the Playwright server (local, `@latest` unpinned; a browser tool can reach any URL); lines 7-9 configure the Mermaid server, a remote HTTP endpoint (`https://mcp.mermaid.ai/mcp`), so anything passed to it leaves the machine. |

**Reading of the table.** Two legs are present on the evidence. The third is undetermined for the triage agent. The policy claims to keep the third leg closed through controls that it says are not yet in place, so the trifecta is neither shown to be complete nor shown to be broken.

## 3. Open unknowns

1. Which tools the triage agent will have. None are specified.
2. Whether real production logs, or only the invented packs, reach it.
3. Whether egress is blocked at the network or tool layer, rather than only stated in the policy.
4. Whether the Playwright and Mermaid servers are available to the triage agent.
5. Whether redaction runs before the agent reads the logs.

## 4. Decision

**Decision (2026-10-30, owner role: Service owner): Option B, accept the risk in writing.**

The Service owner accepts that the triage agent may run with private data and untrusted content present, and with the external-communication leg not shown to be closed (unknown 3). The acceptance is bounded by the autonomy policy: day-one Level 1, no send, write or egress tools enabled, and no task enabled until permissions and redaction are checked (policy line 15). The acceptance lapses on any trigger below. Option A was considered and not chosen; its description is kept for the record.

### Option A: Redesign to remove one leg (external communication)

- **What it means.** Enforce, outside the agent, that it has no egress and no write tools: no Playwright or Mermaid servers, no network path except to the log store, read-only credentials. Policy line 15 already states this as a requirement; this option makes it a verified control instead of a statement.
- **Cost (from the evidence, not estimated).** The controls must be built and tested. The policy lists permission setup, redaction and the platform as "undefined" and says no task is enabled until they are checked. No source states effort or time, so none is given here. Tasks that need write rights (3 and 5) stay disabled, as the policy already has them.
- **Residual risk.** Private data and untrusted content remain present, so the agent can still read injected text and be misled. That affects the accuracy of summaries and proposals, and a human reviewer is the control. The option depends on the egress block actually holding; unknown 3 must be resolved by a test, not a document.

### Option B: Accept the risk in writing

- **What it means.** Run with all three legs potentially present and rely on the policy's human review and level limits.
- **Cost.** Little build work, but the acceptance needs a named owner and a trigger for revisiting. The policy already says it does not claim the controls exist (policy "Review disposition").
- **Residual risk.** Untrusted log content could steer an agent that holds private data and may have an outbound path. The evidence does not show that the outbound path is closed (unknown 3). The policy's level limits do not address this combination.
- **Revisit trigger (any one).** (1) Any write or egress tool is enabled for the triage agent. (2) Real customer data, not the invented packs, reaches it. (3) Redaction is shown not to run before the agent reads logs. (4) The on-call rotation goes live.

## 5. Evidence basis and limits

- Every claim above comes from the files cited. Nothing here comes from a running system.
- `ops/` is a planned-deployment description and is used only as a sample of log content.
- The incident packs were searched by keyword, not fully read for injection text.
- This draft was written by an AI agent from an evidence table; the owner must check each citation before relying on it.
