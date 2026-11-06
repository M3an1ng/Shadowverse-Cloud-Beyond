using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

internal static class AiPolicyTests
{
    internal static void Search()
    {
        var me = Guid.NewGuid();
        var enemy = Guid.NewGuid();
        CardInstanceDto Card(Guid owner, int attack, int health, CardType type = CardType.Follower, params CardKeyword[] keywords)
            => new() { InstanceId = Guid.NewGuid(), OwnerPlayerId = owner, Type = type, Zone = CardZone.Field,
                CurrentAttack = attack, CurrentHealth = health, MaxHealth = health,
                CanAttack = owner == me, MaxAttacksPerTurn = 1, ZoneStayTurn = 2, Keywords = new(keywords) };
        var myLeader = Card(me, 0, 20, CardType.Leader);
        var theirLeader = Card(enemy, 0, 5, CardType.Leader);
        var strong = Card(me, 5, 3);
        var weak = Card(me, 1, 2);
        var ward = Card(enemy, 1, 1, CardType.Follower, CardKeyword.Ward);
        var allies = new PlayerStateDto { PlayerId = me, LeaderCard = myLeader, Field = new() { strong, weak } };
        var foes = new PlayerStateDto { PlayerId = enemy, LeaderCard = theirLeader, Field = new() { ward } };
        var game = new GameStateDto { CurrentPlayerId = me, Phase = GamePhase.Main, Players = new() { allies, foes } };
        var find = typeof(GameStateDto).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!.GetMethod("Find")!;
        object? Find() => find.Invoke(null, new object[] { game, me, default(System.Threading.CancellationToken), null!, null! });
        object Property(object obj, string name) => obj.GetType().GetProperty(name)!.GetValue(obj)!;
        void Check(bool pass, string name)
        {
            if (!pass) throw new Exception(name);
            Console.WriteLine("PASS " + name);
        }
        var plan = Find()!;
        var attack = (AttackActionDto)Property(Property(plan, "First"), "Data");
        Check((bool)Property(plan, "ProjectsLethal") && attack.AttackerUid == weak.InstanceId && attack.TargetUid == ward.InstanceId,
            "clear Ward with the small follower before lethal");
        Check(allies.Field.Count == 2 && foes.Field.Count == 1 && foes.LeaderCard!.CurrentHealth == 5, "simulation preserves live snapshot");
        foes.Field.Clear();
        allies.Field.Clear();
        allies.Field.Add(weak with { CanEvolve = true, AttacksThisTurn = 1 });
        allies.CanSuperEvolve = true;
        allies.CanEvolve = true;
        allies.CurrentEP = 1;
        allies.CurrentSEP = 1;
        plan = Find()!;
        Check((string)Property(Property(plan, "First"), "Type") == "PerformEvolve", "prefer EP to SEP for a small uncontested follower");
        Check((double)Property(plan, "Gain") < 0, "save EP on an uncontested body");
        allies.Field.Clear();
        Check(Find() == null, "empty board has no combat plan");


        var storm = Card(me, 5, 2, CardType.Follower, CardKeyword.Storm) with { Zone = CardZone.Hand, CurrentCost = 3 };
        allies.Hand.Add(storm);
        allies.CurrentPP = 3;
        var searchType = find.DeclaringType!;
        var moveType = searchType.GetNestedType("Move", BindingFlags.NonPublic)!;
        var stormMove = Activator.CreateInstance(moveType, new object?[] { "PlayCard", new PlayCardActionDto { CardUid = storm.InstanceId }, storm, null });
        var moves = Array.CreateInstance(moveType, 1);
        moves.SetValue(stormMove, 0);
        object? StormPlan() => find.Invoke(null, new object[] { game, me, default(System.Threading.CancellationToken), moves, null! });
        plan = StormPlan()!;
        Check((bool)Property(plan, "ProjectsLethal") && (string)Property(Property(plan, "First"), "Type") == "PlayCard", "storm lethal from hand");
        Check(allies.CurrentPP == 3 && allies.Hand.Count == 1 && allies.Field.Count == 0, "storm simulation preserves hand and PP");
        allies.CurrentPP = 2;
        Check(StormPlan() == null, "unaffordable storm is excluded");
        allies.PpBoostUsesAvailable = 1;
        allies.IsPpBoostArmed = true;
        Check((bool)Property(StormPlan()!, "ProjectsLethal"), "armed PP boost can fund a Storm lethal");
        Check(allies.IsPpBoostArmed && allies.PpBoostUsesAvailable == 1, "search does not spend live PP boost");
        allies.IsPpBoostArmed = false;
        var boost = typeof(GameStateDto).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTurnExecutor")!
            .GetMethod("ShouldArmBoost", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool Arm() => (bool)boost.Invoke(null, new object[] { allies, foes })!;
        Check(Arm(), "PP boost can enable a decisive follower");
        allies.Hand[0] = storm with { CurrentAttack = 1, CurrentHealth = 1, Keywords = new() };
        Check(!Arm(), "save PP boost instead of developing a weak body");

        allies.Field.Add(weak with { CanEvolve = true, AttacksThisTurn = 1 });
        var effects = new Dictionary<Guid, (double Evolve, double SuperEvolve)> { [weak.InstanceId] = (0, 3) };
        plan = find.Invoke(null, new object[] { game, me, default(System.Threading.CancellationToken), null!, effects })!;
        Check((string)Property(Property(plan, "First"), "Type") == "PerformSuperEvolve", "evolution trigger can justify spending SEP");

        allies.Hand.Clear();
        allies.Field = new() { Card(me,3,6), Card(me,2,5), Card(me,1,3), Card(me,6,5), Card(me,7,3) };
        allies.Field = allies.Field.Select(card => card with { CanEvolve = true }).ToList();
        foes.LeaderCard = Card(enemy,0,8,CardType.Leader);
        foes.Field = new() { Card(enemy,4,3,CardType.Follower,CardKeyword.Ward),
            Card(enemy,2,2,CardType.Follower,CardKeyword.Ward), Card(enemy,3,4,CardType.Follower,CardKeyword.Shield),
            Card(enemy,2,3), Card(enemy,7,6) };
        plan = Find()!;
        Check((bool)Property(plan, "ProjectsLethal"), "retain a lethal line through two Wards on a full board");

        allies.Field = new() { Card(me,2,6,CardType.Follower,CardKeyword.IgnoreWard),
            Card(me,5,4), Card(me,2,6), Card(me,2,3), Card(me,5,1) };
        allies.Field = allies.Field.Select(card => card with { CanEvolve = true }).ToList();
        foes.LeaderCard = Card(enemy,0,9,CardType.Leader);
        foes.Field = new() { Card(enemy,3,2,CardType.Follower,CardKeyword.Ward,CardKeyword.Shield),
            Card(enemy,7,6), Card(enemy,2,2), Card(enemy,3,6,CardType.Follower,CardKeyword.Shield), Card(enemy,1,1) };
        plan = Find()!;
        Check((bool)Property(plan, "ProjectsLethal"), "recover a shielded Ward line with the board-ranked fallback");

        var estimate = searchType.GetMethod("EstimateBoardDamage", BindingFlags.Static | BindingFlags.NonPublic)!;
        int Threat(CardInstanceDto attacker, CardInstanceDto defender) =>
            (int)estimate.Invoke(null, new object[] { new[] { attacker }, new[] { defender } })!;
        var blocker = Card(me,0,1,CardType.Follower,CardKeyword.Ward);
        var large = Card(enemy,10,10);
        Check(Threat(large, blocker) == 0, "one Ward consumes one complete attack");
        Check(Threat(large with { Keywords = new() { CardKeyword.IgnoreWard } }, blocker) == 10, "Ward does not stop a bypassing attacker");
        Check(Threat(large with { Keywords = new() { CardKeyword.Shackle } }, blocker) == 0, "Shackled attackers do not add next-turn damage");
        Check(Threat(large with { MaxAttacksPerTurn = 2 }, blocker) == 10, "remaining attack still reaches the leader");
    }

    internal static void Targets()
    {
        var type = typeof(GameStateDto).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTargetScoring")!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var find = type.GetMethod("FindEffects", flags)!;
        var score = type.GetMethod("Score", flags)!;
        var me = Guid.NewGuid();
        var foe = Guid.NewGuid();
        CardInstanceDto Card(Guid owner, int attack, int health, CardType kind = CardType.Follower) =>
            new() { OwnerPlayerId=owner, Type=kind, CurrentAttack=attack, CurrentHealth=health, MaxHealth=kind==CardType.Leader?20:health };
        object Effects(params EffectConfig[] effects) => find.Invoke(null, new object[] { effects, "target1" })!;
        double Score(CardInstanceDto card, object effects) => (double)score.Invoke(null, new[] { card, (object)me, effects })!;
        EffectConfig Amount(EffectType effect, string name, int value) =>
            new() { Type=effect, TargetSelector=new() { "ResolvedTargets.target1" }, Parameters=new() { [name]=new StaticValueConfig { Value=value } } };
        void Check(bool result, string label)
        {
            if (!result) throw new Exception(label);
            Console.WriteLine("PASS " + label);
        }
        var damage = Effects(Amount(EffectType.Damage, "DamageAmount", 3));
        Check(Score(Card(foe,0,3,CardType.Leader),damage)>Score(Card(foe,8,3),damage),"lethal damage goes face");
        Check(Score(Card(foe,5,3),damage)>Score(Card(foe,8,4),damage),"damage clears a threat rather than chipping a larger unit");
        Check(Score(Card(foe,2,3),damage)>Score(Card(me,7,3),damage),"damage prefers an enemy");
        Check(Score(Card(foe,2,3),damage)>Score(Card(foe,7,1) with { Keywords=new() { CardKeyword.Shield } },damage),"shield prevents projected damage");
        var heal = Effects(Amount(EffectType.Heal, "HealAmount", 3));
        Check(Score(Card(me,0,15,CardType.Leader),heal)>Score(Card(me,0,20,CardType.Leader),heal),"heal prefers a wounded ally");
        var other = new EffectConfig { Type=EffectType.Destroy, TargetSelector=new() { "target2" } };
        var combined = Effects(other, Amount(EffectType.Damage, "DamageAmount", 3));
        Check(Score(Card(foe,8,4),combined)==Score(Card(foe,8,4),damage),"different target pools are scored independently");
        var destroy = Effects(new EffectConfig { Type=EffectType.Destroy });
        Check(Score(Card(foe,4,4),destroy)>Score(Card(foe,9,9) with { Keywords=new() { CardKeyword.DestroyResist } },destroy),"destroy respects protection");
        var unsupported = Effects(new EffectConfig { Type=EffectType.Transform });
        Check(!((IEnumerable)unsupported).GetEnumerator().MoveNext(),"unclassified effects fall back to model");
    }

    internal static void Abilities()
    {
        var type = typeof(GameStateDto).Assembly.GetType("ShadowverseBeyond.Server.Services.AiAbilityScoring")!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var score = type.GetMethod("Score", flags)!;
        var material = type.GetMethod("MaterialValue", flags)!;
        var owner = Guid.NewGuid();
        var player = new PlayerStateDto { PlayerId=owner, DeckCount=20, CurrentPP=4, MaxPP=4 };
        var normal = new CardInstanceDto { CardId=1, CurrentCost=4, Type=CardType.Follower, CurrentAttack=3, CurrentHealth=3 };
        var spare = normal with { CardId=2, CurrentCost=8 };
        var storm = normal with { CardId=3, Keywords=new() { CardKeyword.Storm } };
        player.Hand.AddRange(new[] { normal, spare, spare, storm });
        double Score(EffectType effect, int cost=0, bool fusion=false, params CardInstanceDto[] targets) =>
            (double)score.Invoke(null,new object?[] { player,owner,normal,fusion,cost,new[] { new EffectConfig { Type=effect } },targets,null })!;
        double Material(CardInstanceDto card) => (double)material.Invoke(null,new object[] { player,card })!;
        void Check(bool result, string label)
        {
            if (!result) throw new Exception(label);
            Console.WriteLine("PASS " + label);
        }
        Check(Material(spare)<Material(normal) && Material(normal)<Material(storm),"fusion protects playable cards and Storm");
        Check(Score(EffectType.Fusion,0,true,spare)>Score(EffectType.Fusion,0,true,storm),"fusion considers the actual material pool");
        Check(Score(EffectType.Fusion,0,true,storm)<0,"skip low-value fusion that consumes Storm");
        Check(Score(EffectType.Draw,2)>Score(EffectType.None,0),"useful paid draw can outrank a free opaque action");
        for (var i=0;i<5;i++) player.Hand.Add(normal);
        Check(Score(EffectType.Draw,2)<0,"avoid paid draw into a full hand");
        for (var i=0;i<5;i++) player.Field.Add(normal);
        Check(Score(EffectType.Summon,1)<0,"avoid paid summon into a full board");
    }

}
