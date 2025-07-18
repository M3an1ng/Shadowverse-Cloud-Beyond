// File: Server/Domain/Factories/ICardFactory.cs
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums; // For CardZone
using System;
namespace ShadowverseBeyond.Server.Domain.Factories
{
    public interface ICardFactory
    {
        CardInstance CreateAndRegisterNewCard(GameState gameState, int cardDefinitionId, Guid ownerPlayerId, CardZone initialZone);
        CardInstance CreateAndRegisterCardCopy(GameState gameState, CardInstance originalCardToCopy, Guid newOwnerPlayerId, CardZone initialZone, bool exactCopy);
    }
}