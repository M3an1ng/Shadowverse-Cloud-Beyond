using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for debug console commands sent from the frontend.
    /// </summary>
    public class DebugCommandDto
    {
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Key/value arguments parsed on the client. Values are strings; server is responsible for validation/parsing.
        /// </summary>
        public Dictionary<string, string> Args { get; set; } = new();
    }
}
