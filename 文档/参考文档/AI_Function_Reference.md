# AI Function & Data Structure Reference V1.0

**Objective:** This document provides a technical overview of key C# classes, methods, and data structures within the Unity project, intended for AI tool analysis and understanding. It focuses on interfaces, core logic flows (using pseudocode), and interactions (using diagrams).

---

## 1. Core Data Management (`Assets/Scripts/System/Core/`)

### 1.1 `DatabaseManager` (Singleton)

**Purpose:** Central repository for runtime static game data (Cards, Effects, Buffs, etc.). Acts as the single source of truth after initial loading.

**Key Public Interface:**

*   `DatabaseManager.instance`: Access the singleton instance.
*   `bool IsReady`: Property indicating if all core data has been loaded.
*   `Dictionary<int, Card> cardInfoDict`: Stores card templates.
*   `Dictionary<int, Effect> effectInfoDict`: Stores effect templates.
*   `Dictionary<int, Buff> buffInfoDict`: Stores buff templates.
*   `Dictionary<int, string> keywordNameDict`: Stores keyword names.
*   `Dictionary<int, string> traitNameDict`: Stores trait names.
*   **(Other dictionaries for various data types)**

**Key Methods:**

*   **`void Init()`**
    *   **Purpose:** Initiates the loading process for all core game data from files via `ResourceManager`. Populates the internal dictionaries upon completion.
    *   **Pseudocode:**
        ```csharp
        function Init():
          IsReady = false
          loadingProgress = 0
          totalLoadTasks = /* number of CSV files to load */

          // Trigger ResourceManager to load each CSV asynchronously or via callbacks
          ResourceManager.instance.LoadCSV<Card>("Assets/StreamingAssets/Cards/info.csv", callback: OnCardDataLoaded)
          ResourceManager.instance.LoadCSV<Effect>("Assets/StreamingAssets/Cards/effect.csv", callback: OnEffectDataLoaded)
          // ... other LoadCSV calls for buffs, keywords, traits, etc.

        function OnDataLoaded(loadedDataDictionary):
          // Store the loaded data in the corresponding internal dictionary
          // e.g., this.cardInfoDict = loadedDataDictionary
          loadingProgress++
          if loadingProgress == totalLoadTasks:
             IsReady = true
             // Potentially fire an event: OnDatabaseReady
        ```

*   **`Card GetCardInfo(int cardId)`**
    *   **Purpose:** Retrieves a card template by its ID.
    *   **Returns:** `Card` object or `null` if not found.
    *   **Pseudocode:**
        ```csharp
        function GetCardInfo(cardId):
          if cardInfoDict.ContainsKey(cardId):
            return cardInfoDict[cardId]
          else:
            LogWarning("Card ID not found: " + cardId)
            return null
        ```
    *   **(Similar `GetEffectInfo`, `GetBuffInfo`, `GetKeywordName`, `GetTraitName` methods exist)**

---

## 2. Core Game Logic (`Assets/Scripts/Game/`)

### 2.1 `Battle` (`Assets/Scripts/Game/Battle/Battle.cs`)

**Purpose:** Represents a single battle instance, holding the core game state and enforcing game rules. It processes player actions and manages the effect queue.

**Key Properties:**

*   `BattleState CurrentState`: Holds the complete current state of the battle (player HP/PP/EP, cards in hand/field/deck, turn info, etc.). (Requires `BattleState.cs` definition).
*   `Queue<Effect> effectQueue`: Queue for pending effects to be processed.
*   `bool IsProcessing`: Flag indicating if an effect is currently being processed.

**Key Methods:**

*   **`void PlayerAction(PlayerAction action)`**
    *   **Purpose:** Processes an action requested by a player (Play Card, Attack, End Turn, etc.). Validates the action based on `CurrentState` and game rules. May enqueue effects.
    *   **Parameters:** `PlayerAction` (Data structure containing action type, source/target IDs, etc.).
    *   **Pseudocode:**
        ```csharp
        function PlayerAction(action):
          // 1. Validate action based on CurrentState (turn phase, PP cost, target validity, etc.)
          if not IsValidAction(action, CurrentState):
            LogError("Invalid action attempted.")
            return

          // 2. Apply immediate state changes (e.g., reduce PP, move card from hand to playing)
          UpdateStateForActionStart(action, CurrentState)

          // 3. Enqueue corresponding effects (e.g., Fanfare, Spell effect)
          card = CurrentState.GetCard(action.sourceCardId) // Example
          if card has Fanfare effects:
             foreach effect in card.FanfareEffects:
               EnqueueEffect(effect, source: card, context: action)

          // 4. Trigger UI/Network updates (usually via BattleManager)
          NotifyManagerOfAction(action) // Indirectly calls BattleManager
        ```

*   **`void EnqueueEffect(Effect effectInstance, BattleUnit sourceUnit, ... context)`**
    *   **Purpose:** Adds a prepared `Effect` instance to the `effectQueue`. Ensures proper context (source, target) is set on the effect instance before adding.
    *   **Parameters:** `Effect effectInstance` (A copy/instance, not the template), context info.
    *   **Pseudocode:**
        ```csharp
        function EnqueueEffect(effectInstance, ... context):
          // Set context on effectInstance (e.g., effectInstance.source = sourceUnit)
          effectQueue.Enqueue(effectInstance)
          // Optionally trigger processing if not already running
          if not IsProcessing:
             ProcessEffectQueue()
        ```

*   **`void ProcessEffectQueue()`**
    *   **Purpose:** Processes effects from the `effectQueue` one by one until the queue is empty. This is the core loop for effect resolution.
    *   **Pseudocode:**
        ```csharp
        function ProcessEffectQueue():
          if IsProcessing or effectQueue.Count == 0:
            return

          IsProcessing = true
          while effectQueue.Count > 0:
            currentEffect = effectQueue.Dequeue()

            // 1. Check Condition (using Parser system)
            if not currentEffect.Condition(CurrentState):
               continue // Skip effect if condition not met

            // 2. Resolve Targets (using EffectTargetInfo/BattleCardFilter)
            resolvedTargets = currentEffect.ResolveTargets(CurrentState)
            currentEffect.SetInvokeTargets(resolvedTargets) // Store targets in the effect instance

            // 3. Apply Ability (using EffectAbilityHandler)
            EffectAbilityHandler.ApplyEffect(currentEffect, CurrentState)
            // Note: Applying an effect might enqueue more effects!

            // 4. Notify Manager for UI/Animation (indirectly)
            NotifyManagerOfEffectResolution(currentEffect)

            // 5. Handle potential triggers caused by this effect (Last Words, Clash etc.)
            //    This might involve calling EffectAbilityHandler.CheckTriggers(...) which could enqueue more effects.

            // Optional: Yield/Wait if waiting for animations via BattleManager signal
            // yield return BattleManager.WaitForAnimation(currentEffect.animationKey)

          IsProcessing = false
          // Maybe check for game end condition here
        ```
    *   **Interaction Diagram (Simplified Sequence):**
        ```mermaid
        sequenceDiagram
            participant B as Battle
            participant EQ as effectQueue
            participant P as Parser
            participant TF as TargetFinder (EffectTargetInfo/Filter)
            participant EAH as EffectAbilityHandler
            participant BM as BattleManager (UI/Network Sync)

            loop While effectQueue is not empty
                B->>EQ: Dequeue()
                activate B
                EQ-->>B: effect
                B->>P: effect.Condition(state)
                P-->>B: conditionMet (bool)
                alt Condition Met
                    B->>TF: effect.ResolveTargets(state)
                    TF-->>B: resolvedTargets
                    B->>EAH: ApplyEffect(effect, state)
                    activate EAH
                    EAH->>B: Modify BattleState (Directly or via methods)
                    EAH->>B: EnqueueEffect() (Potentially, for chained effects)
                    deactivate EAH
                    B->>BM: NotifyEffectResolved(effect) # For UI/Animation
                    B->>EAH: CheckTriggers(effect, state) # Check for Reactions
                    activate EAH
                    EAH->>B: EnqueueEffect() (Potentially, for triggered effects)
                    deactivate EAH
                    B->>BM: WaitForAnimation() # Optional wait
                    BM-->>B: Animation Complete
                end
                deactivate B
            end
        ```

*   **`void NextPhase()` / `void OnTurnStart()` / `void OnTurnEnd()`**
    *   **Purpose:** Manages turn progression and triggers associated effects (Turn Start/End effects, PP increase, card draw).
    *   **Pseudocode (`OnTurnStart` example):**
        ```csharp
        function OnTurnStart(activePlayerUnit):
          // 1. Increase Max PP (if applicable)
          activePlayerUnit.IncreaseMaxPP()
          // 2. Refill Current PP
          activePlayerUnit.RefillPP()
          // 3. Draw Card
          DrawCard(activePlayerUnit)
          // 4. Trigger Turn Start Effects (using EffectAbilityHandler)
          EffectAbilityHandler.CheckTriggers(timing: TurnStart, player: activePlayerUnit, state: CurrentState)
          // 5. Update Buff Durations
          activePlayerUnit.field.ForEach(card => card.BuffController.OnTurnStart())
          // ... check for buffs expiring ...
          // 6. Update State (Turn number, active player)
          CurrentState.currentTurn++
          CurrentState.activePlayerIndex = activePlayerUnit.index
          // 7. Notify Manager
          NotifyManagerOfTurnStart()
        ```

### 2.2 Effect Execution (`Assets/Scripts/Game/Effect/` & `Assets/Scripts/System/Parser/`)

**Core Classes:** `Effect`, `EffectAbilityHandler`, `EffectDatabase`, `EffectParseHandler`, `EffectTargetInfo`, `Parser`, `Operator`, `Identifier`.

*   **`EffectAbilityHandler.ApplyEffect(Effect effect, BattleState state)` (Static or via Instance)**
    *   **Purpose:** Main entry point to execute a single resolved effect's ability.
    *   **Pseudocode:**
        ```csharp
        function ApplyEffect(effect, state):
           // 1. Get the specific ability function based on effect.ability enum
           abilityFunction = EffectAbilityHandler.GetAbilityFunc(effect.ability)

           // 2. Potentially calculate dynamic values in abilityOptionDict using Parser
           Parser.UpdateEffectExpressions(effect, state) // e.g., calculate "damage = source.atk * 2"

           // 3. Execute the ability function
           abilityFunction(effect, state) // This function modifies BattleState

           // 4. Handle post-processing (e.g., remove consumed effect)
           EffectAbilityHandler.PostprocessEffect(effect, state)
        ```

*   **`EffectAbilityHandler.CheckTriggers(EffectTiming timing, ... context)`**
    *   **Purpose:** Finds and potentially enqueues effects that trigger based on the current game event (timing).
    *   **Pseudocode:**
        ```csharp
        function CheckTriggers(timing, ... context):
          // Iterate through potential sources of triggered effects (cards in field, leader buffs, amulets etc.)
          foreach sourceObject in RelevantObjects(state, context):
             foreach effect in sourceObject.GetEffects():
                if effect.timing == timing:
                   // Check effect's specific place/condition requirements
                   if effect.MeetsTriggerConditions(state, context):
                      // Create instance, set context, enqueue
                      triggeredEffectInstance = new Effect(effect) // Copy template
                      triggeredEffectInstance.SetContext(...)
                      Battle.instance.EnqueueEffect(triggeredEffectInstance, ...)
        ```

*   **`Parser.ParseEffectExpression(string expression, Effect contextEffect, BattleState state)`**
    *   **Purpose:** Evaluates a mathematical or logical expression string from an effect's data.
    *   **Returns:** `int` result.
    *   **Pseudocode (Simplified Activity):**
        ```mermaid
        graph LR
            A[Start: Expression String] --> B{Split into Tokens (Operands/Operators)};
            B --> C{Initialize Result = 0, CurrentOperator = null};
            C --> D{Get Next Token};
            D --> E{Is it Operand?};
            E -- Yes --> F[Resolve Operand Value using Identifier.GetIdentifier(token, contextEffect, state)];
            F --> G{Apply Previous Operator?};
            G -- Yes --> H[result = Operator.Operate(result, previousOperator, operandValue)];
            G -- No --> H[result = operandValue];
            H --> D;
            E -- No --> I{Is it Operator?};
            I -- Yes --> J[Store Operator];
            J --> D;
            I -- No --> K[End of Expression?];
            K -- Yes --> L[Return Final Result];
            K -- No --> M[Error: Invalid Token];
            M --> L;
        ```
    *   **Note:** Actual implementation likely handles operator precedence differently or uses a more robust parsing method (like shunting-yard). The pseudocode shows a simple left-to-right evaluation.

*   **`Identifier.GetIdentifier(string identifierString, ... context)`**
    *   **Purpose:** Resolves a string identifier (e.g., "me.hp", "target.atk", "random(1,6)") into its current numerical value based on game state and context.
    *   **Returns:** `int` value.
    *   **Pseudocode:**
        ```csharp
        function GetIdentifier(identifierString, context):
          parts = identifierString.Split('.') // e.g., "effect.source.atk" -> ["effect", "source", "atk"]
          currentValueSource = ResolveInitialContext(parts[0], context) // e.g., "me" -> context.invokeUnit, "effect" -> context.effect

          for i = 1 to parts.Length - 1:
             currentValueSource = GetPropertyOrNestedObject(currentValueSource, parts[i])
             if currentValueSource == null: return 0 // Error or default

          // Handle special functions like random(), count(), hasKeyword()
          if IsFunctionCall(parts.Last()):
             return ExecuteFunction(parts.Last(), currentValueSource, context)
          else:
             // Final part is a property name
             return GetPropertyValue(currentValueSource, parts.Last())
        ```

### 2.3 Data Structures (`Card`, `Effect`, `Buff`)

These classes primarily hold data loaded from CSVs and runtime state. They implement `IIdentifyHandler` for the Parser system.

*   **`Card` (`Assets/Scripts/Game/Card/Card.cs`)**
    *   **Key Properties:** `id`, `cardName`, `cost`, `atk`, `hp`, `craft`, `cardType`, `rarity`, `traits` (List), `keywords` (List), `effects` (List<`Effect`>), `options` (Dictionary<string, string>), `artworkHandle` (Addressable handle).
    *   **Key Methods:** `Card.Get(id)` (static factory), `new Card(rhs)` (copy constructor), `IIdentifyHandler.HandleIdentifier(string id)` (property access for Parser).

*   **`Effect` (`Assets/Scripts/Game/Effect/Effect.cs`)**
    *   **Key Properties:** `id`, `effectName`, `timing` (Enum), `target` (string rule), `place` (Enum), `conditionString`, `ability` (Enum), `abilityOptionDict` (parameters), `outputOptionDict` (runtime calculated values), `source`, `invokeUnit`, `invokeTarget` (runtime context).
    *   **Key Methods:** `Effect.Get(id)` (static factory), `new Effect(rhs)` (copy), `Condition()`, `SetInvokeTarget()`, `Apply()`, `IIdentifyHandler.HandleIdentifier(string id)`.

*   **`Buff` (`Assets/Scripts/Game/Buff/Buff.cs`)**
    *   **Key Properties:** `id`, `buffName`, `costMod`, `atkMod`, `hpMod`, `traits` (List), `keywords` (List), `effects` (List<`Effect`>), `condDictList` (conditions), `timing`, `options`, `source`, `StayTurn`.
    *   **Key Methods:** `new Buff(...)` (constructor from data), `Condition()`, `OnTurnStart()`, `IIdentifyHandler.HandleIdentifier(string id)`.

---

## 3. UI & Network Coordination (`Assets/Scripts/Manager/`)

### 3.1 `BattleManager` (Singleton) (`Assets/Scripts/Manager/BattleManager.cs`)

**Purpose:** Coordinates battle scene logic, UI updates, and network communication. Acts as a bridge between `Battle` (logic) and Views/Photon.

**Key Properties:**

*   `Queue<BattleState> hudQueue`: Queue of state changes waiting for UI presentation.
*   `BattleSystemView systemView`, `BattleUnitView myView`, `opView`: References to main UI view controllers.
*   `PhotonView myPhotonView`: Reference for sending RPCs.

**Key Methods:**

*   **`void ProcessQueue()`**
    *   **Purpose:** Processes the `hudQueue`, sending state updates sequentially to the UI views and waiting for their completion signals (`IsDone`).
    *   **Pseudocode:**
        ```csharp
        coroutine ProcessQueue():
          while true:
             yield return WaitForCondition(() => hudQueue.Count > 0) // Wait until queue has items

             SetLock(true) // Lock player input
             currentStateUpdate = hudQueue.Dequeue()

             // Reset completion flags on views
             systemView.IsDone = false
             myView.IsDone = false
             opView.IsDone = false

             // Send state to views
             systemView.SetState(currentStateUpdate)
             myView.SetState(currentStateUpdate)
             opView.SetState(currentStateUpdate)

             // Wait for all views to signal completion (animations finished etc.)
             yield return WaitForCondition(() => systemView.IsDone && myView.IsDone && opView.IsDone)

             SetLock(false) // Unlock input
             // Maybe trigger next step in Battle logic if needed
        ```
    *   **Interaction Diagram (Simplified Sequence):**
        ```mermaid
        sequenceDiagram
            participant BM as BattleManager
            participant HQ as hudQueue
            participant SV as SystemView
            participant MV as MyUnitView
            participant OV as OpUnitView
            participant B as Battle (Indirect)

            B->>HQ: Enqueue(newState)
            BM->>HQ: Dequeue()
            activate BM
            HQ-->>BM: stateUpdate
            BM->>SV: SetState(stateUpdate)
            BM->>MV: SetState(stateUpdate)
            BM->>OV: SetState(stateUpdate)
            activate SV
            activate MV
            activate OV
            SV-->>BM: IsDone = true
            deactivate SV
            MV-->>BM: IsDone = true
            deactivate MV
            OV-->>BM: IsDone = true
            deactivate OV
            BM->>B: NotifyUIComplete() # Optional signal back to Battle
            deactivate BM
        ```

*   **`void EnemyPlayerAction(PlayerAction action)`**
    *   **Purpose:** Called by local UI/Input handlers when the local player performs an action. Sends the action to opponents via RPC.
    *   **Parameters:** `PlayerAction` action.
    *   **Pseudocode:**
        ```csharp
        function EnemyPlayerAction(action):
          // Convert PlayerAction to serializable data (e.g., int array, byte array)
          serializedAction = SerializeAction(action)
          // Send RPC
          myPhotonView.RPC("RPCPlayerAction", RpcTarget.Others, serializedAction)
        ```

*   **`[PunRPC] void RPCPlayerAction(byte[] serializedAction)`** (Located in `BattleUnitView` but conceptually handled via `BattleManager` coordination)
    *   **Purpose:** Receives an action from a remote opponent via Photon RPC. Deserializes it and passes it to the `Battle` logic.
    *   **Parameters:** `byte[] serializedAction`.
    *   **Pseudocode:**
        ```csharp
        function RPCPlayerAction(serializedAction):
          // Deserialize action data
          action = DeserializeAction(serializedAction)
          // Apply the action to the local Battle simulation
          Battle.instance.PlayerAction(action)
        ```

---

This reference provides a starting point for understanding the core functional units and data flow. Further details can be found by examining the specific C# source code files referenced.