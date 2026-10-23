using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.Cards;

public class CardCommunityControllerTests
{
    private static User BuildUser(Guid userId, string username)
    {
        return new User
        {
            Id = userId,
            Username = username,
            Email = $"{username}@example.com",
            PasswordHash = "hash",
            IsActive = true
        };
    }

    private static CardDefinition BuildCard(int cardId, string name)
    {
        return new CardDefinition
        {
            Id = cardId,
            Name = name,
            Description = "test",
            Type = CardType.Spell,
            Class = CardClass.Neutral,
            Rarity = CardRarity.Bronze,
            CardSet = "Legacy",
            Cost = 1
        };
    }

    [Fact]
    public async Task Vote_ClickSameButtonTwice_ShouldCancelVote_AndProgressQuestOnce()
    {
        await using var harness = await CardCommunityHarness.CreateAsync();
        var userId = Guid.NewGuid();
        harness.Db.Users.Add(BuildUser(userId, "alice"));
        harness.Db.CardDefinitions.Add(BuildCard(1001, "Card A"));
        harness.Db.QuestDefinitions.Add(new QuestDefinition
        {
            Id = 201,
            Name = "Vote once",
            Description = "Vote on one card",
            Type = QuestType.CollectCards,
            ActivityType = QuestActivityType.Vote,
            Category = QuestCategory.Daily,
            TargetValue = 1,
            CoinReward = 80,
            DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        var first = await harness.Controller.Vote(1001, new CardCommunityController.VoteCardRequest
        {
            UserId = userId,
            VoteType = "up"
        });
        var firstOk = Assert.IsType<OkObjectResult>(first.Result);
        var firstPayload = Assert.IsType<CardCommunityController.CardVoteSummaryDto>(firstOk.Value);
        Assert.Equal(1, firstPayload.UpvoteCount);
        Assert.Equal(0, firstPayload.DownvoteCount);
        Assert.Equal("up", firstPayload.UserVote);
        Assert.False(firstPayload.RewardGranted);
        Assert.Equal(0, firstPayload.RewardCoins);
        Assert.Null(firstPayload.CurrentCoins);
        Assert.NotNull(firstPayload.QuestProgress);
        Assert.Single(firstPayload.QuestProgress!.CompletedQuestIds);

        var second = await harness.Controller.Vote(1001, new CardCommunityController.VoteCardRequest
        {
            UserId = userId,
            VoteType = "up"
        });
        var secondOk = Assert.IsType<OkObjectResult>(second.Result);
        var secondPayload = Assert.IsType<CardCommunityController.CardVoteSummaryDto>(secondOk.Value);
        Assert.Equal(0, secondPayload.UpvoteCount);
        Assert.Equal(0, secondPayload.DownvoteCount);
        Assert.Null(secondPayload.UserVote);
        Assert.False(secondPayload.RewardGranted);
        Assert.Equal(0, secondPayload.RewardCoins);
        Assert.Null(secondPayload.CurrentCoins);

        var transactions = await harness.Db.CoinTransactions
            .Where(item => item.UserId == userId)
            .ToListAsync();
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task Vote_ChangeDirection_ShouldNotGrantSecondReward()
    {
        await using var harness = await CardCommunityHarness.CreateAsync();
        var userId = Guid.NewGuid();
        harness.Db.Users.Add(BuildUser(userId, "alice"));
        harness.Db.CardDefinitions.Add(BuildCard(1006, "Card F"));
        await harness.Db.SaveChangesAsync();

        await harness.Controller.Vote(1006, new CardCommunityController.VoteCardRequest
        {
            UserId = userId,
            VoteType = "up"
        });

        var changed = await harness.Controller.Vote(1006, new CardCommunityController.VoteCardRequest
        {
            UserId = userId,
            VoteType = "down"
        });
        var changedOk = Assert.IsType<OkObjectResult>(changed.Result);
        var changedPayload = Assert.IsType<CardCommunityController.CardVoteSummaryDto>(changedOk.Value);
        Assert.Equal("down", changedPayload.UserVote);
        Assert.False(changedPayload.RewardGranted);
        Assert.Null(changedPayload.CurrentCoins);

        var transactions = await harness.Db.CoinTransactions
            .Where(item => item.UserId == userId)
            .ToListAsync();
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task Comment_CreateDeleteAndRecreate_ShouldProgressQuestWithoutDirectCoins()
    {
        await using var harness = await CardCommunityHarness.CreateAsync();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        harness.Db.Users.AddRange(
            BuildUser(ownerUserId, "owner"),
            BuildUser(otherUserId, "other"));
        harness.Db.CardDefinitions.Add(BuildCard(1002, "Card B"));
        harness.Db.QuestDefinitions.Add(new QuestDefinition
        {
            Id = 202,
            Name = "Comment once",
            Description = "Comment on one card",
            Type = QuestType.CollectCards,
            ActivityType = QuestActivityType.Comment,
            Category = QuestCategory.Daily,
            TargetValue = 1,
            CoinReward = 100,
            DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        var created = await harness.Controller.CreateComment(1002, new CardCommunityController.CreateCardCommentRequest
        {
            UserId = ownerUserId,
            Content = "first comment"
        });
        var createdResult = Assert.IsType<CreatedAtActionResult>(created.Result);
        var createdComment = Assert.IsType<CardCommunityController.CardCommentDto>(createdResult.Value);
        Assert.False(createdComment.RewardGranted);
        Assert.Equal(0, createdComment.RewardCoins);
        Assert.Null(createdComment.CurrentCoins);
        Assert.NotNull(createdComment.QuestProgress);
        Assert.Single(createdComment.QuestProgress!.CompletedQuestIds);

        var forbiddenDelete = await harness.Controller.DeleteComment(1002, createdComment.Id, otherUserId);
        Assert.IsType<ForbidResult>(forbiddenDelete);

        var ownerDelete = await harness.Controller.DeleteComment(1002, createdComment.Id, ownerUserId);
        Assert.IsType<NoContentResult>(ownerDelete);

        var recreated = await harness.Controller.CreateComment(1002, new CardCommunityController.CreateCardCommentRequest
        {
            UserId = ownerUserId,
            Content = "second comment"
        });
        var recreatedResult = Assert.IsType<CreatedAtActionResult>(recreated.Result);
        var recreatedComment = Assert.IsType<CardCommunityController.CardCommentDto>(recreatedResult.Value);
        Assert.False(recreatedComment.RewardGranted);
        Assert.Equal(0, recreatedComment.RewardCoins);
        Assert.Null(recreatedComment.CurrentCoins);

        var transactions = await harness.Db.CoinTransactions
            .Where(item => item.UserId == ownerUserId)
            .ToListAsync();
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task Community_ShouldShareByCardId_AndIsolateDifferentCards()
    {
        await using var harness = await CardCommunityHarness.CreateAsync();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        harness.Db.Users.AddRange(
            BuildUser(userA, "alice"),
            BuildUser(userB, "bob"));
        harness.Db.CardDefinitions.AddRange(
            BuildCard(1003, "Card C"),
            BuildCard(1004, "Card D"));
        await harness.Db.SaveChangesAsync();

        await harness.Controller.CreateComment(1003, new CardCommunityController.CreateCardCommentRequest
        {
            UserId = userA,
            Content = "card-c comment a"
        });
        await harness.Controller.CreateComment(1003, new CardCommunityController.CreateCardCommentRequest
        {
            UserId = userB,
            Content = "card-c comment b"
        });
        await harness.Controller.CreateComment(1004, new CardCommunityController.CreateCardCommentRequest
        {
            UserId = userA,
            Content = "card-d comment a"
        });
        await harness.Controller.Vote(1003, new CardCommunityController.VoteCardRequest
        {
            UserId = userA,
            VoteType = "up"
        });
        await harness.Controller.Vote(1003, new CardCommunityController.VoteCardRequest
        {
            UserId = userB,
            VoteType = "down"
        });

        var cardC = await harness.Controller.Get(1003, userA, 1, 20);
        var cardCOk = Assert.IsType<OkObjectResult>(cardC.Result);
        var cardCPayload = Assert.IsType<CardCommunityController.CardCommunityResponse>(cardCOk.Value);
        Assert.Equal(2, cardCPayload.TotalComments);
        Assert.Equal(1, cardCPayload.UpvoteCount);
        Assert.Equal(1, cardCPayload.DownvoteCount);
        Assert.Equal("up", cardCPayload.UserVote);

        var cardD = await harness.Controller.Get(1004, userA, 1, 20);
        var cardDOk = Assert.IsType<OkObjectResult>(cardD.Result);
        var cardDPayload = Assert.IsType<CardCommunityController.CardCommunityResponse>(cardDOk.Value);
        Assert.Equal(1, cardDPayload.TotalComments);
        Assert.Equal(0, cardDPayload.UpvoteCount);
        Assert.Equal(0, cardDPayload.DownvoteCount);
        Assert.Null(cardDPayload.UserVote);
    }

    [Fact]
    public async Task Community_ShouldReturnLatestVoteActivities()
    {
        await using var harness = await CardCommunityHarness.CreateAsync();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        harness.Db.Users.AddRange(
            BuildUser(userA, "alice"),
            BuildUser(userB, "bob"));
        harness.Db.CardDefinitions.Add(BuildCard(1005, "Card E"));
        await harness.Db.SaveChangesAsync();

        await harness.Controller.Vote(1005, new CardCommunityController.VoteCardRequest
        {
            UserId = userA,
            VoteType = "up"
        });
        await harness.Controller.Vote(1005, new CardCommunityController.VoteCardRequest
        {
            UserId = userB,
            VoteType = "down"
        });
        await harness.Controller.Vote(1005, new CardCommunityController.VoteCardRequest
        {
            UserId = userA,
            VoteType = "down"
        });

        var result = await harness.Controller.Get(1005, userA, 1, 20);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CardCommunityController.CardCommunityResponse>(ok.Value);

        Assert.Equal(2, payload.VoteActivities.Count);
        var aliceActivity = Assert.Single(payload.VoteActivities.Where(item => item.UserId == userA));
        var bobActivity = Assert.Single(payload.VoteActivities.Where(item => item.UserId == userB));
        Assert.Equal("down", aliceActivity.VoteType);
        Assert.Equal("down", bobActivity.VoteType);
    }

    [Fact]
    public async Task CreateComment_WhenQuestProgressFails_ShouldRollbackComment()
    {
        var questProgressMock = new Mock<IQuestProgressService>();
        questProgressMock
            .Setup(item => item.RecordActivityAsync(
                It.IsAny<Guid>(),
                It.IsAny<QuestActivityType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("quest progress failed"));

        await using var harness = await CardCommunityHarness.CreateAsync(questProgressService: questProgressMock.Object);
        var user = BuildUser(Guid.NewGuid(), "owner");
        harness.Db.Users.Add(user);
        harness.Db.CardDefinitions.Add(BuildCard(1007, "Card G"));
        await harness.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CommunityService.CreateCommentAsync(1007, user, "will rollback"));

        Assert.Equal(0, await harness.Db.CardComments.CountAsync());
        Assert.Equal(0, await harness.Db.CoinTransactions.CountAsync());
    }

    [Fact]
    public async Task Vote_WhenQuestProgressFails_ShouldRollbackVote()
    {
        var questProgressMock = new Mock<IQuestProgressService>();
        questProgressMock
            .Setup(item => item.RecordActivityAsync(
                It.IsAny<Guid>(),
                It.IsAny<QuestActivityType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("quest progress failed"));

        await using var harness = await CardCommunityHarness.CreateAsync(questProgressService: questProgressMock.Object);
        var user = BuildUser(Guid.NewGuid(), "owner");
        harness.Db.Users.Add(user);
        harness.Db.CardDefinitions.Add(BuildCard(1008, "Card H"));
        await harness.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CommunityService.VoteAsync(1008, user, CardVoteType.Upvote));

        Assert.Equal(0, await harness.Db.CardVotes.CountAsync());
        Assert.Equal(0, await harness.Db.CoinTransactions.CountAsync());
    }

    [Fact]
    public async Task CommunityRewardResponses_AndCoinHistory_ShouldExposeUpdatedBalance()
    {
        await using var harness = await CardCommunityHarness.CreateAsync();
        var userId = Guid.NewGuid();
        harness.Db.Users.Add(BuildUser(userId, "alice"));
        harness.Db.CardDefinitions.Add(BuildCard(1009, "Card I"));
        await harness.Db.SaveChangesAsync();

        var comment = await harness.Controller.CreateComment(1009, new CardCommunityController.CreateCardCommentRequest
        {
            UserId = userId,
            Content = "hello"
        });
        var commentOk = Assert.IsType<CreatedAtActionResult>(comment.Result);
        var commentPayload = Assert.IsType<CardCommunityController.CardCommentDto>(commentOk.Value);
        Assert.False(commentPayload.RewardGranted);
        Assert.Null(commentPayload.CurrentCoins);

        var vote = await harness.Controller.Vote(1009, new CardCommunityController.VoteCardRequest
        {
            UserId = userId,
            VoteType = "up"
        });
        var voteOk = Assert.IsType<OkObjectResult>(vote.Result);
        var votePayload = Assert.IsType<CardCommunityController.CardVoteSummaryDto>(voteOk.Value);
        Assert.False(votePayload.RewardGranted);
        Assert.Equal(0, votePayload.RewardCoins);
        Assert.Null(votePayload.CurrentCoins);

        var history = await harness.EconomyService.GetCoinHistoryAsync(userId, 1, 10);
        Assert.Empty(history);
    }

    private sealed class CardCommunityHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private CardCommunityHarness(
            SqliteConnection connection,
            ApplicationDbContext db,
            CardCommunityController controller,
            CardCommunityService communityService,
            IEconomyService economyService)
        {
            _connection = connection;
            Db = db;
            Controller = controller;
            CommunityService = communityService;
            EconomyService = economyService;
        }

        public ApplicationDbContext Db { get; }
        public CardCommunityController Controller { get; }
        public CardCommunityService CommunityService { get; }
        public IEconomyService EconomyService { get; }

        public static async Task<CardCommunityHarness> CreateAsync(
            IEconomyService? economyService = null,
            IQuestProgressService? questProgressService = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var transactionService = new TransactionService(db, NullLogger<TransactionService>.Instance);
            var resolvedEconomyService = economyService
                ?? new EconomyService(db, NullLogger<EconomyService>.Instance, transactionService);
            var resolvedQuestProgressService = questProgressService ?? new QuestProgressService(
                db,
                transactionService,
                NullLogger<QuestProgressService>.Instance);
            var communityService = new CardCommunityService(
                db,
                transactionService,
                resolvedQuestProgressService,
                NullLogger<CardCommunityService>.Instance);
            var controller = new CardCommunityController(db, communityService);

            return new CardCommunityHarness(connection, db, controller, communityService, resolvedEconomyService);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
