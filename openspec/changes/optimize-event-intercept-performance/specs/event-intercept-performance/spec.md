## ADDED Requirements
### Requirement: Cached intercept metadata
The system SHALL cache event parameter metadata and binders for RewriteEvent and ReplaceEvent to avoid repeated reflection during runtime.

#### Scenario: Repeated rewrites reuse cached binders
- **WHEN** multiple RewriteEvent intercepts target the same event type
- **THEN** the runtime uses cached parameter binders instead of re-scanning reflection data

### Requirement: Replacement constructor caching
The system SHALL cache constructor binders for replacement events and reuse them across intercepts.

#### Scenario: Repeated replacements reuse cached constructors
- **WHEN** multiple ReplaceEvent intercepts target the same replacement event type
- **THEN** the runtime reuses cached constructor binders to create replacement events

### Requirement: Shared event parameter cache
The system SHALL expose cached event parameter metadata (names, display names, setters, coercers) for reuse by runtime and editor services.

#### Scenario: Event metadata reuse
- **WHEN** an editor or DSL feature queries event parameters
- **THEN** it reuses the shared cache rather than scanning reflection data again
