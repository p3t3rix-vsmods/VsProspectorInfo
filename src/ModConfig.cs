using Foundation.ModConfig;
using ProspectorInfo.Models;

namespace ProspectorInfo
{
    public class ModConfig : ModConfigBase
    {
        public override string ModCode => "vsprospectorinfo";

        public bool RenderTexturesOnMap { get; set; } = false;
        public ColorWithAlpha TextureColor { get; set; } = new ColorWithAlpha(150, 125, 150, 128);
        public ColorWithAlpha BorderColor { get; set; } = new ColorWithAlpha(0, 0, 0, 200);
        public int BorderThickness { get; set; } = 1;
        public bool RenderBorder { get; set; } = true;
        public bool AutoToggle { get; set; } = true;
    }
}
