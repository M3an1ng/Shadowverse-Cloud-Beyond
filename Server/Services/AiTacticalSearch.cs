using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Extensions;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

/// <summary>Bounded public-board search. Plans are recomputed after every committed action.</summary>
internal static class AiTacticalSearch
{
    private const int MaxDepth = 8;
    private const int BeamWidth = 8;
    private const int MaxNodes = 256;

    internal sealed record Move(string Type, object Data, CardInstanceDto Source, CardInstanceDto? Target = null);
    internal sealed record Plan(Move First, double Gain, bool ProjectsLethal, bool PreventsVisibleLethal, bool SpendsSep = false)
    {
        public int Priority => ProjectsLethal ? 2 : PreventsVisibleLethal ? 1 : 0;
    }
    internal sealed record PendingDamage(Guid SourceId, Guid[] TargetIds, int Amount, int HandCount);
    internal sealed record EndTurnForecast(IReadOnlyList<PendingDamage> Damage, IReadOnlySet<Guid> UnsafeAttackers);
    private sealed record Node(GameStateDto State, Move? First, double Value, bool PlayedCard = false, double AbilityValue = 0,
        bool InvalidatesEndTurn = false);

    public static Plan? Find(GameStateDto state, Guid actor, CancellationToken token, IReadOnlyList<Move>? stormPlays = null,
        IReadOnlyDictionary<Guid, (double Evolve, double SuperEvolve)>? evolutionValues = null)
        => FindWithEndTurn(state, actor, token, stormPlays, evolutionValues, null);

    internal static Plan? FindWithEndTurn(GameStateDto state, Guid actor, CancellationToken token,
        IReadOnlyList<Move>? stormPlays, IReadOnlyDictionary<Guid, (double Evolve, double SuperEvolve)>? evolutionValues,
        EndTurnForecast? ending)
        => FindWithIntent(state, actor, token, stormPlays, evolutionValues, ending, null);

    internal static Plan? FindWithIntent(GameStateDto state, Guid actor, CancellationToken token,
        IReadOnlyList<Move>? stormPlays, IReadOnlyDictionary<Guid, (double Evolve, double SuperEvolve)>? evolutionValues,
        EndTurnForecast? ending, string? intent)
    {
        var engines = ReadEngines(state, actor);
        var best = Search(state, actor, token, stormPlays, evolutionValues, ending, engines, intent, prioritizeLethal: true);
        var own = state.Players.First(p => p.PlayerId == actor);
        var enemy = state.Players.First(p => p.PlayerId != actor);
        if (best?.ProjectsLethal != true
            && (HasEnoughPotentialDamage(state, actor, stormPlays)
                || (HasVisibleLethalThreat(own, enemy) && best?.PreventsVisibleLethal != true)))
        {
            var alternative = Search(state, actor, token, stormPlays, evolutionValues, ending, engines, intent, prioritizeLethal: false);
            if (alternative != null && alternative.Priority > (best?.Priority ?? 0)) best = alternative;
        }
        if (best?.SpendsSep == true && own.CanEvolve && own.CurrentEP > 0)
        {
            // Compare the same board with SEP unavailable. Preserve SEP when EP achieves the same tactical goal.
            var normalState = state with { Players = state.Players.Select(player => player.PlayerId == actor
                ? player with { CanSuperEvolve = false } : player).ToList() };
            var normal = Search(normalState, actor, token, stormPlays, evolutionValues, ending, engines, intent, prioritizeLethal: true);
            if (normal != null && normal.Priority >= best.Priority && (normal.Priority > 0 || normal.Gain > 0)
                && best.Gain - normal.Gain <= 4.5 + 1e-9)
                best = normal;
        }
        return best;
    }

    internal static bool HasVisibleLethalThreat(PlayerStateDto defender, PlayerStateDto attacker) =>
        defender.LeaderCard is { CurrentHealth: > 0 } leader
        && EstimateBoardDamage(attacker.Field, defender.Field) >= leader.CurrentHealth;

    internal static bool CanProjectGeneratedStormLethal(PlayerStateDto player, PlayerStateDto opponent,
        int spellCost, int selfDamage, int count, int tokenCost, int tokenAttack)
    {
        if (count <= 0 || tokenAttack <= 0 || tokenCost < 0 || spellCost < 0 || selfDamage < 0)
            return false;
        return player.LeaderCard is { } ownLeader && ownLeader.CurrentHealth > selfDamage
            && opponent.LeaderCard is { CurrentHealth: > 0 } enemy && enemy.CurrentHealth <= (long)count * tokenAttack
            && !enemy.HasKeyword(CardKeyword.Shield)
            && player.CurrentPP >= spellCost + (long)count * tokenCost
            && player.Hand.Count - 1 + count <= GameConstants.Hand.MaxHandSize
            && player.Field.Count + count <= GameConstants.Field.MaxFieldSize
            && !opponent.Field.Any(card => card.Type == CardType.Follower && card.HasKeyword(CardKeyword.Ward));
    }

    private static Plan? Search(GameStateDto state, Guid actor, CancellationToken token, IReadOnlyList<Move>? stormPlays,
        IReadOnlyDictionary<Guid, (double Evolve, double SuperEvolve)>? evolutionValues, EndTurnForecast? ending, IReadOnlySet<Guid> engines, string? intent, bool prioritizeLethal)
    {
        var terminal = ProjectEndTurn(state, actor, ending);
        var baseline = Evaluate(terminal, actor, engines, intent);
        var originalPlayer = state.Players.First(p => p.PlayerId == actor);
        var originalReserve = ReserveValue(originalPlayer);
        var defensiveEmergency = HasVisibleLethalThreat(originalPlayer, state.Players.First(p => p.PlayerId != actor));
        var terminalOwn = terminal.Players.First(player => player.PlayerId == actor);
        var terminalEnemy = terminal.Players.First(player => player.PlayerId != actor);
        var terminalLethal = terminalEnemy.LeaderCard is { CurrentHealth: <= 0 };
        var terminalSafe = defensiveEmergency && !HasVisibleLethalThreat(terminalOwn, terminalEnemy);
        Plan? best = (terminalLethal || terminalSafe) && terminalOwn.LeaderCard is { CurrentHealth: > 0 } leader
            ? new Plan(new Move("EndTurn", new { }, leader), 0, terminalLethal, terminalSafe) : null;
        var frontier = new List<Node>();
        var visited = new HashSet<string>();
        var expanded = 0;
        for (var depth = 0; depth < MaxDepth && expanded < MaxNodes; depth++)
        {
            var next = new List<Node>();
            var parents = depth == 0 ? new[] { new Node(state, null, baseline) } : frontier.ToArray();
            foreach (var parent in parents)
            {
                foreach (var move in Moves(parent.State, actor, stormPlays))
                {
                    token.ThrowIfCancellationRequested();
                    if (expanded >= MaxNodes) break;
                    expanded++;
                    var projected = Apply(parent.State, actor, move);
                    if (!visited.Add(Key(projected) + ":" + parent.InvalidatesEndTurn)) continue;
                    var first = parent.First ?? move;
                    var playedCard = parent.PlayedCard || move.Type == "PlayCard";
                    var abilityValue = parent.AbilityValue;
                    if (evolutionValues?.TryGetValue(move.Source.InstanceId, out var estimates) == true)
                    {
                        if (move.Type == "PerformEvolve") abilityValue += estimates.Evolve;
                        if (move.Type == "PerformSuperEvolve") abilityValue += estimates.SuperEvolve;
                    }
                    var invalidatesEndTurn = parent.InvalidatesEndTurn || move.Type != "PerformAttack"
                        || ending?.UnsafeAttackers.Contains(move.Source.InstanceId) == true;
                    var evaluated = invalidatesEndTurn ? projected : ProjectEndTurn(projected, actor, ending);
                    var value = Evaluate(evaluated, actor, engines, intent) + abilityValue - .04 * depth;
                    var enemy = evaluated.Players.First(p => p.PlayerId != actor);
                    var lethal = enemy.LeaderCard is { CurrentHealth: <= 0 };
                    var own = evaluated.Players.First(p => p.PlayerId == actor);
                    var reserveSpent = originalReserve - ReserveValue(own);
                    var preventsLethal = defensiveEmergency && own.LeaderCard is { CurrentHealth: > 0 }
                        && !HasVisibleLethalThreat(own, enemy);
                    var plan = new Plan(first, value - baseline - reserveSpent, lethal, preventsLethal,
                        own.CurrentSEP < originalPlayer.CurrentSEP);
                    if ((lethal || preventsLethal || !playedCard)
                        && (best == null || plan.Priority > best.Priority
                            || (plan.Priority == best.Priority && plan.Gain > best.Gain)))
                        best = plan;
                    if (!lethal) next.Add(new Node(projected, first, value, playedCard, abilityValue, invalidatesEndTurn));
                }
            }
            if (next.Count == 0) break;
            // Keep separate slots for trades and for clearing the path to the leader.
            // A sacrifice that removes a Ward or Shield can look poor in the board evaluation.
            var boardLane = next.OrderByDescending(n => n.Value);
            if (!prioritizeLethal)
            {
                frontier = boardLane.Take(BeamWidth).ToList();
                continue;
            }
            var lethalLane = next.OrderByDescending(n => LethalPotential(n.State, actor, stormPlays))
                .ThenByDescending(n => n.Value).Take(BeamWidth / 2);
            frontier = lethalLane.Concat(boardLane)
                .Distinct().Take(BeamWidth).ToList();
        }
        return best;
    }

    // Overestimate damage so Ward, Shield and available evolution never prune an otherwise possible line.
    private static bool HasEnoughPotentialDamage(GameStateDto state, Guid actor, IReadOnlyList<Move>? stormPlays)
    {
        var me = state.Players.First(p => p.PlayerId == actor);
        var enemy = state.Players.First(p => p.PlayerId != actor);
        if (enemy.LeaderCard is not { CurrentHealth: > 0 } leader) return false;

        var damage = me.Field.Where(card => card.Type == CardType.Follower)
            .Sum(card => Math.Max(0, card.CurrentAttack) * Math.Max(0, card.MaxAttacksPerTurn - card.AttacksThisTurn));
        damage += stormPlays?.Sum(play => Math.Max(0, play.Source.CurrentAttack) * Math.Max(1, play.Source.MaxAttacksPerTurn)) ?? 0;
        var maxAttacks = Math.Max(
            me.Field.Select(card => Math.Max(0, card.MaxAttacksPerTurn - card.AttacksThisTurn)).DefaultIfEmpty().Max(),
            stormPlays?.Select(play => play.Source.MaxAttacksPerTurn).DefaultIfEmpty().Max() ?? 0);
        damage += me.Field.Where(card => card.IsSuperEvolved)
            .Sum(card => Math.Max(0, card.MaxAttacksPerTurn - card.AttacksThisTurn));
        if (me.CanSuperEvolve) damage += 4 * maxAttacks;
        else if (me.CanEvolve) damage += 2 * maxAttacks;
        return damage >= leader.CurrentHealth;
    }

    private static double LethalPotential(GameStateDto state, Guid actor, IReadOnlyList<Move>? stormPlays)
    {
        var me = state.Players.First(p => p.PlayerId == actor);
        var enemy = state.Players.First(p => p.PlayerId != actor);
        var readyDamage = me.Field.Where(c => c.CanPerformAttackDto(actor)
            && !c.Keywords.Contains(CardKeyword.CannotAttackLeader)
            && (c.ZoneStayTurn > 0 || c.HasStorm()))
            .Sum(c => c.CurrentAttack * (c.MaxAttacksPerTurn - c.AttacksThisTurn));
        var stormDamage = me.Field.Count < GameConstants.Field.MaxFieldSize && stormPlays != null
            ? stormPlays.Where(p => p.Source.CurrentCost <= SpendablePp(me)
                && me.Hand.Any(c => c.InstanceId == p.Source.InstanceId))
                .Select(p => p.Source.CurrentAttack).DefaultIfEmpty().Max()
            : 0;
        var wardBurden = enemy.Field.Where(c => c.Type == CardType.Follower && c.Keywords.Contains(CardKeyword.Ward)
            && !c.Keywords.Contains(CardKeyword.Ambush) && !c.Keywords.Contains(CardKeyword.Pressure))
            .Sum(c => Math.Max(1, c.CurrentHealth) + (c.Keywords.Contains(CardKeyword.Shield) ? 3 : 0) + 1);
        var leaderShield = enemy.LeaderCard?.Keywords.Contains(CardKeyword.Shield) == true ? 3 : 0;
        return readyDamage + stormDamage - wardBurden - leaderShield - (enemy.LeaderCard?.CurrentHealth ?? 20);
    }

    private static int SpendablePp(PlayerStateDto player) =>
        player.CurrentPP + (player.IsPpBoostArmed && player.PpBoostUsesAvailable > 0 ? GameConstants.PlayPoints.PpBoostAmount : 0);

    private static IEnumerable<Move> Moves(GameStateDto state, Guid actor, IReadOnlyList<Move>? stormPlays)
    {
        var me = state.Players.First(p => p.PlayerId == actor);
        var opponent = state.Players.First(p => p.PlayerId != actor);
        if (stormPlays != null && me.Field.Count < GameConstants.Field.MaxFieldSize)
        {
            foreach (var play in stormPlays)
            {
                if (play.Source.CurrentCost <= SpendablePp(me) && me.Hand.Any(c => c.InstanceId == play.Source.InstanceId))
                    yield return play;
            }
        }

        foreach (var unit in me.Field)
        {
            if (unit.CanPerformAttackDto(actor))
            {
                foreach (var target in AiService.LegalAttackTargets(unit, opponent))
                    yield return new Move("PerformAttack", new AttackActionDto { AttackerUid = unit.InstanceId, TargetUid = target.InstanceId }, unit, target);
            }
            if (!unit.CanEvolve || unit.Keywords.Contains(CardKeyword.BanEvolve)) continue;
            if (me.CanEvolve && (me.CurrentEP >= GameConstants.Evolution.EvolutionPointCost || unit.Keywords.Contains(CardKeyword.FreeEvolve)))
                yield return new Move("PerformEvolve", new EvolveActionDto { UnitUid = unit.InstanceId.ToString() }, unit);
            if (me.CanSuperEvolve && me.CurrentSEP >= GameConstants.SuperEvolution.SuperEvolutionPointCost)
                yield return new Move("PerformSuperEvolve", new SuperEvolveActionDto { UnitUid = unit.InstanceId.ToString() }, unit);
        }
    }

    private static GameStateDto Apply(GameStateDto state, Guid actor, Move move)
    {
        var players = state.Players.Select(p => p with { Field = new(p.Field) }).ToList();
        var me = players.First(p => p.PlayerId == actor);
        var enemy = players.First(p => p.PlayerId != actor);
        var source = move.Source;
        if (move.Type == "PlayCard")
        {
            me.Hand = me.Hand.Where(c => c.InstanceId != source.InstanceId).ToList();
            if (source.CurrentCost > me.CurrentPP)
            {
                me.IsPpBoostArmed = false;
                me.PpBoostUsesAvailable--;
            }
            me.CurrentPP = Math.Max(0, me.CurrentPP - source.CurrentCost);
            me.Field.Add(source with { Zone = CardZone.Field, ZoneStayTurn = 0, CanAttack = true });
            return state with { Players = players };
        }

        if (move.Target is not { } target)
        {
            var super = move.Type == "PerformSuperEvolve";
            var bonus = super ? 3 : 2;
            Replace(me.Field, source with {
                CurrentAttack = source.CurrentAttack + bonus, CurrentHealth = source.CurrentHealth + bonus,
                MaxHealth = source.MaxHealth + bonus, IsEvolved = true, IsSuperEvolved = super,
                CanEvolve = false, CanAttack = !source.Keywords.Contains(CardKeyword.Shackle)
            });
            if (super) me.CurrentSEP -= GameConstants.SuperEvolution.SuperEvolutionPointCost;
            else if (!source.Keywords.Contains(CardKeyword.FreeEvolve)) me.CurrentEP -= GameConstants.Evolution.EvolutionPointCost;
            me.CanEvolve = me.CanSuperEvolve = false;
            return state with { Players = players };
        }

        var outgoing = target.Keywords.Contains(CardKeyword.Shield) ? 0 : source.CurrentAttack;
        var updatedTarget = Hit(target, outgoing);
        var updatedSource = source with {
            AttacksThisTurn = source.AttacksThisTurn + 1,
            Keywords = source.Keywords.Where(k => k != CardKeyword.Ambush).ToList()
        };
        if (target.Type == CardType.Leader)
        {
            enemy.LeaderCard = updatedTarget;
            Replace(me.Field, updatedSource);
        }
        else
        {
            var retaliation = source.IsSuperEvolved || source.Keywords.Contains(CardKeyword.Shield) ? 0 : target.CurrentAttack;
            updatedSource = Hit(updatedSource, retaliation);
            var targetDies = Dies(updatedTarget, source);
            var sourceDies = Dies(updatedSource, target);
            if (targetDies) enemy.Field.RemoveAll(c => c.InstanceId == target.InstanceId);
            else Replace(enemy.Field, updatedTarget);
            if (sourceDies) me.Field.RemoveAll(c => c.InstanceId == source.InstanceId);
            else Replace(me.Field, updatedSource);
            if (targetDies && source.IsSuperEvolved && enemy.LeaderCard is { } leader)
                enemy.LeaderCard = Hit(leader, leader.Keywords.Contains(CardKeyword.Shield) ? 0 : 1);
        }
        if (source.Keywords.Contains(CardKeyword.Drain) && me.LeaderCard is { } ownLeader)
            me.LeaderCard = ownLeader with { CurrentHealth = Math.Min(ownLeader.MaxHealth, ownLeader.CurrentHealth + outgoing) };
        return state with { Players = players };
    }

    private static GameStateDto ProjectEndTurn(GameStateDto state, Guid actor, EndTurnForecast? ending)
    {
        if (ending == null || ending.Damage.Count == 0) return state;
        var players = state.Players.Select(player => player with { Field = new(player.Field) }).ToList();
        var own = players.First(player => player.PlayerId == actor);
        foreach (var damage in ending.Damage)
        {
            if (own.Hand.Count != damage.HandCount || !own.Field.Concat(own.Emblem).Any(card => card.InstanceId == damage.SourceId)) continue;
            foreach (var player in players)
            {
                foreach (var target in player.Field.ToArray())
                {
                    if (!damage.TargetIds.Contains(target.InstanceId) || target.Keywords.Contains(CardKeyword.EffectImmune)
                        || target.BuffSummaries.Count > 0) continue;
                    var hit = Hit(target, target.Keywords.Contains(CardKeyword.Shield) ? 0 : damage.Amount);
                    if (hit.CurrentHealth <= 0) player.Field.RemoveAll(card => card.InstanceId == target.InstanceId);
                    else Replace(player.Field, hit);
                }
                if (player.LeaderCard is { } leader && damage.TargetIds.Contains(leader.InstanceId)
                    && !leader.Keywords.Contains(CardKeyword.EffectImmune) && leader.BuffSummaries.Count == 0)
                    player.LeaderCard = Hit(leader, leader.Keywords.Contains(CardKeyword.Shield) ? 0 : damage.Amount);
            }
        }
        return state with { Players = players };
    }

    private static CardInstanceDto Hit(CardInstanceDto card, int damage) => card with {
        CurrentHealth = card.CurrentHealth - damage,
        Keywords = card.Keywords.Where(k => k != CardKeyword.Shield).ToList()
    };

    private static bool Dies(CardInstanceDto card, CardInstanceDto opponent) =>
        card.CurrentHealth <= 0 || (opponent.Keywords.Contains(CardKeyword.Bane)
            && !card.Keywords.Contains(CardKeyword.DestroyResist) && !card.Keywords.Contains(CardKeyword.EffectImmune));

    private static void Replace(List<CardInstanceDto> field, CardInstanceDto card)
    {
        var index = field.FindIndex(c => c.InstanceId == card.InstanceId);
        if (index >= 0) field[index] = card;
    }

    private static HashSet<Guid> ReadEngines(GameStateDto state, Guid actor)
    {
        var engines = new HashSet<Guid>();
        foreach (var card in state.Players.Where(player => player.PlayerId != actor).SelectMany(player => player.Field))
        {
            if (card.ActAbilities.Count > 0 || card.BuffSummaries.Any(buff => buff.GrantedAbilities.Count > 0))
            {
                engines.Add(card.InstanceId);
                continue;
            }
            if (card.CardId <= 0 || GameDataProvider.Instance == null) continue;
            // Read rules once per search, rather than querying definitions for every simulated node.
            if (GameDataProvider.Instance.GetAbilityConfigs(card.CardId).Any(ability => ability.IsEnabled
                && (ability.Trigger.ValidZones.Count == 0 || ability.Trigger.ValidZones.Contains(CardZone.Field))
                && (ability.Type is AbilityType.Continuous or AbilityType.Activated
                    || (!ability.HasKeyword(AbilityKeyword.Fanfare) && !ability.HasKeyword(AbilityKeyword.EvolveEffect)
                        && !ability.HasKeyword(AbilityKeyword.SuperEvolveEffect) && !ability.HasKeyword(AbilityKeyword.Lastword)
                        && ability.Trigger.EventTypes.Count > 0))))
                engines.Add(card.InstanceId);
        }
        return engines;
    }

    private static double Evaluate(GameStateDto state, Guid actor, IReadOnlySet<Guid> engines, string? intent)
    {
        var me = state.Players.First(p => p.PlayerId == actor);
        var enemy = state.Players.First(p => p.PlayerId != actor);
        var health = me.LeaderCard?.CurrentHealth ?? 20;
        var enemyHealth = enemy.LeaderCard?.CurrentHealth ?? 20;
        if (enemyHealth <= 0) return 10000 + health;
        var threat = EstimateBoardDamage(enemy.Field, me.Field);
        var survival = threat >= health ? 13 + (threat - health) : 0;
        var safeToRace = health >= 9 && threat <= Math.Min(4, (health - 6) / 2);
        var minorWeight = intent is "stabilize" or "value" ? .5 : .35;
        double EnemyValue(CardInstanceDto card)
        {
            var engine = engines.Contains(card.InstanceId);
            var minor = card.Type == CardType.Follower && card.CurrentAttack <= 2 && card.CurrentHealth <= 3
                && card.MaxAttacksPerTurn <= 1 && !engine
                && !card.Keywords.Any(keyword => keyword is CardKeyword.Ward or CardKeyword.Bane
                    or CardKeyword.Drain or CardKeyword.Shield or CardKeyword.Aura);
            return (BoardValue(card) + (engine ? 1.5 : 0)) * (safeToRace && minor ? minorWeight : 1);
        }
        var faceWeight = intent == "pressure" ? 1 : .8;
        return me.Field.Sum(BoardValue) - (intent == "stabilize" ? 1.18 : 1.14) * enemy.Field.Sum(EnemyValue)
            + (intent == "stabilize" ? .8 : .65) * health
            - (enemyHealth <= 6 ? Math.Max(.9, faceWeight) * enemyHealth - .9 : faceWeight * enemyHealth)
            - survival + 3.5 * me.CurrentEP + 5.5 * me.CurrentSEP;
    }

    // Public board only. Estimate how much face damage remains after the defender's Wards.
    internal static int EstimateBoardDamage(IReadOnlyList<CardInstanceDto> attackers, IReadOnlyList<CardInstanceDto> defenders)
    {
        var face = 0;
        var bypass = 0;
        var freeClearDamage = 0;
        var cheapestClear = int.MaxValue;
        foreach (var card in attackers)
        {
            if (card.Type != CardType.Follower || card.CurrentAttack <= 0 || card.MaxAttacksPerTurn <= 0
                || card.Keywords.Contains(CardKeyword.Shackle)) continue;

            var damage = card.CurrentAttack * card.MaxAttacksPerTurn;
            var canHitFace = !card.Keywords.Contains(CardKeyword.CannotAttackLeader);
            if (canHitFace)
            {
                face += damage;
                if (card.Keywords.Contains(CardKeyword.IgnoreWard)) bypass += damage;
            }
            if (card.Keywords.Contains(CardKeyword.CannotAttackFollower)) continue;
            if (canHitFace) cheapestClear = Math.Min(cheapestClear, card.CurrentAttack);
            else freeClearDamage += damage;
        }

        if (face == 0) return 0;
        var wardHealth = 0;
        var shieldCount = 0;
        foreach (var card in defenders)
        {
            if (card.Type != CardType.Follower || !card.Keywords.Contains(CardKeyword.Ward)
                || card.Keywords.Contains(CardKeyword.Ambush) || card.Keywords.Contains(CardKeyword.Pressure)) continue;
            wardHealth += Math.Max(1, card.CurrentHealth);
            if (card.Keywords.Contains(CardKeyword.Shield)) shieldCount++;
        }

        if (wardHealth == 0 || freeClearDamage >= wardHealth && shieldCount == 0) return face;
        if (cheapestClear == int.MaxValue) return bypass;

        // Clearing a one-health Ward still consumes an entire attack. Treat Bane and
        // unusual damage effects as uncertainty, rather than claiming a forced result.
        var clearingCost = Math.Max(cheapestClear, wardHealth - freeClearDamage);
        return Math.Max(bypass, face - clearingCost);
    }

    // Additional opportunity cost beyond the reserve already priced into the beam's board evaluation.
    private static double ReserveValue(PlayerStateDto player) =>
        1.3 * player.CurrentEP + 1.7 * player.CurrentSEP
        + (player.CurrentEP > 0 ? .7 : 0) + (player.CurrentSEP > 0 ? .9 : 0)
        + 2 * player.PpBoostUsesAvailable;

    private static double BoardValue(CardInstanceDto card) => card.Type == CardType.Follower
        ? 1.5 + card.CurrentAttack * 1.05 + card.CurrentHealth * .65
            + (card.Keywords.Contains(CardKeyword.Ward) ? .65 : 0)
            + (card.Keywords.Contains(CardKeyword.Shield) ? .8 : 0)
        : 1.25;

    private static string Key(GameStateDto state) => string.Join("|", state.Players.Select(p =>
        $"{p.LeaderCard?.CurrentHealth}:{p.LeaderCard?.Keywords.Contains(CardKeyword.Shield)}:{p.CurrentPP}:{p.IsPpBoostArmed}:{p.PpBoostUsesAvailable}:{p.CurrentEP}:{p.CurrentSEP}:{p.CanEvolve}:{p.CanSuperEvolve}:"
        + string.Join(",", p.Field.Select(c => $"{c.InstanceId}:{c.CurrentHealth}:{c.AttacksThisTurn}:{c.IsEvolved}:{c.IsSuperEvolved}:{c.Keywords.Contains(CardKeyword.Shield)}"))));
}
