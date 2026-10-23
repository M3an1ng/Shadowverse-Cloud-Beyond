using System;

namespace ShadowverseBeyond.Server.Services
{
    public class EmoteCatalogOptions
    {
        public string RelativeRoot { get; set; } = "assets/emotes";
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(60);
    }
}
