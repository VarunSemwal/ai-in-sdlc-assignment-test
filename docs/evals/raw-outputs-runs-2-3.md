# Raw reviewer outputs, runs 2 and 3 (baseline, prompt v1)

Verbatim output from one fresh general-purpose subagent per case per run, same setup as [run 1](./raw-outputs-run-1.md). Not scored here.

# Run 2

## case-01
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:49` — `Ids` is declared nullable, but the loop enumerates it without checking for `null`. A request with missing or `null` IDs will throw `NullReferenceException` instead of returning a client error. **Confidence: high.**

## case-02
- `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:28,32` — `customer` and `status` are interpolated directly into SQL, allowing crafted query parameters to alter the query and potentially expose or modify data. Use parameterized queries instead. **Confidence: High.**

## case-03
- `backend/src/InvoiceApp.Domain/DiscountCalculator.cs`, line 15 — High confidence: Rounding the discount toward zero before subtraction, then truncating the result, can leave the returned amount a cent too high. For example, `Apply(10.00m, 12.36m)` returns `8.77m`, whereas rounding the discounted amount away from zero returns `8.76m`.

## case-04
No findings. The field and constructor parameter are renamed consistently, and all repository references in the diff use the new field.

## case-05
- `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:44` — `Delete` has no `[RequireRole("Admin")]` restriction, so any signed-in user may delete invoices. **Confidence: High**.

## case-06
No findings.

## case-07
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, line 34 — Logging the destructured invoice and `CurrentUser` can expose invoice data and user/session details in application logs, where they may be retained or accessible more broadly than the API response. **Confidence: High**

## case-08
- `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs:9` — Replacing `ConcurrentDictionary` with `Dictionary` makes repository access unsafe when requests run concurrently. Reads and writes may race, causing lost updates or runtime failures. **Confidence: High.**
- `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs:19–24` — `ContainsKey` followed by assignment is not atomic. Concurrent `Add` calls for the same ID can both pass the check, so the duplicate-invoice exception may not be thrown and one invoice may overwrite the other. **Confidence: High.**

## case-09
- **README.md, line 24:** The instructions claim signing out and back in resets sample data, but the preceding text only establishes that data is recreated on process start. If sign-in does not explicitly reset the in-memory data, users will be told to take an action that does not work. **Confidence: medium.**

## case-10
- `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs:37` — The catch-all converts any unhandled exception into HTTP 200 with `status = "completed"`. Failed requests will appear successful to clients, masking errors and potentially causing incorrect follow-up behavior. **Confidence: High.**

## case-11
- `backend/src/InvoiceApp.Domain/ApprovalService.cs:67` — The condition is reversed: pending invoices are rejected, while invoices in other states pass this check and can be decided. It should reject when the status is not `PendingApproval`. **Confidence: high.**

## case-12
No findings. The variable rename preserves the existing filtering and response behavior.

# Run 3

## case-01
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:48` — If `Ids` is null, the `foreach` throws a `NullReferenceException`, resulting in an unhandled server error instead of a client error. **Confidence: High.**
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:49` — If approving any ID fails, earlier approvals in the same request may already have been applied, leaving a partially completed bulk operation. **Confidence: Medium.**

## case-02
- `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:28,32` — High confidence: `customer` and `status` are interpolated directly into SQL, so crafted query parameters can alter the query. Use parameterized queries instead.

## case-03
- `backend/src/InvoiceApp.Domain/DiscountCalculator.cs:14–15` — `MidpointRounding.ToZero` truncates the result instead of rounding it to cents. For example, `Apply(1.009m, 0m)` returns `1.00m`, whereas the previous behavior returned `1.01m`. **Confidence: high.**

## case-04
No defects found in the diff.

## case-05
- `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:44` — `Delete` has no `[RequireRole("Admin")]`, so any signed-in user who can reach this endpoint may delete invoices, contrary to the project's role restrictions for destructive actions. **Confidence: High.**

## case-06
No findings.

## case-07
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:35–36` — Logging the full invoice and current-user objects can expose invoice details and session/user data in application logs, where they may be retained or broadly accessible. Log only non-sensitive identifiers and approval outcome. **Confidence: High**.

## case-08
- `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs:10` — Replacing `ConcurrentDictionary` with `Dictionary` makes repository access unsafe when requests run concurrently. Concurrent reads and writes can cause exceptions or inconsistent behavior. **Confidence: high.**
- `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs:18` — The duplicate check and subsequent insertion are separate operations, so concurrent `Add` calls for the same ID can both pass the check and overwrite an invoice instead of rejecting the duplicate. **Confidence: high.**

## case-09
- **`README.md:26`** — The instruction to reset sample data by signing out and back in is likely incorrect: signing in again does not ordinarily reset the in-memory store, so users may believe data was reset when it was not. **Confidence: medium.**

## case-10
- `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs:35` — The new catch-all converts every unexpected exception into HTTP 200 with `{"status":"completed"}`. This hides server failures and tells clients the operation succeeded when it may have failed. **Confidence: High**.

## case-11
- `backend/src/InvoiceApp.Domain/ApprovalService.cs:67` — The condition now throws when an invoice *is* pending, but allows non-pending invoices through. Valid approval/rejection decisions will fail, while decisions on invoices in other states may proceed. **Confidence: high.**

## case-12
No defects found in this diff. The variable rename preserves the existing filtering and response behavior.
