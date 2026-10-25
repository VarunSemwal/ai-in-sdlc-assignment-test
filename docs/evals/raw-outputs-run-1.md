# Raw reviewer outputs, run 1 (baseline, prompt v1)

Verbatim output from one fresh general-purpose subagent per case, on the harness default model (name not exposed to the session; fill in the `model` column yourself). Each agent was told to read only its own diff and apply the v1 review prompt. Not scored here.

## case-01
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:49` — `Ids` is nullable, but the loop enumerates it without a null check. A request with `Ids` omitted or set to `null` causes a `NullReferenceException` instead of a client error. **Confidence: High.**
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:51` — The approval call bypasses `Handle(...)`, so service errors may not map to the intended 404, 409, or 400 responses and could become server errors. **Confidence: Medium.**

## case-02
- `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:28` — The SQL query interpolates the user-supplied `customer` value directly. This can allow SQL injection; the `status` interpolation at line 32 has the same flaw. Use parameterized queries. **Confidence: High**

## case-03
- **File:** `backend/src/InvoiceApp.Domain/DiscountCalculator.cs` **Line:** 15–16 **Finding:** `MidpointRounding.ToZero` truncates both the discount and the final amount instead of rounding to the nearest cent. This can systematically change invoice totals—for example, a fractional-cent discount is truncated, and the result may differ from the previous `AwayFromZero` behavior. **Confidence:** High

## case-04
No findings. The diff consistently renames the repository field and constructor parameter; behavior is unchanged.

## case-05
- `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:44` — The delete action has no `[RequireRole("Admin")]` restriction, so non-admin users who can access the API can delete invoices. Destructive actions should be Admin-only. **Confidence: high**.

## case-06
No findings. The diff adds tests for whitespace rejection reasons, approval metadata, and unknown invoices; no defect is evident in these additions.

## case-07
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:34–36` — The new log serializes the full invoice and current user/session object, which can expose sensitive invoice data or user information in application logs. **Confidence: High**

## case-08
- `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`, line 8 — Replacing `ConcurrentDictionary` with `Dictionary` makes repository access unsafe under concurrent API requests. Concurrent reads and writes can race or corrupt dictionary state; additionally, `Add`'s `ContainsKey` check followed by assignment is not atomic, so duplicate IDs may be accepted. **Confidence: High.**

## case-09
- **README.md:24** — "To reset the sample data without restarting, sign out and sign in again" is misleading: signing in again does not reset the in-memory data, so changes remain. **Confidence: High**.

## case-10
- `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs:35` — The catch-all converts every unexpected exception into HTTP 200 with `status = "completed"`. Callers can be told an operation succeeded even when it failed, and failures become harder to detect. Return an appropriate server-error response or let the exception-handling middleware handle it. **Confidence: High**

## case-11
- `backend/src/InvoiceApp.Domain/ApprovalService.cs:67` — The condition is reversed: it now throws when an invoice *is* pending approval, while allowing decisions on invoices in any other status. This blocks valid approvals and may permit invalid state transitions. **Confidence: High**

## case-12
No findings. The variable rename preserves the existing filtering and response behavior.
