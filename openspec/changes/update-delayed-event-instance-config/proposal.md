# Change: Instance-Scoped Event Delay Configuration

## Why
Current delayed-event behavior is bound to event classes, but gameplay needs different delay modes per event instance (e.g., per draw, per card move). We need a configurable, instance-scoped delay model with safe defaults.

## What Changes
- Introduce a per-instance DelayConfig for delayed events with default disabled behavior.
- Update the transaction engine to read DelayConfig instead of class-level delay flags.
- Migrate existing delayed events (CardMoved, CombatResolution, TurnStart, DrawEvent) to configure DelayConfig at creation sites, preserving current order semantics.
- Add guardrails to prevent delay configuration changes after events are enqueued.

## Impact
- Affected specs: event-delay (new)
- Affected code: Server/Domain/Events, Server/Services/GameActionTransactionService.cs, event creation sites
