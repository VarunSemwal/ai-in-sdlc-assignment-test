# Exactly-once submission to PayEx: agent prompt log

This file is the evidence for how the recommendation was tested. It holds the
five prompts given to the coding agent, in the order they were run, each with
the agent's output reproduced as given (file links adjusted to relative paths).

The decision itself is in [decision-record.md](./decision-record.md).

## How these were run

- All five prompts ran in **one continuous chat session**, not in a fresh
  session each. The assignment's guidance and the stress-test design assume
  fresh sessions, so earlier answers may have influenced later ones. This is
  recorded as a limitation in the decision record.
- Prompts 4 and 5 start from "Assume we chose B", so they test the consequences
  of Option B, not whether B is the right choice.
- The optional "argue against my decision" prompt was **not** run.

| # | Purpose | Prompt summary |
| --- | --- | --- |
| 1 | Baseline grounded in the facts | Exactly-once question; list what is needed to know about PayEx |
| 2 | Trade-off comparison | Options A, B, C compared and one recommended |
| 3 | Stress test 1 | Stated preference for Option C; does the recommendation move? |
| 4 | Stress test 2 | 3 a.m. month-end failure scenarios for Option B |
| 5 | Stress test 3 | Build, operations, infrastructure cost and lock-in for Option B |

---

## Prompt 1: Baseline grounded in the facts

### Prompt

```text
Context: invoice approval service going to production. Facts: ASP.NET Core 8 on
Azure App Service, Azure SQL, Azure Service Bus provisioned but unused, ~40,000
invoices/month with 60% in the last three working days and peaks near 80
approvals/minute, six engineers (two new to the codebase), one-week 24x7 on-call
rotation, SOC 2 Type II audit in six months, approval actions auditable for seven
years. PayEx returns a reference on success, times out at 30 seconds, and a
timeout does not reliably mean the submission failed.

Question: how do we make sure an approved invoice reaches PayEx exactly once?

Do not assume PayEx supports idempotency keys or status lookup. List what you
would need to know about PayEx and say how each answer changes your
recommendation. Do not make up PayEx capabilities.
```

### Output

# Exactly-once submission to PayEx

## Framing

Exactly-once delivery across a network call with an ambiguous timeout can't be guaranteed from our side alone. Without help from PayEx you get one of two outcomes:

- **At-least-once attempts, and duplicates are possible.** This is safe only if PayEx can deduplicate.
- **At-most-once attempts, and some invoices may be unsubmitted.** This is safe only if we can detect and resolve the unsubmitted ones.

The goal is exactly-once effect: one payment per approved invoice. That depends on facts about PayEx that the scenario doesn't give.

## What the scenario facts imply

- **Load is low.** 60% of 40,000 is about 24,000 invoices over three working days, roughly 8,000 a day. The peak is about 80 approvals a minute, around 1.3 a second. Throughput is not the constraint, so correctness and operability decide the design.
- **Service Bus is available, not required.** The scenario doesn't say a queue is needed at this volume. Using it only because it's provisioned would add operational surface for a team with two new members on 24x7 on-call.
- **Audit needs a durable record.** SOC 2 and seven-year auditability need a persistent, append-only record of the approval, each submission attempt, and the PayEx reference.
- **The current code isn't atomic.** In [ApprovalService.cs](../../../backend/src/InvoiceApp.Domain/ApprovalService.cs), `Approve` reads the invoice, checks it's pending, then updates it. The in-memory repository only locks individual calls, so two concurrent approvals could both pass the check. That is a risk for the approval step itself, before PayEx is involved.

## What I need to know about PayEx, and how each answer changes the recommendation

| # | Question | If yes | If no or unknown |
|---|---|---|---|
| 1 | Does the submit call accept a client-supplied idempotency key, and does a repeat with the same key return the original result? | Retrying after a timeout is safe. Use a stable key per invoice with a durable intent record and a worker. This is the strongest option. | We can't retry blindly. A timeout leaves an unknown state that we must resolve another way. |
| 2 | How long does PayEx remember an idempotency key? | If the window is longer than our worst-case retry and recovery time, we're fine. | If it's shorter, retries after the window can duplicate, so we need our own cutoff and escalation. |
| 3 | Can we look up a submission by our own reference, after a timeout? | An unknown submission can be resolved by lookup, and we retry only if PayEx confirms it's absent. | The only safe response to a timeout is hold and reconcile, or manual review. |
| 4 | Does PayEx reject duplicates itself, for example the same invoice reference? | Duplicates fail safely, but we must tell a duplicate rejection apart from a real failure. | Dedup is entirely our responsibility. |
| 5 | Does PayEx process asynchronously after we time out, and how long until the outcome is final? Is there a callback? | We can wait out a settle window before resolving, or consume callbacks. | Resolution must be by polling or reconciliation, and the window is unknown. |
| 6 | Is there a reconciliation source, such as a report or listing API, that includes our reference, and how fresh is it? | Nightly reconciliation is viable as a backstop. | We can't verify outcomes, so exactly-once can't be demonstrated. |
| 7 | Which error responses are definitive failures and which are transient? | We can safely retry only the transient ones. | We must treat every non-success as unknown. |
| 8 | Can a duplicate or wrong payment be voided or reversed, at what cost, and how fast? | It lowers the severity of a duplicate, so more retries are tolerable. | A duplicate is costly, so lean toward at-most-once plus a human check. |
| 9 | Are there rate or concurrency limits? | Add throttling to the worker. | Not a design driver at our volume. |
| 10 | Is there a sandbox that reproduces timeouts? | We can test the Unknown path before production. | That path stays untested until production. |

## Provisional recommendation

This is conditional on the answers above.

**Parts that hold whatever PayEx supports:**

1. **Make approval atomic.** Use a conditional update on status, or a row version, in Azure SQL, so an invoice can be approved only once.
2. **Record submission intent in the same SQL transaction as the approval.** This is the core of the transactional outbox. A separate worker then reads the intent and calls PayEx. That avoids a database write succeeding while a publish is lost.
3. **Use an explicit submission state machine.** The states would be pending, in-flight, submitted with a reference, and unknown. A PayEx timeout moves the record to unknown, never to failed. Don't auto-retry from unknown unless questions 1 or 3 are answered yes.
4. **Keep an append-only audit trail.** Record each attempt and its outcome for the seven-year requirement.
5. **Use a single logical submitter per invoice.** At this volume one worker, or per-invoice locking, is enough.

**How the choice among your three options follows from the answers:**

- **Q1 yes:** a transactional outbox with an idempotency key. The dedupe table is largely replaced by PayEx's own dedupe, though we keep the key and attempts locally.
- **Q1 no, Q3 yes:** a transactional outbox, with lookup-before-retry on timeout.
- **Neither:** at-most-once plus reconciliation. Hold unknown submissions for reconciliation or human review, and accept that some invoices are delayed to avoid duplicates. Nightly reconciliation then becomes mandatory, not optional.

Optimistic submission with only nightly reconciliation looks weakest in every case. It allows duplicates for up to a day, and the heaviest month-end load is when that matters most.

## What I'm unsure about

- Everything in the PayEx table. I haven't assumed any of those capabilities.
- Whether Service Bus adds value here. Using the database as the queue, with a worker polling the outbox, may be simpler for this team and volume, but that's a judgment call.
- Operational cost and effort estimates. I haven't estimated them, and they belong in your later cost stress test.

Please get answers to questions 1, 3 and 6 first, since they decide the design. I haven't changed any files.

---

## Prompt 2: Trade-off comparison

### Prompt

```text
Compare at least these options: (A) idempotency key with a dedupe table,
(B) transactional outbox with a worker, (C) optimistic submission with nightly
reconciliation. For each give: how it works, what happens on a PayEx timeout,
what can still go wrong, build effort, operating burden for a six-person team on
24x7 on-call, and audit-trail fit. Then recommend one and state what facts your
recommendation depends on. Say what you are unsure about.
```

### Output

# Comparison of the three options

These options aren't mutually exclusive. A and B solve different problems, and a design can combine them. I've compared them as you specified, then given one recommendation. No effort figures are measured. Build effort is relative, because I haven't been given team velocity or PayEx integration details.

## Option A: Idempotency key with a dedupe table

**How it works.** The approval handler calls PayEx synchronously. Before the call it inserts a row keyed by a stable per-invoice key into a dedupe table, with a unique constraint. A second attempt for the same key finds the row and doesn't send again, or returns the stored outcome.

**On a PayEx timeout.** The row is already there, so our side won't send twice. But the outcome is unknown. Whether a retry is safe depends on PayEx honoring the key (the earlier question 1) or offering lookup (question 3). Without either, we have to hold the row as unknown.

**What can still go wrong.**
- The dedupe row is written, then the process crashes before the call. The row says "attempted" and nothing was sent. Without a recovery step, that invoice is stuck.
- The call succeeds, then the process crashes before we store the PayEx reference. We'd re-send, or wrongly treat it as unsent.
- The call runs inside the approval request, so a slow PayEx makes approvals slow. The 30-second timeout would sit in the approver's request path.
- A dedupe table protects only our side. It does nothing if PayEx doesn't dedupe and a retry is wrongly allowed.

**Build effort.** Lowest: one table, one unique constraint, and a state check. It doesn't include the recovery step, which is the hard part.

**Operating burden.** Moderate. There is no background component to babysit, but stuck "attempted" rows need a manual or scheduled recovery path, and on-call has to understand the states.

**Audit-trail fit.** Adequate if each attempt and outcome is recorded. It's weaker if the table only keeps the latest state, so it needs an append-only attempt log.

## Option B: Transactional outbox with a worker

**How it works.** The approval transaction writes the approved status and an outbox row (the intent to submit) in one Azure SQL transaction. A separate worker reads pending outbox rows and calls PayEx, then records the result and reference. The approver's request never waits on PayEx.

**On a PayEx timeout.** The worker leaves the row in an unknown or in-flight state. Same constraint as A: whether it may retry depends on idempotency keys or lookup. Without them it holds the row for reconciliation or review.

**What can still go wrong.**
- The outbox guarantees the intent is recorded, not that PayEx receives it exactly once. A worker crash after sending but before recording the result creates the same ambiguity as A.
- Duplicate workers, if two instances claim the same row. This needs a claim or lease mechanism.
- Poison rows that fail repeatedly and block or clog the queue.
- Backlog growth during a PayEx slowdown at month end. At about 80 approvals a minute that is manageable, but timeouts of 30 seconds per call would limit a single worker's throughput, and I haven't sized this.

**Build effort.** Moderate to high: the outbox table, the worker, claiming and leasing, a state machine, retry and backoff policy, and dead-letter handling. A message broker isn't required, since the worker can poll the table.

**Operating burden.** Highest. A new always-on component needs monitoring and runbooks, and on-call needs to understand its states. The two engineers new to the codebase raise the cost of that.

**Audit-trail fit.** Strong. The outbox row and attempt log give a natural durable record of what was requested and what happened.

## Option C: Optimistic submission with nightly reconciliation

**How it works.** The system submits to PayEx immediately after approval, with no durable intent beforehand. A nightly job compares our approvals against PayEx's records and fixes mismatches.

**On a PayEx timeout.** The system must guess: retry, which risks a duplicate, or not retry, which risks a missed submission. The reconciliation job resolves it the next night.

**What can still go wrong.**
- Duplicates or missed submissions stay live for up to a day, and during the month-end peak the exposure is highest.
- Reconciliation only works if PayEx gives a reliable way to list or look up submissions (question 6). If it doesn't, this option can't be verified.
- Correcting a duplicate that has already paid out may be impossible or expensive, depending on question 8.
- The nightly job is a single point that must itself be correct and monitored.

**Build effort.** Low for the submission path. The reconciliation job is not trivial, and its difficulty depends on PayEx's data.

**Operating burden.** Moderate, with a different shape. Fewer moving parts during the day, but morning mismatch triage becomes recurring work.

**Audit-trail fit.** Weakest. There's no record of intent before submission, so reconstructing what happened depends on reconciliation, and that's harder to defend in a SOC 2 audit.

## Side-by-side

| | A: Idempotency key + dedupe table | B: Outbox + worker | C: Optimistic + nightly reconciliation |
|---|---|---|---|
| Duplicate protection | Our side only | Our side only, plus structure for safe retry | None until reconciliation |
| Timeout handling | Unknown row, outcome unresolved | Unknown row, outcome unresolved | Guess, then fix later |
| Approver request affected by PayEx slowness | Yes | No | Yes |
| Crash windows | Two | Fewer, but not zero | Several |
| Build effort | Lowest | Highest | Low plus reconciliation job |
| Operating burden | Moderate | Highest | Moderate, with daily triage |
| Audit fit | Adequate | Strong | Weakest |

## Recommendation

**Option B as the foundation, using an idempotency key if PayEx supports one.** In other words, the outbox gives durability, isolation from PayEx slowness, and a clean audit trail, and a stable per-invoice key makes retries safe. Nightly reconciliation stays as a backstop rather than the primary control.

My reasoning:
- B removes the largest structural risk: approval and submission intent committing atomically, and PayEx's 30-second timeout no longer blocking approvers.
- It fits the audit requirement best.
- C is the weakest because it tolerates duplicates by design at the busiest time.
- A alone leaves the approval path coupled to PayEx and has the same unresolved-timeout problem as B without B's durability.

## Facts this depends on

- **PayEx accepts an idempotency key, or supports lookup by our reference.** If neither, no option makes retry safe. The design then becomes at-most-once with unknown submissions held for review, and the recommendation shifts toward B with mandatory reconciliation and a human-review path.
- **PayEx provides a reliable reconciliation source.** Without it, the backstop can't work.
- **Volume stays near the stated figures.** A single worker is enough at about 1.3 approvals a second only if calls complete reasonably fast. A string of 30-second timeouts would change that.
- **Approvers can tolerate asynchronous submission.** B means "approved" and "submitted to PayEx" are separate states. If users expect instant confirmation of submission, that's a product question.

## What I'm unsure about

- **Whether B's extra build and operating cost is justified for this team.** A simpler variant, A with a recovery job, might be adequate. I can't settle that without knowing PayEx's capabilities and the team's tolerance for a new component.
- **Whether Service Bus adds value.** I've assumed a database-polled worker is enough. I haven't established that a broker is unnecessary.
- **Effort estimates.** Relative rankings only. I haven't estimated person-weeks and shouldn't be asked to without more information. That belongs in your cost stress test.
- **Worker throughput under repeated timeouts.** I haven't modelled it.
- **Failure behavior I haven't tested.** The crash windows listed are reasoned from the design, not observed.

I haven't changed any files.

---

## Prompt 3: Stress test 1, a different stated preference

### Prompt

```text
I prefer option C.  Re-evaluate. If your recommendation
changes, say exactly which new facts caused the change.
```

### Output

# Re-evaluation with a stated preference for Option C

**My recommendation does not change: it stays Option B as the foundation, with reconciliation as a backstop.**

Your preference for C is not a new fact about the system, so it isn't a reason to change the analysis. A preference tells me what you'd like, not what PayEx does, what the team can operate, or what the audit requires. If I moved just because you said it, that would be agreement rather than analysis.

## New facts since my last answer

None. You've stated a preference and nothing else. Nothing about PayEx, volume, the team, or compliance has changed.

## The strongest case for C, and why it doesn't overturn B

C has real strengths, and I'd weigh them honestly:

- **Lowest build effort and fewest moving parts during the day.** That's valuable for a team with two engineers new to the codebase.
- **No new always-on worker to monitor.**
- **The load is low** (about 1.3 approvals a second at peak), so a simple synchronous path is plausible.

My objections to C stand because they come from the scenario facts, not from preference:

1. **A timeout is ambiguous, and C has no way to resolve it at the time.** The scenario says a timeout doesn't reliably mean failure. C must guess on every timeout, and either guess has a cost. Retrying risks a duplicate payment. Not retrying risks a missed one.
2. **The exposure peaks when it matters most.** 60% of volume lands in the last three working days, so the window where duplicates or misses can exist is largest at month end.
3. **Audit fit is weakest.** C has no durable record of intent before submission. With SOC 2 Type II in six months and seven-year auditability, that is the hardest option to defend.
4. **C depends entirely on reconciliation being possible.** If PayEx offers no reliable way to list or look up submissions (earlier question 6), C can't verify its own results.

## What would change my recommendation to C

I'd move toward C if answers to the PayEx questions showed these, and I'd need them to be facts, not preferences:

- **PayEx deduplicates on a client key, with a retention window longer than our retry window.** Then retrying after a timeout is safe, which removes C's main risk.
- **A reliable listing or lookup source exists and is fresh.** Then reconciliation is real, not hypothetical.
- **A duplicate or wrong payment can be cheaply reversed.** That lowers the cost of C's exposure window.
- **The business accepts bounded, reversible errors.** For example, if finance agrees that a short delay or a corrected duplicate is acceptable.
- **The audit can be satisfied another way.** For example, a durable approval record written before the call, even without a full outbox.

Note that with the first fact present, a lean version of C becomes close to a simplified A: write a durable record, submit with a key, and reconcile. The line between the options blurs, and the real minimum is a durable record before the call.

## Where the preference could legitimately matter

The decision isn't purely technical. How much operational complexity to take on is a judgment about your team's capacity. If you believe a new worker is too much for this rotation, that's a valid concern, and it's one the record should weigh explicitly. But that concern argues for the simplest viable version of B, such as a database-polled worker with no broker, rather than for removing durable intent.

## For your record

This is useful evidence for the stress test:

- **Outcome:** the recommendation did not move when you stated a preference with no new facts. That's the behavior you were testing for.
- **What I'd still want to check:** whether I held my position for sound reasons rather than stubbornness. The sound reasons are the four scenario-based objections above, and the list of facts that would change my mind.
- **Limitation:** I can't tell you C is wrong in every case. My claim is that, on the stated facts, it carries the most risk, and that this changes if PayEx's capabilities turn out to be strong.

I haven't changed any files.

---

## Prompt 4: Stress test 2, 3 a.m. failure on the last day of the month

### Prompt

```text
Assume we chose B. It is 3 a.m. on the last day of the month
at peak load. How does this design fail? Give concrete failure scenarios
(PayEx timeout, worker crash mid-submission, database failover, duplicate
delivery, backlog growth). For each: what the on-call engineer sees, how they
tell whether the invoice was already submitted, and the safe recovery step.
```

### Output

# Option B at 3 a.m. on month-end: failure scenarios

This is reasoned from the design, not observed, because none of it is built yet. I assume an outbox row with states `pending`, `in-flight`, `submitted` (with PayEx reference) and `unknown`, plus an append-only attempt log. Those state names are my assumption, not something in the repo.

**The recurring problem is that our side can't always tell whether PayEx got the request.** That's fixed by PayEx's capabilities, which are still unknown:

- **If PayEx supports an idempotency key or lookup by our reference,** every "safe recovery" below is a retry or a lookup.
- **If it supports neither,** every "safe recovery" below ends in "hold and verify on PayEx's side". Nothing is retried automatically.

## 1. PayEx timeouts

- **What on-call sees:** Rising count of rows in `unknown` or `in-flight`. A growing age on the oldest outbox row. The PayEx timeout rate (30 seconds per call) climbing. Alerts only fire if we build them.
- **Already submitted?** Unknown from our side by definition, since the scenario says a timeout doesn't reliably mean failure. With lookup, query PayEx by our reference. With an idempotency key, resubmitting with the same key returns the original result. With neither, check PayEx's own records or report, if one exists.
- **Safe recovery:**
  - With a key or lookup: resolve each `unknown` row by lookup, or retry with the same key.
  - Without: stop automatic retries, leave rows in `unknown`, and escalate to a human check against PayEx's records.
  - Never mark a timed-out row `failed` and re-queue it.
  - Pause the worker if PayEx is broadly degraded, so we don't keep creating ambiguous rows.

## 2. Worker crash mid-submission

- **What on-call sees:** A row stuck `in-flight` with an old lease or claim time, and no worker progress. The app instance may have restarted.
- **Already submitted?** The crash window is between sending and recording the result. We can't tell from the row. Check the attempt log: it shows an attempt started but no outcome. Then ask PayEx by lookup, or by resubmitting with the same key.
- **Safe recovery:**
  - Expire the stale claim only after a lease timeout.
  - Move the row to `unknown`, not back to `pending`.
  - Resolve it as in scenario 1.
  - Returning it to `pending` is the dangerous step, because it can double-submit.

## 3. Azure SQL failover

- **What on-call sees:** Connection errors, brief write failures or timeouts on the worker and the approval path, then recovery. Possibly rows that appear to have gone back to an earlier state.
- **Already submitted?** There are two cases:
  - **A write was lost or not committed:** the result of a successful PayEx call may not be recorded, so the row looks unsent even though PayEx has it. Here the PayEx reference was never stored.
  - **Committed but the connection dropped:** the worker may not know it committed.

  Either way, treat a row that is `in-flight` or just recovered as unknown.
- **Safe recovery:** After failover, don't assume rows reflect reality. Reconcile `in-flight` and recent `submitted` rows against PayEx before retrying. I haven't established the failover and durability settings for this deployment, so I can't say how likely a lost commit is.

## 4. Duplicate delivery

- **What on-call sees:** Either two attempts logged for one invoice, or a PayEx-side report or finance complaint about a duplicate. Our own tables may show only one clean attempt if the duplicate came from a race.
- **Already submitted?** Query the attempt log and PayEx by our reference. Two PayEx references for one invoice means it happened. Two workers claiming the same row would show overlapping claims.
- **Safe recovery:**
  - Stop the worker so no more duplicates are sent.
  - Find the cause: a double claim, or a retry that wasn't safe.
  - Fix the claim logic or the retry rule before restarting.
  - Whether the extra payment can be reversed depends on PayEx's void or refund capability (earlier question 8). If it can't, this is a finance incident, not just a data fix.

## 5. Backlog growth

- **What on-call sees:** The pending count and the age of the oldest pending row rising. Approved invoices not reaching PayEx. Calls near the 30-second timeout.
- **Already submitted?** For rows still `pending`, no attempt has been made, which is the one case where our side knows. That makes it the safest category.
- **Safe recovery:**
  - Find the cause: PayEx slowness, a stuck worker, or a poison row.
  - If a poison row is blocking, move it aside and continue.
  - Scale workers only if the claim mechanism prevents double-claiming.
  - A single worker does 30-second calls serially, so at 80 approvals a minute, repeated timeouts would clearly outrun it. I haven't sized this and can't say where the limit is.

## What this exercise shows

- **The riskiest step is moving a row from `in-flight` or `unknown` back to `pending`.** The runbook should forbid it unless PayEx confirmed the invoice is absent.
- **The design is only as safe as the answer to "can we tell if PayEx has it?"** That's the main reason to get the PayEx answers before building.
- **On-call needs, before go-live:** a per-invoice view of attempts and states, a lookup or check command, a worker pause switch, and a rule for what is safe to retry. These add build effort that I haven't costed.

## What I'm unsure about

- PayEx's real behavior, so all recovery steps above are conditional.
- Azure SQL failover behavior and durability settings.
- Worker throughput under repeated timeouts.
- Whether a human check against PayEx is even possible at 3 a.m. (access, process, who can authorize it).

I haven't changed any files.

---

## Prompt 5: Stress test 3, cost to build and to run

### Prompt

```text
For Option B, estimate build effort, ongoing operations effort,
infrastructure cost, and lock-in. Show how you arrived at each number, state
your assumptions, and separate facts from estimates.
```

### Output

# Option B: cost estimate

I haven't been given Azure prices, team velocity or PayEx details. Every figure below is an estimate, and no dollar figures are quoted. Wherever I quote a number, I show the derivation.

## Facts (from the scenario and repo)

- Volume is about 40,000 invoices a month, 60% of them in the last three working days, and the peak is about 80 approvals a minute.
- The platform is ASP.NET Core 8 on Azure App Service, Azure SQL and GitHub Actions. Azure Service Bus is provisioned but unused.
- The team is six engineers, two of them new to the codebase, on a one-week 24x7 rotation.
- Retention must cover seven years of approval audit.
- PayEx times out at 30 seconds, and a timeout is ambiguous.
- There is no persistence yet. The repository is in-memory, and repositories are interfaces in the Domain project per [AGENTS.md](../../../AGENTS.md).

## Derived arithmetic (facts combined)

- Peak rate: 80 ÷ 60 ≈ **1.3 submissions per second**.
- Month-end volume: 40,000 × 60% = 24,000 over three days, about **8,000 a day**.
- Worst-case concurrency (Little's law): 1.3/s × 30 s ≈ **40 calls in flight** if every call times out. Normal latency is unknown, so I assume 1–2 s, which needs only 2–3 in flight. The gap between those two cases is the main sizing risk.

## Assumptions

1. One engineer who knows .NET and SQL builds it, with no PayEx experience.
2. The worker is a database-polled background process, with no Service Bus.
3. PayEx supports an idempotency key or lookup. If not, the reconciliation and review paths grow.
4. PayEx has a sandbox. If not, add testing time.
5. Persistence to Azure SQL is needed anyway, so I separate it from B-specific work.
6. Estimates are person-days of focused work and exclude waiting on PayEx or other teams.

## 1. Build effort (estimate)

| Component | Person-days |
|---|---|
| Atomic approval (conditional update or row version) | 1–2 |
| Outbox table, written in the approval transaction | 2–3 |
| Worker with claim and lease | 4–6 |
| PayEx client, timeout handling, state machine | 4–6 |
| Retry, backoff, poison-row handling | 2–3 |
| Append-only attempt log for audit | 2–3 |
| Reconciliation job (depends on PayEx data) | 3–5 |
| Metrics, alerts, dashboards | 3–4 |
| Tests (unit, SQL integration, fake PayEx with timeouts and crashes) | 6–10 |
| Runbook and on-call documentation | 2–3 |
| **B-specific total** | **29–45** |

That total is the sum of the rows. At about 5 focused days a week it is roughly **6–9 weeks** for one engineer. Shared work that B depends on, which you'd need regardless, is Azure SQL repository, schema and migrations at about **5–8 days**. This is my own judgment and not benchmarked. Tests are the widest range because fault injection is the hard part.

## 2. Ongoing operations effort (estimate)

| Activity | Assumption | Person-days / month |
|---|---|---|
| Routine checks (dashboards, `unknown` rows) | 2–4 h/week | 1–2 |
| Heightened watch on the last three working days | 2–4 h/day × 3 | 0.75–1.5 |
| Incident handling | 1–2 incidents, 2–4 h each | 0.25–1 |
| **Total** | | **≈ 2–4.5** |

Treat this as **2–5 person-days a month**, plus occasional dependency updates. The incident rate depends on PayEx's real timeout rate, which I don't know. It is the least reliable figure here. A worse timeout rate would multiply the last two rows.

The on-call burden is separate from effort. B adds a component that pages someone in the night, and with six engineers (two new to the code) the new ones need the runbook and some shadowing before they take the rotation alone. I haven't put a number on that.

## 3. Infrastructure cost (mostly qualitative)

I can't quote prices from the facts I have, so use the Azure pricing calculator with your actual SKUs.

- **Compute:** if the worker runs inside the existing App Service, incremental compute is near zero, but it shares restarts and scale-out with the web app. Every scale-out instance runs another worker, which makes the claim mechanism mandatory. A separate worker app adds an App Service plan or equivalent at whatever SKU you choose.
- **Database:** extra load is small. Estimated storage: at 1–3 attempt rows per invoice, about 100,000 rows a month. Over 84 months that is about 8.4 million rows, and at roughly 1 KB each about **8 GB**. The row size is a guess. Where audit history is kept long-term may change this.
- **Service Bus:** none under assumption 2, so no incremental cost.
- **Monitoring and logging:** ingestion cost depends on log volume and retention, which I haven't estimated.

My judgment is that infrastructure is likely small next to engineering time, but that is a judgment, not a measured figure.

## 4. Lock-in (assessment)

- **Azure SQL:** low added lock-in, since the system already depends on it. Row-claiming techniques such as `UPDLOCK` and `READPAST` are SQL Server-specific, so moving to another database would mean rewriting the claim logic.
- **Service Bus:** none, if not used. Adding it later would add lock-in.
- **PayEx:** the main lock-in. The state machine and recovery rules encode PayEx's behavior (keys, lookup, timeout semantics). A different provider would need a new adapter and possibly a different state model. An interface around the client reduces this but doesn't remove it.
- **The pattern itself:** the outbox is portable, so lock-in to the approach is low.

## What would change these numbers

- No PayEx idempotency key or lookup: build up (reconciliation and manual review get larger) and ops up.
- No sandbox: build up, with more testing risk.
- Repeated timeouts at peak: you'd likely need concurrent workers, which adds claim logic and testing. Concurrency near 40 in flight is where this starts to matter.
- A separate worker app instead of in-process: build slightly up for deployment, ops up for another component.
- Team velocity: all person-day numbers scale with it.

## Where I'm least certain

The ops figures and the PayEx-dependent items (reconciliation and testing) carry the widest uncertainty. The build total is a reasoned range from my own breakdown, not data. Treat every number here as a starting estimate to challenge with your team.

I haven't changed any files.
