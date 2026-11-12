using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiLessonRetrievalTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static readonly Type Retrieval=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiLessonRetrieval")!;
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static AiDefinitionStamp Rules()=> (AiDefinitionStamp)typeof(AiStrategyPlanner).Assembly
        .GetType("ShadowverseBeyond.Server.Services.AiDefinitionRules")!.GetMethod("Read",Hidden)!.Invoke(null,[GameDataProvider.Instance])!;
    private static AiStrategyPlanner.Lesson[] Fallback(AiStrategyPlanner.Lesson[] lessons,string opponent)=>
        (AiStrategyPlanner.Lesson[])typeof(AiStrategyPlanner).GetMethod("SelectLessons",Hidden)!.Invoke(null,[lessons,opponent])!;
    private static (AiStrategyPlanner.Lesson[] Lessons,AiExperienceRetrievalFact Fact) Select(AiStrategyPlanner.Lesson[] history,
        AiDefinitionStamp rules,GameStateDto game,int[]? focus=null,string decision="test")
    {
        var result=Retrieval.GetMethod("Select",Hidden)!.Invoke(null,[history,"Neutral",rules,game,game.Players[0].PlayerId,
            (Func<AiStrategyPlanner.Lesson[],string,AiStrategyPlanner.Lesson[]>)Fallback,decision,focus])!;
        return ((AiStrategyPlanner.Lesson[])result.GetType().GetProperty("Lessons")!.GetValue(result)!,
            (AiExperienceRetrievalFact)result.GetType().GetProperty("Fact")!.GetValue(result)!);
    }
    private static AiObservedResources Before()=>new(16,12,3,3,1,1,true,true,false,2,2,2,1,1,0,0,"own",true,true);
    private static AiStrategyPlanner.Lesson Lesson(AiDefinitionStamp rules,int card,int sequence)=>
        new("Neutral",sequence%2==0,sequence%3==0,8,"fixture",[],[card],DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            Attacks:[new(3,card,6,false,true,2,1,1,16,12,1)],DefinitionRules:rules,
            CompletedActions:[new(3,new("PerformAttack",card,6,null,0),Before(),Before(),new(card,"Field",2,2),new(card,"Field",2,2),
                new(6,"Field",1,1),null,"not-ended")]);
    private static GameStateDto Snapshot(Guid actor,Guid enemy)=>new(){CurrentPlayerId=actor,Players=[
        new(){PlayerId=actor,TurnNumber=3,CurrentPP=3,CurrentEP=1,CurrentSEP=1,CanEvolve=true,CanSuperEvolve=true,
            LeaderCard=new(){CardId=1,CurrentHealth=16,Type=CardType.Leader},Hand=[new(){CardId=2,Type=CardType.Spell,InstanceId=Guid.NewGuid()},new(){CardId=3,Type=CardType.Spell,InstanceId=Guid.NewGuid()}]},
        new(){PlayerId=enemy,LeaderCard=new(){CardId=1,CurrentHealth=12,Type=CardType.Leader},Hand=[new(){CardId=404,Name="PRIVATE_HIDDEN_CANARY",InstanceId=Guid.NewGuid()}]}]};

    internal static void RankingAndBoundaries()
    {
        var rules=new AiDefinitionStamp(new string('a',64),new string('b',64),3,0,0,true,[]);
        var game=Snapshot(Guid.NewGuid(),Guid.NewGuid());
        var relevant=Lesson(rules,2,0);var history=new[]{relevant}.Concat(Enumerable.Range(1,16).Select(index=>Lesson(rules,100+index,index))).ToArray();
        Check(!Fallback(history,"Neutral").Contains(relevant),"fixture did not place relevant evidence outside the old request");
        var selected=Select(history,rules,game);
        Check(selected.Lessons.Length==8&&selected.Lessons.Contains(relevant),"older relevant native observation was not retrieved");
        var match=selected.Fact.Matches.Single(item=>item.OwnCardIds.Contains(2));
        Check(ReferenceEquals(selected.Lessons[match.LessonIndex],relevant)&&match.HistoryIndexAtRequest==0&&match.Evidence=="completedActions"
            &&match.EvidenceIndex==0&&match.ResourceChecksKnown>0,"retrieval provenance does not resolve to the selected observed action");
        var flipped=history.Select(lesson=>lesson with{Won=!lesson.Won,Plans=["UNTRUSTED_PROPOSAL #2"],ProposedTactics=[new(3,0,JsonSerializer.SerializeToElement(new{cardId=2}),"proposed",null)]}).ToArray();
        var after=Select(flipped,rules,game);
        Check(selected.Fact.Matches.Select(item=>item.HistoryIndexAtRequest).SequenceEqual(after.Fact.Matches.Select(item=>item.HistoryIndexAtRequest)),"win/loss or unexecuted advice affected relevance scoring");
        var armed=game with{Players=[game.Players[0] with{IsPpBoostArmed=true,PpBoostUsesAvailable=1},game.Players[1]]};
        var matchingBoost=relevant with{CompletedActions=[relevant.CompletedActions![0] with{Before=Before() with{BoostArmed=true,BoostRemaining=1}}],Attacks=[]};
        var wrongBoost=relevant with{FinishedAt=DateTimeOffset.UtcNow,Attacks=[]};
        Check(ReferenceEquals(Select([matchingBoost,wrongBoost],rules,armed).Lessons[0],matchingBoost),"PP+1 readiness and remaining uses did not distinguish similar positions");
        var enemyTurn=game with{CurrentPlayerId=game.Players[1].PlayerId};
        var matchingTurn=relevant with{CompletedActions=[relevant.CompletedActions![0] with{Before=Before() with{ActiveSide="enemy"}}],Attacks=[]};
        Check(ReferenceEquals(Select([matchingTurn,wrongBoost],rules,enemyTurn).Lessons[0],matchingTurn),"opponent-turn choices reused an own-turn position without distinguishing it");
        var hiddenOnly=Lesson(rules,900,100) with{CompletedActions=[new(3,new("PerformAttack",900,404,null,0),Before(),Before(),new(900,"Field",2,2),null,new(404,"Field",1,1),null,"not-ended")],Attacks=[]};
        Check(Select([hiddenOnly],rules,game).Fact.RelevantSelected==0,"enemy hidden hand influenced retrieval");
        var future=game.Players[1] with{Deck=[new(){CardId=900}],Graveyard=[new(){CardId=900}],Extra=[new(){CardId=900}]};
        Check(Select([hiddenOnly],rules,game with{Players=[game.Players[0],future]}).Fact.RelevantSelected==0,"enemy nonpublic zones influenced retrieval");
        var current=Enumerable.Range(1,9).Select(index=>Lesson(rules,100+index,index)).ToArray();
        var stale=relevant with{DefinitionRules=rules with{Fingerprint="old-rules"}};
        Check(!Select(current.Append(stale).ToArray(),rules,game).Lessons.Contains(stale),"similar old rule data displaced available current observations");
        var onlyOld=Select([stale],rules,game);
        Check(onlyOld.Fact.Matches.Single().RuleCompatibility!="same-config-and-engine"&&onlyOld.Fact.CompatibleSelected==0,"fallback hid rule incompatibility");
        var unrelated=Lesson(rules,777,0) with{Attacks=[],CompletedActions=null,Plans=["#2"],Played=[2]};
        Check(Select([unrelated],rules,game).Fact.RelevantSelected==0,"generic played ID or prose became a detailed situational observation");
        var focused=Select([Lesson(rules,777,0)],rules,game,[777]);
        Check(focused.Fact.Matches.Single().FocusCardIds.SequenceEqual(new[]{777}),"current legal option did not contribute to retrieval");
        var richBefore=Before() with{BloodPool=2,OwnEmblemIds=[],EnemyEmblemIds=[]};
        var rich=relevant with{CompletedActions=[relevant.CompletedActions![0] with{Before=richBefore}],Attacks=[]};
        var resourceOwn=game.Players[0] with{BloodPool=2};var resourceGame=game with{Players=[resourceOwn,game.Players[1]]};
        var wrongBlood=rich with{FinishedAt=DateTimeOffset.MaxValue,CompletedActions=[rich.CompletedActions![0] with{Before=richBefore with{BloodPool=1}}]};
        Check(ReferenceEquals(Select([rich,wrongBlood],rules,resourceGame).Lessons[0],rich),"fresh wrong-blood example displaced matching earlier resources");
        var wrongBloodFact=Select([wrongBlood],rules,resourceGame).Fact.Matches.Single();Check(wrongBloodFact.ResourceMismatches.Contains("own-blood-pool"),"blood mismatch not explained");
        var emblem=new CardInstanceDto{CardId=3,OwnerPlayerId=resourceOwn.PlayerId,Zone=CardZone.Emblem,Type=CardType.Amulet};
        var withEmblem=resourceGame with{Players=[resourceOwn with{Emblem=[emblem]},game.Players[1]]};
        var matchesEmblem=rich with{CompletedActions=[rich.CompletedActions![0] with{Before=richBefore with{OwnEmblemIds=[3]}}]};
        Check(ReferenceEquals(Select([matchesEmblem,rich],rules,withEmblem).Lessons[0],matchesEmblem),"engine emblem presence did not affect similarity");
        var incomplete=matchesEmblem with{CompletedActions=[matchesEmblem.CompletedActions![0] with{Before=richBefore with{OwnEmblemIds=[3],OwnEmblemsOmitted=1}}]};
        Check(Select([incomplete],rules,withEmblem).Fact.Matches.Single().ResourceUnknown.Contains("own-emblems"),"truncated emblems were compared as a complete registry");
        var oldUnknown=Select([relevant with{Attacks=[]}],rules,resourceGame).Fact.Matches.Single();Check(oldUnknown.ResourceUnknown.Contains("own-blood-pool")&&!oldUnknown.ResourceMismatches.Contains("own-blood-pool"),"legacy missing blood treated as recorded zero");
        Check(Select([],rules,game).Lessons.Length==0&&Select([],rules,game).Fact.Available==0,"empty history invented observations");
    }

    internal static void DrawSourcePlanning()
    {
        const int crest=1474276, field=1474115;
        var prior=GameDataProvider.Instance;
        var folder=Path.Combine(Path.GetTempPath(),"ai-refill-experience-"+Guid.NewGuid().ToString("N"));
        var definitions=new Dictionary<int,CardDefinition>
        {
            [2]=new(){Id=2,Name="first legal card",Type=CardType.Spell,Cost=1},
            [3]=new(){Id=3,Name="second legal card",Type=CardType.Spell,Cost=1},
            [crest]=new(){Id=crest,Name="refill crest",Type=CardType.Amulet},
            [field]=new(){Id=field,Name="field draw",Type=CardType.Follower}
        };
        var revision=Guid.NewGuid();
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch
        {
            "get_DataRevision"=>revision,
            "GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),
            "GetAllCardDefinitions"=>definitions,
            "GetAbilityConfigs"=>new List<AbilityConfig>(),
            "GetAllAbilityConfigs"=>definitions.Keys.ToDictionary(id=>id,id=>new List<AbilityConfig>()),
            "GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),
            _=>null
        });
        try
        {
            GameDataProvider.SetInstanceForTesting(data);
            var rules=Rules();
            Check(rules.Available,"draw fixture rules unavailable");
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=3,InitialDeckDefinitionIds=[2,3]};
            var enemy=new PlayerState(Guid.NewGuid());
            var game=new GameState("refill-experience",own,enemy,own.PlayerId);
            var snapshot=Snapshot(own.PlayerId,enemy.PlayerId);
            var visible=snapshot.Players[0];
            visible.Emblem=[new(){CardId=crest,OwnerPlayerId=own.PlayerId,Zone=CardZone.Emblem}];
            visible.Field=[new(){CardId=field,OwnerPlayerId=own.PlayerId,Zone=CardZone.Field}];
            visible.IsSuperEvolutionUnlocked=true;
            visible.DeckCount=3;
            visible.Deck=[new(){CardId=906,Name="PRIVATE_OWN_DECK_ORDER"}];
            var template=Lesson(rules,999,0);
            var before=Before() with {OwnHand=0,OwnEmblemIds=[crest]};
            var settled=template.CompletedActions![0] with
            {
                Action=new("EndTurn",null,null,null,0),ActorBefore=null,ActorAfter=null,TargetBefore=null,TargetAfter=null,
                Before=before,After=before with {OwnHand=5},
                EndTurnDraws=new([new(crest,4,4),new(field,1,1)],false,false){OwnDeckBefore=3}
            };
            var actions=Enumerable.Repeat(template.CompletedActions![0],32).ToArray();
            actions[15]=settled;
            var relevant=template with {CompletedActions=actions,Attacks=[]};
            var history=new[]{relevant}.Concat(Enumerable.Range(1,16).Select(index=>Lesson(rules,100+index,index))).ToArray();
            Check(!Fallback(history,"Neutral").Contains(relevant),"fixture lesson was already in the baseline");
            var result=Select(history,rules,snapshot,decision:"turn-plan");
            var match=result.Fact.Matches.Single();
            Check(match.Evidence=="completedActions" && match.EvidenceIndex==15 && match.DrawSourceIds!.SequenceEqual([crest,field])
                && match.ResourceMismatches.Contains("empty-hand-state") && !match.ResourceMismatches.Contains("own-deck-count"),
                "draw match lost its actual source, episode or empty-hand applicability");
            var noSources=snapshot with {Players=[visible with {Field=[],Emblem=[],Hand=[new(){CardId=crest}]},
                snapshot.Players[1] with {Field=[new(){CardId=field}]}]};
            Check(Select(history,rules,noSources,decision:"turn-plan").Fact.RelevantSelected==0,
                "hand or enemy source was treated as a current own public refill source");
            Check(Select(history,rules,snapshot,focus:[crest],decision:"discover").Fact.RelevantSelected==0,
                "end-turn evidence displaced discovery relevance");
            var changed=snapshot with {Players=[visible with {DeckCount=2,IsSuperEvolutionUnlocked=false},snapshot.Players[1]]};
            var mismatch=Select(history,rules,changed,decision:"turn-plan").Fact.Matches.Single();
            Check(mismatch.ResourceMismatches.Contains("own-deck-count") && mismatch.ResourceMismatches.Contains("super-evolution-unlocked"),
                "different deck size or unlock timing was hidden");
            var legacy=template with {Attacks=[],CompletedActions=[settled with {EndTurnDraws=settled.EndTurnDraws! with {OwnDeckBefore=null}}]};
            Check(Select([legacy],rules,snapshot,decision:"turn-plan").Fact.Matches.Single().ResourceUnknown.Contains("own-deck-count"),
                "missing deck count was treated as a known count");
            var unverified=legacy with {CompletedActions=[settled with {EndTurnDraws=new([new(null,4,4)],false,false)}]};
            Check(Select([unverified],rules,snapshot,decision:"turn-plan").Fact.RelevantSelected==0,
                "a draw with no verified source triggered retrieval");

            var terminal=settled with
            {
                Result="lost",After=before with {OwnHand=4},
                EndTurnDraws=new([new(crest,4,4),new(field,1,0){SourceEvidence="public-source-ability"}],true,false)
                {OwnDeckBefore=2,ExhaustedBatchIndex=1}
            };
            var terminalActions=actions.ToArray();
            terminalActions[15]=terminal;
            var terminalLesson=relevant with {CompletedActions=terminalActions};
            var terminalHistory=new[]{terminalLesson}.Concat(history.Skip(1)).ToArray();
            var sameDrawPosition=snapshot with {Players=[visible with {DeckCount=2},snapshot.Players[1]]};
            var missingSource=sameDrawPosition with {Players=[sameDrawPosition.Players[0] with {Field=[]},snapshot.Players[1]]};
            var terminalMatch=Select(terminalHistory,rules,sameDrawPosition,decision:"turn-plan").Fact.Matches.Single();
            Check(terminalMatch is {DrawDeckExhausted:true,ExhaustedDrawSourceId:field}
                && !terminalMatch.ResourceMismatches.Contains("exhausted-draw-source-missing"),
                "the terminal batch was not linked to its own public source");
            var missingMatch=Select(terminalHistory,rules,missingSource,decision:"turn-plan").Fact.Matches.Single();
            Check(missingMatch.ResourceMismatches.Contains("exhausted-draw-source-missing")
                && missingMatch.DrawSourceIds!.SequenceEqual([crest]),
                "the earlier successful draw hid the missing terminal source");
            var legacyTerminal=terminalLesson with {CompletedActions=[terminal with
                {EndTurnDraws=terminal.EndTurnDraws! with {ExhaustedBatchIndex=null}}]};
            var legacyMatch=Select([legacyTerminal],rules,sameDrawPosition,decision:"turn-plan").Fact.Matches.Single();
            Check(legacyMatch.DrawDeckExhausted==true && legacyMatch.ExhaustedDrawSourceId==null
                && legacyMatch.ResourceUnknown.Contains("exhausted-draw-source"),"legacy failure gained a guessed source");

            var deck=typeof(AiStrategyPlanner).GetMethod("ReadDeck",Hidden)!.Invoke(null,[game,own.PlayerId]);
            var key=(string)typeof(AiStrategyPlanner).GetMethod("DeckKey",Hidden)!.Invoke(null,[deck])!;
            foreach(var (lessons,position,exhaustion,sourceMissing,enabled) in new[]
            {
                (history,snapshot,false,false,false), (history,snapshot,false,false,true),
                (terminalHistory,sameDrawPosition,true,false,false), (terminalHistory,sameDrawPosition,true,false,true),
                (terminalHistory,missingSource,true,true,true)
            })
            {
                var memory=Path.Combine(folder,$"{exhaustion}-{sourceMissing}-{enabled}");
                Directory.CreateDirectory(memory);
                File.WriteAllText(Path.Combine(memory,key+".json"),JsonSerializer.Serialize(lessons,Json));
                using var provider=new Provider(drawEvidence:true,exhaustionEvidence:exhaustion);
                var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                {
                    ["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/offline",
                    ["AiStrategy:ApiKey"]="offline-only",["AiStrategy:Model"]="fixture",["AiStrategy:MemoryPath"]=memory,
                    ["AiStrategy:GuideCacheEnabled"]="false",["AiStrategy:SituationalExperienceEnabled"]=enabled.ToString()
                }).Build();
                using var planner=new AiStrategyPlanner(provider,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
                planner.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
                var plan=planner.PlanAsync(game,position,own.PlayerId,default,(id,_)=>Task.FromResult(new PlayabilityResultDto
                {
                    IsPlayable=visible.Hand.Any(card=>card.InstanceId==id),
                    SelectedPlayMode=PlayMode.Base,
                    PlayModes=[new(){Mode=PlayMode.Base,IsAvailable=true,EffectiveCost=1}]
                })).GetAwaiter().GetResult();
                var preferFirst=enabled && !sourceMissing;
                Check(plan?.Play?.CardId==(preferFirst?2:3) && plan.Play.HandIndex==(preferFirst?0:1) && plan.Play.Cost==1
                    && plan.GetType().GetProperty("PlayBinding",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plan)!=null,
                    "verified draw observation did not change the accepted and bound legal play");
                var input=provider.Inputs.Single(item=>item.TryGetProperty("legalPlays",out _));
                Check(provider.Inputs.Count==2 && input.GetProperty("lessons").GetArrayLength()==8
                    && !input.GetRawText().Contains("PRIVATE_"),"draw retrieval increased requests or leaked hidden data");
                if(!enabled)continue;
                var linked=input.GetProperty("lessonRetrieval").GetProperty("matches")[0];
                Check(!exhaustion || (linked.GetProperty("drawDeckExhausted").GetBoolean()
                    && linked.GetProperty("exhaustedDrawSourceId").GetInt32()==field),
                    "planner matched the older observation without its terminal draw evidence");
                var observed=input.GetProperty("lessons")[linked.GetProperty("lessonIndex").GetInt32()].GetProperty("observed");
                Check(observed.GetProperty("retrievedEvidence").GetProperty("included").GetBoolean()
                    && observed.GetProperty("completedActions").GetProperty("samples").GetArrayLength()==4,
                    "the matched middle action did not reach the bounded planner excerpt");
            }
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(prior);
            var resolved=Path.GetFullPath(folder);
            Check(resolved.StartsWith(Path.Combine(Path.GetTempPath(),"ai-refill-experience-"),StringComparison.OrdinalIgnoreCase),
                "unexpected refill fixture cleanup path");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }

    internal static void PlannerAndChoice(string? output=null)
    {
        var prior=GameDataProvider.Instance;var directory=Path.Combine(Path.GetTempPath(),"ai-situational-experience-"+Guid.NewGuid().ToString("N"));
        var results=new List<object>();
        try{
            var definitions=new Dictionary<int,CardDefinition>{[1]=new(){Id=1,Type=CardType.Leader,Name="leader",Health=20},
                [2]=new(){Id=2,Type=CardType.Spell,Name="resource core",Description="fixture",Cost=1},
                [3]=new(){Id=3,Type=CardType.Spell,Name="tempo alternative",Cost=1},[5]=new(){Id=5,Type=CardType.Spell,Name="third option",Cost=1}};
            var revision=Guid.NewGuid();var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
                "get_DataRevision"=>revision,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
                "GetAbilityConfigs"=>new List<AbilityConfig>(),"GetAllAbilityConfigs"=>definitions.Keys.ToDictionary(id=>id,id=>new List<AbilityConfig>()),
                "GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
            GameDataProvider.SetInstanceForTesting(data);var rules=Rules();Check(rules.Available,"fixture definitions unavailable");
            foreach(var enabled in new[]{false,true}){
                var folder=Path.Combine(directory,enabled?"contextual":"baseline");Directory.CreateDirectory(folder);
                using var provider=new Provider();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                    ["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/offline",["AiStrategy:ApiKey"]="offline-only",
                    ["AiStrategy:Model"]="fixture",["AiStrategy:MemoryPath"]=folder,["AiStrategy:GuideCacheEnabled"]="false",
                    ["AiStrategy:SituationalExperienceEnabled"]=enabled.ToString()}).Build();
                using var planner=new AiStrategyPlanner(provider,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
                var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=3,InitialDeckDefinitionIds=[2,3]};var enemy=new PlayerState(Guid.NewGuid());
                var game=new GameState("experience-"+enabled,own,enemy,own.PlayerId);var snapshot=Snapshot(own.PlayerId,enemy.PlayerId);
                var deck=typeof(AiStrategyPlanner).GetMethod("ReadDeck",Hidden)!.Invoke(null,[game,own.PlayerId]);
                var key=(string)typeof(AiStrategyPlanner).GetMethod("DeckKey",Hidden)!.Invoke(null,[deck])!;
                snapshot.Players[0].BloodPool=2;
                var resourceLesson=Lesson(rules,2,0);resourceLesson=resourceLesson with{CompletedActions=[resourceLesson.CompletedActions![0] with{Before=Before() with{BloodPool=2,OwnEmblemIds=[],EnemyEmblemIds=[]}}]};
                var lessons=new[]{resourceLesson}.Concat(Enumerable.Range(1,16).Select(index=>Lesson(rules,100+index,index))).ToArray();
                File.WriteAllText(Path.Combine(folder,key+".json"),JsonSerializer.Serialize(lessons,Json));
                planner.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
                var plan=planner.PlanAsync(game,snapshot,own.PlayerId,default,(_,_)=>Task.FromResult(new PlayabilityResultDto{IsPlayable=true})).GetAwaiter().GetResult();
                Check(plan?.Prioritize.Single()==(enabled?2:3),"history-sensitive offline response was not accepted by planning");
                if(enabled){snapshot.Players[0].BloodPool=1;snapshot.Players[0].Emblem=[new(){CardId=3,OwnerPlayerId=own.PlayerId,Zone=CardZone.Emblem,Type=CardType.Amulet}];}
                var options=new[]{2,3,5}.Select(id=>new CardInstanceDto{CardId=id,InstanceId=Guid.NewGuid(),Name=definitions[id].Name}).ToList();
                var request=new PlayerChoiceRequest{RequestingPlayerId=own.PlayerId,TargetKey="discover",MinSelectionCount=1,MaxSelectionCount=1,
                    SelectableTargetIds=options.Select(card=>card.InstanceId).ToList(),SelectableCardInstances=options};
                var chosen=((Task<List<Guid>?>)typeof(AiStrategyPlanner).GetMethod("ChooseFromOptionsAsync",BindingFlags.Instance|BindingFlags.NonPublic)!
                    .Invoke(planner,[game,snapshot,request,new CardInstance(Guid.NewGuid(),2,own.PlayerId),null,CancellationToken.None])!).GetAwaiter().GetResult();
                Check(chosen?.Single()==options[enabled?0:1].InstanceId,"choice path did not receive the situational observation");
                var inputs=provider.Inputs.Where(input=>input.TryGetProperty("lessonRetrieval",out _)).ToArray();
                Check(inputs.Length==2&&inputs.All(input=>input.GetProperty("lessonRetrieval").GetProperty("selected").GetInt32()==8),"request count or lesson cap changed");
                if(enabled)foreach(var input in inputs){
                    var match=input.GetProperty("lessonRetrieval").GetProperty("matches").EnumerateArray().Single(m=>m.GetProperty("ownCardIds").EnumerateArray().Any(id=>id.GetInt32()==2));
                    var actions=input.GetProperty("lessons")[match.GetProperty("lessonIndex").GetInt32()].GetProperty("observed").GetProperty("completedActions");
                    var columns=actions.GetProperty("columns").EnumerateArray().Select(c=>c.GetString()).ToList();
                    Check(actions.GetProperty("samples")[0].GetProperty("before")[columns.IndexOf("ownBloodPool")].GetInt32()==2&&actions.GetProperty("samples")[0].GetProperty("before")[columns.IndexOf("ownEmblemIds")].GetArrayLength()==0,"planning/discovery input omitted matched public resources");
                }
                Check(provider.SharedPrefixes.Distinct().Count()==1,"public resource changes or decision kind changed the fixed rule prefix");
                if(enabled){var mismatches=inputs[1].GetProperty("lessonRetrieval").GetProperty("matches")[0].GetProperty("resourceMismatches").EnumerateArray().Select(x=>x.GetString()).ToArray();Check(mismatches.Contains("own-blood-pool")&&mismatches.Contains("own-emblems"),"changed resource applicability not visible to next decision");}
                Check(inputs.All(input=>!input.GetRawText().Contains("PRIVATE_HIDDEN_CANARY")&&!input.GetRawText().Contains(own.PlayerId.ToString())),"private data leaked into retrieval input");
                var report=typeof(AiStrategyPlanner).GetMethod("GetReport",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(planner,[game]);
                var fact=(AiExperienceRetrievalFact)report!.GetType().GetProperty("ExperienceRetrieval")!.GetValue(report)!;
                Check(fact.Decision.Contains("discover")&&fact.RelevantSelected==(enabled?1:0),"latest report lost retrieval provenance");
                // The fixture suppresses only the automatic review queue after accepted requests.
                // Persist through the normal finish observer without spawning another mock request.
                var table=typeof(AiStrategyPlanner).GetField("matches",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(planner)!;
                object?[] lookup=[game,null];Check((bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table,lookup)!,"fixture match not observed");
                var state=lookup[1]!;state.GetType().GetField("Attempts")!.SetValue(state,0);state.GetType().GetField("ModeRequests")!.SetValue(state,0);
                planner.ObserveCommittedEvents(game,[new ShadowverseBeyond.Server.Domain.Events.GameEndEvent(own.PlayerId,enemy.PlayerId,"offline persistence boundary")]);
                var stored=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(Path.Combine(folder,key+".json")),Json)!.Last();
                Check(stored.ExperienceRetrievals?.Length==2&&stored.Recording?.Sections["experienceRetrievals"].Observed==2,
                    "finished history lost prepared retrievals or retention coverage");
                Check(provider.Inputs.Count==3,"retrieval or persistence introduced an extra guide/reflection request");
                results.Add(new{enabled,planningPrioritize=plan!.Prioritize,discoveredCard=options[enabled?0:1].CardId,prepared=inputs.Length,persisted=stored.ExperienceRetrievals!.Length,fact});
            }
            if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,scriptedProvider=true,wholeMatchOrSemanticLearningClaim=false,cases=results},new JsonSerializerOptions(Json){WriteIndented=true}));
        }finally{
            GameDataProvider.SetInstanceForTesting(prior);var resolved=Path.GetFullPath(directory);
            Check(resolved.StartsWith(Path.Combine(Path.GetTempPath(),"ai-situational-experience-"),StringComparison.OrdinalIgnoreCase),"unexpected cleanup directory");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }
    private static AiExperienceMatch ExcerptMatch(string kind,int index)=>new(0,0,null,"same-config-and-engine",kind,index,3,[2],[],[],1,1);
    private static JsonElement Excerpt(AiStrategyPlanner.Lesson lesson,AiExperienceMatch? match)=>JsonSerializer.SerializeToElement(
        typeof(AiStrategyPlanner).GetMethod("LessonAtMatch",Hidden)!.Invoke(null,[lesson,match]),Json).GetProperty("observed");
    private static AiStrategyPlanner.Lesson MiddleLesson(AiDefinitionStamp rules)
    {
        var template=Lesson(rules,999,0).CompletedActions![0];
        var actions=Enumerable.Range(0,32).Select(index=>template with{Turn=3,Before=Before() with{OwnHealth=10+index},After=Before()}).ToArray();
        actions[15]=template with {ActorBefore=new(2,"Hand",null,null),Action=new("PlayCard",2,null,null,0),
            OwnHandChanges=new(1,3,[new("departed",new(0,2,"Spell",1,null,null,[]),null),
                new("arrived",null,new(0,7,"Follower",0,1,1,[])),new("arrived",null,new(1,7,"Follower",0,1,1,[])),
                new("arrived",null,new(2,7,"Follower",0,1,1,[]))])};
        return Lesson(rules,2,0) with{CompletedActions=actions,Attacks=[]};
    }
    internal static void ExcerptBoundaries()
    {
        var prior=GameDataProvider.Instance;
        var definitions=new Dictionary<int,CardDefinition>{[1]=new(){Id=1,Name="excerpt boundary",Type=CardType.Spell}};
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
            "GetAllCardDefinitions"=>definitions,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),
            "GetAllAbilityConfigs"=>new Dictionary<int,List<AbilityConfig>>(),"GetAbilityConfigs"=>new List<AbilityConfig>(),
            "GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
        try{GameDataProvider.SetInstanceForTesting(data);ExcerptBoundaryCore();}
        finally{GameDataProvider.SetInstanceForTesting(prior);}
    }
    private static void ExcerptBoundaryCore()
    {
        var rules=new AiDefinitionStamp(new string('a',64),new string('b',64),3,0,0,true,[]);var lesson=MiddleLesson(rules);
        int[] Indexes(JsonElement observed)=>observed.GetProperty("completedActions").GetProperty("samples").EnumerateArray().Select(row=>row.GetProperty("sourceIndex").GetInt32()).ToArray();
        Check(Indexes(Excerpt(lesson,null)).SequenceEqual(new[]{0,1,30,31}),"ordinary history excerpt changed without a matching retrieval");
        var focused=Excerpt(lesson,ExcerptMatch("completedActions",15));var indices=Indexes(focused);
        Check(indices.SequenceEqual(new[]{14,15,16,31})&&focused.GetProperty("retrievedEvidence").GetProperty("included").GetBoolean(),
            "retrieved middle action was still excluded or lacked explicit inclusion provenance");
        var chosen=focused.GetProperty("completedActions").GetProperty("samples").EnumerateArray().Single(row=>row.GetProperty("sourceIndex").GetInt32()==15);
        Check(chosen.GetProperty("ownHandChanges").GetProperty("changes").EnumerateArray().Count(change=>change.GetProperty("after").ValueKind==JsonValueKind.Object
            &&change.GetProperty("after").GetProperty("cardId").GetInt32()==7&&change.GetProperty("after").GetProperty("cost").GetInt32()==0)==3,
            "matched action lost its actual resource endpoint details");
        foreach(var index in new[]{0,1,15,30,31}){
            var packet=Excerpt(lesson,ExcerptMatch("completedActions",index));var sample=packet.GetProperty("completedActions");
            Check(Indexes(packet).Length==4&&Indexes(packet).Contains(index)&&Indexes(packet).Distinct().Count()==4
                &&sample.GetProperty("omitted").GetInt32()==28,"focused endpoint exceeded its budget or lost the matched action");
        }
        foreach(var index in new[]{-1,32,int.MaxValue}){
            var invalid=Excerpt(lesson,ExcerptMatch("completedActions",index));
            Check(!invalid.GetProperty("retrievedEvidence").GetProperty("included").GetBoolean()&&Indexes(invalid).SequenceEqual(new[]{0,1,30,31}),
                "invalid retained index became invented focused evidence");
        }
        foreach(var kind in new[]{"attacks","proposedPlays","unknown"}){
            var invalid=Excerpt(lesson,ExcerptMatch(kind,15));
            Check(!invalid.GetProperty("retrievedEvidence").GetProperty("included").GetBoolean(),"wrong-category lookup borrowed completed action evidence");
        }
        var missing=Excerpt(lesson with{CompletedActions=null},ExcerptMatch("completedActions",15));
        Check(missing.GetProperty("completedActions").ValueKind==JsonValueKind.Null&&!missing.GetProperty("retrievedEvidence").GetProperty("included").GetBoolean(),
            "legacy missing completed actions became known empty or a successful match");
        foreach(var length in new[]{0,1,2,4}){
            var packet=Excerpt(lesson with{CompletedActions=lesson.CompletedActions!.Take(length).ToArray()},ExcerptMatch("completedActions",0));
            Check(packet.GetProperty("completedActions").GetProperty("samples").GetArrayLength()==length,"small histories duplicated actions to fill the excerpt budget");
        }
        var attack=Lesson(rules,2,0).Attacks![0];
        var attacks=Enumerable.Range(0,32).Select(index=>attack with{Turn=3,OwnHealth=100+index,Face=index>=30,Storm=index!=15}).ToArray();
        var history=lesson with{Attacks=attacks,CompletedActions=[]};
        var before=Excerpt(history,null).GetProperty("attacks");
        Check(before.GetProperty("examples").GetArrayLength()==8&&!before.GetProperty("exampleIndices").EnumerateArray().Any(i=>i.GetInt32()==15),"attack fixture did not hide its ordinary-trade middle context");
        var after=Excerpt(history,ExcerptMatch("attacks",15));var attackExcerpt=after.GetProperty("attacks");
        var ids=attackExcerpt.GetProperty("exampleIndices").EnumerateArray().Select(i=>i.GetInt32()).ToArray();
        Check(ids.Length==8&&ids.Distinct().Count()==8&&ids.Contains(15)&&attackExcerpt.GetProperty("examples")[Array.IndexOf(ids,15)].GetProperty("ownHealth").GetInt32()==115
            &&after.GetProperty("retrievedEvidence").GetProperty("included").GetBoolean(),"focused normal attack context was lost, over budget or joined by value instead of retained index");
        Check(attackExcerpt.GetProperty("stormFace").GetInt32()==before.GetProperty("stormFace").GetInt32()
            &&attackExcerpt.GetProperty("stormTrade").GetInt32()==before.GetProperty("stormTrade").GetInt32(),"excerpt selection rewrote whole retained attack counts");
        var lone=Excerpt(history with{Attacks=[attack with{Storm=false,Face=false}]},ExcerptMatch("attacks",0)).GetProperty("attacks");
        Check(lone.GetProperty("examples").GetArrayLength()==1,"single non-Storm trade could not be presented when matched");
    }
    internal static void ExcerptPlannerAndChoice(string? output=null)
    {
        var prior=GameDataProvider.Instance;var directory=Path.Combine(Path.GetTempPath(),"ai-focused-experience-"+Guid.NewGuid().ToString("N"));var results=new List<object>();
        try{
            var definitions=new Dictionary<int,CardDefinition>{[1]=new(){Id=1,Type=CardType.Leader,Name="leader",Health=20},
                [2]=new(){Id=2,Type=CardType.Spell,Name="resource source",Cost=1},[3]=new(){Id=3,Type=CardType.Spell,Name="tempo",Cost=1},
                [5]=new(){Id=5,Type=CardType.Spell,Name="third option",Cost=1},[7]=new(){Id=7,Type=CardType.Follower,Name="free resource",Cost=0,Attack=1,Health=1}};
            var revision=Guid.NewGuid();var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
                "get_DataRevision"=>revision,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
                "GetAbilityConfigs"=>new List<AbilityConfig>(),"GetAllAbilityConfigs"=>definitions.Keys.ToDictionary(id=>id,id=>new List<AbilityConfig>()),
                "GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
            GameDataProvider.SetInstanceForTesting(data);var rules=Rules();Check(rules.Available,"excerpt fixture rule catalog unavailable");
            foreach(var enabled in new[]{false,true}){
                var folder=Path.Combine(directory,enabled?"focused":"ordinary");Directory.CreateDirectory(folder);
                using var provider=new Provider(true);var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                    ["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/offline",["AiStrategy:ApiKey"]="offline-only",
                    ["AiStrategy:Model"]="fixture",["AiStrategy:MemoryPath"]=folder,["AiStrategy:GuideCacheEnabled"]="false",
                    ["AiStrategy:SituationalExperienceEnabled"]="true",["AiStrategy:FocusedExperienceExcerpts"]=enabled.ToString()}).Build();
                using var planner=new AiStrategyPlanner(provider,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
                var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=3,InitialDeckDefinitionIds=[2,3]};var enemy=new PlayerState(Guid.NewGuid());
                var game=new GameState("excerpt-"+enabled,own,enemy,own.PlayerId);var snapshot=Snapshot(own.PlayerId,enemy.PlayerId);
                var deck=typeof(AiStrategyPlanner).GetMethod("ReadDeck",Hidden)!.Invoke(null,[game,own.PlayerId]);
                var key=(string)typeof(AiStrategyPlanner).GetMethod("DeckKey",Hidden)!.Invoke(null,[deck])!;
                var lessons=new[]{MiddleLesson(rules)}.Concat(Enumerable.Range(1,16).Select(index=>Lesson(rules,100+index,index))).ToArray();
                File.WriteAllText(Path.Combine(folder,key+".json"),JsonSerializer.Serialize(lessons,Json));
                planner.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
                var plan=planner.PlanAsync(game,snapshot,own.PlayerId,default,(_,_)=>Task.FromResult(new PlayabilityResultDto{IsPlayable=true})).GetAwaiter().GetResult();
                Check(plan?.Prioritize.Single()==(enabled?2:3),"planning did not receive and accept the history-sensitive offline reply");
                if(enabled){snapshot.Players[0].BloodPool=1;snapshot.Players[0].Emblem=[new(){CardId=3,OwnerPlayerId=own.PlayerId,Zone=CardZone.Emblem,Type=CardType.Amulet}];}
                var options=new[]{2,3,5}.Select(id=>new CardInstanceDto{CardId=id,InstanceId=Guid.NewGuid(),Name=definitions[id].Name}).ToList();
                var request=new PlayerChoiceRequest{RequestingPlayerId=own.PlayerId,TargetKey="discover",MinSelectionCount=1,MaxSelectionCount=1,
                    SelectableTargetIds=options.Select(card=>card.InstanceId).ToList(),SelectableCardInstances=options};
                var selected=((Task<List<Guid>?>)typeof(AiStrategyPlanner).GetMethod("ChooseFromOptionsAsync",BindingFlags.Instance|BindingFlags.NonPublic)!
                    .Invoke(planner,[game,snapshot,request,new CardInstance(Guid.NewGuid(),2,own.PlayerId),null,CancellationToken.None])!).GetAwaiter().GetResult();
                Check(selected?.Single()==options[enabled?0:1].InstanceId,"discovery did not receive and accept the matched-middle-action excerpt");
                var inputs=provider.Inputs.Where(input=>input.TryGetProperty("lessonRetrieval",out _)).ToArray();
                Check(inputs.Length==2&&inputs.All(input=>input.GetProperty("lessons").GetArrayLength()==8),"focused excerpts added a request or enlarged the lesson cap");
                foreach(var input in inputs){
                    var match=input.GetProperty("lessonRetrieval").GetProperty("matches").EnumerateArray().Single(row=>row.GetProperty("ownCardIds").EnumerateArray().Any(id=>id.GetInt32()==2));
                    Check(match.GetProperty("evidenceIndex").GetInt32()==15,"ablation changed which episode was retrieved");
                    var lesson=input.GetProperty("lessons")[match.GetProperty("lessonIndex").GetInt32()].GetProperty("observed");
                    var samples=lesson.GetProperty("completedActions").GetProperty("samples");
                    Check(samples.GetArrayLength()==4&&samples.EnumerateArray().Any(row=>row.GetProperty("sourceIndex").GetInt32()==15)==enabled,
                        "same selected episode was not packed with the expected bounded sampling intervention");
                    Check(!input.GetRawText().Contains("PRIVATE_HIDDEN_CANARY")&&!input.GetRawText().Contains(own.PlayerId.ToString()),"focused model input leaked hidden data or player IDs");
                }
                Check(provider.Inputs.Count==3,"focused history caused another guide or reflection request");
                results.Add(new{enabled,historyGames=lessons.Length,matchedRetainedIndex=15,planningPrioritize=plan!.Prioritize,
                    discoveredCard=options[enabled?0:1].CardId,requests=provider.Inputs.Count,lessonsPerRequest=8,completedActionsPerLesson=4});
            }
            if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,scriptedProvider=true,
                selectedHistoryAndEpisodeHeldConstant=true,semanticLearningOrStrengthClaim=false,cases=results},new JsonSerializerOptions(Json){WriteIndented=true}));
        }finally{
            GameDataProvider.SetInstanceForTesting(prior);var resolved=Path.GetFullPath(directory);
            Check(resolved.StartsWith(Path.Combine(Path.GetTempPath(),"ai-focused-experience-"),StringComparison.OrdinalIgnoreCase),"unexpected excerpt cleanup path");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }
    private sealed class Provider:HttpMessageHandler,IHttpClientFactory
    {
        internal List<JsonElement> Inputs=[];
        internal List<string> SharedPrefixes=[];
        private readonly bool completedEvidence;
        private readonly bool refillEvidence;
        private readonly bool terminalDrawEvidence;
        internal Provider(bool completed=false,bool drawEvidence=false,bool exhaustionEvidence=false)
        {
            completedEvidence=completed;refillEvidence=drawEvidence;terminalDrawEvidence=exhaustionEvidence;
        }
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            using var envelope=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));SharedPrefixes.Add(JsonSerializer.Serialize(envelope.RootElement.GetProperty("messages").EnumerateArray().Take(2)));var fields=new Dictionary<string,JsonElement>();
            foreach(var message in envelope.RootElement.GetProperty("messages").EnumerateArray().Where(row=>row.GetProperty("role").GetString()=="user")){
                using var part=JsonDocument.Parse(message.GetProperty("content").GetString()!);foreach(var item in part.RootElement.EnumerateObject())fields[item.Name]=item.Value.Clone();
            }
            var input=JsonSerializer.SerializeToElement(fields,Json);Inputs.Add(input);
            var related=input.TryGetProperty("lessons",out var lessons)&&lessons.EnumerateArray().Any(lesson=>
                refillEvidence?lesson.GetProperty("observed").GetProperty("completedActions").GetProperty("samples").EnumerateArray().Any(action=>
                    action.GetProperty("sourceIndex").GetInt32()==15
                    && action.TryGetProperty("endTurnDraws",out var draw) && draw.ValueKind==JsonValueKind.Object
                    && draw.GetProperty("ownDeckBefore").GetInt32()==(terminalDrawEvidence?2:3)
                    && draw.GetProperty("batches").EnumerateArray().Select(batch=>batch.GetProperty("resolved").GetInt32())
                        .SequenceEqual(terminalDrawEvidence?[4,0]:[4,1])
                    && (!terminalDrawEvidence || (draw.GetProperty("deckExhausted").GetBoolean()
                        && draw.GetProperty("exhaustedBatchIndex").GetInt32()==1
                        && draw.GetProperty("batches")[1].GetProperty("sourceCardId").GetInt32()==1474115)))
                :completedEvidence?lesson.GetProperty("observed").GetProperty("completedActions").GetProperty("samples").EnumerateArray().Any(action=>
                    action.GetProperty("actorBefore").GetProperty("cardId").GetInt32()==2
                    &&action.GetProperty("ownHandChanges").ValueKind==JsonValueKind.Object
                    &&action.GetProperty("ownHandChanges").GetProperty("changes").EnumerateArray().Any(change=>change.GetProperty("after").ValueKind==JsonValueKind.Object
                        &&change.GetProperty("after").GetProperty("cardId").GetInt32()==7&&change.GetProperty("after").GetProperty("cost").GetInt32()==0))
                :lesson.GetProperty("observed").GetProperty("attacks").GetProperty("examples").EnumerateArray().Any(attack=>attack.GetProperty("attackerCardId").GetInt32()==2));
            if (terminalDrawEvidence && input.TryGetProperty("lessonRetrieval",out var retrieval)
                && retrieval.ValueKind==JsonValueKind.Object)
                related &= retrieval.GetProperty("matches").EnumerateArray().Any(match=>
                    match.TryGetProperty("exhaustedDrawSourceId",out var source) && source.GetInt32()==1474115
                    && !match.GetProperty("resourceMismatches").EnumerateArray().Any(field=>
                        field.GetString() is "exhausted-draw-source-missing" or "own-deck-count" or "super-evolution-unlocked"));
            object answer;
            if(refillEvidence && input.TryGetProperty("legalPlays",out var legal))
            {
                var play=legal.EnumerateArray().Single(candidate=>candidate.GetProperty("cardId").GetInt32()==(related?2:3));
                answer=new AiStrategyPlanner.TurnPlan("setup","offline refill evidence",[],[],
                    Play:new(play.GetProperty("handIndex").GetInt32(),play.GetProperty("cardId").GetInt32(),
                        "Base",play.GetProperty("cost").GetInt32(),"use"));
            }
            else answer=input.TryGetProperty("legalPlays",out _)?new{focus="setup",plan="offline history-sensitive fixture",prioritize=new[]{related?2:3},hold=Array.Empty<int>()}
                :input.TryGetProperty("kind",out _)?new{indexes=new[]{related?0:1}}:new{gamePlan="offline guide",cards=new[]{new{id=2,role="core"},new{id=3,role="tempo"}}};
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(answer)}}}}))};
        }
    }
    private sealed class Host:IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="offline";public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}=".";
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=".";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
