using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Services;

public partial class EconomyService
{
    #region 卡包系统

    public async Task<List<CardPackDefinitionDto>> GetAvailableCardPacksAsync(DeckEnvironment? environment = null, bool includeInactive = false)
    {
        var query = _context.CardPackDefinitions
            .Include(cpd => cpd.CardSetDefinition)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(cpd => cpd.IsActive);
        }

        var cardPacks = await query
            .OrderBy(cpd => cpd.Region)
            .ThenBy(cpd => cpd.Sequence)
            .ToListAsync();

        if (environment.HasValue)
        {
            cardPacks = cardPacks
                .Where(cpd => (cpd.CardSetDefinition?.Environments ?? new List<DeckEnvironment>()).Contains(environment.Value))
                .ToList();
        }

        return cardPacks.Select(CardPackDefinitionDto.FromEntity).ToList();
    }

    public async Task<UserCardPackDto?> PurchaseCardPackAsync(Guid userId, int cardPackDefinitionId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(PurchaseCardPackAsync)))
        {
            return null;
        }

        try
        {
            return await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var cardPackDefinition = await _context.CardPackDefinitions
                    .Include(cpd => cpd.CardSetDefinition)
                    .FirstOrDefaultAsync(cpd => cpd.Id == cardPackDefinitionId && cpd.IsActive);

                if (cardPackDefinition == null)
                {
                    _logger.LogWarning("卡包定义不存在或已停用: CardPackDefinitionId={CardPackDefinitionId}", cardPackDefinitionId);
                    return null;
                }

                var success = await SpendCoinsAsync(
                    userId,
                    cardPackDefinition.Price,
                    CoinTransactionType.CardPackPurchase,
                    $"购买{cardPackDefinition.Name}",
                    cardPackDefinitionId.ToString());

                if (!success)
                {
                    return null;
                }

                var userCardPack = new UserCardPack
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CardPackDefinitionId = cardPackDefinitionId,
                    PurchasedAt = DateTime.UtcNow,
                    CardPackDefinition = cardPackDefinition
                };

                _context.UserCardPacks.Add(userCardPack);

                _logger.LogInformation("成功购买卡包: UserId={UserId}, CardPackId={CardPackId}, Price={Price}",
                    userId, userCardPack.Id, cardPackDefinition.Price);

                return UserCardPackDto.FromEntity(userCardPack);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "购买卡包失败: UserId={UserId}, CardPackDefinitionId={CardPackDefinitionId}",
                userId, cardPackDefinitionId);
            throw;
        }
    }

    public async Task<List<UserCardDto>> OpenCardPackAsync(Guid userId, Guid cardPackId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(OpenCardPackAsync)))
        {
            return new List<UserCardDto>();
        }

        try
        {
            var result = await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var userCardPack = await _context.UserCardPacks
                    .Include(ucp => ucp.CardPackDefinition)
                    .ThenInclude(cpd => cpd.CardSetDefinition)
                    .FirstOrDefaultAsync(ucp => ucp.Id == cardPackId && ucp.UserId == userId);

                if (userCardPack == null)
                {
                    _logger.LogWarning("用户卡包不存在: UserId={UserId}, CardPackId={CardPackId}", userId, cardPackId);
                    return new List<UserCardDto>();
                }

                if (userCardPack.IsOpened)
                {
                    _logger.LogWarning("卡包已经开启过: UserId={UserId}, CardPackId={CardPackId}", userId, cardPackId);
                    return new List<UserCardDto>();
                }

                var generatedCards = await GenerateCardsForPackAsync(userCardPack);

                userCardPack.Open(generatedCards);

                _context.UserCards.AddRange(generatedCards);

                _logger.LogInformation("成功开启卡包: UserId={UserId}, CardPackId={CardPackId}, CardsCount={CardsCount}",
                    userId, cardPackId, generatedCards.Count);

                return generatedCards.Select(UserCardDto.FromEntity).ToList();
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开启卡包失败: UserId={UserId}, CardPackId={CardPackId}", userId, cardPackId);
            throw;
        }
    }

    public async Task<List<UserCardPackDto>> GetUserCardPacksAsync(Guid userId, bool unopenedOnly = false)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(GetUserCardPacksAsync)))
        {
            return new List<UserCardPackDto>();
        }

        var query = _context.UserCardPacks
            .Include(ucp => ucp.CardPackDefinition)
            .ThenInclude(cpd => cpd.CardSetDefinition)
            .Where(ucp => ucp.UserId == userId);

        if (unopenedOnly)
        {
            query = query.Where(ucp => !ucp.IsOpened);
        }

        var userCardPacks = await query
            .OrderByDescending(ucp => ucp.PurchasedAt)
            .ToListAsync();

        return userCardPacks.Select(ucp => UserCardPackDto.FromEntity(ucp)).ToList();
    }

    #endregion
}
