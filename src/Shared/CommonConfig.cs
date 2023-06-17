using Foundation.ModConfig;

namespace ProspectTogether.Shared
{
    public abstract class CommonConfig : ModConfigBase
    {
        public int SaveIntervalMinutes { get; set; } = 5;
    }
}
