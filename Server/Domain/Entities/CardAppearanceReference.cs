namespace ShadowverseBeyond.Server.Domain.Entities;

// Enough to resolve the owner's battle appearance after the source instance leaves play.
public readonly record struct CardAppearanceReference(Guid OwnerPlayerId, int DefinitionId, int? AlternateArtDefinitionId)
{
    public static CardAppearanceReference From(CardInstance card) =>
        new(card.OwnerPlayerId, card.CurrentDefinitionId, card.AlternateArtDefinitionId);
}
