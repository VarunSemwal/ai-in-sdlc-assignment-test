# Decision record: exactly-once submission of approved invoices to PayEx

- **Status:** Draft. The decision below is conditional on PayEx facts that have
  not been confirmed (see [Open questions](#open-questions-that-must-be-answered-before-build)).
- **Date:** 2026-10-23
- **Evidence:** [README.md](./README.md) holds the prompts and agent outputs this
  record was built from.

## Context

The Invoice Approval service becomes a tier-2 service in January and must submit
approved invoices to an external payment provider, PayEx.

Facts taken from the assignment scenario:

- ASP.NET Core 8 on Azure App Service, Azure SQL, GitHub Actions.
- Azure Service Bus is provisioned and unused.
- About 40,000 invoices a month, 60% in the last three working days, peaking near
  80 approvals a minute.
- Six engineers, two new to the codebase, one-week 24x7 on-call rotation.
- SOC 2 Type II audit in six months. Approval actions must be auditable for
  seven years.
- PayEx returns a reference on success and times out at 30 seconds. A timeout
  does not reliably mean the submission failed.

Facts taken from the current code, which has no database, queue or PayEx
integration:

- `ApprovalService.Approve` in
  [ApprovalService.cs](../../../backend/src/InvoiceApp.Domain/ApprovalService.cs)
  reads the invoice, checks it is pending, then updates it. These are separate
  steps. The in-memory repository in
  [InMemoryInvoiceRepository.cs](../../../backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs)
  locks each call individually, so two concurrent approvals of one invoice could
  both pass the check. This is the approval step itself, independent of PayEx.

Derived arithmetic: 24,000 invoices over three days is about 8,000 a day, and
80 a minute is about 1.3 a second. Throughput is not the constraint;
correctness and operability are.

## What "exactly once" can mean here

A call across a network with an ambiguous timeout cannot be guaranteed
exactly-once from our side alone. Without PayEx's help we can only choose:

- **At-least-once attempts**, which is safe only if PayEx can deduplicate.
- **At-most-once attempts**, which is safe only if unsubmitted invoices can be
  detected and resolved.

The goal of this record is therefore **one payment per approved invoice**
(exactly-once effect), not a claim of exactly-once delivery.

## Options considered

| | A: Idempotency key + dedupe table | B: Transactional outbox + worker | C: Optimistic submission + nightly reconciliation |
| --- | --- | --- | --- |
| Approval request waits on PayEx | Yes | No | Yes |
| Durable record of intent before the call | Dedupe row | Outbox row, same transaction as approval | No |
| On PayEx timeout | Unknown, outcome unresolved | Unknown, outcome unresolved | Guess, then correct next night |
| Audit-trail fit | Adequate | Strong | Weakest |
| Build effort (relative) | Lowest | Highest | Low, plus reconciliation job |
| Operating burden (relative) | Moderate | Highest | Moderate, with daily triage |

None of the options resolves a timeout by itself. Each depends on whether PayEx
supports an idempotency key or lookup by our reference.

## Decision

Adopt **Option B as the foundation**, using a PayEx idempotency key or lookup
**if PayEx confirms it supports one**, with nightly reconciliation as a
backstop rather than the primary control.

Concretely:

1. **Make approval atomic** in Azure SQL (a conditional update on status or a row
   version), so an invoice can be approved only once.
2. **Write the submission intent (outbox row) in the same SQL transaction as the
   approval.**
3. **Submit from a separate worker**, polling the outbox table. Do not use
   Service Bus for the first release (see the trade-off below).
4. **Use an explicit submission state machine.** A PayEx timeout moves the row to
   an `unknown` state, never to failed, and never back to pending.
5. **Never automatically retry from `unknown`** unless PayEx confirms either that
   a repeat with the same key is safe or, by lookup, that the invoice is absent.
6. **Keep an append-only attempt log** of each submission attempt and outcome,
   retained to meet the seven-year audit requirement.
7. **Run nightly reconciliation** against PayEx, if PayEx provides a source to
   reconcile against.

The state names (`pending`, `in-flight`, `submitted`, `unknown`) are a working
proposal, not something defined in the repository.

### Guarantee this design gives

- **If PayEx supports an idempotency key or lookup:** retries after a timeout are
  safe, giving one payment per approved invoice.
- **If it supports neither:** the design gives at-most-once automatic
  submission. Timed-out invoices are held for human verification against PayEx,
  so some invoices are delayed to avoid duplicate payments.

### Why not A or C

- **C** is rejected because it must guess on every timeout, and the window for
  duplicates or misses is largest at month end. It also has no durable record of
  intent, which is the hardest position to defend in a SOC 2 audit.
- **A on its own** keeps the 30-second PayEx call inside the approver's request.
  Its dedupe row is durable, so the real difference from B is where the call
  runs, not durability.

## Consequences

**Positive**

- Approval and submission intent commit together, so an approval cannot be
  recorded without a record that it must be submitted.
- Approvers are not blocked by PayEx slowness.
- The outbox row and attempt log give a durable audit trail.

**Negative or accepted costs**

- "Approved" and "submitted to PayEx" become separate states. This is a product
  change that approvers and downstream users must accept.
- A new always-on component needs monitoring, runbooks and on-call training.
- The state machine encodes PayEx behavior, so changing provider means a new
  adapter and possibly a new state model.
- Row-claiming techniques such as `UPDLOCK` and `READPAST` are SQL Server
  specific.

**Rough cost (agent estimates, not measured)**

These are the agent's estimates from [README.md](./README.md), Prompt 5. They
rest on assumptions (one .NET engineer with no PayEx experience, PayEx offers a
key or lookup, a sandbox exists, velocity of about five focused days a week) and
should be replaced with team figures.

| Item | Estimate | Confidence |
| --- | --- | --- |
| B-specific build | 29–45 person-days (about 6–9 weeks for one engineer) | Low to medium |
| Shared persistence work | 5–8 person-days | Low |
| Ongoing operations | 2–5 person-days a month | Low |
| Extra audit storage over seven years | About 8 GB | Low (row size and rows per invoice assumed) |
| Infrastructure price | Not estimated | None |

## Trade-off accepted: no Service Bus in the first release

The design polls the outbox table instead of using the provisioned Service Bus.
This is a judgment for a six-person team with two new members, to reduce the
operational surface. It has **not** been evaluated against Service Bus
properly. It should be revisited if throughput or fan-out needs grow.

## Open questions that must be answered before build

The decision depends on facts the scenario does not provide. They are listed in
order of impact on the design.

1. Does the PayEx submit call accept a client idempotency key, and does a repeat
   return the original result? For how long is a key remembered?
2. Can a submission be looked up by our own reference after a timeout?
3. Is there a reconciliation source (a report or listing) that includes our
   reference, and how fresh is it?
4. Can a duplicate or wrong payment be voided or reversed, at what cost and how
   fast?
5. Does PayEx continue processing after we time out, and is there a callback or a
   defined settle time?
6. Which PayEx errors are definitive failures and which are transient?
7. Is there a sandbox that can reproduce timeouts?
8. What are PayEx's rate and concurrency limits?

## What would make us revisit this decision

Revisit and re-run the analysis if any of the following occurs.

**PayEx capability changes the premise**

- PayEx offers neither an idempotency key nor lookup. The design then becomes
  hold-and-verify for every timeout, and the cost of manual review and
  reconciliation must be re-weighed against the staffing available.
- PayEx confirms strong deduplication, a long key retention window, a fresh
  reconciliation source and cheap reversal of duplicates. A simpler design
  (a durable approval record before the call, with synchronous submission and
  reconciliation) may then be sufficient, and Option B's extra worker may not
  be worth its cost.
- PayEx behavior in practice differs from what was confirmed, for example
  timeouts that complete later in an unexpected way.

**Operational evidence**

- A duplicate or missing payment reaches production.
- The measured timeout rate or backlog age shows a single worker cannot keep up
  at month end. The agent's worst case was about 40 calls in flight if every
  call times out, but normal latency is unknown.
- The number of rows stuck in `unknown` or needing manual review exceeds what
  on-call can resolve (a threshold to be set once real rates are known).
- Actual build or operating effort exceeds the estimate range above by a margin
  the team sets.

**Organizational and compliance**

- The audit finds the attempt log or retention inadequate.
- Approvers or finance cannot accept "approved" and "submitted" being separate
  states.
- Team capacity changes, for example the rotation cannot support a new
  component.

**Platform**

- Service Bus offers a clear advantage because of fan-out, scale or another
  consumer.
- Volume grows materially beyond the stated figures.

## Where the agent was wrong or useless

This section records specific weaknesses in the agent's output. Each item cites
the prompt in [README.md](./README.md) where it occurred.

### Wrong or overstated

1. **Contradicted itself on Option C (Prompts 1 and 3).** Prompt 1 said C
   "looks weakest in every case." In Prompt 3, after listing conditions under
   which C becomes viable, it said the line between C and a simplified A
   "blurs". "In every case" was an overstatement; the accurate claim is
   "weakest on the stated facts, and weaker still without PayEx help."
2. **Overdrew the A-versus-B distinction (Prompt 2).** It said A leaves "the
   same unresolved-timeout problem as B without B's durability." A's dedupe row
   is durable, so that is not the difference. The real differences are that A
   runs the PayEx call inside the approver's request and has a weaker built-in
   attempt trail. The recommendation still stands, but this reason was
   misstated.
3. **Unsupported numbers in the comparison table (Prompt 2).** "Crash windows:
   Two" for A and "Several" for C were not derived from any analysis shown.
   They were given a precision they did not have.
4. **Invented state names presented as design (Prompt 4).** The failure analysis
   used `pending`, `in-flight`, `submitted` and `unknown`. The agent did say
   these were its assumption, but every recovery step depends on them, so the
   analysis is only as sound as that invented model.
5. **Speculative failure mode (Prompt 4).** The Azure SQL failover scenario
   included a committed write being lost. The agent admitted it had not
   established the deployment's failover and durability settings, so the
   likelihood of that scenario is unknown and it should not be treated as a
   known risk.
6. **Loose arithmetic in the storage estimate (Prompt 5).** "1–3 attempt rows per
   invoice" on 40,000 invoices is 40,000–120,000 rows a month, but the agent
   used "about 100,000" without explaining why it picked the high end. The
   1 KB row size was a guess.

### Useless for the decision as given

7. **It could not answer the facts that decide the design (Prompt 1).** The
   ten-question PayEx table is a good checklist, but only the PayEx owner can
   fill it in. Until that happens, the recommendation is conditional and the
   agent could not narrow it further.
8. **The cost estimates are not anchored to anything (Prompt 5).** The
   person-day ranges come from the agent's own breakdown, with no team
   velocity, no PayEx details and no prior comparable work. The operating
   effort figures depend on the PayEx timeout rate, which is unknown, and the
   agent said so. No infrastructure price was produced at all, so the cost
   question remains unanswered in dollar terms.
9. **Dismissed Service Bus without evidence (Prompts 1, 2 and 5).** It assumed a
   database-polled worker is enough and said it had not established that a
   broker is unnecessary. No comparison against Service Bus was done, and this
   record accepts that gap rather than closing it.
10. **Did not use the repository for worker sizing.** It named a worst case of
    about 40 calls in flight but assumed normal PayEx latency of 1–2 seconds
    with no evidence. Nothing in the repository or scenario supports that
    figure.

### Weaknesses in how the test was run (not the agent's fault)

11. **Stress tests were not independent.** All five prompts ran in one chat
    session instead of fresh sessions, so the Option B recommendation from
    Prompt 2 was in context for Prompts 3 to 5. Prompts 4 and 5 also begin from
    "assume we chose B", so they examine B's consequences and do not test
    whether B is right.
12. **Only one alternative preference was tested.** The agent held its position
    when told "I prefer option C." That is good, but it was not tested with a
    preference for A, and one run cannot distinguish a well-reasoned position
    from a stubborn one. The agent said as much.
13. **The preference test was phrased tersely.** The prompt did not restate the
    context, relying on the earlier conversation.

### What the agent did usefully

Recorded for balance, since it affected the decision:

- It stated early and repeatedly that exactly-once delivery is not achievable
  without PayEx support, and it refused to assume PayEx capabilities.
- It spotted the non-atomic read-check-update in `ApprovalService.Approve`.
- It identified moving a row from `in-flight` or `unknown` back to `pending` as
  the riskiest recovery step, which became design rule 4 and 5 above.
- It kept the recommendation unchanged under the stated preference for C and
  listed the facts that would change it, which became the revisit triggers.

## Not yet done

- The PayEx questions above are unanswered.
- Nothing in this record has been implemented or tested. The failure scenarios
  and estimates are reasoned, not observed.
- The optional "argue against my decision" prompt was not run.
- The fresh-session versions of the stress tests were not run. Re-running
  Prompts 3 to 5 in separate sessions would strengthen the evidence.
