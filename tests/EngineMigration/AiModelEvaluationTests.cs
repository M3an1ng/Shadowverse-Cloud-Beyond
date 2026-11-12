using ShadowverseBeyond.Server.Dtos.Enums;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;

internal static class AiModelEvaluationTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static readonly Type Gate = typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiModelStrengthGate")!;
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static StrategyMatchResult[] Matches(Func<int, bool>? wins = null) => Enumerable.Range(1, 24)
        .Select(e => new StrategyMatchResult(e, 41000 + (e - 1) / 4, e % 2 == 1 ? A : B,
            e % 2 == 1 ? "deck-a" : "deck-b", e % 4 is 0 or 1, wins?.Invoke(e) == true ? "win" : "loss", 1, new(), new())).ToArray();
    private static AiModelEvaluation Judge(StrategyMatchResult[] matches, int failed = 0) =>
        (AiModelEvaluation)Gate.GetMethod("Evaluate", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, ["candidate", "incumbent", matches, failed, new HashSet<Guid> { A, B }])!;

    internal static void StrengthGate()
    {
        var pass = Judge(Matches(e => e <= 20 ? (e - 1) % 4 < 3 : (e - 1) % 4 < 2));
        Check(pass.Accepted && pass.WinRateLowerBound > .5 && pass.Games == 24 && pass.Wins == 17
            && pass.SeedWins == 5 && pass.SeedDraws == 1 && pass.PairedSignP < .05,
            "balanced candidate with a clear screened advantage failed the gate");
        Check(!Judge(Matches(e => e <= 16)).Accepted, "weak 16/24 evidence was called a stable advantage");
        Check(!Judge(Matches(e => e <= 17)).Accepted,
            "correlated wins in a few favorable opening seeds passed promotion");
        Check(!Judge(Matches(e => e <= 16).Select(m => m.Result == "loss" ? m with { Result = "draw" } : m).ToArray()).Accepted,
            "draws were treated as wins for superiority");
        Check(!Judge(Matches(_ => true)[..23]).Accepted && !Judge(Matches(_ => true), 1).Accepted,
            "incomplete or failed games passed promotion");
        var duplicate = Matches(_ => true); duplicate[23] = duplicate[22];
        Check(!Judge(duplicate).Accepted, "duplicate episodes passed promotion");
        var wrongSeed = Matches(_ => true); wrongSeed[1] = wrongSeed[1] with { ShuffleSeed = 999 };
        Check(!Judge(wrongSeed).Accepted, "unpaired starting shuffles passed promotion");
        var wrongDeck = Matches(_ => true); wrongDeck[0] = wrongDeck[0] with { DeckId = Guid.NewGuid() };
        Check(!Judge(wrongDeck).Accepted, "unrecognized deck passed promotion");
        var badBalance = Matches(_ => true).Select(m => m with { First = true }).ToArray();
        Check(!Judge(badBalance).Accepted, "missing second-player games passed promotion");
        var deckRegression = Matches(e => e % 2 == 0 || e % 4 == 1 && e <= 17);
        Check(deckRegression.Count(m => m.Result == "win") == 17 && !Judge(deckRegression).Accepted,
            "a strong deck concealed regression in the other deck");
        var roleRegression = Matches(e => e % 4 is 0 or 1 || e is 2 or 6 or 10 or 14 or 18);
        Check(roleRegression.Count(m => m.Result == "win") == 17 && !Judge(roleRegression).Accepted,
            "first-player wins concealed second-player regression");
    }

    internal static void StrategyBenchmarkGroups()
    {
        const int seed = 41000;
        var matches = Matches(e => e is 1 or 2 or 3 or 5 or 9 or 10 or 11)[..11]
            .Select(match => match with { Opening = new($"fixture-a-{match.ShuffleSeed}", $"fixture-b-{match.ShuffleSeed}") })
            .ToArray();
        matches[5] = matches[5] with { Result = "draw" };
        matches[6] = matches[6] with { Result = "draw" };
        matches[3] = matches[3] with { DifferentCompletedActions = 2, DifferentCommittedOptions = 3 };
        matches[7] = matches[7] with { DifferentCommittedOptions = 1 };
        matches[9] = matches[9] with { DifferentCompletedActions = 8, DifferentCommittedOptions = 9 };
        var status = new LocalTrainingStatus(Planned: 12, BenchmarkSeed: seed,
            PlayerDeckId: A, OpponentDeckId: B, CompareStrategy: true, StrategyMatches: matches.Reverse().ToArray());
        var report = status.StrategyBenchmark!;
        Check(report.PlannedGroups == 3 && report.CompleteGroups.SequenceEqual(new[] { 0, 1 })
            && report.Wins == 4 && report.Losses == 2 && report.Draws == 2 && report.UnpairedGames == 3
            && report.GroupsAhead == 1 && report.GroupsBehind == 0 && report.GroupsTied == 1
            && report.GamesWithDifferentActions == 1 && report.DifferentCompletedActions == 2
            && report.GamesWithDifferentOptions == 2 && report.DifferentCommittedOptions == 4,
            "partial games biased the balanced totals");
        Check((status with { CompareStrategy = false }).StrategyBenchmark == null,
            "ordinary training unexpectedly reported LLM groups");
        var invalidGroups = new[]
        {
            new[] { matches[0], matches[1], matches[2], matches[2] },
            new[] { matches[0], matches[1] with { ShuffleSeed = seed + 1 }, matches[2], matches[3] },
            new[] { matches[0], matches[1] with { First = true }, matches[2], matches[3] }
        };
        foreach (var invalid in invalidGroups)
        {
            var incomplete = (status with { StrategyMatches = invalid }).StrategyBenchmark!;
            Check(incomplete.CompleteGroups.Length == 0 && incomplete.Wins == 0 && incomplete.UnpairedGames == 4,
                "invalid pairing passed as a complete group");
        }
        var mismatched = matches[..4];
        mismatched[1] = mismatched[1] with { Opening = new("different", mismatched[1].Opening!.DeckB) };
        var uncheckedOpening = (status with { StrategyMatches = mismatched }).StrategyBenchmark!;
        Check(uncheckedOpening.CompleteGroups.Length == 0 && uncheckedOpening.OpeningMismatchGroups == 1
            && uncheckedOpening.UnpairedGames == 4 && uncheckedOpening.DifferentCommittedOptions == 0,
            "a nominally paired group with a different opening entered the comparison");
        var missing = matches[..4];
        missing[2] = missing[2] with { Opening = null };
        Check((status with { StrategyMatches = missing }).StrategyBenchmark!.OpeningMismatchGroups == 1,
            "a nominally paired group without observed opening evidence entered the comparison");
        var mirror = status with
        {
            OpponentDeckId = A,
            StrategyMatches = matches[..4].Select(match => match with { DeckId = A }).ToArray()
        };
        Check(mirror.StrategyBenchmark!.CompleteGroups.SequenceEqual(new[] { 0 }),
            "using the same deck in both slots invalidated a complete group");
    }

    internal static void StrategyExperiencePairs()
    {
        const int seed = 41000;
        var matches = Enumerable.Range(0, 8).Select(episode =>
        {
            var slot = episode / 2;
            var history = episode % 2 == 0;
            return new StrategyMatchResult(episode + 1, seed, slot % 2 == 0 ? A : B, "fixture",
                slot % 4 is 0 or 3, history ? "win" : "loss", 1, new(1, 1), new(),
                Opening: new("fixture-a", "fixture-b"), WithStrategyExperience: history, ExperienceGames: history ? 2 : 0,
                AfterMulligan: new($"post-a-{slot}", $"post-b-{slot}"));
        }).ToArray();
        var status = new LocalTrainingStatus(Planned: 8, BenchmarkSeed: seed, PlayerDeckId: A, OpponentDeckId: B,
            CompareStrategy: true, CompareStrategyExperience: true, StrategyMatches: matches.Reverse().ToArray());
        var report = status.StrategyExperienceBenchmark!;
        Check(status.StrategyBenchmark == null && report.CompleteGroups.SequenceEqual([0])
            && report.WithHistory.Wins == 4 && report.WithoutHistory.Losses == 4
            && report.WithHistory.GamesWithHistory == 4 && report.WithoutHistory.GamesWithHistory == 0
            && report.PairOutcomes == new AiStrategyExperiencePairOutcomes(4, 0, 0, 4),
            "experience pairs did not keep treatment and control separate");
        // Equal aggregate scores can hide an improvement and a regression at different seats.
        var mixed = matches.Select(match => match with { Result = match.Episode switch
        {
            1 or 4 => "win", 2 or 3 => "loss", _ => "draw"
        }}).ToArray();
        mixed[1] = mixed[1] with { Turn = new(), Choice = new(1, 0) };
        var balanced = (status with { StrategyMatches = mixed }).StrategyExperienceBenchmark!;
        Check(balanced.WithHistory.Wins == balanced.WithoutHistory.Wins
            && balanced.PairOutcomes == new AiStrategyExperiencePairOutcomes(1, 1, 2, 3),
            "equal totals concealed pair-level gains, regressions, or a missing model response");
        var mismatch = matches.ToArray();
        mismatch[3] = mismatch[3] with { Opening = new("different-a", "fixture-b") };
        var rejected = (status with { StrategyMatches = mismatch }).StrategyExperienceBenchmark!;
        Check(rejected.UnpairedGames == 8 && rejected.OpeningMismatchGroups == 1 && rejected.WithHistory.Games == 0
            && rejected.PairOutcomes == new AiStrategyExperiencePairOutcomes(0, 0, 0, 0),
            "an actual opening mismatch entered the history comparison");
        foreach (var different in new AiBenchmarkOpening?[] { null, new("different-post-a", "post-b") })
        {
            var postMismatch = matches.ToArray();
            postMismatch[3] = postMismatch[3] with { AfterMulligan = different };
            var result = (status with { StrategyMatches = postMismatch }).StrategyExperienceBenchmark!;
            Check(result.PostMulliganMismatchGroups == 1 && result.UnpairedGames == 8 && result.WithHistory.Games == 0,
                "different or missing settled mulligan entered the paired comparison");
        }
        var contaminated = matches.ToArray();
        contaminated[1] = contaminated[1] with { ExperienceGames = 1 };
        Check((status with { StrategyMatches = contaminated }).StrategyExperienceBenchmark!.CompleteGroups.Length == 0,
            "a control that received historical games was included");
        Check((status with { StrategyMatches = matches[..7] }).StrategyExperienceBenchmark!.CompleteGroups.Length == 0,
            "an incomplete history comparison group was included");
        RootEvidence(matches[0], matches[1]);
    }

    private static void RootEvidence(StrategyMatchResult history, StrategyMatchResult control)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags staticMethod = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiExperienceRootTrace")!;
        var capture = type.GetMethod("Capture", instance)!;
        var record = type.GetMethod("Record", instance)!;
        var compare = type.GetMethod("Compare", staticMethod)!;
        var beginChoices = type.GetMethod("BeginChoices", instance)!;
        var captureChoice = type.GetMethod("CaptureChoice", instance)!;
        var attach = typeof(StrategyMatchResult).GetProperty("ExperienceRoots", instance)!;
        object Build(int handIndex, int pp, int hiddenId, bool missing = false, int? selected = null,
            int secondOffer = 31, bool accepted = true, bool target = false, CardZone offeredZone = CardZone.Staging)
        {
            var player = Guid.NewGuid();
            var enemy = Guid.NewGuid();
            var game = new GameState(Guid.NewGuid().ToString(), new(player), new(enemy), player);
            CardInstanceDto Hand(Guid owner, int id) => new() {InstanceId=Guid.NewGuid(),OwnerPlayerId=owner,
                Zone=CardZone.Hand,CardId=id,CurrentCost=1};
            var own = new PlayerStateDto {PlayerId=player,TurnNumber=3,CurrentPP=3,Hand=[Hand(player,10),Hand(player,10)]};
            own.Hand[0].BuffSummaries.Add(new BuffSummaryDto
            {
                SourceCardDefinitionId = 20,
                NumericEffects = [new() { Field="Cost", Layer="Additive", Value=-1,
                    PolicyKey=Guid.NewGuid().ToString(), Duration="Permanent" }]
            });
            var opposing = new PlayerStateDto {PlayerId=enemy,Hand=[Hand(enemy,hiddenId)]};
            var snapshot = new GameStateDto {GameId=game.GameId,CurrentPlayerId=player,Players=[own,opposing]};
            var trace = Activator.CreateInstance(type, nonPublic:true)!;
            var end = new PlayerActionWrapper("EndTurn", player, new object());
            record.Invoke(trace,[capture.Invoke(trace,[game,snapshot,end])]);
            own.CurrentPP = pp;
            var play = new PlayerActionWrapper("PlayCard", player,
                new PlayCardActionDto {CardUid=own.Hand[handIndex].InstanceId,PlayMode=PlayMode.Base,PaidCost=1});
            if (selected is int pick)
            {
                beginChoices.Invoke(trace, null);
                var offers = target ? own.Hand : [Hand(player,30) with { Zone=offeredZone },
                    Hand(player,secondOffer) with { Zone=offeredZone }];
                var request = new PlayerChoiceRequest
                {
                    GameId=game.GameId,RequestingPlayerId=player,TargetKey=target?"discard":"discover",
                    SelectableTargetIds=offers.Select(card=>card.InstanceId).ToList(),
                    SelectableCardInstances=target?[]:offers,
                    MinSelectionCount=1,MaxSelectionCount=1
                };
                var response = new TaskCompletionSource<PlayerChoiceResult>();
                var pending = new PendingChoiceContext(request,response,default);
                captureChoice.Invoke(trace,[game,snapshot,request,pending,null]);
                if (accepted) response.SetResult(new() { ChosenTargetIds=[offers[pick].InstanceId] });
            }
            record.Invoke(trace,[missing ? null : capture.Invoke(trace,[game,snapshot,play])]);
            return trace;
        }
        AiExperienceRootComparison Measure(object a, object b)
        {
            attach.SetValue(history,a);
            attach.SetValue(control,b);
            return (AiExperienceRootComparison)compare.Invoke(null,[history,control])!;
        }
        var left = Build(0, 3, 72);
        Check(Measure(left, Build(0,3,99)) is {Status:"same-recorded-roots",MatchedRoots:2},
            "runtime identities or the opposing hidden card invalidated the recorded prefix");
        Check(Measure(left, Build(1,3,99)) is {Status:"root-action-difference",MatchedRoots:1,
            History.Source.Index:0,Control.Source.Index:1}, "different copies of the same card were collapsed");
        Check(Measure(left, Build(1,2,99)) is {Status:"position-difference",MatchedRoots:1,History:null,Control:null},
            "different sampled positions were misreported as a comparable action");
        Check(Measure(left, Build(1,3,99,missing:true)) is {Status:"unobserved",MatchedRoots:1},
            "a missing root was treated as comparable");
        var firstOffer = Build(0,3,72,selected:0);
        Check(Measure(firstOffer, Build(0,3,99,selected:1)) is { Status:"subchoice-difference",
            MatchedRoots:1,ChoiceIndex:0,HistoryChoice.Selected:[{CardId:30}],ControlChoice.Selected:[{CardId:31}] },
            "different accepted discover cards at the same root were missed");
        Check(Measure(firstOffer, Build(0,3,99,selected:0)) is {Status:"same-recorded-roots",MatchedRoots:2},
            "fresh discover runtime IDs changed the same selection");
        Check(Measure(firstOffer, Build(0,3,99,selected:1,secondOffer:32)) is {Status:"choice-context-difference",HistoryChoice:null},
            "different discover offers were described as comparable picks");
        Check(Measure(firstOffer, Build(0,3,99,selected:1,offeredZone:CardZone.Deck))
            is {Status:"choice-context-difference"}, "deck instances were compared as staged discover definitions");
        Check(Measure(firstOffer, Build(0,3,99,selected:1,accepted:false)) is {Status:"choice-unobserved",HistoryChoice:null},
            "a proposed choice without engine acceptance was counted");
        Check(Measure(Build(0,3,72,selected:0,target:true),Build(0,3,99,selected:1,target:true))
            is {Status:"subchoice-difference",HistoryChoice.Selected:[{Location.Index:0}],ControlChoice.Selected:[{Location.Index:1}]},
            "target selection collapsed separate copies at the same location type");
    }

    internal static void OpeningEvidence()
    {
        var capture = typeof(AiBenchmarkOpening).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)!;
        AiBenchmarkOpening? Read(GameState game, Guid player) => (AiBenchmarkOpening?)capture.Invoke(null, [game, player]);
        var player1 = new PlayerState(A);
        var player2 = new PlayerState(B);
        var game = new GameState("opening-audit", player1, player2, A) { IsLocalAiTraining = true };
        void Cards(PlayerState player, int[] ids)
        {
            player.InitialDeckDefinitionIds = ids.ToList();
            player.Hand.Add(new CardInstance(Guid.NewGuid(), ids[0], player.PlayerId));
            foreach (var id in ids.Skip(1)) player.Deck.Add(new CardInstance(Guid.NewGuid(), id, player.PlayerId));
        }
        Cards(player1, [1, 2, 3]);
        Cards(player2, [1, 3, 2]);
        var opening = Read(game, A);
        Check(opening != null && opening.DeckA != opening.DeckB && opening.DeckA.Length == 64
            && Read(game, B) == new AiBenchmarkOpening(opening.DeckB, opening.DeckA),
            "opening lost order or compared player slots rather than deck slots");
        var other = new GameState("opening-remapped", new PlayerState(B), new PlayerState(A), B) { IsLocalAiTraining = true };
        Cards(other.Player2, [1, 2, 3]);
        Cards(other.Player1, [1, 3, 2]);
        Check(Read(other, A) == opening, "opening fingerprint used runtime identities or first-player role");
        player1.Deck.Reverse();
        Check(Read(game, A) == null, "deck snapshot no longer matched the actual deal");
        other.Player2.HasCompletedMulligan = true;
        Check(Read(other, A) == null, "post-mulligan state was accepted as an opening");
    }

    internal static void SeededMulligan()
    {
        var seed = typeof(PlayerState).GetProperty("BenchmarkMulliganSeed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var capture = typeof(AiBenchmarkOpening).GetMethod("CaptureAfterMulligan", BindingFlags.Static | BindingFlags.NonPublic)!;

        (AiBenchmarkOpening Opening, int KeptCard) Run(bool reverseSubmission, int firstSeed, int replacement)
        {
            var a = new PlayerState(Guid.NewGuid());
            var b = new PlayerState(Guid.NewGuid());
            seed.SetValue(a, firstSeed);
            seed.SetValue(b, 88913);
            foreach (var player in new[] { a, b })
            {
                for (var id = 1; id <= 12; id++)
                {
                    var card = new CardInstance(Guid.NewGuid(), id, player.PlayerId)
                    {
                        CurrentZone = id <= 3 ? CardZone.Hand : CardZone.Deck
                    };
                    (id <= 3 ? player.Hand : player.Deck).Add(card);
                }
            }
            var game = new GameState("seeded-mulligan", a, b, a.PlayerId) { IsLocalAiTraining = true };
            foreach (var player in reverseSubmission ? new[] { b, a } : new[] { a, b })
                Check(player.PerformMulligan([player.Hand[player == a ? replacement : 1].InstanceId], game).Success,
                    "native mulligan failed");
            Check(capture.Invoke(null, [game, a.PlayerId]) == null, "mulligan-phase state was treated as ready for AI");
            game.Phase = GamePhase.Main;
            return ((AiBenchmarkOpening)capture.Invoke(null, [game, a.PlayerId])!, a.Hand[0].BaseDefinitionId);
        }

        var settled = Run(false, 14821, 1);
        Check(settled.KeptCard == 1 && Run(true, 14821, 1).Opening == settled.Opening,
            "equivalent replacements differed under the same seat seeds or moved a kept card");
        var changedSeed = Run(false, 14822, 1).Opening;
        var changedChoice = Run(false, 14821, 2).Opening;
        Check(changedSeed.DeckA != settled.Opening.DeckA && changedChoice.DeckA != settled.Opening.DeckA
            && changedSeed.DeckB == settled.Opening.DeckB && changedChoice.DeckB == settled.Opening.DeckB,
            "a change to one seat contaminated the other or did not change the actual opening");
    }

    internal static void Recheck(string input, string output)
    {
        var original=JsonSerializer.Deserialize<AiModelEvaluation>(File.ReadAllText(input),AiLearningModel.Json)
            ?? throw new Exception("missing evaluation report");
        var next=(AiModelEvaluation)Gate.GetMethod("Evaluate",BindingFlags.NonPublic|BindingFlags.Static)!
            .Invoke(null,[original.CandidateVersion,original.IncumbentVersion,original.Matches,original.Failed,
                original.Matches.Select(m=>m.DeckId).ToHashSet()])!;
        File.WriteAllText(output,JsonSerializer.Serialize(next,new JsonSerializerOptions(AiLearningModel.Json){WriteIndented=true}));
        Console.WriteLine($"Current gate: {next.Wins}W {next.Losses}L {next.Draws}D; seed blocks {next.SeedWins}/{next.SeedLosses}/{next.SeedDraws}; accepted={next.Accepted}; provider calls=0");
    }

    private static AiNetwork Net(double bias) => new() { Format = 1, FeatureVersion = 3, SnapshotId = "offline-gate-fixture",
        W1 = new double[512 * 128], B1 = new double[128], W2 = new double[128], B2 = bias };
    private static AiModelArtifact Artifact(string version, double bias) => new() { Version = version,
        Weights = new() { ValueModel = "value" }, Models = new() { ["value"] = Net(bias) } };
    private static object Invoke(object? target, MethodInfo method, params object?[] args)
    {
        try { return method.Invoke(target, args)!; }
        catch (TargetInvocationException e) when (e.InnerException != null)
        { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    private sealed class Host : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "offline";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    internal static void IsolatedModelsAndTrialPersistence()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sv-ai-gate-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "model.json");
        try
        {
            var incumbent = Artifact("incumbent", -.6); var candidate = Artifact("candidate-sentinel", .6);
            File.WriteAllText(path, JsonSerializer.Serialize(incumbent, AiLearningModel.Json));
            var originalBytes = File.ReadAllBytes(path);
            var model = new AiLearningModel(new Host(), new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["AiModel:Path"] = path }).Build(), NullLogger<AiLearningModel>.Instance);
            incumbent = model.Current!;
            var own = Guid.NewGuid(); var enemy = Guid.NewGuid();
            var ownPlayer = new PlayerState(own) { InitialDeckDefinitionIds = [11,12] };
            var specialistKey = AiLearningModel.DeckKey(ownPlayer.InitialDeckDefinitionIds);
            candidate.Specialists[specialistKey] = new() { ValueModel = "special" };
            candidate.Models["special"] = Net(1.2);
            var game = new GameState("candidate-game", ownPlayer, new PlayerState(enemy), own);
            var begin = typeof(AiLearningModel).GetMethod("BeginComparison", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var select = typeof(AiLearningModel).GetMethod("Weights", BindingFlags.NonPublic | BindingFlags.Instance,
                null, [typeof(GameState), typeof(Guid)], null)!;
            var state = new GameStateDto { CurrentPlayerId = own, Players = [new() { PlayerId = own }, new() { PlayerId = enemy }] };
            var rejected = false;
            try { Invoke(model, begin, game, own, candidate, incumbent); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "a normal game accepted unvalidated candidate weights");
            game.IsLocalAiTraining = true;
            var first = (IDisposable)Invoke(model, begin, game, own, candidate, incumbent);
            var candidateWeights = (AiWeights)Invoke(model, select, game, own);
            var incumbentWeights = (AiWeights)Invoke(model, select, game, enemy);
            Check(model.Value(state, own, candidateWeights) > 0 && model.Value(state, enemy, incumbentWeights) < 0,
                "the two seats did not evaluate using different frozen artifacts sharing the same network ID");
            Check(candidateWeights.ValueModel == "special" && model.Value(state, own, candidateWeights) > .8,
                "candidate comparison discarded the matching deck specialist");
            var otherGame = new GameState("other-training-game", new PlayerState(own), new PlayerState(enemy), own) { IsLocalAiTraining = true };
            using (var otherLease = (IDisposable)Invoke(model, begin, otherGame, enemy, candidate, incumbent)) {
                Check(model.Value(state, own, (AiWeights)Invoke(model, select, otherGame, own)) < 0
                    && model.Value(state, enemy, (AiWeights)Invoke(model, select, otherGame, enemy)) > 0
                    && model.Value(state, own, (AiWeights)Invoke(model, select, game, own)) > .8,
                    "concurrent training games mixed their model seats");
            }
            var normalGame = new GameState(game.GameId, new PlayerState(own), new PlayerState(enemy), own);
            Check(model.Value(state, own, (AiWeights)Invoke(model, select, normalGame, own)) < 0,
                "a normal game inherited a matching training game ID's candidate");
            Check(model.Score(state, own, "play", new() { InstanceId = Guid.NewGuid(), OwnerPlayerId = own }, weights: candidateWeights) > 0,
                "live action scoring ignored the candidate artifact");
            Check(model.Value(state, own) < 0 && model.Current.Version == "incumbent", "candidate leaked into the live model");
            Check(!JsonSerializer.Serialize(candidateWeights, AiLearningModel.Json).Contains("candidate-sentinel"),
                "temporary inference artifacts leaked into portable weights");
            rejected = false;
            try { Invoke(model, begin, game, own, candidate, incumbent); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "a second comparison replaced an active lease");
            first.Dispose();
            using (var second = (IDisposable)Invoke(model, begin, game, own, candidate, incumbent))
            {
                first.Dispose();
                Check(model.Value(state, own, (AiWeights)Invoke(model, select, game, own)) > 0,
                    "disposing an old lease removed a newer comparison");
            }
            Check(model.Value(state, own, (AiWeights)Invoke(model, select, game, own)) < 0, "comparison cleanup retained candidate weights");
            ((Task)Invoke(model, typeof(AiLearningModel).GetMethod("SaveTrialAsync", BindingFlags.NonPublic | BindingFlags.Instance)!,
                candidate, CancellationToken.None)).GetAwaiter().GetResult();
            var evaluation = Judge(Matches(e => e <= 16));
            ((Task)Invoke(model, typeof(AiLearningModel).GetMethod("SaveEvaluationAsync", BindingFlags.NonPublic | BindingFlags.Instance)!,
                evaluation, CancellationToken.None)).GetAwaiter().GetResult();
            Check(File.ReadAllBytes(path).SequenceEqual(originalBytes) && model.Current.Version == "incumbent"
                && !File.Exists(path + ".previous"), "saving an unpromoted trial replaced active weights");
            Check(AiLearningModel.Load(path + ".candidate").Version == candidate.Version
                && JsonSerializer.Deserialize<AiModelEvaluation>(File.ReadAllText(path + ".candidate-evaluation.json"), AiLearningModel.Json)?.Accepted == false,
                "unpromoted learning or rejection evidence was lost");
            var request = JsonSerializer.Deserialize<LocalTrainingRequest>("{\"candidateArtifact\":{\"version\":\"injected\"}}", AiLearningModel.Json)!;
            Check(typeof(LocalTrainingRequest).GetProperty("CandidateArtifact", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(request) == null,
                "HTTP training requests could inject unvalidated model artifacts");
        }
        finally { foreach (var file in Directory.EnumerateFiles(dir)) File.Delete(file); Directory.Delete(dir); }
    }
}
