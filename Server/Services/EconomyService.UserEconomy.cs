using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

public partial class EconomyService
{
    #region 用户经济信息

    public async Task<UserEconomy> GetOrCreateUserEconomyAsync(Guid userId)
    {
        if (!await UserExistsAsync(userId))
        {
            throw new InvalidOperationException($"Cannot create economy for missing user {userId}.");
        }

        var userEconomy = _context.UserEconomies.Local.FirstOrDefault(ue => ue.UserId == userId)
            ?? await _context.UserEconomies.FirstOrDefaultAsync(ue => ue.UserId == userId);

        if (userEconomy == null)
        {
            userEconomy = new UserEconomy
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Coins = 1000, // 初始金币

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserEconomies.Add(userEconomy);
            // 外层事务统一保存，避免初始化时提前写入领奖或结算的其他待保存状态。
            if (!_transactionService.IsInTransaction())
            {
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("创建新用户经济信息: UserId={UserId}", userId);
        }

        return userEconomy;
    }

    public async Task<bool> InitializeUserEconomyAsync(Guid userId)
    {
        try
        {
            if (!await EnsurePersistedUserAsync(userId, nameof(InitializeUserEconomyAsync)))
            {
                return false;
            }

            await GetOrCreateUserEconomyAsync(userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化用户经济信息失败: UserId={UserId}", userId);
            throw;
        }
    }

    #endregion
}
