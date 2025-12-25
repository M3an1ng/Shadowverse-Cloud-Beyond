using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Dsl;
using ShadowverseBeyond.Server.Infrastructure.Data;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.DSL;

public class DslFavoritesControllerTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static User BuildUser(Guid userId)
    {
        return new User
        {
            Id = userId,
            Username = "tester",
            Email = "tester@example.com",
            PasswordHash = "hash"
        };
    }

    [Fact]
    public async Task CreateSearchUpdateDeleteAndUse_Works()
    {
        var dbName = $"dsl-favorites-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);

        var userId = Guid.NewGuid();
        db.Users.Add(BuildUser(userId));
        await db.SaveChangesAsync();

        var controller = new DslFavoritesController(db, NullLogger<DslFavoritesController>.Instance);

        var createRequest = new CreateAbilityDslFavoriteRequest
        {
            UserId = userId,
            Label = "测试收藏",
            Expression = "5",
            ExpectedType = "Int32",
            Tags = new List<string> { "tag-a" }
        };

        var createdResult = await controller.Create(createRequest);
        var created = Assert.IsType<CreatedAtActionResult>(createdResult.Result);
        var createdDto = Assert.IsType<AbilityDslFavoriteDto>(created.Value);
        Assert.Equal("测试收藏", createdDto.Label);

        var searchResult = await controller.Search(userId, null, "Int32", 1, 10);
        var searchOk = Assert.IsType<OkObjectResult>(searchResult.Result);
        var searchDto = Assert.IsType<DslFavoriteSearchResultDto>(searchOk.Value);
        Assert.Equal(1, searchDto.Total);

        var updateRequest = new UpdateAbilityDslFavoriteRequest
        {
            UserId = userId,
            Label = "更新收藏",
            Tags = new List<string> { "tag-b" }
        };

        var updateResult = await controller.Update(createdDto.Id, updateRequest);
        var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updateDto = Assert.IsType<AbilityDslFavoriteDto>(updateOk.Value);
        Assert.Equal("更新收藏", updateDto.Label);

        var useResult = await controller.MarkUsed(createdDto.Id, userId);
        var useOk = Assert.IsType<OkObjectResult>(useResult.Result);
        var usedDto = Assert.IsType<AbilityDslFavoriteDto>(useOk.Value);
        Assert.Equal(1, usedDto.UsageCount);
        Assert.NotNull(usedDto.LastUsedAt);

        var deleteResult = await controller.Delete(createdDto.Id, userId);
        Assert.IsType<NoContentResult>(deleteResult);
    }
}
