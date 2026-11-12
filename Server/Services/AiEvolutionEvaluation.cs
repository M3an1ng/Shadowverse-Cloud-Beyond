using System.Runtime.CompilerServices;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiEvolutionMatchFacts(int ReplacementTurns, int HandClearTurns,
    int ReplacementCommits, int HandClearCommits, AiEvolutionExample[] Examples)
{
    public int HandClearInputTurns { get; init; }
    public int RefillForecastTurns { get; init; }
    public AiRefillObservation[] RefillExamples { get; init; } = [];
    public AiHandClearBlock[] HandClearBlocks { get; init; } = [];
    public int DeckExhaustionForecastTurns { get; init; }
    public int DeckExhaustionClearCommits { get; init; }
    public int DeckExhaustionEndTurnCommits { get; init; }
    public bool CandidateDeckExhausted { get; init; }
    public bool OpponentDeckExhausted { get; init; }
    public AiDeckExhaustionObservation[] DeckExhaustionExamples { get; init; } = [];
}

public sealed record AiRefillObservation(int Turn,int HandBefore,int HandAfter,int? ForecastDraw,
    int? EnemyHealthBefore,int? EnemyHealthAfter)
{
    public AiRefillBlock? Block { get; init; }
    public int? ForecastFirstDraw { get; init; }
}

public sealed record AiDeckExhaustionObservation(int Turn,string Action,int OwnDeckBefore,int HandBefore,
    int HandAfter,int FirstDraw,int LaterDraw,bool ConfirmedDeckExhaustion);

public sealed record AiRefillBlock(string Reason,int? PublicCardId=null,string? Zone=null,string[]? Events=null);

public sealed record AiHandClearBlock(int Turn, int SourceId, string Reason, int? BlockingCardId, string[] Events);

public sealed record AiEvolutionExample(int Turn, int CardId, string Action, string Route,
    int EpBefore, int EpAfter, int SepBefore, int SepAfter, int HandBefore, int HandAfter,
    int? HealthBefore, int? HealthAfter, bool IsSuperEvolved);

internal sealed class AiEvolutionEvaluation(Guid candidate,bool refillOnly)
{
    private static readonly ConditionalWeakTable<GameState, AiEvolutionEvaluation> Evaluations = new();
    private readonly HashSet<int> replacementTurns = [];
    private readonly HashSet<int> handClearTurns = [];
    private readonly HashSet<int> handClearInputs = [];
    private readonly List<AiHandClearBlock> blocks = [];
    private readonly List<AiEvolutionExample> examples = [];
    private readonly HashSet<int> refillForecasts = [];
    private readonly List<AiRefillObservation> refills = [];
    private readonly HashSet<int> exhaustionForecasts = [];
    private readonly List<AiDeckExhaustionObservation> exhaustions = [];
    private int replacementCommits, handClearCommits, exhaustionClearCommits, exhaustionEndTurnCommits;

    internal Guid Candidate { get; } = candidate;
    internal bool RefillOnly { get; } = refillOnly;
    internal static void Bind(GameState game, Guid actor,bool refillOnly) => Evaluations.Add(game, new(actor,refillOnly));
    internal static AiEvolutionEvaluation? For(GameState game) =>
        Evaluations.TryGetValue(game, out var result) ? result : null;

    internal void Observe(int turn, PlayerStateDto before, AiCombatProjection.Frame? combat, AiTacticalSearch.EndTurnForecast? ending)
    {
        if (before.CanEvolve && combat is { } rules && before.Field.Any(card => card.CanEvolve
            && rules.OrdinaryEvolutionReplacements.Contains(card.InstanceId)
            && !card.Keywords.Contains(CardKeyword.BanEvolve)
            && (before.CurrentEP > 0 || card.Keywords.Contains(CardKeyword.FreeEvolve))))
            replacementTurns.Add(turn);
        if (ending?.HandClearEvolutions.Count > 0) handClearTurns.Add(turn);
        if (ending?.Refill != null) refillForecasts.Add(turn);
        if (ending?.DeckExhaustion != null) exhaustionForecasts.Add(turn);
    }

    internal void Commit(GameState game, int turn, GameStateDto state, string kind, object action,
        AiCombatProjection.Frame? combat, AiTacticalSearch.EndTurnForecast? ending)
    {
        var before=state.Players.Single(player=>player.PlayerId==Candidate);
        var enemyHealthBefore=state.Players.Single(player=>player.PlayerId!=Candidate).LeaderCard?.CurrentHealth;
        if (ending?.DeckExhaustion is { } shortage && game.GetPlayerState(Candidate) is { } afterShortage)
        {
            var cleared=afterShortage.Hand.Count==0 && AiTacticalSearch.ClearsHandIntoDeckLoss(before,ending,action);
            var ended=kind=="EndTurn" && AiTacticalSearch.EndsInDeckLoss(state,Candidate,ending);
            if (cleared) exhaustionClearCommits++;
            if (ended) exhaustionEndTurnCommits++;
            if ((cleared || ended) && exhaustions.Count<12)
                exhaustions.Add(new(turn,kind,before.DeckCount,before.Hand.Count,afterShortage.Hand.Count,
                    shortage.DrawCount,shortage.CountOnBoard(before)-shortage.DrawCount,IsDeckExhaustion(game)));
        }
        if (kind == "EndTurn" && before.Hand.Count == 0 && before.Emblem.Any(card=>card.CardId==AiEternalEngine.Emblem)
            && refills.Count < 12 && game.GetPlayerState(Candidate) is { } owner)
            refills.Add(new(turn,0,owner.Hand.Count,ending?.Refill is {LaterDrawsKnown:true} refill?refill.CountOnBoard(before):null,
                enemyHealthBefore,game.GetOpponentPlayerState(Candidate)?.Leader?.CurrentHealth)
                { Block=ending?.RefillBlock,ForecastFirstDraw=ending?.Refill?.DrawCount });
        var rawId = action switch { EvolveActionDto ordinary => ordinary.UnitUid, SuperEvolveActionDto super => super.UnitUid, _ => null };
        if (!Guid.TryParse(rawId, out var id) || game.FindCardInstance(id) is not { IsEvolved: true } card
            || game.GetPlayerState(Candidate) is not { } after || before.Field.FirstOrDefault(unit => unit.InstanceId == id) is not { } unit)
            return;
        var replacement = kind == "PerformEvolve" && card.IsSuperEvolved && combat?.OrdinaryEvolutionReplacements.Contains(id) == true;
        var handClear = after.Hand.Count == 0 && ending?.HandClearEvolutions.Any(route => route.Source == id
            && (kind == "PerformEvolve" ? route.Ordinary : route.Super)) == true;
        if (replacement) replacementCommits++;
        if (handClear) handClearCommits++;
        if ((replacement || handClear) && examples.Count < 12)
            examples.Add(new(turn, unit.CardId, kind, replacement ? "ordinary-to-super" : "hand-clear",
                before.CurrentEP, after.CurrentEvolutionPoints, before.CurrentSEP, after.CurrentSuperEvolutionPoints,
                before.Hand.Count, after.Hand.Count, before.LeaderCard?.CurrentHealth, after.Leader?.CurrentHealth, card.IsSuperEvolved));
    }

    internal void ObserveHandClearInput(int turn) => handClearInputs.Add(turn);

    internal void BlockHandClear(int turn, int source, string reason, int? blocker = null, string[]? events = null)
    {
        if (blocks.Count >= 16 || blocks.Any(row => row.Turn == turn && row.SourceId == source
            && row.Reason == reason && row.BlockingCardId == blocker))
            return;
        blocks.Add(new(turn, source, reason, blocker, events ?? []));
    }

    private static bool IsDeckExhaustion(GameState game) => game.IsGameOver && game.GameMessage=="牌堆抽空";

    internal AiEvolutionMatchFacts Snapshot(GameState game) =>
        new(replacementTurns.Count, handClearTurns.Count, replacementCommits, handClearCommits, [.. examples])
        {
            HandClearInputTurns=handClearInputs.Count,HandClearBlocks=[.. blocks],
            RefillForecastTurns=refillForecasts.Count,RefillExamples=[.. refills],
            DeckExhaustionForecastTurns=exhaustionForecasts.Count,DeckExhaustionClearCommits=exhaustionClearCommits,
            DeckExhaustionEndTurnCommits=exhaustionEndTurnCommits,DeckExhaustionExamples=[.. exhaustions],
            CandidateDeckExhausted=IsDeckExhaustion(game) && game.WinnerId!=Candidate,
            OpponentDeckExhausted=IsDeckExhaustion(game) && game.WinnerId==Candidate
        };
}
