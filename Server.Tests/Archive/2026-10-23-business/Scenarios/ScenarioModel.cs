using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public enum ScenarioPlayer
    {
        Player1 = 1,
        Player2 = 2
    }

    public sealed class ScenarioCase
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int FocusCardId { get; init; }
        public ScenarioSetup Setup { get; init; } = new();
        public List<ScenarioAction> Actions { get; init; } = new();
        public List<IScenarioExpectation> Expectations { get; init; } = new();
        public Dictionary<string, ScenarioChoice> Choices { get; init; } = new();
    }

    public sealed class ScenarioSetup
    {
        public ScenarioPlayer ActivePlayer { get; init; } = ScenarioPlayer.Player1;
        public ScenarioPlayer FirstPlayer { get; init; } = ScenarioPlayer.Player1;
        public ScenarioPlayerSetup Player1 { get; init; } = new();
        public ScenarioPlayerSetup Player2 { get; init; } = new();
        public List<ScenarioCardSpec> Cards { get; init; } = new();
    }

    public sealed class ScenarioPlayerSetup
    {
        public int MaxPlayPoints { get; set; } = 0;
        public int CurrentPlayPoints { get; set; } = 0;
        public int MaxEvolutionPoints { get; set; } = 0;
        public int CurrentEvolutionPoints { get; set; } = 0;
        public int MaxSuperEvolutionPoints { get; set; } = 0;
        public int CurrentSuperEvolutionPoints { get; set; } = 0;
        public int? GraveyardCount { get; set; }
        public int? BloodPool { get; set; }
        public int TurnNumber { get; set; } = 1;
        public int PlayedCardCountThisTurn { get; set; } = 0;
        public bool CanEvolve { get; set; } = true;
        public bool CanSuperEvolve { get; set; } = true;
        public int? LeaderHealth { get; set; }
    }

    public sealed class ScenarioCardSpec
    {
        public string Key { get; init; } = string.Empty;
        public int CardId { get; init; }
        public ScenarioPlayer Owner { get; init; } = ScenarioPlayer.Player1;
        public CardZone Zone { get; init; } = CardZone.Hand;
        public int? Damage { get; init; }
        public int? AttacksThisTurn { get; init; }
        public int? ZoneStayTurn { get; init; }
        public bool? IsEvolved { get; init; }
        public bool? IsSuperEvolved { get; init; }
        public bool RegisterAbilities { get; init; } = true;
        public int? CountdownOverride { get; init; }
    }

    public abstract record ScenarioAction(ScenarioPlayer Player);

    public sealed record PlayCardAction(
        ScenarioPlayer Player,
        string CardKey,
        PlayMode? PlayMode = null,
        int? PaidCost = null) : ScenarioAction(Player);

    public sealed record EvolveAction(ScenarioPlayer Player, string CardKey) : ScenarioAction(Player);

    public sealed record SuperEvolveAction(ScenarioPlayer Player, string CardKey) : ScenarioAction(Player);

    public sealed record ActAction(ScenarioPlayer Player, string CardKey) : ScenarioAction(Player);

    public sealed record AttackAction(
        ScenarioPlayer Player,
        string AttackerKey,
        string TargetKey) : ScenarioAction(Player);

    public sealed record TurnStartAction(ScenarioPlayer Player) : ScenarioAction(Player);

    public sealed record TurnEndAction(ScenarioPlayer Player) : ScenarioAction(Player);

    public sealed record CountdownTickAction(ScenarioPlayer Player, string CardKey, int ChangeAmount = -1) : ScenarioAction(Player);

    public sealed record DestroyAction(ScenarioPlayer Player, string CardKey, EventReasonSource Reason = EventReasonSource.Ability) : ScenarioAction(Player);

    public sealed record ReturnAction(ScenarioPlayer Player, string CardKey, EventReasonSource Reason = EventReasonSource.Ability) : ScenarioAction(Player);

    public sealed record TuckAction(ScenarioPlayer Player, string CardKey, EventReasonSource Reason = EventReasonSource.Ability) : ScenarioAction(Player);

    public sealed record VanishAction(ScenarioPlayer Player, string CardKey, string Reason = "Effect") : ScenarioAction(Player);

    public sealed class ScenarioChoice
    {
        public List<string> CardKeys { get; init; } = new();
        public List<int> Indices { get; init; } = new();
    }

    public interface IScenarioExpectation
    {
        void Assert(ScenarioContext context);
    }
}
