# Assumptions and Design Decisions

1. Student identity is passed in `X-Student-Id` because the brief specifies that header and does not require account registration. A production system should authenticate parents and derive identity from a secure token.
2. Payment is simulated by clicking “Pay admission fee”; no real payment provider is integrated.
3. `HOLD_DURATION_SECONDS` controls hold duration; default is 600 seconds.
4. One student may have at most one HELD or CONFIRMED admission across all classes. EXPIRED and CANCELLED records are historical and do not block another application.
5. HELD and CONFIRMED records consume seats. EXPIRED and CANCELLED records do not.
6. A background worker expires holds every two seconds. API reads and new applications also expire overdue holds.
7. Row-level class locking prevents overselling a class. A transaction-scoped advisory lock prevents a student creating active applications in two classes concurrently. A partial unique index is a database-level backstop.
8. Admin authentication is not implemented because the assignment does not specify authentication/authorization. The demo admin panel is not production-secure.
9. Class editing/deletion is omitted because the specified API requires class creation and listing, not editing/deletion.
10. Timestamps are stored and returned in UTC.
11. Frontend polls every three seconds so status and seat counts update when other users act or a hold expires.
12. Invalid input returns 400, missing records return 404, and capacity/active-application conflicts return 409.
