using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Mappings;

public class GameStateProfile : Profile
{
    public GameStateProfile()
    {
        // Existing event/AI callers keep IMapper; the explicit projection owns all runtime fields.
        CreateMap<GameState, GameStateDto>()
            .ConvertUsing(source => source == null ? null! : GameStateProjection.Create(source, GameServiceProvider.CurrentGameContext));
    }
}
