# Eval answer key

Ground-truth defect assessments for the twelve diffs in `ops/evals/cases/`.
Line numbers refer to the changed file in each diff.

| Case | Defect | File and line | Defect summary |
|---|---|---|---|
| case-01 | Yes | `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:48` | `Ids` is nullable, but the action iterates it without checking for null, so a missing or null list causes an unhandled exception. |
| case-02 | Yes | `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:27,31` | Customer and status input are interpolated directly into SQL, allowing SQL injection. |
| case-03 | Yes | `backend/src/InvoiceApp.Domain/DiscountCalculator.cs:15-16` | The calculation truncates the discount before subtracting it and rounds the result toward zero, which can produce an incorrect discounted amount. |
| case-04 | No | `backend/src/InvoiceApp.Domain/ApprovalService.cs:12-15` | The change only renames the repository field and constructor parameter; service behavior is unchanged. |
| case-05 | Yes | `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:44` | The new destructive delete action has no Admin role restriction, contrary to the app's authorization rules. |
| case-06 | No | `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs:62` | The added tests check existing rejection validation, approval metadata, and missing-invoice behavior without introducing a defect. |
| case-07 | Yes | `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs:34` | Logging the full invoice and user session can expose customer and session data in application logs. |
| case-08 | Yes | `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs:8` | Replacing the concurrent dictionary with an unsynchronized dictionary makes concurrent repository access unsafe. |
| case-09 | Yes | `README.md:23` | Signing out and back in does not reset the in-memory invoice store, so the new reset instruction is false. |
| case-10 | Yes | `backend/src/InvoiceApp.Api/Controllers/ApiControllerBase.cs:37` | The catch-all converts unexpected failures into an HTTP 200 "completed" response, hiding errors and falsely reporting success. |
| case-11 | Yes | `backend/src/InvoiceApp.Domain/ApprovalService.cs:67` | The condition is inverted, so pending invoices are rejected as invalid while already-decided invoices can pass the guard and be decided again. |
| case-12 | No | `backend/src/InvoiceApp.Api/Controllers/InvoicesController.cs:25,34,38` | The change consistently renames a local variable without changing filtering or response behavior. |
