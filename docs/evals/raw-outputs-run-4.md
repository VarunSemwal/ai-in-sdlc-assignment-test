# Raw reviewer outputs, run 4 (changed variable: prompt v2)

Verbatim output from one fresh general-purpose subagent per case. Same set-up as runs 1 to 3 (read only its own diff, same agent type, fresh agent per case). The only difference is the prompt, which has one added sentence:

> For each finding, also state the concrete input or situation in which it goes wrong; if you cannot name one, do not report it.

Not scored here.

## case-01
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:49` — If the request omits `ids` or sends `"ids": null`, `request.Ids` is null and the `foreach` throws, resulting in a server error instead of a handled client error. **Confidence: high.** Validate that `Ids` is non-null (and return a client error if it isn't).

## case-02
- `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:31` — The SQL query interpolates `customer` directly; `status` is also interpolated at line 35. A request such as `customer=' OR 1=1 -- ` can alter the query and return invoices beyond the intended search. Use parameterized queries. **Confidence: High.**

## case-03
- `backend/src/InvoiceApp.Domain/DiscountCalculator.cs:15` — For `amount = 1.01m` and `discountPercent = 0.5m`, the computed discount is `0.00505m`, which `ToZero` rounds to `0.00m`. The method therefore returns `1.01m`, dropping the discount; rounding the final discounted amount to cents would produce `1.00m`. **Confidence: high.**

## case-04
- `backend/src/InvoiceApp.Domain/ApprovalService.cs:14` — Renaming the public constructor parameter from `repository` to `invoices` breaks callers using the named argument `repository:`; for example, `new ApprovalService(repository: repo)` no longer compiles. **Confidence: High.**

## case-05
- `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:44` — `Delete` has no role restriction, so a signed-in user with a non-admin role (for example, an Approver) can delete an invoice. Destructive actions should be Admin-only. **Confidence: High.**

## case-06
No findings.

## case-07
- `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:35` — Logging the full invoice and `CurrentUser` object can expose sensitive invoice fields or user/session data in application logs. **Concrete situation:** an approved invoice contains confidential supplier or financial details, or `CurrentUser` includes identifying claims; those values are serialized into the log entry. **Confidence: high.**

## case-08
- `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`, line 8 — Replacing `ConcurrentDictionary` with `Dictionary` makes repository operations unsafe when requests run concurrently. For example, two simultaneous `Add` calls for the same invoice ID can both pass `ContainsKey` and then overwrite the same entry, bypassing the duplicate-ID error. **Confidence: High.**

## case-09
- **README.md:24** — Signing out and back in is not a reliable way to reset sample data: the diff says the data lives in the in-memory store and is recreated when the process starts. **Situation:** change an invoice, then sign out and sign in again; the change may still be present. **Confidence: high.**

## case-10
- `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs:35` — Any exception not caught by the earlier handlers is returned as HTTP 200 with `{"status":"completed"}`, falsely signaling success and hiding the failure. For example, if an action throws an `IOException`, the client receives a successful response instead of an error. **Confidence: High.**

## case-11
- `backend/src/InvoiceApp.Domain/ApprovalService.cs:67` — The condition is inverted: `EnsurePending` now throws for pending invoices and lets invoices in other states proceed to a decision. **Concrete case:** deciding an `Approved` invoice no longer fails this check. **Confidence: High.**

## case-12
No findings. The variable rename preserves the existing filtering and response behavior.
