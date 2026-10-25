using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Mappings;

public class CardInstanceProfile : Profile
{
    public CardInstanceProfile()
    {
        // Existing event/AI callers keep IMapper; the explicit projection owns all runtime fields.
        CreateMap<CardInstance, CardInstanceDto>()
            .ConvertUsing(source => source == null ? null! : CardInstanceProjection.Create(source, GameServiceProvider.CurrentGameContext));
    }
}
