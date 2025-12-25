using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public class JsonRecordedEventMapper : IRecordedEventMapper
    {
        public IReadOnlyList<RecordedClientDtoEnvelopeDto> MapClientDtos(IReadOnlyList<GameEventDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<RecordedClientDtoEnvelopeDto>();

            var envelopes = new List<RecordedClientDtoEnvelopeDto>(dtos.Count);
            foreach (var dto in dtos)
            {
                try
                {
                    var dtoType = dto.GetType();
                    var json = JsonSerializer.Serialize(dto, dtoType);
                    envelopes.Add(new RecordedClientDtoEnvelopeDto(
                        dtoType.AssemblyQualifiedName ?? dtoType.FullName ?? dtoType.Name,
                        json));
                }
                catch
                {
                    // Best-effort: skip serialization failures.
                }
            }

            return envelopes;
        }

        public GameEventDto? TryDeserializeClientDto(RecordedClientDtoEnvelopeDto envelope)
        {
            if (string.IsNullOrWhiteSpace(envelope.Type) || string.IsNullOrWhiteSpace(envelope.Json)) return null;

            try
            {
                var dtoType = Type.GetType(envelope.Type, throwOnError: false);
                if (dtoType == null) return null;
                if (!typeof(GameEventDto).IsAssignableFrom(dtoType)) return null;

                return JsonSerializer.Deserialize(envelope.Json, dtoType) as GameEventDto;
            }
            catch
            {
                return null;
            }
        }
    }
}

