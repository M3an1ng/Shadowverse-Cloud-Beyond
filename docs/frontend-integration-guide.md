# Frontend Integration Guide

> Reviewed: 2026-10-12. Based on the source code, configuration, and read-only database statistics in the current local working tree, including uncommitted implementations. This does not represent the deployed production version.
>
> For developers integrating or rebuilding the frontend. This guide describes the existing protocol; it does not require the frontend to reimplement battle rules.

## At a Glance

| What you need to know | Current implementation |
|---|---|
| Languages | Vanilla JavaScript, HTML, CSS, and Vite on the frontend; C# / ASP.NET Core 8 on the backend |
| API calls and connections | HTTP JSON for the card catalog, accounts, decks, and rooms; SignalR at `/gamehub` for battles |
| Game state | Receive a full snapshot through `ReceiveGameState`, then process individual events; `TurnStarted` also carries a full snapshot |
| Player actions | Mulligan, play cards, attack, evolve, super-evolve, use PP+1, activate abilities, fuse, submit effect choices, end the turn, surrender, chat, and send emotes |
| Card count | The local database contains **2,811 card definitions**, including **2,234 with `IsCollectible=true`**; the repository seed file has only **77 entries** and is not the full catalog |
| Classes / factions | Seven playable classes plus Neutral, for eight categories in total |
| AI opponents | **Available.** A rules-based backend AI executes actions automatically; the frontend and a large language model do not need to play on its behalf |

Two distinctions are essential:

1. **HTTP and SignalR do not use the same property casing.** Ordinary HTTP properties usually use camelCase; ordinary SignalR properties retain PascalCase. Explicitly named properties retain their assigned names.
2. **A room ID is not a game ID.** Use `roomId` for rooms and connections, and `activeGameId` / the state's `GameId` for an individual battle. The legacy `gameId` in a room response is a compatibility field that actually refers to the room ID.

## 1. Languages and Technology Stack

| Component | Actual technology | Integration implications |
|---|---|---|
| Frontend | Vanilla JavaScript ES Modules, HTML, and CSS | React, Vue, and TypeScript are not required by the current implementation |
| Frontend development and build | Vite 6; `npm run dev` / `npm run build` | The existing project can be started or built directly |
| Battle client | `@microsoft/signalr`, declared as `^8.0.7` | Use the SignalR SDK rather than sending and receiving JSON over a raw WebSocket |
| Backend | C# and ASP.NET Core, targeting `net8.0` | Battle resolution, rule validation, and AI run on the backend |
| Database | EF Core + SQLite | Access data through the API, not directly from the frontend |

An integrating frontend can continue using JavaScript. You may also organize a new UI with TypeScript or another suitable language, provided it follows the existing HTTP / SignalR protocol. **The current project itself does not use TypeScript.**

Source references: [Frontend dependencies](../package.json), [Backend project](../Server/ShadowverseBeyond.Server.csproj).

## 2. API Calls and Connections

### 2.1 Addresses and Startup

| Purpose | Development address / path |
|---|---|
| Frontend page | `http://127.0.0.1:5173` |
| Backend HTTP | `http://localhost:5100` |
| API | Use `/api/...` from the page |
| Battle connection | Use `/gamehub` from the page |
| Development log connection | `/loghub`; not required for battle integration |

Vite already forwards `/api`, `/gamehub`, and `/loghub` to the backend, with WebSocket support for both Hub proxies. The existing frontend uses relative URLs. A separately deployed frontend needs an equivalent proxy or the correct backend address; development ports should not be copied into a production configuration unchanged.

Run these commands separately from the repository root:

```powershell
dotnet run --project "Server/ShadowverseBeyond.Server.csproj" --launch-profile http
```

```powershell
npm run dev
```

**Starting the backend applies database migrations and runs seed logic.** The backend was not started, the database was not migrated, and no game was created merely to verify this guide.

Source references: [Proxy configuration](../vite.config.js), [Backend launch configuration](../Server/Properties/launchSettings.json), [Serialization and endpoint registration](../Server/Program.cs).

### 2.2 Common HTTP Endpoints

Use `Content-Type: application/json` for JSON requests. Below, `{roomId}` is an actual room ID, `{cardId}` is an integer card definition ID, and `{deckId}` is a GUID.

| Method and path | Purpose | Request / response details |
|---|---|---|
| `POST /api/auth/register` | Register | Send `username` and `password`; optionally include `guestId` to migrate guest-owned decks |
| `POST /api/auth/login` | Log in | Send `username` and `password`; returns fields such as `userId`, `username`, `avatarUrl`, `role`, and `permissionLevel` |
| `GET /api/cards` | All card definitions | Returns an array directly, not `{items: [...]}`; this endpoint has no pagination parameters |
| `GET /api/cards/{cardId}` | One card definition | Returns a single card definition object |
| `GET /api/cards/{cardId}/abilities` | A card's active abilities | Returns an ability array, including keywords, descriptions, and `jsonConfig` |
| `GET /api/abilities/{abilityId}` | One active ability | The ability ID is a database integer, not a runtime GUID for the current game |
| `GET /api/decks?userId={userId}` | Account-owned decks | Returns an array of deck summaries |
| `GET /api/decks?guestId={guestId}` | Guest-owned decks | Guest and account identities are passed separately |
| `GET /api/decks/{deckId}?userId={userId}` | Deck details | Use `guestId` instead of `userId` for a guest; the details include `cards` |
| `POST /api/match` | Create a room | Optional fields include `hostPlayerId`, `useAiFallback`, and `ttlMinutes`; returns HTTP 201 and a room object |
| `GET /api/match` | Current rooms | Returns `{items: [...]}` |
| `GET /api/match/{roomId}` | Room details | Returns a room object |
| `POST /api/match/{roomId}/join` | Join a room | Send `joinCode`, optionally with `playerId`; the response provides guest-seat information |
| `POST /api/match/{roomId}/deck` | Select a battle deck | Send `playerId`, `token`, and `deckId`, plus either `userId` or `guestId` |
| `POST /api/match/{roomId}/ready` | Mark ready | Send `playerId` and `token` |
| `POST /api/match/{roomId}/start` | Start as the host | Send `playerId` and `token`; readiness and valid deck selections are required |
| `POST /api/match/{roomId}/leave` | Leave a room | Send `playerId` and `token`; this is different from surrendering during a battle |
| `GET /api/emotes` | Emote catalog | Use an emote ID from this catalog when sending an emote |
| `GET /api/tribes` | Tribe catalog | Tribes are not the same concept as classes / factions |
| `GET /api/localization/enums/{enumName}` | Chinese enum display names | Returns an identifier-to-Chinese-name dictionary; common names include `CardClass`, `CardType`, `CardKeyword`, `AbilityKeyword`, and `PlayMode` |

A card entry in deck details has this form:

```json
{
  "cardDefinitionId": 1401302,
  "count": 3
}
```

Here, `count` is the number of copies of that definition in the deck, not the size of the card catalog. Battle actions do not use `cardDefinitionId`; they use the instance GUID from the game state.

JavaScript example for loading the card catalog:

```javascript
const response = await fetch('/api/cards');
if (!response.ok) {
  throw new Error(`Failed to load the card catalog: ${response.status} ${await response.text()}`);
}
const cards = await response.json();
console.log('Card definitions returned by the current API:', cards.length);
```

The login response **does not contain an account JWT / accessToken / refreshToken**. A room's `hostToken` / `guestToken` is a connection token for a battle seat, not an account login token. Likewise, `userId`, `guestId`, and the seat's `playerId` must not automatically be treated as the same value.

### 2.3 From Room Creation to Battle

1. Log in or use a guest identity, then load that identity's decks.
2. The host creates a room and saves `roomId`, `hostPlayerId`, `hostToken`, and `joinCode`.
3. A human opponent joins using the invitation code and saves `guestPlayerId` and `guestToken`; see Section 8 for AI-mode conditions.
4. Players select their own decks through `/deck`. The room's `requiredEnvironment` / `requiredRule` constrain the opponent's deck selection.
5. Both human players mark ready; the host calls `/start`. Relevant room states are `Waiting`, `Ready`, and `Started`; a closed room is `Closed`. These are strings, so do not assume that all HTTP values are automatically lowercase.
6. The start response's `activeGameId` identifies this individual game. Establish the SignalR connection using `roomId`, your own seat ID, and your seat token.
7. Receive the initial state, complete the mulligan, and render the UI according to the backend's current-player and action-availability updates.

Responses for readiness, deck selection, and starting may omit tokens. **Do not overwrite a previously saved seat token with a response's `null`.** When the same room starts another game, update `activeGameId` and clear the previous game's UI state.

Common room API errors: HTTP 400 for parameter, invitation-code, or deck issues; 403 for a nonparticipant or unauthorized operation; 404 for a missing resource; and 409 for a full, started, closed, or expired room, or unmet start conditions. An error response can be a string. Read the actual response body instead of always assuming `{message: ...}`.

Source references: [Room controller](../Server/Controllers/MatchController.cs), [Room states and start conditions](../Server/Services/MatchService.cs), [Deck protocol](../Server/Dtos/DeckDtos.cs), [Account protocol](../Server/Controllers/AuthController.cs).

### 2.4 SignalR Connection Example

The following `roomId`, `playerId`, and `token` come from the room workflow above. `renderGameState` is the integrating frontend's own rendering function, and `showGameError` is its own error-display function.

```javascript
import * as signalR from '@microsoft/signalr';

const query = new URLSearchParams({ roomId, playerId, token });
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`/gamehub?${query.toString()}`)
  .withAutomaticReconnect()
  .build();

// Register before start; the server sends initial state when you connect.
connection.on('ReceiveGameState', state => renderGameState(state));
connection.on('TurnStarted', event => renderGameState(event.GameState));
connection.on('ReceiveError', message => showGameError(message));

// A complete UI must also register the incremental events it uses from Section 4.
connection.onreconnected(async () => {
  await connection.invoke('RequestInitialGameState');
});

await connection.start();
```

- Prefer `roomId` for new integrations. The backend still accepts the compatibility `gameId` parameter, which the existing legacy frontend connection code also uses.
- The server sends initial state automatically. Call `RequestInitialGameState()` to resynchronize, or `RequestPlayabilityUpdate()` to refresh action availability only.
- The existing player frontend calls `Heartbeat()` every 30 seconds to refresh room expiration. This is an application-level heartbeat, not SignalR's own connection detection. Stop the timer and connection when leaving the screen.
- Spectate through `/gamehub?roomId=...&spectate=1`; the room must already have started. Also pass `joinCode` when `RequireJoinCodeForReplayReads=true`. Optional fields are `spectatorName`, `spectatorUserId`, and `spectatorAvatar`.
- A spectator UI should not expose battle-action buttons. Chat uses the corresponding spectator identity context.

**A completed `invoke()` does not mean battle resolution succeeded.** Most battle Hub methods enqueue an action. Confirm its final state changes or failure through events / `ReceiveError`. Do not deduct PP, calculate damage, or remove cards locally merely because `invoke()` completed successfully.

Source references: [GameHub](../Server/Hubs/GameHub.cs), [Existing connection and event registration](../src/main.js), [Existing action calls](../src/gameManager.js).

### 2.5 Property and Enum Casing

| Data channel | Example | Reason |
|---|---|---|
| Ordinary HTTP properties | `roomId`, `playerId`, `cardDefinitionId` | HTTP uses camelCase |
| Actual HTTP enum properties | Card `type: "follower"`; deck `class: "forestcraft"` | The HTTP enum converter uses camelCase |
| HTTP fields already converted to strings | Card definition `class: "Forestcraft"`, `rarity: "Silver"`; room `status: "Started"` | These are already strings, so enum casing rules no longer apply |
| Ordinary SignalR properties | `GameId`, `CurrentPlayerId`, `InstanceId`, `CurrentPP` | SignalR retains C# property names |
| SignalR enums | `Phase: "Main"`, `Type: "Follower"`, `Zone: "Hand"` | The SignalR enum converter does not specify camelCase |
| Explicit property names | Instance `id`, `counters`, `counterDisplayNames`, `counterSummaryEntries` | `[JsonPropertyName]` overrides the naming policy, including for SignalR |

Therefore, **do not mechanically lowercase the first character of every response property, and do not treat a static card definition as a battle instance**. Dictionary keys, such as instance GUIDs in availability dictionaries and `Forestcraft` in localization dictionaries, are actual data keys too.

## 3. Game-State Structure

### 3.1 Full State: `GameStateDto`

The receiving method is `ReceiveGameState(state)`. The `event.GameState` inside `TurnStarted(event)` uses the same structure.

| Property | JSON type | Purpose |
|---|---|---|
| `GameId` | string | Individual game ID, not room ID |
| `CurrentPlayerId` | GUID string | Player whose turn it is |
| `FirstPlayerId` | GUID string | Player who goes first |
| `Phase` | string | `Initialization`, `Mulligan`, `TurnStart`, `Main`, `Combat`, `TurnEnd`, `GameOver`; this is the complete declared phase set, not a guarantee that every game passes through every phase |
| `GameStatus` | string | `Starting`, `InProgress`, `Finished`, `Aborted` |
| `Players` | PlayerState array | The two players; identify yourself and your opponent by `PlayerId`, not array position |
| `GameMessage` | string / null | State-related message |
| `TurnTimeoutSeconds` | number | Turn time limit used by this game |
| `TurnStartedAtUtc`, `TurnEndsAtUtc` | UTC date string / null | Turn start and end times for the countdown display |
| `IsGameOver` | boolean | Whether the game has ended |
| `WinnerId` | string / null | Winning player's ID |
| `Player1`, `Player2` | PlayerState / null | Read-only convenience properties on the DTO may also serialize; do not treat them as a separate source of state |

**There is no global `TurnNumber`.** Each player has their own `TurnNumber`; read the current turn count from the player identified by `CurrentPlayerId`.

### 3.2 Player State: `PlayerStateDto`

| Category | Properties | Description |
|---|---|---|
| Identity | `PlayerId`, `IsAI`, `IsFirst`, `Class`, `DisplayName`, `AvatarUrl` | Player identity, turn order, class, and display information |
| PP | `CurrentPP`, `MaxPP` | Current and maximum PP |
| PP+1 | `PpBoostUsesAvailable`, `IsPpBoostArmed` | Remaining uses and whether the boost is armed |
| Evolution | `CurrentEP`, `MaxEP`, `IsEvolutionUnlocked`, `CanEvolve` | Current points, limit, unlock status, and this turn's availability |
| Super-evolution | `CurrentSEP`, `MaxSEP`, `IsSuperEvolutionUnlocked`, `CanSuperEvolve` | Corresponding super-evolution state |
| Leader | `LeaderCard` | A card instance; read health from `LeaderCard.CurrentHealth` and `LeaderCard.MaxHealth` |
| Deck | `DeckCount`, `Deck` | Deck size and instance array; use `DeckCount` for the displayed count |
| Zones | `Hand`, `Field`, `Graveyard`, `Extra`, `Emblem` | Hand, battlefield, graveyard, extra zone, and emblem zone; all contain instance arrays |
| Counters | `GraveyardCount`, `BloodPool`, `PlayedCardCountThisTurn` | Graveyard count, blood pool, and cards played this turn |
| Custom counters | `counters`, `counterDisplayNames`, `counterSummaryEntries` | Value dictionary, display-name dictionary, and ordered summaries; summary entries are `{key, label, value}` |
| Turns and mulligan | `TurnNumber`, `HasCompletedMulligan`, `HasTakenFirstTurn` | Per-player turn count and mulligan completion; the last property is retained for compatibility |

The leader is not included in `Field` and should be rendered separately. `Emblem` is also separate from ordinary battlefield slots.

**Information-display boundary:** The current initial-state path maps the complete game state directly. The DTO and mapping include `Hand` and `Deck`, without filtering hands / decks by opponent perspective in that path. Do not assume the response contains only counts because the opponent's cards would normally be hidden. The UI should distinguish public zones, your hand, opponent card backs, and spectator perspectives according to the game mode. This guide does not describe server-side hidden-information filtering as an already implemented feature.

### 3.3 Battle Card Instances: `CardInstanceDto`

| Category | Properties | Description |
|---|---|---|
| Two IDs | `InstanceId`, `id` | `InstanceId` is the instance GUID for this game; `id` is the integer static-definition ID. Two copies of the same card still have different instance GUIDs |
| Basic information | `Name`, `CardNames`, `Type`, `Rarity`, `Class`, `Tribe`, `Description` | `CardNames` lists names the instance can be treated as; `Tribe` is a string array |
| Cost | `Cost`, `CurrentCost` | Original and current cost; play-mode costs also require availability updates |
| Stats | `Attack`, `Health`, `CurrentAttack`, `CurrentHealth`, `MaxAttack`, `MaxHealth` | Base and current / maximum stats; `MaxAttack` can be null |
| Position and ownership | `Zone`, `ZoneIndex`, `OwnerPlayerId`, `ZoneStayTurn` | Zone, zone position, owner, and turns spent in the current zone; `ZoneIndex` can be null |
| Attack | `CanAttack`, `AttacksThisTurn`, `MaxAttacksPerTurn` | Attack availability, uses, and limit |
| Evolution | `CanEvolve`, `IsEvolved`, `IsSuperEvolved`, `EvolvedState` | Intrinsic evolution eligibility and current state; `EvolvedState` can be null |
| Activated abilities | `ActAbilities`, `ActsThisTurn`, `MaxActsPerTurn` | Ability summaries, uses, and limit |
| Fusion | `FusionAbilities`, `FusedCards`, `FusionsThisTurn`, `MaxFusionsPerTurn` | Ability summaries, fused material instances, uses, and limit |
| Keywords and descriptions | `Keywords`, `EffectDescriptions` | Instance keyword strings and effect display text; the backend executes the actual rules |
| Amulets and Earth Sigils | `CurrentCountdown`, `Earthsigil` | Countdown can be null; note the exact spelling `Earthsigil` |
| Counters and buffs | `counters`, `counterDisplayNames`, `counterSummaryEntries`, `BuffSummaries`, `BuffStatSummary` | Counter and buff summaries for detail views |
| Art | `ArtPreview`, `ArtDetail` | Preview and detail image paths; can be null |

Each `ActAbilities` / `FusionAbilities` entry is `{RuntimeId, Name, Description}`. When sending an activation / fusion action, obtain `AbilityRuntimeId` from these entries or the server's availability data; do not replace it with an integer database ability ID.

Zone enum values: `Field`, `Hand`, `Deck`, `Graveyard`, `Extra`, `Emblem`, `Playing`, `Removed`, `Staging`, `None`. The last four include temporary / nonpersistent resolution zones; a permanent UI panel is not required for each one.

Source references: [Game-state DTO](../Server/Dtos/GameStateDto.cs), [Player DTO](../Server/Dtos/PlayerStateDto.cs), [Instance DTO](../Server/Dtos/CardInstanceDto.cs), [Player mapping](../Server/Mappings/PlayerStateProfile.cs).

## 4. Event Categories

### 4.1 Do Not Mix Three Different Naming Layers

1. **Frontend subscription name**, such as `connection.on('CardPlayed', handler)`.
2. **Event DTO `Type`**, an identifier inside the payload that does not have to match the subscription name or domain enum.
3. **Backend domain `EventType`**, used for ability triggers, resolution, and recording, such as `PlayCard`, `Damage`, and `Destroy`. Not every domain event has an identically named SignalR subscription.

Use the **subscription names** below for UI integration. Most game events inherit `GameEventDto` and carry `Type`, `SourcePlayerId`, `TimestampUtc`, and a nullable `Announcement`. Do not force full-state, chat, or availability objects into that structure. `SourcePlayerId` is not necessarily the damaged target or the owner of an updated card; read the event's specific target properties.

### 4.2 Subscription Names in the Current Client Interface

| Category | Subscription names | Frontend handling |
|---|---|---|
| Full synchronization | `ReceiveGameState` | Synchronize the UI using the state snapshot |
| Turns | `TurnStarted`, `TurnEnded`, `PlayerMulliganCompleted` | `TurnStarted.GameState` is a new snapshot; update mulligan and active-player displays |
| Connection | `PlayerConnectionStatusUpdated` | `PlayerId`, `IsConnected`, `Reason` |
| PP | `PpUpdate`, `PpMaxUpdate`, `PpBoostStateUpdated` | PP event properties are `CurrentPp` / `MaxPp`, unlike `CurrentPP` / `MaxPP` in player state; PP+1 updates include `IsArmed` |
| Evolution resources | `EpUpdate`, `EpMaxUpdate`, `SepUpdate`, `SepMaxUpdate` | Read `CurrentEp` / `MaxEp` and `CurrentSep` / `MaxSep` respectively |
| Action state | `AttackStateUpdated`, `EvolveStateUpdated`, `SuperEvolveStateUpdated`, `ActStateUpdated`, `FusionStateUpdated` | Update instance or player buttons; attack, activation, and fusion updates use `TargetCardInstanceId` |
| Hand and deck | `HandUpdated`, `DeckUpdate` | `UpdatedHand` is the hand array; `DeckCount` is the latest deck count |
| Graveyard and emblems | `GraveyardUpdate`, `EmblemUpdated` | Update graveyard counts / entries and `AddedEmblems` |
| Instance synchronization | `CardInstanceUpdated`, `CardMoved`, `CounterChanged` | Batch `UpdatedCards`; movement `CardUid`, `FromZone`, `ToZone`; old and new counter values |
| Playing and summoning | `CardPlayed`, `CardSummoned` | Instances are in `PlayedCardInstance` and `SummonedCard` respectively; do not interchange these property names |
| Evolution result | `UnitEvolved` | Update the instance using `EvolvedUnitState`; its `IsSuperEvolved` distinguishes a super-evolution result |
| Leaving a zone | `UnitDestroyed`, `CardVanished`, `CardReturned`, `Tuck` | Destruction, banishment, return to hand, and return to deck; synchronize using target IDs and zones without removing the same card twice |
| Attack and damage | `AttackDeclared`, `DamageDealt`, `HealApplied`, `HealthChanged`, `StatChange` | Separate animations from numeric synchronization; damage amount is not the latest health value |
| Keywords | `KeywordAdded`, `KeywordRemoved` | Read `CardInstanceId` and `Keyword` |
| Ability display | `AbilityTriggered`, `Fusion`, `CardRevealed` | Ability notifications, fusion animation, and revealed cards; `Fusion` is a result event, while the action method is `RequestFusion` |
| Hand availability | `ReceiveHandPlayabilityUpdate` | `PlayerId` plus a `PlayabilityResults` dictionary |
| Activation availability | `ReceiveActPlayabilityUpdate` | Battlefield activation buttons and per-ability costs / targets |
| Declared single-result availability interface | `ReceivePlayabilityResult` | This name remains in the interface; use the batch updates above for the main integration rather than assuming every card play produces a single result |
| Waiting for player choice | `PlayerChoiceRequested` | Read `ChoiceRequest`; display candidates and selection-count limits |
| Game end | `GameOver` | `WinnerId`, `LoserId`, `Reason`; stop showing battle actions |
| Errors | `ReceiveError` | The argument is an error string directly, not an event DTO |
| Battle history | `MatchHistorySync`, `LogEvent` | Used / destroyed card history and domain-event log display |
| Chat | `ChatHistory`, `ChatMessage` | History and new messages; `MessageType` distinguishes text, emotes, and system messages |
| Development diagnostics | `ConditionTraceFailed`, `ConditionTraceSucceeded` | Ability-condition tracing in the developer console, not a source of battle stats |
| Generic events | `ReceiveGameEvent` | Events outside the dedicated mappings; identify them by `Type` and their actual properties |

This table lists the complete method names in the client interface. **A declared interface method does not mean every action produces that event.** Card abilities, phases, recipients, and mappings determine what is actually sent.

Although DTO files such as `BuffAppliedEventDto`, `BuffRemovedEventDto`, `SilenceAppliedEventDto`, `SilenceRemovedEventDto`, `CardDrawnEventDto`, and `CardDiscardedEventDto` exist, the current mapping does not declare a separate, identically named client method for each. Do not register subscriptions based on file names alone. DTOs without dedicated mappings use `ReceiveGameEvent`; hand, stat, and buff displays must also follow the relevant synchronization events and instance summaries.

### 4.3 Avoid Applying the Same Change Twice

- `DamageDealt` is suitable for damage-number displays; `HealthChanged.NewHealth` is the latest health value. Do not subtract `DamageAmount` again.
- `CardInstanceUpdated.UpdatedCards` contains instance snapshots. Update by `InstanceId`, not by replacing every card with the same static `id`.
- `HandUpdated.UpdatedHand` is a full hand update, not an instruction to append to the old array.
- `CardMoved`, play, summon, and leave-zone events may describe the same resolution chain. Keep each instance unique within the zone state; do not insert duplicates.
- After a connection interruption or inconsistent state, call `RequestInitialGameState()` to recover. Once the full snapshot arrives, do not reapply a locally estimated result for the same action.

Source references: [Client interface](../Server/Hubs/ClientInterfaces/IGameClient.cs), [DTO-to-subscription mapping](../Server/Services/SignalRClientMethodMapper.cs), [Event DTO directory](../Server/Dtos/SignalR/Events/), [Complete domain-event enum](../Server/Domain/Events/EventType.cs).

## 5. Available Player Actions

### 5.1 Battle Methods and Exact Arguments

The following use `connection.invoke(methodName, argument)`. Action objects use PascalCase, matching the current frontend. GUIDs are strings in JavaScript.

| Player action | Hub method | Argument |
|---|---|---|
| Opening mulligan | `SubmitMulligan` | Pass an array of instance GUID strings to replace directly; submit `[]` even when keeping every card |
| Play a card | `PlayCard` | `{CardUid, TargetUids?, PlayMode?, AbilityRuntimeId?, PaidCost?}` |
| Attack with a follower | `PerformAttack` | `{AttackerUid, TargetUid}` |
| Evolve | `PerformEvolve` | `{UnitUid}` |
| Super-evolve | `PerformSuperEvolve` | `{UnitUid}` |
| Second-player PP+1 | `PerformPpBoost` | No argument |
| Activate an ability | `RequestAct` | `{CardUid, AbilityRuntimeId?}` |
| Fuse | `RequestFusion` | `{CardUid, AbilityRuntimeId?}`; materials are selected through a subsequent choice event |
| Submit an effect choice | `SubmitTargetChoice` | `{PendingChoiceId, SourceAbilityRuntimeId, TargetKey, ChosenTargetIds, WasCancelled}` |
| End the turn | `EndTurn` | Pass a request-ID string directly, not a DTO object |
| Surrender | `Surrender` | Pass a request-ID string directly |
| Text chat | `SendChatMessage` | `{ClientMessageId?, Content, MessageType: "text"}` |
| Emote | `SendChatMessage` | `{ClientMessageId?, MessageType: "emote", EmoteId}`; obtain the ID from the emote catalog |
| Resynchronize state | `RequestInitialGameState` | No argument |
| Refresh availability | `RequestPlayabilityUpdate` | No argument |
| Room heartbeat | `Heartbeat` | No argument |

A development entry point, `ExecuteDebugCommand({Command, Args})`, also exists but is not an ordinary player battle action. `CatchUp(fromSeq, limit)` and `Seek(targetSeq)` catch up / seek through recorded events. The default configuration is `Replay.EnableJournalWrites=false`, so their presence does not guarantee a complete replay by default.

Typical calls, with variables obtained from the current server state or a pending choice:

```javascript
await connection.invoke('PlayCard', {
  CardUid: card.InstanceId,
  TargetUids: []
});

await connection.invoke('PerformAttack', {
  AttackerUid: attacker.InstanceId,
  TargetUid: opponent.LeaderCard.InstanceId
});

await connection.invoke('PerformEvolve', { UnitUid: follower.InstanceId });
await connection.invoke('EndTurn', crypto.randomUUID());
```

When attacking a leader, pass **`opponent.LeaderCard.InstanceId`**, not the opponent's `PlayerId`. The presence of an `EndTurnActionDto` file does not change the fact that the Hub currently receives a string.

### 5.2 Play Modes and Button Availability

`PlayMode` has four values: `Base` (normal play), `Enhance`, `Accelerate`, and `Crystalize`. **The actual protocol spelling is `Crystalize`, not `Crystallize`.**

The structure of `ReceiveHandPlayabilityUpdate`:

- `PlayerId`: the player this update targets.
- `PlayabilityResults`: a dictionary keyed by hand-card instance GUID, with `PlayabilityResultDto` values.
- Result properties: `IsPlayable`, `RequiresTargetSelection`, `HasValidTargets`, `FailureReason`, `ValidTargetIds`, `CanPlayWithoutTarget`.
- Mode properties: `PlayModes`, `SelectedPlayMode`, `SelectedAbilityRuntimeId`; each mode contains `Mode`, `EffectiveCost`, `IsAvailable`, and `Reason`.

Use these values to highlight playable cards, show play-mode menus, display costs, and highlight targets. For a nonbase mode, send `PlayMode`, `PaidCost`, and the corresponding runtime ability ID for the selected mode. `PaidCost` is the cost the frontend expects to pay; the backend still validates actual payment. A check of `CurrentPP >= card.Cost` alone is insufficient.

`ReceiveActPlayabilityUpdate` is also keyed by instance GUID. Its results include `ActCost`, `IsPlayable`, target information, and an `Abilities` array. When a card has multiple activated abilities, display availability for each specific `AbilityRuntimeId` rather than assigning a single shared button state to the entire card.

Evolution buttons must consider the player's unlock state, points, and `CanEvolve`, along with instance eligibility. Super-evolution, attacks, PP+1, activation, and fusion also cannot be enabled solely from a card's appearance or type.

### 5.3 When the Server Requests a Choice

Receive `PlayerChoiceRequested(event)` and read `event.ChoiceRequest`:

| Property | Description |
|---|---|
| `PendingChoiceId` | ID of the pending choice |
| `GameId`, `RequestingPlayerId` | Game and player required to choose |
| `SourceAbilityRuntimeId`, `TargetKey` | Runtime ability ID and choice key to send back unchanged |
| `Message` | Choice prompt |
| `SelectableTargetIds` | Selectable target instance GUIDs |
| `SelectableCardInstances` | Renderable candidate instances, including deck / graveyard candidates not currently in a clickable zone |
| `MinSelectionCount`, `MaxSelectionCount` | Minimum / maximum selections |
| `IsCancellable` | Whether cancellation is allowed |

Submission example:

```javascript
await connection.invoke('SubmitTargetChoice', {
  PendingChoiceId: choice.PendingChoiceId,
  SourceAbilityRuntimeId: choice.SourceAbilityRuntimeId,
  TargetKey: choice.TargetKey,
  ChosenTargetIds: selectedIds,
  WasCancelled: false
});
```

Offer cancellation only when `IsCancellable=true`, and submit `WasCancelled: true` according to the protocol. Choices are not always just clicking one battlefield unit; the UI must support multiple selections, candidate-card panels, and cases that require no target selection.

Source references: [Action DTO directory](../Server/Dtos/SignalR/Actions/), [Playability DTO](../Server/Dtos/PlayabilityResultDto.cs), [Play-mode availability](../Server/Dtos/PlayModeAvailabilityDto.cs), [Choice request](../Server/Domain/Abilities/Targets/PlayerChoiceRequest.cs).

## 6. Card Data Format and Counts

### 6.1 Static Definition Format

`GET /api/cards` returns `CardDefinitionDto[]`. Below is a **main-property example** for the local card "Caravan Mammoth" (`商队猛犸象`), presented using the source code's serialization rules. It is not an HTTP response captured during this work; see the table for the complete property list. Actual Chinese data values are preserved in the JSON rather than translated into invented API values.

```json
{
  "id": 1401302,
  "name": "商队猛犸象",
  "type": "follower",
  "rarity": "Silver",
  "card_set": "星云基础",
  "card_set_definition_id": 2,
  "class": "Neutral",
  "cost": 7,
  "attack": 10,
  "health": 10,
  "countdown": null,
  "description": "",
  "artPreview": "/assets/cards/1401302/preview.webp",
  "artDetail": "/assets/cards/1401302/detail.webp",
  "isCollectible": true,
  "keywords": [],
  "tribe": []
}
```

| Property | Type / purpose |
|---|---|
| `id` | number; card definition ID |
| `name` | string; card name |
| `type` | HTTP enum string: `leader`, `follower`, `spell`, `amulet`, `crest`, `faith` |
| `rarity` | string; usually from `CardRarity.ToString()`: `Special`, `Bronze`, `Silver`, `Gold`, `Legend` |
| `class` | string; class identifier, see Section 7 |
| `cost` | number; base cost |
| `attack`, `health`, `countdown` | number / null; not all card types use these properties |
| `description` | string / null; display text, not executable effect code |
| `card_set` | string; card-set / series name |
| `card_set_definition_id` | number / null; card-set definition ID |
| `card_set_code`, `card_set_abbr` | string / null; card-set code and abbreviation |
| `card_set_environment` | Enum string array / null; available formats such as `rotation`, `unlimited`, `open`, `compile` |
| `artPreview`, `artDetail` | string / null; preview and detail image paths; do not substitute the old seed's `art` property for the API protocol |
| `updatedAt`, `updatedBy` | Date string / null and string / null; editing metadata |
| `isCollectible` | boolean; marks a collectible definition, not ownership by the current account or eligibility in every format |
| `keywords` | string array; card-state keywords |
| `tribe` | string array / null; tribes |
| `effects` | Simple-effect array / null, with entries `{trigger, action, value, target}`; this retained simplified format is not the complete ability DSL |

Actual abilities are loaded separately from `/api/cards/{cardId}/abilities`. Common properties include `id`, `name`, `description`, `abilityKeywords`, `abilityType`, `priority`, `isActive`, `cardId`, and `jsonConfig`. Keyword entries contain `keyword` / `value` information. `jsonConfig` describes ability triggers, conditions, targets, effects, and related configuration. Valid normal data is an already parsed JSON object; do not always call `JSON.parse()` again. The card-ability endpoint may return the original string when configuration parsing fails, so handle the actual value type.

For a game UI, the frontend primarily displays card definitions, instances, and ability text. **Do not execute DSL in the browser to determine battle results.** For card / ability editor integration, also consult [Card Creation and DSL](05-卡牌创作与DSL.md).

### 6.2 Local Card-Catalog Statistics

Scope: read-only queries against `CardDefinitions` in `Server/Data/shadowverse.db`, dated 2026-10-12. The server was not started and no production API was called.

| Scope | Count |
|---|---:|
| All card definitions in the database | **2,811** |
| `IsCollectible=true` | **2,234** |
| `IsCollectible=false` | **577** |
| Entries in repository seed `data/card_data.json` | **77** |
| Collectible / noncollectible seed entries | **58 / 19** |

By type:

| Type | Count |
|---|---:|
| Follower | 1,674 |
| Spell | 650 |
| Amulet | 241 |
| Crest | 222 |
| Faith | 23 |
| Leader | 1 |
| **Total** | **2,811** |

The `GET /api/cards` repository reads all definitions in the current database, not just collectible definitions. Seeds mainly initialize an empty database; changing the seed file does not overwrite an existing database. Card editing can change the catalog, so use the API array length for the actual count in a new environment. **Do not hard-code 2,811 as a frontend limit.**

A standard deck defaults to 40 cards and at most three copies of the same card, subject to format, mode, and copy-limit policies. These are deck rules, not catalog counts.

Source references: [Definition DTO](../Server/Dtos/CardDefinitionDto.cs), [Definition mapping](../Server/Mappings/CardDefinitionProfile.cs), [Card-catalog API](../Server/Controllers/CardsController.cs), [Card repository](../Server/Infrastructure/Persistence/EFCardRepository.cs), [Seed loading](../Server/Infrastructure/Data/DataSeeder.cs).

## 7. Classes and Keyword Lists

### 7.1 Classes / Factions: `CardClass`

| Identifier | Display meaning | Enum value | Local card definitions |
|---|---|---:|---:|
| `Neutral` | Neutral | 0 | 312 |
| `Forestcraft` | Elves / Forestcraft | 1 | 369 |
| `Swordcraft` | Royal Guard / Swordcraft | 2 | 354 |
| `Runecraft` | Mages / Runecraft | 3 | 374 |
| `Dragoncraft` | Dragons / Dragoncraft | 4 | 340 |
| `Abysscraft` | Nightmare / Abysscraft | 5 | 354 |
| `Havencraft` | Bishop / Havencraft | 6 | 314 |
| `Portalcraft` | Avenger / Portalcraft | 7 | 394 |
| **Total** | **Seven classes plus Neutral** | | **2,811** |

This project uses `Abysscraft` for the Nightmare class; do not independently split it into the older game's Shadowcraft / Bloodcraft categories. An HTTP card definition's `class` uses the strings above. HTTP deck enum values typically use camelCase, while SignalR enum values use the PascalCase identifiers above.

### 7.2 Card-State Keywords: `CardKeyword`

| Identifier | English meaning | Value |
|---|---|---:|
| `None` | None | 0 |
| `Storm` | Storm | 1 |
| `Ward` | Ward | 2 |
| `Bane` | Bane | 3 |
| `Rush` | Rush | 4 |
| `Ambush` | Ambush | 5 |
| `Drain` | Drain | 6 |
| `Countdown` | Countdown | 7 |
| `Aura` | Aura | 8 |
| `Pressure` | Intimidation / Pressure | 9 |
| `Shackle` | Cannot attack | 10 |
| `Shield` | Barrier / Shield | 11 |
| `CannotAttackLeader` | Cannot attack the leader | 12 |
| `CannotAttackFollower` | Cannot attack followers | 13 |
| `SpellWard` | Spell Ward | 14 |
| `CannotUse` | Cannot be used | 15 |
| `IgnoreWard` | Ignores Ward | 16 |
| `DestroyResist` | Cannot be destroyed | 20 |
| `VanishResist` | Cannot be banished | 21 |
| `FreeEvolve` | Free evolution | 22 |
| `BanEvolve` | Cannot evolve | 23 |
| `EarthSigil` | Earth Sigil | 24 |
| `EffectImmune` | Immune to other cards' abilities | 25 |

There are 22 identifiers excluding `None`. Values are not contiguous; do not infer enum values from array indices.

### 7.3 Ability Keywords: `AbilityKeyword`

| Identifier | English meaning | Value |
|---|---|---:|
| `None` | None | 0 |
| `Fanfare` | Fanfare | 101 |
| `Lastword` | Last Words | 102 |
| `AttackEffect` | On attack | 103 |
| `FightEffect` | On clash | 104 |
| `EvolveEffect` | On evolution | 105 |
| `Combo` | Combo | 106 |
| `Rally` | Rally | 107 |
| `SpellBoost` | On Spellboost | 108 |
| `Awake` | Awakening | 109 |
| `Necromance` | Necromancy | 110 |
| `Venge` | Vengeance | 111 |
| `Reson` | Resonance | 112 |
| `Eager` | Avarice | 113 |
| `EarthRitual` | Earth Rite | 114 |
| `Enhance` | Enhance | 115 |
| `Bury` | Burial Rite | 116 |
| `Reanimate` | Reanimate | 117 |
| `Accelerate` | Accelerate | 118 |
| `Crystalize` | Crystallize | 119 |
| `Discover` | Discover | 120 |
| `Intromezzo` | Interlude | 121 |
| `Wrath` | Wrath | 122 |
| `Choose` | Choose a mode | 123 |
| `Act` | Activate | 124 |
| `Sacrifice` | Blood Sacrifice | 125 |
| `SuperEvolveEffect` | On super-evolution | 126 |
| `Fusion` | Fusion | 127 |
| `Invoke` | Invocation | 128 |

There are 28 identifiers excluding `None`. Spellings such as `Lastword`, `Necromance`, and `Intromezzo` must match this project; do not substitute spellings from other game references. Ability keywords belong to ability configuration and are not the same as the card-state keywords in an instance's `Keywords`.

These lists are the enums declared in the source. **They do not mean every mechanic has card coverage or that all combinations were runtime-verified during this work.** The existing localization API supplies Chinese display names; the English meanings here explain the guide and do not imply that the API has been translated or that an English localization system already exists.

### 7.4 Tribes Are Not Classes

`tribe` / `Tribe` is a maintainable string array. `/api/tribes` supplies the catalog, with entries containing `name`, `aliases`, `behavior`, `isReserved`, `isEnabled`, and `usageCount`. Use that catalog rather than the class enum.

The local database currently has 41 enabled names. Their exact stored values are preserved below, with English glosses for reading this guide rather than replacement API identifiers:

Music (`乐曲`), Departed (`亡者`), Puppet (`人偶`), Light Trace (`光迹`), All (`全部`), Artifact (`创造物`), Composite (`合成物`), Earth Sigil (`土之印`), Sacred Beast (`圣兽`), Soldier (`士兵`), Spirit Fox (`妖狐`), Fairy (`妖精`), Retainer (`家臣`), Colossus (`巨像`), Sovereign (`帝王`), Phantasmal Melody (`幻奏`), Shikigami (`式神`), Godslayer (`弑灭者`), Machina (`机械`), Case (`案件`), Meteor Flame (`流星炎`), Ocean (`海洋`), Cinderella (`灰姑娘`), Realm Hunter (`猎境者`), Mejiro Family (`目白家`), Red Eyes (`真红眼`), Spirit (`精魂`), Loot (`财宝`), Umamusume (`赛马娘`), Vehicle (`载具`), Lost Child (`迷子`), Carefree (`逍遥`), Levin (`雷维翁`), Musical Puppet (`颂乐人偶`), Landscape (`风景`), Spice (`香料`), Mysteria (`马纳历亚`), Poltergeist (`骚灵`), Witch Candidate (`魔女候补`), Demon Realm (`魔界`), and Luminous (`鲁米那斯`).

The stored value `全部` ("All") has reserved semantics; it is not another class. The tribe catalog is editable, so its count is not a protocol constant.

Source references: [Class, type, zone, and mode enums](../Server/Dtos/Enums/GameEnum.cs), [Keyword enums](../Server/Domain/Constants/Keywords.cs), [Localization API](../Server/Controllers/LocalizationController.cs), [Tribe API](../Server/Controllers/TribesController.cs).

## 8. AI Opponents

**Yes: a rules-based backend AI is already implemented.** `PlayerStateDto.IsAI` identifies an AI player. It does not rely on the frontend to generate actions and is not a conversational AI calling a large language model API.

### 8.1 Creating an AI Battle

1. Set `useAiFallback: true` when creating a room with `POST /api/match`.
2. The host selects their own `deckId` through `/api/match/{roomId}/deck`, and also supplies `aiDeckId` or `aiDeckCode` to select the AI's deck.
3. If the AI deck belongs to another identity, pass `aiUserId` or `aiGuestId`. If no AI owner is provided, the current backend looks up the deck using the host's deck owner.
4. The host marks ready and starts the game, then connects to `/gamehub` with their own seat information.

The current AI-seat condition is **`useAiFallback=true` and no human guest seat**. When a human guest is present, the human-player readiness conditions apply. This does not promise automatic AI replacement for a disconnected human. The AI deck must already be selected and match the player's format / rule; setting `useAiFallback` alone does not mean the backend generates an arbitrary deck.

### 8.2 Current AI Capabilities

- Mulligan: selects high-cost cards according to its rules; the current strategy replaces cards costing more than 3.
- Turn execution: checks PP+1, then attempts to play a card, evolve, and attack one action at a time; ends the turn when no more actions remain.
- Strategy consists of simplified coded rules and priorities, not complex search or model inference.
- The frontend receives the same states and events as in a human battle; no separate AI event protocol is required.

**Capability boundary:** The current executor has no separate decision steps for super-evolution, activated abilities, or fusion. Do not interpret the existence of player endpoints as proof that the AI uses every player action. The implementation was reviewed, but no complete battle was run to guarantee correct AI behavior for arbitrary decks and complex ability combinations.

Source references: [AI decisions](../Server/Services/AiService.cs), [AI turn execution](../Server/Services/AiTurnExecutor.cs), [AI room conditions](../Server/Services/MatchService.cs).

## 9. Final Integration Checklist

- Keep static card definition IDs, battle instance GUIDs, integer database ability IDs, and runtime ability GUIDs separate.
- Complete the room's HTTP workflow before entering battle. Use your seat token rather than deriving it from the login response.
- Register events before connecting; support initial snapshots, incremental events, and reconnection synchronization.
- Drive the UI from server availability and resolution events. Do not independently pay costs, calculate damage, or execute card DSL.
- Support selection-count limits, cancellation permissions, and candidate cards outside the currently displayed battlefield.
- State the environment behind every count. Read the catalog again through the API after deployment; do not treat the seed count as the full catalog.

### Verification Scope

The Chinese source guide reviewed controllers, the Hub, DTOs, event mappings, existing frontend calls, enums, the AI executor, and configuration. Card and tribe counts came from read-only SQLite queries, and seed counts from JSON parsing. After writing that guide, UTF-8 readability, example JSON, source links, method / enum lists, and count totals were checked. This English edition translates the same implementation snapshot; it does not introduce a new deployment or database audit.

The backend was not started, no browser battle was run, the database was not modified, and no production API or complete set of keyword mechanics was claimed to have passed live battle verification. This work adds documentation only and does not change business logic.
