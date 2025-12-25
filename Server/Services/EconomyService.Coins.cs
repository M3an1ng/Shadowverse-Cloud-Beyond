using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

public partial class EconomyService
{
    #region 金币管理

    public async Task<int> GetUserCoinsAsync(Guid userId)
    {
        var userEconomy = await GetOrCreateUserEconomyAsync(userId);
        return userEconomy.Coins;
    }

    public async Task<bool> AddCoinsAsync(Guid userId, int amount, CoinTransactionType type, string? description = null, string? relatedEntityId = null)
    {
        if (amount <= 0)
        {
            _logger.LogWarning("尝试添加非正数金币: UserId={UserId}, Amount={Amount}", userId, amount);
            return false;
        }

        try
        {
            return await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var userEconomy = await GetOrCreateUserEconomyAsync(userId);

                userEconomy.Coins += amount;
                userEconomy.UpdatedAt = DateTime.UtcNow;

                var coinTransaction = new CoinTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Amount = amount,
                    Type = type,
                    Description = description,
                    RelatedEntityId = relatedEntityId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CoinTransactions.Add(coinTransaction);

                _logger.LogInformation("成功添加金币: UserId={UserId}, Amount={Amount}, Type={Type}", userId, amount, type);
                return true;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加金币失败: UserId={UserId}, Amount={Amount}", userId, amount);
            return false;
        }
    }

    public async Task<bool> SpendCoinsAsync(Guid userId, int amount, CoinTransactionType type, string? description = null, string? relatedEntityId = null)
    {
        if (amount <= 0)
        {
            _logger.LogWarning("尝试扣除非正数金币: UserId={UserId}, Amount={Amount}", userId, amount);
            return false;
        }

        try
        {
            return await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var userEconomy = await GetOrCreateUserEconomyAsync(userId);

                if (userEconomy.Coins < amount)
                {
                    _logger.LogWarning("金币不足: UserId={UserId}, Required={Amount}, Available={Available}", userId, amount, userEconomy.Coins);
                    return false;
                }

                userEconomy.Coins -= amount;
                userEconomy.UpdatedAt = DateTime.UtcNow;

                var coinTransaction = new CoinTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Amount = -amount,
                    Type = type,
                    Description = description,
                    RelatedEntityId = relatedEntityId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CoinTransactions.Add(coinTransaction);

                _logger.LogInformation("成功扣除金币: UserId={UserId}, Amount={Amount}, Type={Type}", userId, amount, type);
                return true;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扣除金币失败: UserId={UserId}, Amount={Amount}", userId, amount);
            return false;
        }
    }

    public async Task<List<CoinTransactionDto>> GetCoinHistoryAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;

        var transactions = await _context.CoinTransactions
            .Where(ct => ct.UserId == userId)
            .OrderByDescending(ct => ct.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return transactions.Select(CoinTransactionDto.FromEntity).ToList();
    }

    #endregion
}

