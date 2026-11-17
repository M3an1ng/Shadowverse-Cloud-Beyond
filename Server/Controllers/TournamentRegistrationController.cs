using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/tournaments/{id:guid}")]
[Authorize(Policy = AccountSession.PlayerPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TournamentRegistrationController(
    ApplicationDbContext db,
    IDeckValidationService validation,
    IDeckCodeService codes,
    TimeProvider clock) : ControllerBase
{
    [HttpGet("registration")]
    [HttpGet("stages/{stageNumber:int}/lineup")]
    public async Task<ActionResult<TournamentRegistrationDto>> Get(
        Guid id, CancellationToken cancellationToken, [FromRoute] int stageNumber = 1)
    {
        if (AccountSession.GetUserId(User) is not Guid userId) return Unauthorized();
        var tournament = await FindPublishedAsync(id, cancellationToken);
        if (tournament?.Stages.SingleOrDefault(stage => stage.Number == stageNumber) is not { } stage)
            return NotFound("没有找到赛事或比赛阶段。");

        var entry = await FindEntryAsync(id, userId, stageNumber, cancellationToken);
        return Ok(await DescribeAsync(tournament, stage, userId, entry, cancellationToken));
    }

    [HttpPut("registration")]
    [HttpPut("stages/{stageNumber:int}/lineup")]
    public async Task<ActionResult<TournamentRegistrationDto>> Submit(
        Guid id, SubmitTournamentEntryRequest request, CancellationToken cancellationToken, [FromRoute] int stageNumber = 1)
    {
        if (AccountSession.GetUserId(User) is not Guid userId) return Unauthorized();
        if (request.DeckIds.Distinct().Count() != request.DeckIds.Length)
            return BadRequest("同一套卡组只能提交一次。");

        // SQLite takes the write lock before counting entrants, so two requests cannot both take the last seat.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var tournament = await FindPublishedAsync(id, cancellationToken);
        if (tournament?.Stages.SingleOrDefault(stage => stage.Number == stageNumber) is not { } stage)
            return NotFound("没有找到赛事或比赛阶段。");

        var entry = await FindEntryAsync(id, userId, stageNumber, cancellationToken);
        if (await BlockedReasonAsync(tournament, stage, entry, cancellationToken) is { } blocked)
            return Conflict(blocked);
        if (request.Revision != (entry?.Revision ?? 0))
            return Conflict("报名记录已有更新，请刷新后重试。");

        var required = tournament.GetRequiredDeckCount(stageNumber);
        if (request.DeckIds.Length != required)
            return BadRequest($"请按顺序提交 {required} 套卡组。");

        var savedDecks = await EligibleDecks(tournament, userId)
            .Where(deck => request.DeckIds.Contains(deck.Id)).Include(deck => deck.Cards)
            .ToDictionaryAsync(deck => deck.Id, cancellationToken);
        if (savedDecks.Count != required)
            return BadRequest("请使用你账号下符合赛事环境和构筑规则的完整卡组。");

        var classes = new HashSet<CardClass>();
        var submitted = new List<(UserDeck Deck, List<DeckCardEntryDto> Cards)>();
        foreach (var idInOrder in request.DeckIds)
        {
            var deck = savedDecks[idInOrder];
            // Mixed-class decks reserve both classes in the submitted lineup.
            if (!classes.Add(deck.Class) || (deck.SecondaryClass is { } secondary && !classes.Add(secondary)))
                return BadRequest("同一名选手的卡组不能有重复职业。");

            var cards = deck.Cards.Select(card => new DeckCardEntryDto
            {
                CardDefinitionId = card.CardDefinitionId,
                Count = card.Count
            }).ToArray();
            var validated = await validation.ValidateAsync(deck.Class, deck.SecondaryClass, cards,
                tournament.DeckEnvironment, tournament.DeckRule, requireComplete: true, cancellationToken);
            if (validated.Data is not { } legalCards)
                return BadRequest($"卡组「{deck.Name}」未通过校验。{validated.Error}");
            submitted.Add((deck, legalCards));
        }

        var cardIds = submitted.SelectMany(deck => deck.Cards.Select(card => card.CardDefinitionId)).Distinct().ToArray();
        var definitions = await db.CardDefinitions.AsNoTracking().Where(card => cardIds.Contains(card.Id))
            .Select(card => new { card.Id, card.Name, card.Cost })
            .ToDictionaryAsync(card => card.Id, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        if (stageNumber == 1 && !tournament.IsRegistrationOpen(now)) return Conflict("报名已截止。");
        if (stageNumber > 1 && !tournament.IsStageDeckSubmissionOpen(stage, now)) return Conflict("重交卡组已截止。");
        if (entry is null)
        {
            var playerName = await db.Users.Where(user => user.Id == userId)
                .Select(user => user.Username).SingleAsync(cancellationToken);
            entry = new TournamentEntry
            {
                Id = Guid.NewGuid(), TournamentId = id, UserId = userId, PlayerName = playerName, RegisteredAtUtc = now
            };
            db.TournamentEntries.Add(entry);
        }

        var previous = entry.Decks.Where(deck => deck.StageNumber == stageNumber).ToDictionary(deck => deck.Slot);
        for (var index = 0; index < submitted.Count; index++)
        {
            var slot = index + 1;
            if (!previous.TryGetValue(slot, out var snapshot))
            {
                snapshot = new TournamentEntryDeck { Id = Guid.NewGuid(), EntryId = entry.Id, StageNumber = stageNumber, Slot = slot };
                entry.Decks.Add(snapshot);
            }
            var (deck, cards) = submitted[index];
            snapshot.SourceDeckId = deck.Id;
            snapshot.Name = deck.Name;
            snapshot.Class = deck.Class;
            snapshot.SecondaryClass = deck.SecondaryClass;
            snapshot.DeckCode = codes.Encode(deck.Class, deck.SecondaryClass, tournament.DeckEnvironment, tournament.DeckRule, cards);
            snapshot.CardsJson = JsonSerializer.Serialize(cards.Select(card =>
            {
                var definition = definitions[card.CardDefinitionId];
                return new TournamentDeckCard(definition.Id, definition.Name, definition.Cost, card.Count);
            }).OrderBy(card => card.Cost).ThenBy(card => card.CardDefinitionId));
        }
        entry.Revision++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(await DescribeAsync(tournament, stage, userId, entry, cancellationToken));
    }

    private Task<Tournament?> FindPublishedAsync(Guid id, CancellationToken cancellationToken) =>
        db.Tournaments.AsNoTracking().Include(tournament => tournament.Stages)
            .SingleOrDefaultAsync(tournament => tournament.Id == id && tournament.Status != TournamentStatus.Draft, cancellationToken);

    private Task<TournamentEntry?> FindEntryAsync(Guid id, Guid userId, int stageNumber, CancellationToken cancellationToken) =>
        db.TournamentEntries.Include(entry => entry.Decks.Where(deck => deck.StageNumber <= stageNumber))
            .SingleOrDefaultAsync(entry => entry.TournamentId == id && entry.UserId == userId, cancellationToken);

    private IQueryable<UserDeck> EligibleDecks(Tournament tournament, Guid userId) =>
        db.UserDecks.AsNoTracking().Where(deck => deck.UserId == userId
            && deck.Environment == tournament.DeckEnvironment && deck.Rule == tournament.DeckRule
            && deck.Cards.Sum(card => card.Count) == GameConstants.Cards.StandardDeckSize);

    private async Task<string?> BlockedReasonAsync(
        Tournament tournament, TournamentStage stage, TournamentEntry? entry, CancellationToken cancellationToken)
    {
        if (stage.Number > 1)
        {
            if (entry is null || !await db.TournamentStageEntries.AnyAsync(participant => participant.TournamentId == tournament.Id
                && participant.StageNumber == stage.Number && participant.EntryId == entry.Id && participant.CanPlay, cancellationToken))
                return "本阶段仅对晋级选手开放。";
            if (!stage.AllowDeckResubmission) return "本阶段沿用之前的卡组。";
            if (!tournament.IsStageDeckSubmissionOpen(stage, clock.GetUtcNow().UtcDateTime))
                return "重交卡组已截止。";
            return null;
        }

        var registrationStatus = tournament.GetRegistrationStatus(clock.GetUtcNow().UtcDateTime);
        if (registrationStatus != "open")
            return registrationStatus == "scheduled" ? "报名尚未开始。" : "报名已截止。";
        if (entry is not null)
            return stage.AllowDeckResubmission ? null : "已报名，卡组已锁定。";
        if (tournament.PlayerLimit is { } limit
            && await db.TournamentEntries.CountAsync(entry => entry.TournamentId == tournament.Id, cancellationToken) >= limit)
            return "报名人数已满。";
        return null;
    }

    private async Task<TournamentRegistrationDto> DescribeAsync(
        Tournament tournament, TournamentStage stage, Guid userId, TournamentEntry? entry, CancellationToken cancellationToken)
    {
        var blocked = await BlockedReasonAsync(tournament, stage, entry, cancellationToken);
        var required = tournament.GetRequiredDeckCount(stage.Number);
        var available = blocked is null
            ? await EligibleDecks(tournament, userId).OrderByDescending(deck => deck.UpdatedAt).ThenBy(deck => deck.Id)
                .Select(deck => new TournamentDeckOptionDto(deck.Id, deck.Name, deck.Class, deck.SecondaryClass))
                .ToArrayAsync(cancellationToken)
            : [];
        var submitted = entry?.GetDecksForStage(stage.Number, required)
            .Select(deck => new TournamentDeckOptionDto(deck.SourceDeckId, deck.Name, deck.Class, deck.SecondaryClass))
            .ToArray() ?? [];
        return new TournamentRegistrationDto(entry?.Id, entry?.Revision ?? 0, required, blocked,
            submitted, available);
    }
}
