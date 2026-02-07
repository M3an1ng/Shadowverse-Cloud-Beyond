using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class PlayerConnectionStatusEventDto
    {
        public Guid PlayerId { get; set; }
        public bool IsConnected { get; set; }
        public string? Reason { get; set; }
    }
}
