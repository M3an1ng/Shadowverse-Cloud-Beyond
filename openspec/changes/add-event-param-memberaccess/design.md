# Design Notes: TriggeringEvent MemberAccess

## Backend resolution strategy
- `context.triggeringEvent` is typed as `IGameEvent`, so MemberAccess cannot use direct property access for event-specific parameters.
- Introduce a helper (e.g., `EventParameterAccessor`) that:
  - Accepts `(IGameEvent ev, string name, Type expectedType)`
  - Uses `EventParameterCache` to find a binding for the event's runtime type
  - Returns `null` if missing or incompatible
  - Casts to `expectedType` for typed chaining
- `ExpressionTreeBuilder` detects MemberAccess on `IGameEvent`:
  - Emits a call to the helper with the member name
  - Converts the result to the expected type for subsequent chained access

## Frontend mapping strategy
- When the current node is `context.triggeringEvent`, use event metadata to surface parameters as property options (name + type).
- Selecting a parameter should emit `context.triggeringEvent.<Param>` (MemberAccess chain).
- For legacy `event*` helpers, map to the equivalent MemberAccess chain when the parameter name is a literal and the event type is known; otherwise keep as raw text.
