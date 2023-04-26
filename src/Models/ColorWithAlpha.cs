using Newtonsoft.Json;
using Vintagestory.API.MathTools;

namespace ProspectorInfo.Models
{
    public class ColorWithAlpha
    {
        public ColorWithAlpha(byte r, byte g, byte b, byte a)
        {
            this.Red = r;
            this.Green = g;
            this.Blue = b;
            this.Alpha = a;
        }

        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public byte Alpha { get; set; }

        [JsonIgnore]
        public int RGBA { get => ColorUtil.ToRgba(this.Alpha, this.Blue, this.Green, this.Red); }
    }
}
