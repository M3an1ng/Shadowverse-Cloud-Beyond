using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

public partial class EconomyService
{
    #region 用户经济信息

    public async Task<UserEconomy> GetOrCreateUserEconomyAsync(Guid userId)
    {
        var userEconomy = await _context.UserEconomies
            .FirstOrDefaultAsync(ue => ue.UserId == userId);

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
            await _context.SaveChangesAsync();

            _logger.LogInformation("创建新用户经济信息: UserId={UserId}", userId);
        }

        return userEconomy;
    }

    public async Task<bool> InitializeUserEconomyAsync(Guid userId)
    {
        try
        {
            await GetOrCreateUserEconomyAsync(userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化用户经济信息失败: UserId={UserId}", userId);
            return false;
        }
    }

    #endregion
}

