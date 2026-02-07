using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public sealed class ScenarioPendingActionService : IPendingActionService
    {
        private readonly Dictionary<Guid, PendingChoiceContext> _pendingChoices = new();
        private readonly Dictionary<(string GameId, Guid PlayerId), PendingChoiceContext> _pendingByPlayer = new();
        private ScenarioContext? _context;

        public void BindContext(ScenarioContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<PlayerChoiceResult>();
            var pending = new PendingChoiceContext(request, tcs, cancellationToken);
            _pendingChoices[request.PendingChoiceId] = pending;
            _pendingByPlayer[(gameId, request.RequestingPlayerId)] = pending;

            var resolved = ResolveChoice(request);
            tcs.SetResult(resolved);

            _pendingChoices.Remove(request.PendingChoiceId);
            _pendingByPlayer.Remove((gameId, request.RequestingPlayerId));

            return Task.FromResult(resolved);
        }

        public void CompletePlayerChoice(Guid choiceId, SubmitTargetChoiceActionDto dto)
        {
            if (_pendingChoices.TryGetValue(choiceId, out var pending))
            {
                pending.Tcs.TrySetResult(new PlayerChoiceResult
                {
                    WasCancelled = false,
                    ChosenTargetIds = dto.ChosenTargetIds?.ToList() ?? new List<Guid>()
                });
            }
        }

        public PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId)
        {
            return _pendingByPlayer.TryGetValue((gameId, playerId), out var pending) ? pending : null;
        }

        public void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult)
        {
            if (_pendingChoices.TryGetValue(choiceId, out var pending))
            {
                pending.Tcs.TrySetResult(forcedResult);
            }
        }

        private PlayerChoiceResult ResolveChoice(PlayerChoiceRequest request)
        {
            if (_context == null)
            {
                return SelectFirstTargets(request);
            }

            var choice = FindScenarioChoice(request);
            if (choice == null)
            {
                return SelectFirstTargets(request);
            }

            var selected = ResolveChoiceTargets(request, choice);
            if (selected.Count == 0)
            {
                return SelectFirstTargets(request);
            }

            return new PlayerChoiceResult
            {
                WasCancelled = false,
                ChosenTargetIds = selected
            };
        }

        private ScenarioChoice? FindScenarioChoice(PlayerChoiceRequest request)
        {
            var choices = _context!.Scenario.Choices;
            var keys = BuildChoiceKeys(request);
            foreach (var key in keys)
            {
                if (choices.TryGetValue(key, out var choice))
                {
                    return choice;
                }
            }

            return null;
        }

        private IEnumerable<string> BuildChoiceKeys(PlayerChoiceRequest request)
        {
            if (_context == null)
            {
                yield break;
            }

            var ability = _context.GameState.AbilityManager.GetAbilityByRuntimeId(request.SourceAbilityRuntimeId);
            if (ability != null)
            {
                yield return $"{ability.Name}.{request.TargetKey}";

                var cardKey = ability.SourceCard != null ? _context.FindCardKey(ability.SourceCard) : null;
                if (!string.IsNullOrWhiteSpace(cardKey))
                {
                    yield return $"{cardKey}.{request.TargetKey}";
                }
            }

            yield return request.TargetKey;
        }

        private List<Guid> ResolveChoiceTargets(PlayerChoiceRequest request, ScenarioChoice choice)
        {
            if (_context == null)
            {
                return new List<Guid>();
            }

            var selected = new List<Guid>();

            if (choice.CardKeys.Count > 0)
            {
                foreach (var key in choice.CardKeys)
                {
                    if (_context.Cards.TryGetValue(key, out var card))
                    {
                        selected.Add(card.InstanceId);
                    }
                    else if (Guid.TryParse(key, out var parsed))
                    {
                        selected.Add(parsed);
                    }
                }
            }
            else if (choice.Indices.Count > 0)
            {
                foreach (var index in choice.Indices)
                {
                    if (index >= 0 && index < request.SelectableTargetIds.Count)
                    {
                        selected.Add(request.SelectableTargetIds[index]);
                    }
                }
            }

            if (selected.Count == 0)
            {
                return selected;
            }

            var validTargets = new HashSet<Guid>(request.SelectableTargetIds);
            selected = selected.Where(validTargets.Contains).ToList();

            return selected;
        }

        private static PlayerChoiceResult SelectFirstTargets(PlayerChoiceRequest request)
        {
            var minCount = Math.Max(0, request.MinSelectionCount);
            var takeCount = Math.Min(minCount, request.SelectableTargetIds.Count);

            return new PlayerChoiceResult
            {
                WasCancelled = false,
                ChosenTargetIds = request.SelectableTargetIds.Take(takeCount).ToList()
            };
        }
    }
}
