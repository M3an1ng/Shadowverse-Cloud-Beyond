# Neon Tactics: Vanguard - VBA to TypeScript Migration Plan

This document tracks the migration of the original Excel/VBA game logic to the React/TypeScript web application.

**Current Phase: Logic Core Implementation**

**Status Legend:**
- ✅ **Done**: Ported and functional.
- ⚠️ **Partial**: Basic structure exists, complex logic missing.
- ❌ **Pending**: Not yet started.

---

## Module Group 0: Infrastructure & Global (基础 & 公共)
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `ThisWorkbook.cls` | `src/store.ts`, `src/constants.tsx` | ✅ | Data loading via `cardDatabase.json` implemented. |
| `·0·0·公共变量.bas` | `src/store.ts` | ✅ | Global state managed by Zustand. |
| `·0·1·基础·Down·Up·IsSelec.bas` | `src/store.ts` | ✅ | Excel cell operations replaced by Store Actions. |
| `·0·2·基础·ShowChoice.bas` | UI Component | ❌ | Need a generic `ChoiceModal` component for "Select" effects. |
| `·0·3·基础·NumInputBox.bas` | UI Component | ❌ | Need a generic `NumberInputModal`. |
| `·0·4·基础·NameInputBox.bas` | UI Component | ❌ | Need a generic `TextInputModal`. |

## Module Group 1: Translation & Parsing (翻译)
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·1·1·翻译·Translate.bas` | `src/utils/dataConverter.ts` | ✅ | ID-Name mapping handled during static generation. |
| `·1·2·翻译·IDTranslate.bas` | `src/utils/dataConverter.ts` | ✅ | Class/Trait mapping handled. |
| `·1·3·翻译·SentenceTranslate.bas` | `src/logic/dslParser.ts` | ✅ | `splitEffect` implemented to handle `{}` nesting. |

## Module Group 2: Card Entry System (录卡 - Editor)
*Low Priority for Gameplay, High Priority for Content Creation.*
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·2·1·录卡·GetCardSetID.bas` | `src/editor/*` | ❌ | |
| `·2·2·录卡·GetCardID.bas` | `src/editor/*` | ❌ | |
| `·2·3·录卡·CreateFollower.bas` | `src/editor/*` | ❌ | |
| `·2·4·录卡·DeleteEffect.bas` | `src/editor/*` | ❌ | |
| `·2·5·录卡·UpByID.bas` | `src/editor/*` | ❌ | |
| `·2·6·录卡·DownByID.bas` | `src/editor/*` | ❌ | |
| `·2·7·录卡·IDByName.bas` | `src/editor/*` | ❌ | |
| `·2·8·录卡·DeleteCard.bas` | `src/editor/*` | ❌ | |
| `EnterCard.frm` & Sub-forms | `src/editor/*` | ❌ | Editor UI pending. |

## Module Group 3: Deck Building (牌组)
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·3·1·牌组·SearchCard.bas` | `src/deckbuilder/*` | ❌ | Card library search. |
| `·3·2·牌组·AddToOrigDeck.bas` | `src/deckbuilder/*` | ❌ | |
| `·3·3·牌组·SaveDeck.bas` | `src/deckbuilder/*` | ❌ | |
| `·3·4·牌组·DeleteDeck.bas` | `src/deckbuilder/*` | ❌ | |
| `·3·5·录卡·SelectDeck.bas` | `src/deckbuilder/*` | ❌ | |

## Module Group 4: Aux Utilities (辅助)
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·4·1·辅助·GetRandom.bas` | Native JS | ✅ | Array/Random utils replaced by standard JS. |
| `·4·2·辅助·NonEmptyArray.bas` | Native JS | ✅ | |
| `·4·3·辅助·UpByMatchID.bas` | `src/store.ts` | ⚠️ | State updates by ID. |
| `·4·4·辅助·DownByMatchID.bas` | `src/store.ts` | ⚠️ | |
| `·4·5·辅助·GetModel.bas` | `src/logic/valueCalculator.ts` | ✅ | **CRITICAL**. Calculating dynamic stats (Cost/Atk/Def). |
| `·4·6·辅助·GetEffect.bas` | `src/utils/dataConverter.ts` | ✅ | Effects are parsed into array in JSON. |
| `·4·7·辅助·SplitEffect.bas` | `src/logic/dslParser.ts` | ✅ | Implemented. |
| `·4·8·辅助·JoinEffect.bas` | `src/logic/effectManager.ts` | ❌ | Merging buff strings. |

## Module Group 5: Steps & Flow (步骤)
*Handling game phases and step-by-step execution.*
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·5·1·步骤·UpSteps.bas` | `src/logic/gameLoop.ts` | ❌ | |
| `·5·2·步骤·EraseSteps.bas` | `src/logic/gameLoop.ts` | ❌ | |
| `·5·3·步骤·OutputSteps.bas` | `src/components/DebugConsole.tsx` | ⚠️ | Basic logging exists. |
| `·5·4·步骤·InputSteps.bas` | `src/logic/gameLoop.ts` | ❌ | |
| `·5·5·步骤·UpStepsArea.bas` | `src/logic/gameLoop.ts` | ❌ | |
| `·5·6·步骤·SearchSteps.bas` | `src/logic/gameLoop.ts` | ❌ | |

## Module Group 6: Logic Core (结算 - The Engine)
*High Priority. The brain of the card game.*
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·6·1·结算·GetTimePoint.bas` | `src/logic/triggerSystem.ts` | ✅ | **CRITICAL**. Evaluating "When X do Y". |
| `·6·2·结算·GetIf.bas` | `src/logic/conditionEvaluator.ts` | ✅ | **CRITICAL**. Evaluating conditions (e.g., `If|Necromancy&3`). |
| `·6·3·结算·GetAbility.bas` | `src/logic/actionEngine.ts` | ⚠️ | Basic execution exists. Needs queue system for complex chains. |
| `·6·4·结算·GetGoal.bas` | `src/logic/targetSelector.ts` | ✅ | Parsing implemented. Runtime selection logic pending. |
| `·6·5·结算·GetX.bas` | `src/logic/valueCalculator.ts` | ✅ | Dynamic variable `X` calculation (e.g., `Damage|X`). |
| `·6·6·结算·GetAction.bas` | `src/logic/observerSystem.ts` | ✅ | Event broadcasting (Observer Pattern). |

## Module Group 7: Game Loop (对局)
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·7·1·对局·StartGame.bas` | `src/store.ts` | ⚠️ | Basic initialization. |
| `·7·2·对局·TurnStart·End.bas` | `src/store.ts` | ⚠️ | Turn phases. |
| `·7·3·对局·MatchPlay.bas` | `src/logic/actionEngine.ts` | ⚠️ | Playing cards. |
| `·7·4·对局·MatchStrike.bas` | `src/logic/combatSystem.ts` | ❌ | Combat logic. |
| `·7·5·对局·MatchEvolve.bas` | `src/logic/evolutionSystem.ts` | ❌ | Evolution mechanics. |
| `·7·6·对局·MatchSuperEvolve.bas` | `src/logic/evolutionSystem.ts` | ❌ | |
| `·7·7·对局·MatchGetPP.bas` | `src/store.ts` | ✅ | PP Management. |
| `·7·8·对局·MatchFuse.bas` | `src/logic/fusionSystem.ts` | ❌ | Fusion mechanics. |

## Module Group 8: UI & Interaction (对局 - UI)
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·8·1·对局·UIRefresh.bas` | React Components | ✅ | React handles rendering automatically. |
| `·8·2·对局·EngageFunction.bas` | `src/components/*` | ⚠️ | Click handlers. |
| `·8·3·对局·DestroyFunction.bas` | `src/components/*` | ⚠️ | Cleanup. |
| `·8·4·对局·CoverHands.bas` | `src/components/Hand.tsx` | ❌ | Visual effects for hand. |
| `·8·5·对局·SwapMove.bas` | `src/components/Board.tsx` | ❌ | Drag and drop / moving units. |
| `·8·6·对局·OutputLog.bas` | `src/components/DebugConsole.tsx` | ✅ | |
| `·8·7·对局·SwapCrest.bas` | `src/components/*` | ❌ | UI for crests/tokens. |
| `ChoiceForm.frm` | `src/components/ChoiceModal.tsx` | ❌ | |
| `EffectForm.frm` | `src/components/EffectModal.tsx` | ❌ | |
| `ImageDisplayForm.frm` | `src/components/CardPreview.tsx` | ✅ | |
| `UserInput.frm` | `src/components/InputModal.tsx` | ❌ | |
| `ButtonClick.cls` | `src/components/*` | ⚠️ | Event handling. |

## Module Group 9: File & System (文件)
| VBA Module | TS Component | Status | Notes |
| :--- | :--- | :--- | :--- |
| `·9·1·文件·SaveGame.bas` | `src/utils/saveSystem.ts` | ❌ | LocalStorage implementation needed. |
| `·9·2·文件·ShowImageFromPath.bas` | `src/components/CardImage.tsx` | ✅ | |
| `·9·3·文件·ShowEffect.bas` | `src/components/CardTooltip.tsx` | ✅ | |

## Immediate Roadmap
1.  **Implement `conditionEvaluator.ts`**: To handle `If|...` logic found in card effects.
2.  **Implement `valueCalculator.ts`**: To handle `X=...` logic (e.g., "Draw X cards").
3.  **Enhance `targetSelector.ts`**: Implement the actual finding of cards in the state based on parsed criteria.
