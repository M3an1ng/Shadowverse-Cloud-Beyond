## ADDED Requirements
### Requirement: LeaveField pre-routing event
The system SHALL provide a committable `LeaveField` event that evaluates leave intents before emitting legacy leave events.

#### Scenario: Route destroy intent
- **WHEN** `LeaveField` receives a `Destroy` leave intent for a valid target
- **THEN** it emits one `Destroy` event as the routed legacy event

#### Scenario: Route vanish intent
- **WHEN** `LeaveField` receives a `Vanish` leave intent for a valid target
- **THEN** it emits one `Vanish` event as the routed legacy event

#### Scenario: Route return intent
- **WHEN** `LeaveField` receives a `Return` leave intent for a valid target
- **THEN** it emits one `Return` event as the routed legacy event

#### Scenario: Route tuck intent
- **WHEN** `LeaveField` receives a `Tuck` leave intent for a valid target
- **THEN** it emits one `Tuck` event as the routed legacy event

### Requirement: Immunity evaluation is centralized in LeaveField
Leave immunity checks SHALL be evaluated by `LeaveField` rather than being split across effect handlers and legacy leave events.

#### Scenario: Ability destroy blocked by destroy immunity
- **WHEN** `LeaveField` receives `Destroy` with reason source `Ability` and target has destroy immunity
- **THEN** `LeaveField` emits no routed leave event

#### Scenario: Ability vanish blocked by vanish immunity
- **WHEN** `LeaveField` receives `Vanish` with reason source `Ability` and target has `VanishResist` on the field
- **THEN** `LeaveField` SHALL route to `Destroy` instead of `Vanish`

#### Scenario: System source bypasses ability-only immunity
- **WHEN** `LeaveField` receives a leave intent with reason source `System`
- **THEN** ability-only immunity checks SHALL NOT block routing

### Requirement: Legacy leave events keep commit semantics
`Destroy/Vanish/Return/Tuck` SHALL preserve their current commit responsibilities after being routed by `LeaveField`.

#### Scenario: Destroy still performs legacy commit flow
- **WHEN** `LeaveField` routes to `Destroy`
- **THEN** `Destroy.Commit` still performs its existing state-change flow and emits move-derived events as before

#### Scenario: Vanish still performs legacy commit flow
- **WHEN** `LeaveField` routes to `Vanish`
- **THEN** `Vanish.Commit` still emits `CardMoved(... -> Removed)` as before

#### Scenario: Return still performs legacy commit flow
- **WHEN** `LeaveField` routes to `Return`
- **THEN** `Return.Commit` still emits `AddCardToHand` and follow-up move events as before

#### Scenario: Tuck still performs legacy commit flow
- **WHEN** `LeaveField` routes to `Tuck`
- **THEN** `Tuck.Commit` still emits `AddCardToDeck` and follow-up move events as before

### Requirement: Direct emitters use LeaveField as initial action
All domain code paths that directly instantiate `Destroy/Vanish/Return/Tuck` for initial leave actions SHALL instantiate `LeaveField` instead.

#### Scenario: Destroy call sites migrated
- **WHEN** an initial destroy action is triggered from handlers or domain events
- **THEN** the code path emits `LeaveField(Destroy)` instead of constructing `Destroy` directly

#### Scenario: Vanish call sites migrated
- **WHEN** an initial vanish action is triggered from handlers or domain events
- **THEN** the code path emits `LeaveField(Vanish)` instead of constructing `Vanish` directly

#### Scenario: Return and tuck call sites migrated
- **WHEN** an initial return or tuck action is triggered from handlers or domain methods
- **THEN** the code path emits `LeaveField(Return/Tuck)` instead of constructing `Return/Tuck` directly
