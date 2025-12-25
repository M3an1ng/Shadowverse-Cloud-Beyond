using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface IRecordedEventMapper
    {
        IReadOnlyList<RecordedClientDtoEnvelopeDto> MapClientDtos(IReadOnlyList<GameEventDto> dtos);

        GameEventDto? TryDeserializeClientDto(RecordedClientDtoEnvelopeDto envelope);
    }
}

