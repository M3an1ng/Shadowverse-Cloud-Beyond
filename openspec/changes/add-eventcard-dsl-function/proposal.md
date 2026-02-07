# Change: Add eventCard DSL accessor

## Why
We need to read CardInstance parameters from TriggeringEvent to express card-based conditions (for example, "when an allied fairy enters play"). The existing eventInt/eventString/eventGuid helpers only handle primitive data and cannot access event parameters that are CardInstance objects.

## What Changes
- Add a new DSL function `eventCard(name)` that resolves a CardInstance from TriggeringEvent parameters (raw payload preferred, otherwise reflection).
- Return null when the parameter is missing or not a CardInstance.
- Register the new function in the core function registry.
- Add tests covering both raw payload and reflection paths.

## Impact
- Impacted specs: dsl-event-params
- Impacted code: Server/Domain/Abilities/DSL/Functions/CoreFunctions/EventParamFunctions.cs, Server/Domain/Abilities/DSL/Functions/FunctionRegistry.cs, Server.Tests/EventParamDslFunctionTests.cs
