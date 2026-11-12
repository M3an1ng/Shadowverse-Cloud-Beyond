using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
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
    internal sealed record Outcome(int? OwnHealth,int? EnemyHealth,int PP,int EP,int SEP,int OwnField,int EnemyField)
    {
        public int? OwnEndTurnDraw { get; init; }
    }
    private static Outcome Result(GameStateDto state,Guid actor) {
        var own=state.Players.Single(player=>player.PlayerId==actor);var enemy=state.Players.Single(player=>player.PlayerId!=actor);
        return new(own.LeaderCard?.CurrentHealth,enemy.LeaderCard?.CurrentHealth,own.CurrentPP,own.CurrentEP,own.CurrentSEP,own.Field.Count,enemy.Field.Count);
    }
    internal sealed record Plan(Move First, double Gain, bool ProjectsLethal, bool PreventsVisibleLethal, bool SpendsSep = false)
    {
        internal IReadOnlyList<Move> Steps {get;init;}=[];
        internal Outcome? Outcome {get;init;}
        internal int LeaderRecovery { get; init; }
        public int Priority => ProjectsLethal ? 2 : PreventsVisibleLethal ? 1 : 0;
    }
    internal sealed record LaterFieldDraw(Guid SourceId,int DrawCount);
    internal sealed record PendingRefill(Guid SourceId,int DrawCount,LaterFieldDraw[] LaterDraws)
    {
        internal const int AddedCards = 2;
        internal bool LaterDrawsKnown { get; init; } = true;
        internal int CountOnBoard(PlayerStateDto player) =>
            DrawCount + LaterDraws.Where(effect=>player.Field.Any(card=>card.InstanceId==effect.SourceId))
                .Sum(effect=>effect.DrawCount);
    }
    internal sealed record PendingDamage(Guid SourceId, Guid[] TargetIds, int Amount, int HandCount)
    {
        internal bool BeforeLaterDraw { get; init; }
    }
    internal sealed record ForecastOmission(Guid SourceId,string Reason);
    internal sealed record EndTurnForecast(IReadOnlyList<PendingDamage> Damage, IReadOnlySet<Guid> UnsafeAttackers) {
        internal IReadOnlyList<ForecastOmission> Omissions {get;init;}=[];
        internal IReadOnlyList<AiHandClearEvolution.Projection> HandClearEvolutions { get; init; } = [];
        internal IReadOnlyList<PendingDamage> EmptyHandDamage { get; init; } = [];
        internal PendingRefill? Refill { get; init; }
        internal AiRefillBlock? RefillBlock { get; init; }
        internal PendingRefill? DeckExhaustion { get; init; }
        internal Guid? LastHandPlayIntoExhaustion { get; init; }
        internal IReadOnlySet<Guid> ExpiringFieldCards {get;init;}=new HashSet<Guid>();
    }
    private sealed record Node(GameStateDto State, Move? First, double Value, bool PlayedCard = false, double AbilityValue = 0,
        bool InvalidatesEndTurn = false,Move[]? Steps=null);

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
        => FindWithValue(state, actor, token, stormPlays, evolutionValues, ending, intent, null);

    // Experimental local value guidance cannot override tactical priorities or spend large reserves by itself.
    internal static Plan? FindWithValue(GameStateDto state, Guid actor, CancellationToken token,
        IReadOnlyList<Move>? stormPlays, IReadOnlyDictionary<Guid, (double Evolve, double SuperEvolve)>? evolutionValues,
        EndTurnForecast? ending, string? intent, Func<GameStateDto, double>? learnedValue)
        =>FindWithCombat(state,actor,token,stormPlays,evolutionValues,ending,intent,learnedValue,null);

    internal static Plan? FindWithCombat(GameStateDto state,Guid actor,CancellationToken token,
        IReadOnlyList<Move>? stormPlays,IReadOnlyDictionary<Guid,(double Evolve,double SuperEvolve)>? evolutionValues,
        EndTurnForecast? ending,string? intent,Func<GameStateDto,double>? learnedValue,AiCombatProjection.Frame? combatRules)
    {
        combatRules=AiCombatProjection.ForPosition(combatRules,state,actor);
        var engines = ReadEngines(state, actor);
        var best = Search(state, actor, token, stormPlays, evolutionValues, ending, engines, intent, learnedValue, combatRules, prioritizeLethal: true);
        var own = state.Players.First(p => p.PlayerId == actor);
        var enemy = state.Players.First(p => p.PlayerId != actor);
        if (best?.ProjectsLethal != true
            && (HasEnoughPotentialDamage(state, actor, stormPlays, combatRules)
                || (HasVisibleLethalThreat(own, enemy) && best?.PreventsVisibleLethal != true)))
        {
            var alternative = Search(state, actor, token, stormPlays, evolutionValues, ending, engines, intent, learnedValue, combatRules, prioritizeLethal: false);
            if (alternative != null && alternative.Priority > (best?.Priority ?? 0)) best = alternative;
        }
        if (best?.SpendsSep == true && !best.ProjectsLethal && own.CanEvolve && own.CurrentEP > 0)
        {
            // Reserve comparison matters for continuing play; a verified lethal needs no EP-versus-SEP re-search.
            var normalState = state with { Players = state.Players.Select(player => player.PlayerId == actor
                ? player with { CanSuperEvolve = false } : player).ToList() };
            var normal = Search(normalState, actor, token, stormPlays, evolutionValues, ending, engines, intent, learnedValue, combatRules, prioritizeLethal: true);
            if (normal != null && normal.Priority >= best.Priority && (normal.Priority > 0 || normal.Gain > 0)
                && best.Gain - normal.Gain <= 4.5 + 1e-9)
                best = normal;
        }
        return best;
    }

    internal static bool HasVisibleLethalThreat(PlayerStateDto defender, PlayerStateDto attacker) =>
        defender.LeaderCard is { CurrentHealth: > 0 } leader
        && EstimateBoardDamage(attacker.Field, defender.Field,defender.LeaderCard) >= leader.CurrentHealth;

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
        IReadOnlyDictionary<Guid, (double Evolve, double SuperEvolve)>? evolutionValues, EndTurnForecast? ending, IReadOnlySet<Guid> engines, string? intent, Func<GameStateDto, double>? learnedValue, AiCombatProjection.Frame? combatRules, bool prioritizeLethal)
    {
        var endsInDeckLoss = EndsInDeckLoss(state,actor,ending);
        var terminal = endsInDeckLoss ? state : ProjectEndTurn(state, actor, ending);
        var originalPlayer = state.Players.First(p => p.PlayerId == actor);
        var baselineDraw = ProjectedRefill(terminal,actor,ending);
        var baseline = Evaluate(terminal, actor, engines, intent,ending?.ExpiringFieldCards) + baselineDraw;
        var originalReserve = ReserveValue(originalPlayer);
        var defensiveEmergency = HasVisibleLethalThreat(originalPlayer, state.Players.First(p => p.PlayerId != actor));
        var terminalOwn = terminal.Players.First(player => player.PlayerId == actor);
        var terminalEnemy = terminal.Players.First(player => player.PlayerId != actor);
        var terminalLethal = terminalEnemy.LeaderCard is { CurrentHealth: <= 0 };
        var terminalSafe = !endsInDeckLoss && defensiveEmergency && !HasVisibleLethalThreat(terminalOwn, terminalEnemy);
        Plan? best=null;
        if((terminalLethal||terminalSafe)&&terminalOwn.LeaderCard is {CurrentHealth:>0} leader) {
            var first=new Move("EndTurn",new {},leader);
            best=new Plan(first,0,terminalLethal,terminalSafe){Steps=[first],Outcome=WithRefill(terminal,actor,baselineDraw)};
        }
        // A public end-turn lethal already wins without spending another resource or risking the source.
        if(best?.ProjectsLethal==true)return best;
        var referenceValue = learnedValue?.Invoke(terminal);
        double LearnedAdjustment(GameStateDto projected)
        {
            if (learnedValue == null || referenceValue is not double initial || !double.IsFinite(initial)) return 0;
            var predicted = learnedValue(projected);
            // Imported values estimate outcomes, not damage. Bound their influence on uncertain basic afterstates.
            return double.IsFinite(predicted) ? Math.Clamp(4 * (Math.Clamp(predicted, -1, 1) - Math.Clamp(initial, -1, 1)), -2, 2) : 0;
        }
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
                    var handClear = MatchHandClear(parent.State, actor, move, ending);
                    var projected = Apply(parent.State, actor, move, combatRules, handClear);
                    if (!visited.Add(Key(projected) + ":" + parent.InvalidatesEndTurn)) continue;
                    var first = parent.First ?? move;
                    var path=(parent.Steps??[]).Append(move).ToArray();
                    var playedCard = parent.PlayedCard || move.Type == "PlayCard";
                    var abilityValue = parent.AbilityValue;
                    if (handClear == null && evolutionValues?.TryGetValue(move.Source.InstanceId, out var estimates) == true)
                    {
                        if (move.Type == "PerformEvolve" && combatRules?.OrdinaryEvolutionReplacements.Contains(move.Source.InstanceId) != true) abilityValue += estimates.Evolve;
                        if (move.Type == "PerformSuperEvolve") abilityValue += estimates.SuperEvolve;
                    }
                    var invalidatesEndTurn = parent.InvalidatesEndTurn
                        || (handClear == null && (move.Type != "PerformAttack"
                            || ending?.UnsafeAttackers.Contains(move.Source.InstanceId) == true));
                    var checkedLastPlay = ending?.LastHandPlayIntoExhaustion is Guid last
                        && path.Any(step => step.Data is PlayCardActionDto {PlayMode:PlayMode.Base} play && play.CardUid==last);
                    var losesOnEndTurn = (!invalidatesEndTurn || checkedLastPlay) && EndsInDeckLoss(projected,actor,ending);
                    var evaluated = invalidatesEndTurn || losesOnEndTurn ? projected : ProjectEndTurn(projected, actor, ending);
                    var draw = invalidatesEndTurn ? 0 : ProjectedRefill(evaluated,actor,ending);
                    var refillValue = draw > 0 ? Math.Max(0,draw-originalPlayer.Hand.Count) : 0;
                    var value = Evaluate(evaluated, actor, engines, intent,ending?.ExpiringFieldCards) + LearnedAdjustment(evaluated)
                        + abilityValue + refillValue - .04 * depth;
                    var enemy = evaluated.Players.First(p => p.PlayerId != actor);
                    var lethal = enemy.LeaderCard is { CurrentHealth: <= 0 };
                    var own = evaluated.Players.First(p => p.PlayerId == actor);
                    var reserveSpent = lethal ? 0 : originalReserve - ReserveValue(own);
                    if (lethal) value = 10000 - .04 * depth; // Finishing the game is independent of leftover evolution points.
                    var preventsLethal = !losesOnEndTurn && defensiveEmergency && own.LeaderCard is { CurrentHealth: > 0 }
                        && !HasVisibleLethalThreat(own, enemy);
                    var plan = new Plan(first, value - baseline - reserveSpent, lethal, preventsLethal,
                        own.CurrentSEP < originalPlayer.CurrentSEP){Steps=path,Outcome=WithRefill(evaluated,actor,draw)};
                    var riskyFirst = !lethal && ClearsHandIntoDeckLoss(originalPlayer,ending,first.Data);
                    if (!losesOnEndTurn && !riskyFirst && (lethal || preventsLethal || !playedCard)
                        && (best == null || plan.Priority > best.Priority
                            || (plan.Priority == best.Priority && plan.Gain > best.Gain)))
                        best = plan;
                    // A move that empties the hand can still set up lethal before ending the turn.
                    if (!lethal) next.Add(new Node(projected, first, losesOnEndTurn ? value-10000 : value,
                        playedCard, abilityValue, invalidatesEndTurn,path));
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
    private static bool HasEnoughPotentialDamage(GameStateDto state, Guid actor, IReadOnlyList<Move>? stormPlays, AiCombatProjection.Frame? rules)
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
        else if (me.CanEvolve) damage += (rules?.OrdinaryEvolutionReplacements.Count > 0 ? 4 : 2) * maxAttacks;
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

    // Shared known combat/evolution mechanics for the learned value evaluator.
    // This remains an approximation: arbitrary triggered abilities are not replayed here.
    internal static GameStateDto EstimateKnownAction(GameStateDto state,Guid actor,string kind,CardInstanceDto source,CardInstanceDto? target=null)
        =>EstimateConfiguredAction(state,actor,kind,source,target,null);

    internal static GameStateDto EstimateConfiguredAction(GameStateDto state,Guid actor,string kind,CardInstanceDto source,CardInstanceDto? target,AiCombatProjection.Frame? combatRules)
    {
        var type=kind switch {"attack"=>"PerformAttack","evolve"=>"PerformEvolve",_=>throw new ArgumentException("Unsupported mechanical estimate",nameof(kind))};
        if(kind=="attack"&&target==null)throw new ArgumentNullException(nameof(target));
        return Apply(state,actor,new(type,new {},source,kind=="attack"?target:null),combatRules);
    }

    internal static Outcome ProjectBasicMove(GameStateDto state,Guid actor,Move move)=>Result(Apply(state,actor,move),actor);

    internal static Outcome ProjectKnownMove(GameStateDto state,Guid actor,Move move,AiCombatProjection.Frame? rules)=>Result(Apply(state,actor,move,rules),actor);

    private static AiHandClearEvolution.Projection? MatchHandClear(GameStateDto state, Guid actor, Move move, EndTurnForecast? ending)
    {
        if (ending == null || move.Type is not ("PerformEvolve" or "PerformSuperEvolve")) return null;
        var own = state.Players.First(player=>player.PlayerId==actor);
        if (!own.Emblem.Any(card => card.CardId == AiEternalEngine.Emblem)
            && own.Hand.Any(card => card.CardId is AiEternalEngine.Reiss or AiEternalEngine.Search))
            return null;
        return ending.HandClearEvolutions.FirstOrDefault(route=>route.Source==move.Source.InstanceId
            && (move.Type=="PerformEvolve"?route.Ordinary:route.Super)
            && own.Hand.Select(card=>card.InstanceId).SequenceEqual(route.Materials));
    }

    private static GameStateDto Apply(GameStateDto state, Guid actor, Move move, AiCombatProjection.Frame? combatRules=null,
        AiHandClearEvolution.Projection? handClear=null)
    {
        if(move.Type=="PerformAttack"&&combatRules!=null&&move.Target!=null) {
            state=AiCombatProjection.BeforeCombat(state,actor,move,combatRules,out _);
            var owner=state.Players.Single(player=>player.PlayerId==actor);
            var opponent=state.Players.Single(player=>player.PlayerId!=actor);
            var prepared=owner.Field.FirstOrDefault(card=>card.InstanceId==move.Source.InstanceId);
            var defender=move.Target.Type==CardType.Leader?opponent.LeaderCard:opponent.Field.FirstOrDefault(card=>card.InstanceId==move.Target.InstanceId);
            if(prepared==null||defender==null||state.Players.Any(player=>player.LeaderCard is {CurrentHealth:<=0})) {
                if(prepared!=null)Replace(owner.Field,prepared with{AttacksThisTurn=prepared.AttacksThisTurn+1,Keywords=prepared.Keywords.Where(key=>key!=CardKeyword.Ambush).ToList()});
                return state;
            }
            move=move with{Source=prepared,Target=defender};
        }
        var players = state.Players.Select(p => p with { Field = new(p.Field),Counters=new(p.Counters,StringComparer.OrdinalIgnoreCase) }).ToList();
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
            return AiCombatProjection.CarryUsage(state,state with { Players = players });
        }

        if (move.Target is not { } target)
        {
            var usesSep = move.Type == "PerformSuperEvolve";
            var super = usesSep || combatRules?.OrdinaryEvolutionReplacements.Contains(source.InstanceId) == true;
            var bonus = super ? 3 : 2;
            Replace(me.Field, source with {
                CurrentAttack = source.CurrentAttack + bonus, CurrentHealth = source.CurrentHealth + bonus,
                MaxHealth = source.MaxHealth + bonus, IsEvolved = true, IsSuperEvolved = super,
                CanEvolve = false, CanAttack = !source.Keywords.Contains(CardKeyword.Shackle)
            });
            if (usesSep) me.CurrentSEP -= GameConstants.SuperEvolution.SuperEvolutionPointCost;
            else if (!source.Keywords.Contains(CardKeyword.FreeEvolve)) me.CurrentEP -= GameConstants.Evolution.EvolutionPointCost;
            me.CanEvolve = me.CanSuperEvolve = false;
            if (handClear != null)
            {
                me.Hand = [];
                if (me.LeaderCard is { } leader)
                    me.LeaderCard = leader with { CurrentHealth=Math.Min(leader.MaxHealth,leader.CurrentHealth+handClear.Heal) };
            }
            return AiCombatProjection.CarryUsage(state,state with { Players = players });
        }

        var outgoing = target.Keywords.Contains(CardKeyword.Shield) ? 0 : AiCombatProjection.IncomingDamage(state,target,source.CurrentAttack,source.InstanceId);
        var updatedTarget = Hit(target, outgoing);
        var updatedSource = source with {
            AttacksThisTurn = source.AttacksThisTurn + 1,
            Keywords = source.Keywords.Where(k => k != CardKeyword.Ambush).ToList()
        };
        if (target.Type == CardType.Leader)
        {
            enemy.LeaderCard = updatedTarget;
            AddLeaderDamageCounter(enemy,outgoing);
            Replace(me.Field, updatedSource);
        }
        else
        {
            var retaliation = source.IsSuperEvolved || source.Keywords.Contains(CardKeyword.Shield) ? 0 : AiCombatProjection.IncomingDamage(state,source,target.CurrentAttack,target.InstanceId);
            updatedSource = Hit(updatedSource, retaliation);
            var targetDies = Dies(updatedTarget, source);
            var sourceDies = Dies(updatedSource, target);
            if (targetDies) { enemy.Field.RemoveAll(c => c.InstanceId == target.InstanceId);AddGraveyardCounter(enemy); }
            else Replace(enemy.Field, updatedTarget);
            if (sourceDies) { me.Field.RemoveAll(c => c.InstanceId == source.InstanceId);AddGraveyardCounter(me); }
            else Replace(me.Field, updatedSource);
            if (targetDies && source.IsSuperEvolved && enemy.LeaderCard is { } leader) {
                var hit=leader.Keywords.Contains(CardKeyword.Shield)?0:1;
                enemy.LeaderCard=Hit(leader,hit);AddLeaderDamageCounter(enemy,hit);
            }
        }
        if (source.Keywords.Contains(CardKeyword.Drain) && me.LeaderCard is { } ownLeader)
            me.LeaderCard = ownLeader with { CurrentHealth = Math.Min(ownLeader.MaxHealth, ownLeader.CurrentHealth + outgoing) };
        return AiCombatProjection.CarryUsage(state,state with { Players = players });
    }

    private static void AddLeaderDamageCounter(PlayerStateDto player,int damage) {
        if(damage<=0)return;
        player.BloodPool+=damage;player.Counters[PlayerState.BloodPoolCounterKey]=player.BloodPool;
    }
    private static void AddGraveyardCounter(PlayerStateDto player) {
        player.GraveyardCount++;player.Counters[PlayerState.GraveyardCounterKey]=player.GraveyardCount;
    }

    private static bool HasDeckShortage(PlayerStateDto own, EndTurnForecast? ending) =>
        ending?.DeckExhaustion is { } shortage
        && own.Emblem.Any(card=>card.InstanceId==shortage.SourceId)
        && own.DeckCount+PendingRefill.AddedCards<shortage.CountOnBoard(own);

    internal static bool EndsInDeckLoss(GameStateDto state, Guid actor, EndTurnForecast? ending)
    {
        if(ending?.DeckExhaustion==null || state.Players.Any(player=>player.LeaderCard is {CurrentHealth:<=0}))
            return false;
        var own=state.Players.Single(player=>player.PlayerId==actor);
        if (own.Hand.Count!=0 || !HasDeckShortage(own,ending)) return false;
        if (own.DeckCount+PendingRefill.AddedCards<ending.DeckExhaustion.DrawCount) return true;
        var beforeDraw=ProjectEndTurn(state,actor,ending);
        return beforeDraw.Players.First(player=>player.PlayerId!=actor).LeaderCard is not {CurrentHealth:<=0};
    }

    internal static bool ClearsHandIntoDeckLoss(PlayerStateDto own, EndTurnForecast? ending, object action)
    {
        if(!HasDeckShortage(own,ending) || own.Hand.Count==0) return false;
        if (action is PlayCardActionDto play)
            return play.PlayMode==PlayMode.Base && LastPlayEntersDeckLoss(own,ending,play.CardUid);
        var raw = action switch
        {
            EvolveActionDto ordinary => ordinary.UnitUid,
            SuperEvolveActionDto super => super.UnitUid,
            _ => null
        };
        return Guid.TryParse(raw,out var source) && ending!.HandClearEvolutions.Any(route=>route.Source==source
            && (action is SuperEvolveActionDto?route.Super:route.Ordinary)
            && own.Hand.Select(card=>card.InstanceId).SequenceEqual(route.Materials));
    }

    internal static bool LastPlayEntersDeckLoss(PlayerStateDto own, EndTurnForecast? ending, Guid source) =>
        ending?.LastHandPlayIntoExhaustion == source && own.Hand is [var last] && last.InstanceId==source
        && HasDeckShortage(own,ending);

    private static int ProjectedRefill(GameStateDto state,Guid actor,EndTurnForecast? ending)
    {
        if (ending?.Refill is not {LaterDrawsKnown:true} refill || state.Players.Any(player=>player.LeaderCard is { CurrentHealth: <=0 })) return 0;
        var own = state.Players.First(player=>player.PlayerId==actor);
        if (own.Hand.Count!=0 || !own.Emblem.Any(card=>card.InstanceId==refill.SourceId)) return 0;
        var requested = refill.CountOnBoard(own);
        return own.DeckCount+PendingRefill.AddedCards>=requested ? requested : 0;
    }

    private static Outcome WithRefill(GameStateDto state,Guid actor,int draw) =>
        Result(state,actor) with { OwnEndTurnDraw = draw>0?draw:null };

    private static GameStateDto ProjectEndTurn(GameStateDto state, Guid actor, EndTurnForecast? ending)
    {
        if (ending == null || (ending.Damage.Count == 0 && ending.EmptyHandDamage.Count == 0)) return state;
        var players = state.Players.Select(player => player with { Field = new(player.Field), Counters = new(player.Counters, StringComparer.OrdinalIgnoreCase) }).ToList();
        var own = players.First(player => player.PlayerId == actor);
        var beforeLaterDrawOnly=own.Hand.Count==0 && HasDeckShortage(own,ending);
        foreach (var damage in ending.Damage.Concat(ending.EmptyHandDamage))
        {
            if (beforeLaterDrawOnly && (!damage.BeforeLaterDraw || players.Any(player=>player.LeaderCard is {CurrentHealth:<=0}))) continue;
            if (own.Hand.Count != damage.HandCount || !own.Field.Concat(own.Emblem).Any(card => card.InstanceId == damage.SourceId)) continue;
            foreach (var player in players)
            {
                foreach (var target in player.Field.ToArray())
                {
                    if (!damage.TargetIds.Contains(target.InstanceId) || target.Keywords.Contains(CardKeyword.EffectImmune)
                        || target.BuffSummaries.Count > 0) continue;
                    var hit = Hit(target, target.Keywords.Contains(CardKeyword.Shield) ? 0 : damage.Amount);
                    if (hit.CurrentHealth <= 0) { player.Field.RemoveAll(card => card.InstanceId == target.InstanceId); AddGraveyardCounter(player); }
                    else Replace(player.Field, hit);
                }
                if (player.LeaderCard is { } leader && damage.TargetIds.Contains(leader.InstanceId)
                    && !leader.Keywords.Contains(CardKeyword.EffectImmune) && leader.BuffSummaries.Count == 0)
                {
                    var dealt = leader.Keywords.Contains(CardKeyword.Shield) ? 0 : damage.Amount;
                    player.LeaderCard = Hit(leader, dealt);
                    AddLeaderDamageCounter(player, dealt);
                }
            }
        }
        return AiCombatProjection.CarryUsage(state,state with { Players = players });
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

    private static double Evaluate(GameStateDto state, Guid actor, IReadOnlySet<Guid> engines, string? intent,IReadOnlySet<Guid>? expiring)
    {
        var me = state.Players.First(p => p.PlayerId == actor);
        var enemy = state.Players.First(p => p.PlayerId != actor);
        var health = me.LeaderCard?.CurrentHealth ?? 20;
        var enemyHealth = enemy.LeaderCard?.CurrentHealth ?? 20;
        if (enemyHealth <= 0) return 10000 + health;
        var threat = EstimateBoardDamage(enemy.Field, me.Field,me.LeaderCard);
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
        return me.Field.Sum(card=>expiring?.Contains(card.InstanceId)==true?0:BoardValue(card)) - (intent == "stabilize" ? 1.18 : 1.14) * enemy.Field.Sum(EnemyValue)
            + (intent == "stabilize" ? .8 : .65) * health
            + (AiRecoveryPolicy.Enabled?1.15 * Math.Clamp(health,0,8):0) // The last few HP are not interchangeable with surplus HP; Drain can buy a turn.
            - (enemyHealth <= 6 ? Math.Max(.9, faceWeight) * enemyHealth - .9 : faceWeight * enemyHealth)
            - survival + 3.5 * me.CurrentEP + 5.5 * me.CurrentSEP;
    }

    // Visible next-turn basic combat only; bounded solves fall back to an upper bound.
    internal static int EstimateBoardDamage(IReadOnlyList<CardInstanceDto> attackers, IReadOnlyList<CardInstanceDto> defenders,
        CardInstanceDto? leader=null) => AiCombatThreat.Evaluate(attackers,defenders,leader).Damage;

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

    private static string Key(GameStateDto state) => AiCombatProjection.UsageKey(state)+"|"+string.Join("|", state.Players.Select(p =>
        $"{p.LeaderCard?.CurrentHealth}:{p.LeaderCard?.Keywords.Contains(CardKeyword.Shield)}:{p.CurrentPP}:{p.IsPpBoostArmed}:{p.PpBoostUsesAvailable}:{p.CurrentEP}:{p.CurrentSEP}:{p.CanEvolve}:{p.CanSuperEvolve}:"
        + $"{p.GraveyardCount}:{p.BloodPool}:"+string.Join(",",p.Counters.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>$"{pair.Key.Length}:{pair.Key}={pair.Value}"))+":"
        + string.Join(",", p.Field.Select(c => $"{c.InstanceId}:{c.CurrentAttack}:{c.CurrentHealth}:{c.MaxHealth}:{c.MaxAttacksPerTurn}:{c.AttacksThisTurn}:{c.IsEvolved}:{c.IsSuperEvolved}:{string.Join('.',c.Keywords.OrderBy(key=>key))}"))));
}
