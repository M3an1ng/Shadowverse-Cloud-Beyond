## 1. Implementation
- [x] 1.1 Add trigger config option for transaction-scoped same-name limiting (default disabled)
- [x] 1.2 Implement transaction-scoped same-name trigger tracking and filtering in the transaction flow
- [x] 1.3 Apply filtering consistently across `Before`/`On`/`After` trigger timings within the same transaction
- [x] 1.4 Add/update tests for enabled and disabled behavior, including same owner vs different owner cases

## 2. Validation
- [x] 2.1 Run OpenSpec strict validation for this change
