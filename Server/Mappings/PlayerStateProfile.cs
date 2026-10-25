using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Mappings;

public class PlayerStateProfile : Profile
{
    public PlayerStateProfile()
    {
        // Existing event/AI callers keep IMapper; the explicit projection owns all runtime fields.
        CreateMap<PlayerState, PlayerStateDto>()
            .ConvertUsing(source => source == null ? null! : PlayerStateProjection.Create(source, GameServiceProvider.CurrentGameContext));
    }
}
