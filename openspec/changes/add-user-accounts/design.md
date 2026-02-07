## Context
The current user flow defaults to a guest identity. We need registration and login with a persistent account id that survives server restarts and can be used across multiple servers.

## Goals / Non-Goals
- Goals: persistent account identity; secure credential storage; minimal UI changes; account-bound resources/decks/match history.
- Non-Goals: third-party OAuth, email verification, password reset, profile customization (unless requested).

## Decisions
- Store account records in the primary database and treat it as the source of truth across servers.
- Hash passwords using a secure, salted algorithm provided by the server framework (no plaintext storage).
- Issue an authenticated session/token on login and use it to associate SignalR connections with an account id.

## Risks / Trade-offs
- Introducing auth adds security and migration complexity; mitigate by using proven hashing + server-side validation.
- Tying resources/decks/match history to accounts may require migration of existing guest data.

## Migration Plan
- Provide a one-time path to link a guest session to a newly registered account.
- Backfill existing guest data to the new account id on registration.

## Open Questions
- What does "cross-server cache" mean in this context: shared DB only, or also distributed cache (e.g., Redis)?
- What username/password rules (length, allowed chars) should we enforce?
- Should guest data be merged on registration or left unclaimed?
- What auth mechanism is preferred for the client (JWT bearer, cookie, or SignalR access token)?
