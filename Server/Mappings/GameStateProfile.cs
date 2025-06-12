using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic; // Required for List

namespace ShadowverseBeyond.Server.Mappings
{
    /// <summary>
    /// Configures AutoMapper profiles for mapping between GameState entities and DTOs.
    /// </summary>
    public class GameStateProfile : Profile
    {
        public GameStateProfile()
        {
            CreateMap<GameState, GameStateDto>()
                // Map Status enum to GameStatus enum (different names)
                .ForMember(dest => dest.GameStatus, opt => opt.MapFrom(src => src.Status))
                // Combine Player1 and Player2 into the Players list
                .ForMember(dest => dest.Players, opt => opt.MapFrom(src => new List<PlayerState> { src.Player1, src.Player2 }));
            // AutoMapper will use PlayerStateProfile for the items in the list.

            // Properties with matching names and types are mapped automatically:
            // GameId, CurrentPlayerId, Phase, FirstPlayerId, IsGameOver, WinnerId, GameMessage

            // GameState.TurnNumber is ignored as it doesn't exist in GameStateDto.
        }
    }
}