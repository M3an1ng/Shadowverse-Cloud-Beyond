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
        var generatedLethal = find.DeclaringType!.GetMethod("CanProjectGeneratedStormLethal", BindingFlags.Static | BindingFlags.NonPublic)!;
        var ghostSide = allies with { CurrentPP = 5, Field = new(), Hand = new() { Card(me, 0, 0, CardType.Spell) } };
        var ghostEnemy = foes with { LeaderCard = theirLeader with { CurrentHealth = 3 }, Field = new() };
        bool GhostLethal(PlayerStateDto own, PlayerStateDto other) =>
            (bool)generatedLethal.Invoke(null, [own, other, 2, 3, 3, 1, 1])!;
        Check(GhostLethal(ghostSide, ghostEnemy), "find three generated Storm followers as lethal");
        Check(!GhostLethal(ghostSide with { CurrentPP = 4 }, ghostEnemy), "generated Storm requires PP for the spell and tokens");
        Check(!GhostLethal(ghostSide with { LeaderCard = myLeader with { CurrentHealth = 3 } }, ghostEnemy), "reject lethal self damage");
        Check(!GhostLethal(ghostSide with { Field = new() { strong, weak, Card(me, 1, 1) } }, ghostEnemy), "generated Storm requires field slots");
        Check(!GhostLethal(ghostSide with { Hand = Enumerable.Range(0, 8).Select(_ => Card(me, 0, 0, CardType.Spell)).ToList() }, ghostEnemy), "generated Storm requires hand slots");
        Check(!GhostLethal(ghostSide, ghostEnemy with { Field = new() { ward } }), "generated Storm does not ignore Ward");

        var executor = typeof(GameStateDto).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTurnExecutor")!;
        var threatened = executor.GetMethod("HasVisibleLethalThreat", BindingFlags.Static | BindingFlags.NonPublic)!;
        var atRisk = ghostSide with { LeaderCard = myLeader with { CurrentHealth = 5 } };
        var attackingEnemy = ghostEnemy with { Field = new() { Card(enemy, 5, 3) } };
        Check((bool)threatened.Invoke(null, [atRisk, attackingEnemy])!, "detect visible lethal before applying pressure advice");
        Check(!(bool)threatened.Invoke(null, [atRisk with { Field = new() { ward } }, attackingEnemy])!,
            "account for Ward before declaring defensive emergency");
        var defender = Card(me, 5, 1);
        var threatening = attackingEnemy.Field[0];
        var danger = game with
        {
            Players = new() { atRisk with { Field = new() { defender } },
                attackingEnemy with { LeaderCard = theirLeader with { CurrentHealth = 20 } } }
        };
        var defense = find.Invoke(null, [danger, me, CancellationToken.None, null, null])!;
        var defensiveAttack = (AttackActionDto)Property(Property(defense, "First"), "Data");
        Check((bool)Property(defense, "PreventsVisibleLethal") && defensiveAttack.TargetUid == threatening.InstanceId,
            "prefer a real trade that prevents visible lethal");
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

        var advance = Card(me, 3, 3) with { CanEvolve = true, ZoneStayTurn = 0 };
        allies.Field = [advance]; allies.Hand.Clear(); allies.CurrentPP = 0;
        allies.CurrentEP = allies.CurrentSEP = 1; allies.CanEvolve = allies.CanSuperEvolve = true;
        foes.Field = [Card(enemy, 4, 5)]; foes.LeaderCard = theirLeader with { CurrentHealth = 20 };
        plan = Find()!;
        Check((string)Property(Property(plan, "First"), "Type") == "PerformEvolve",
            "use EP when normal evolution already clears the same threat and survives");
        var incidentalSuperEffect = new Dictionary<Guid, (double Evolve, double SuperEvolve)>
            { [advance.InstanceId] = (2, 4) };
        plan = find.Invoke(null, [game, me, CancellationToken.None, null, incidentalSuperEffect])!;
        var epOnly = game with { Players = game.Players.Select(player => player.PlayerId == me
            ? player with { CanSuperEvolve = false } : player).ToList() };
        var epOnlyPlan = find.Invoke(null, [epOnly, me, CancellationToken.None, null, incidentalSuperEffect])!;
        Check((double)Property(epOnlyPlan, "Gain") > 0, "ordinary evolution has useful gain in the resource comparison");
        Check((string)Property(Property(plan, "First"), "Type") == "PerformEvolve",
            $"an additional modest SEP trigger does not bypass the EP-versus-SEP comparison: {Property(Property(plan, "First"), "Type")} gain={Property(plan, "Gain")}");
        foes.Field.Clear(); foes.LeaderCard = theirLeader with { CurrentHealth = 6 };
        allies.Field = [advance with { ZoneStayTurn = 2 }];
        plan = Find()!;
        Check((bool)Property(plan, "ProjectsLethal") && (bool)Property(plan, "SpendsSep"),
            "allow SEP when EP cannot reach lethal");

        var endingType = searchType.GetNestedType("EndTurnForecast", BindingFlags.NonPublic)!;
        var pendingType = searchType.GetNestedType("PendingDamage", BindingFlags.NonPublic)!;
        var withEnding = searchType.GetMethod("FindWithEndTurn", BindingFlags.Static | BindingFlags.NonPublic)!;
        var waiter = Card(me, 2, 4) with { CanAttack = false };
        var fox = Card(me, 3, 2, CardType.Follower, CardKeyword.Storm);
        var sweptWard = Card(enemy, 3, 3, CardType.Follower, CardKeyword.Ward);
        allies.Field = [waiter, fox]; allies.Hand.Clear(); allies.CanEvolve = allies.CanSuperEvolve = false;
        foes.Field = [sweptWard]; foes.LeaderCard = theirLeader with { CurrentHealth = 20 };
        object Ending(params Guid[] unsafeAttackers)
        {
            var hits = Array.CreateInstance(pendingType, 1);
            hits.SetValue(Activator.CreateInstance(pendingType, [waiter.InstanceId,
                new[] { sweptWard.InstanceId, foes.LeaderCard!.InstanceId }, 3, 0]), 0);
            return Activator.CreateInstance(endingType, [hits, new HashSet<Guid>(unsafeAttackers)])!;
        }
        object? EndPlan(object ending) => withEnding.Invoke(null, [game, me, CancellationToken.None, null, null, ending]);
        plan = EndPlan(Ending(fox.InstanceId))!;
        Check(plan == null || (double)Property(plan, "Gain") <= 0,
            "do not sacrifice a Storm fox to clear a follower already dying at turn end");
        foes.Field = [sweptWard with { Keywords = [], CurrentAttack = 2 }];
        allies.Field = [waiter, fox with { CardId = 1474114, Name = "醐醐狐" }];
        plan = EndPlan(Ending())!;
        var foxAttack = (AttackActionDto)Property(Property(plan, "First"), "Data");
        Check(foxAttack.TargetUid == foes.LeaderCard!.InstanceId,
            "醐醐狐 attacks face while 款待者 clears the three-health follower at turn end");
        foes.LeaderCard = foes.LeaderCard with { CurrentHealth = 3 };
        plan = EndPlan(Ending(fox.InstanceId))!;
        Check((bool)Property(plan, "ProjectsLethal") && (string)Property(Property(plan, "First"), "Type") == "EndTurn",
            "end turn for a guaranteed empty-hand lethal instead of making a hand-changing attack");
        foes.LeaderCard = foes.LeaderCard with { CurrentHealth = 20 };
        foes.Field = [sweptWard with { CurrentHealth = 4, CurrentAttack = 1 }];
        allies.Field = [waiter, weak with { CurrentAttack = 1, CurrentHealth = 5 }];
        plan = EndPlan(Ending())!;
        var chip = (AttackActionDto)Property(Property(plan, "First"), "Data");
        Check((double)Property(plan, "Gain") > 0 && chip.TargetUid == sweptWard.InstanceId,
            "chip a four-health threat so the end-turn sweep can finish it");
        Check(foes.Field[0].CurrentHealth == 4 && allies.Hand.Count == 0,
            "end-turn forecast does not mutate the real board or hand");
        foes.LeaderCard = foes.LeaderCard with { CurrentHealth = 3 };
        allies.Hand = [storm];
        plan = EndPlan(Ending())!;
        Check(plan == null || !(bool)Property(plan, "ProjectsLethal"),
            "do not reuse an empty-hand sweep after the hand condition changes");

        var raceStorm = Card(me, 3, 3, CardType.Follower, CardKeyword.Storm) with { ZoneStayTurn = 0 };
        var harmless = Card(enemy, 1, 1);
        object RacePlan(int ownHp, params CardInstanceDto[] enemies)
        {
            var race = game with { Players = [
                new() { PlayerId = me, LeaderCard = myLeader with { CurrentHealth = ownHp }, Field = [raceStorm] },
                new() { PlayerId = enemy, LeaderCard = theirLeader with { CurrentHealth = 20 }, Field = enemies.ToList() }] };
            return find.Invoke(null, [race, me, CancellationToken.None, null, null])!;
        }
        AttackActionDto RaceAttack(object proposal) => (AttackActionDto)Property(Property(proposal, "First"), "Data");
        Check(RaceAttack(RacePlan(20, harmless)).TargetUid == theirLeader.InstanceId,
            "healthy Storm attacker ignores a harmless one-one and pressures the leader");
        Check(RaceAttack(RacePlan(20, harmless with { CurrentAttack = 2, CurrentHealth = 2 })).TargetUid == theirLeader.InstanceId,
            "healthy Storm attacker does not spend its face damage clearing a minor two-two");
        Check(RaceAttack(RacePlan(20, harmless with { CurrentAttack = 2, CurrentHealth = 3 })).TargetUid == theirLeader.InstanceId,
            "one ordinary two-three can be left alive while Storm pressures the leader");
        Check(RaceAttack(RacePlan(20, harmless with { EffectDescriptions = ["获得疾驰"], Keywords = [CardKeyword.Storm] })).TargetUid == theirLeader.InstanceId,
            "a keyword-only attached description is not mistaken for a recurring resource engine");
        Check(RaceAttack(RacePlan(2, harmless)).TargetUid == harmless.InstanceId,
            "low-health defense can still clear the small follower");
        Check(RaceAttack(RacePlan(20, harmless with { Keywords = [CardKeyword.Ward] })).TargetUid == harmless.InstanceId,
            "Storm still clears Ward before hitting face");
        Check(RaceAttack(RacePlan(20, harmless with { ActAbilities = [new(){ Name = "每回合增加资源" }] })).TargetUid == harmless.InstanceId,
            "a small resource engine is not treated as a harmless body");
        var intentSearch = searchType.GetMethod("FindWithIntent", BindingFlags.Static | BindingFlags.NonPublic)!;
        var intentBoard = game with { Players = [
            new() { PlayerId = me, LeaderCard = myLeader, Field = [raceStorm with { CurrentAttack = 6, CurrentHealth = 6 }] },
            new() { PlayerId = enemy, LeaderCard = theirLeader with { CurrentHealth = 20 }, Field = [harmless with { CurrentAttack = 3, CurrentHealth = 3 }] }] };
        var localAttack = RaceAttack(find.Invoke(null, [intentBoard, me, CancellationToken.None, null, null])!);
        var pressureAttack = RaceAttack(intentSearch.Invoke(null, [intentBoard, me, CancellationToken.None, null, null, null, "pressure"])!);
        Check(localAttack.TargetUid == harmless.InstanceId && pressureAttack.TargetUid == theirLeader.InstanceId,
            "model pressure intent can change a close trade-versus-face decision");
        intentBoard.Players[0].LeaderCard = myLeader with { CurrentHealth = 3 };
        var pressuredDefense = intentSearch.Invoke(null, [intentBoard, me, CancellationToken.None, null, null, null, "pressure"])!;
        Check((bool)Property(pressuredDefense, "PreventsVisibleLethal") && RaceAttack(pressuredDefense).TargetUid == harmless.InstanceId,
            "model pressure intent cannot override preventing visible lethal");
        var horde = Enumerable.Range(0,5).Select(_=>Card(enemy,2,2)).ToArray();
        Check(RaceAttack(RacePlan(12, horde)).TargetUid != theirLeader.InstanceId,
            "several small followers remain a real board threat");

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
        var discard = Effects(new EffectConfig { Type=EffectType.Discard, TargetSelector=new() { "target1" } });
        Check(((IEnumerable)discard).GetEnumerator().MoveNext(), "discard targets use material valuation rather than random target selection");
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
        var priority = type.GetMethod("FragileHandPriority", flags)!;
        var fleetingReiss = normal with { CardId = 1474120, EffectDescriptions = ["于手牌中发动。当自己使用其他卡片时，舍弃这张卡片"] };
        var fleetingOther = fleetingReiss with { CardId = 1474121 };
        double Priority(CardInstanceDto card) => (double)priority.Invoke(null, new object[] { player, card })!;
        if (Priority(fleetingReiss) <= Priority(fleetingOther) || Priority(normal) != 0)
            throw new Exception("temporary searched Reiss lost sequence priority or ordinary cards were boosted");
        var namedRule = fleetingReiss with
        {
            EffectDescriptions = [],
            BuffSummaries = [new() { GrantedAbilities = [new() { Name = "于手牌中发动。当自己使用其他卡片时，舍弃这张卡片", Description = "" }] }]
        };
        if (Priority(namedRule) != Priority(fleetingReiss))
            throw new Exception("name-only granted self-discard rule was ignored");
        player.Emblem.Add(new() { CardId = 1474276 });
        if (Priority(fleetingReiss) != Priority(fleetingOther))
            throw new Exception("already-established Reiss retained special sequence priority");
        player.Emblem.Clear();
        var reiss = normal with { CardId = 1474120, CurrentCost = 2, CurrentAttack = 2, CurrentHealth = 2 };
        var baseReiss = reiss with { CardId = 1474121 };
        var reissValue = (double)material.Invoke(null, new object[] { player, reiss })!;
        var ordinaryValue = (double)material.Invoke(null, new object[] { player, baseReiss })!;
        if (reissValue <= ordinaryValue) throw new Exception("first affordable Reiss engine was treated as ordinary discard fodder");
        player.Emblem.Add(new() { CardId = 1474276 });
        if ((double)material.Invoke(null, new object[] { player, reiss })! != ordinaryValue)
            throw new Exception("Reiss engine retained setup priority after the emblem was established");
        player.Emblem.Clear();

        double Score(EffectType effect, int cost=0, bool fusion=false, params CardInstanceDto[] targets) =>
            (double)score.Invoke(null,new object?[] { player,owner,normal,fusion,cost,new[] { new EffectConfig { Type=effect } },targets,null })!;
        double Material(CardInstanceDto card) => (double)material.Invoke(null,new object[] { player,card })!;
        void Check(bool result, string label)
        {
            if (!result) throw new Exception(label);
            Console.WriteLine("PASS " + label);
        }
        var plannedMaterial = type.GetMethod("PlannedMaterialValue", flags)!;
        var materialBias = type.GetMethod("MaterialBias", flags)!;
        var setup = spare with { CardId = 11 };
        var fodder = setup with { CardId = 12 };
        var materialPlayer = player with { Hand = [setup, fodder] };
        var retain = new ShadowverseBeyond.Server.Services.AiStrategyPlanner.TurnPlan("setup", "keep setup for next turn", [], [11]);
        double Planned(CardInstanceDto card, ShadowverseBeyond.Server.Services.AiStrategyPlanner.TurnPlan? current) =>
            (double)plannedMaterial.Invoke(null, [materialPlayer, card, current])!;
        Check(Planned(setup, null) == Planned(fodder, null) && Planned(setup, retain) > Planned(fodder, retain),
            "a valid keep plan did not influence discard and fusion material ordering");
        Check((double)materialBias.Invoke(null, [materialPlayer, new[] { setup }, retain])! < 0
            && (double)materialBias.Invoke(null, [materialPlayer, new[] { setup, fodder }, retain])! == 0,
            "fusion ignored its actual material pool or penalized retention when expendable material remained");
        var singleton = Planned(setup, retain) - Planned(setup, null);
        materialPlayer = materialPlayer with { Hand = [setup, setup, fodder] };
        Check(Planned(setup, retain) - Planned(setup, null) < singleton,
            "duplicate combo pieces were all protected as unique essentials");
        Check(Planned(setup, retain) - Planned(setup, null) <= 1.25,
            "model retention overrode local material valuation with an unbounded bias");
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
