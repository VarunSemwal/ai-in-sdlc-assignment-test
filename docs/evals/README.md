# Review evaluation: three runs, plus one changed-variable run

**Status:** verdicts in [results.csv](./results.csv) were drafted by the agent and then re-checked against the diffs and the answer key. The `DRAFT` tags are removed. Two judgement calls are the owner's rulings, recorded in the `notes` column. The scoring is still not blind (see the section below).

## What was run

- **Cases:** the ten fixed diffs in [ops/evals/cases/](../../ops/evals/cases) plus two added by the owner, [case-11](../../ops/evals/cases/case-11.diff) (a defect) and [case-12](../../ops/evals/cases/case-12.diff) (clean). That makes 12 cases: 9 with a defect and 3 clean. The owner states that `case-11` is based on a real defect they have seen and `case-12` on a real change they have seen. This is the owner's statement; the agent cannot verify it.
- **Ground truth:** the owner's [answer-key.md](./answer-key.md), written before any run. The agent that scored the runs had read the key.
- **Review prompt v1:** the prompt from [ops/evals/README.md](../../ops/evals/README.md), used unchanged for runs 1 to 3.
- **Reviewer:** one fresh general-purpose subagent per case per run, told to read only its own diff. No `AGENTS.md`, no chat history, no answer key. The owner reports the model setting was **Auto**, which is a selection mode and not a model name. Auto can choose a model per request, so the 48 reviews may not all have run on the same model, and the underlying models were not recorded. The `model` column says `Auto`. Differences between runs (including run 4 against runs 1 to 3) could therefore partly reflect different models and not only the prompt change. Results cannot be reproduced or compared with runs on a named model.
- **Runs:** 12 cases x 3 runs on v1 = 36 reviews, then 12 reviews for the changed-variable run = 48 in total.
- **Raw outputs, verbatim:** [run 1](./raw-outputs-run-1.md), [runs 2 and 3](./raw-outputs-runs-2-3.md), [run 4](./raw-outputs-run-4.md).

## The one thing changed

Only the prompt changed, and only by one added sentence. Run 4 used the same agent type, the same fresh-agent-per-case set-up and the same diffs.

| | Prompt |
| --- | --- |
| v1 (runs 1 to 3) | Review this diff for defects. For each finding give the file, the line, what goes wrong, and how confident you are. If you find nothing, say so explicitly rather than listing general observations. |
| v2 (run 4) | v1, then: For each finding, also state the concrete input or situation in which it goes wrong; if you cannot name one, do not report it. |

**Hypothesis, written by the agent before run 4 ran:** v2 would cut extra findings on `case-01` and `case-08` without lowering catches on `case-07` and `case-09`. It also warned that v1 might already be at the ceiling, so v2 could not show gains on false alarms, only no change or harm.

## Scoring rules

- **Caught:** the finding names the defect in the answer key. A line number off by a few lines was accepted.
- **Missed:** no finding names the defect, or the finding is hedged so that it does not assert one. The owner ruled the hedged `case-09` findings (runs 2 and 3) as missed.
- **False alarm:** a clean case where the review asserts a defect. Style notes do not count. The owner ruled the `case-04` run 4 finding (a named-argument source-compatibility note) as a valid note and not an asserted defect.
- **Extra findings on a defect case** that are not in the key do not change the verdict. They are recorded in `notes`.

## Results

| Run | Prompt | Caught (of 9 defect cases) | Missed | False alarms (of 3 clean cases) |
| --- | --- | --- | --- | --- |
| 1 | v1 | 9 | 0 | 0 |
| 2 | v1 | 8 | 1 (`case-09`) | 0 |
| 3 | v1 | 8 | 1 (`case-09`) | 0 |
| **v1 total** | | **25 of 27 (93%)** | **2 of 27 (7%)** | **0 of 9 (0%)** |
| 4 | v2 | 9 of 9 (100%) | 0 | 0 of 3 (0%) |

## Did the change help?

**The data does not show a reliable improvement.**

- **The only difference** in verdicts is `case-09`: v1 missed it in two of three runs and caught it once, and v2 caught it in the one run. That is one case, worth 11 percentage points of the defect rate.
- **Sample size is too small.** v2 has a single run of 12 cases. `case-09` is unstable under v1 itself (caught once, missed twice), so one catch in run 4 is within what v1 might have produced on a lucky run. Nothing here supports a claim that v2 is better. At this size I would need many more cases and runs to see an effect of a few points.
- **False alarms:** no difference (0 of 9 versus 0 of 3). With only three clean cases per run, a false-alarm rate of zero says little. A reviewer with a 20% false-alarm rate would show no false alarm in three clean cases about half the time.
- **Confidence:** low that v2 is better, and also low that it is worse. v1 is already close to the ceiling on this set, so the set cannot discriminate between the prompts.

### Against the hypothesis

| Prediction | Observed |
| --- | --- |
| Fewer extra findings on `case-01` | Supported. v1 added a second finding in runs 1 and 3 (a `Handle(...)` bypass and a partial bulk operation) and none in run 2. Run 4 had one finding only. |
| Fewer extra findings on `case-08` | Weakly supported. v1 runs 2 and 3 reported the non-atomic `Add` as a separate second finding. Run 4 reported it inside the single finding, as the example of the failing situation. |
| Catches on `case-07` and `case-09` not lowered | Held. Both were caught in run 4. |
| Ceiling effect on false alarms | Held. Nothing to improve. |

### An effect not predicted

In run 4 the `case-04` reviewer produced a finding on a clean rename, saying callers using the named argument `repository:` would stop compiling. The v1 reviewers all said "no findings" on this case. The owner ruled it a valid note and not a false alarm. It is still output a reviewer has to read and dismiss, and it appeared after the added sentence asked for a concrete situation. One case is a weak signal, but it points in the direction of the risk the added sentence was meant to reduce.

## Where the agent was wrong or useless

These are observations about the agent's contribution to this evaluation.

1. **The same agent set up the runs and scored them.** The agent had read the answer key before launching, so the scoring is not blind. The owner's rulings on two verdicts changed the results (`case-09` runs 2 and 3, `case-04` run 4). That shows the draft scoring was a set of judgement calls, not a measurement.
2. **The reviewers disagreed on line numbers for the same defect.** `case-09` was cited at line 24 in runs 1, 2 and 4 and at line 26 in run 3. `case-10` was cited at 35 in runs 1, 3 and 4 and at 37 in run 2, and the answer key says 37. `case-08` was cited at lines 8, 9 and 10 across runs. The review prompt asks for a line, so this makes the line unreliable evidence of a correct catch, and scoring had to accept a tolerance.
3. **Hedged findings.** On `case-09`, two of the three v1 reviews hedged ("if sign-in does not explicitly reset", "likely incorrect"). A reviewer that hedges on a real defect is hard to act on, and the strict rule counts it as missed.
4. **Extra, unsupported findings on `case-01`.** In v1 the reviewers raised a `Handle(...)` bypass and a partial-bulk-operation concern that are not in the answer key. They were not scored as false alarms because the case contains a real defect, but a reader would have to triage them. The agent did not verify whether either is real.
5. **Worked examples were not checked at first.** Several run 2 to 4 findings quote arithmetic (the `case-03` examples with specific decimals). The first scoring pass accepted them without recomputing. They were recomputed afterwards against the diff and are correct.
6. **No model identity.** The session could not see which model ran the reviews. The owner reports the setting was Auto, so the `model` column says `Auto`, and the model may have varied between reviews. This weakens the "only the prompt changed" claim for run 4, since a model difference could also contribute.
7. **The agent's earlier advice partly mispredicted.** It said v2 might drop `case-07` and `case-09` catches because they have no obvious failing input. Both were caught in run 4. It was right that v1 would sit near the ceiling.
8. **Instruction drift was not checked.** The reviewers were told to make one tool call and read only their own diff. The agent did not verify how many calls each made.

## What would make this evaluation stronger

- Many more cases, with more clean ones. Three clean cases cannot support a false-alarm rate.
- More runs per prompt, so the spread between runs can be compared with the difference between prompts.
- Scoring by someone who has not seen the output generation, with the model name recorded.
- A tolerance rule for line numbers, written before scoring.

## Files

| File | Purpose |
| --- | --- |
| [results.csv](./results.csv) | All 48 rows with verdicts and notes |
| [answer-key.md](./answer-key.md) | Owner's ground truth |
| [raw-outputs-run-1.md](./raw-outputs-run-1.md) | Reviewer output, run 1 |
| [raw-outputs-runs-2-3.md](./raw-outputs-runs-2-3.md) | Reviewer output, runs 2 and 3 |
| [raw-outputs-run-4.md](./raw-outputs-run-4.md) | Reviewer output, run 4 (v2) |
