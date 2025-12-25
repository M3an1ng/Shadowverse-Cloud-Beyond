using ShadowverseBeyond.Server.Dtos.Replay;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface IRawEventValidator
    {
        bool TryValidate(RawEventEnvelopeDto rawEvent, out string? errorMessage);
    }
}

