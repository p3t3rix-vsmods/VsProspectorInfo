using Newtonsoft.Json;
using Vintagestory.API.MathTools;

namespace ProspectorInfo.Models
{
    public class ColorWithAlpha
    {
        public ColorWithAlpha(int r, int g, int b, int a)
        {
            this.Red = r;
            this.Green = g;
            this.Blue = b;
            this.Alpha = a;
        }

        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }
        public int Alpha { get; set; }

        [JsonIgnore]
        public int RGBA { get => ColorUtil.ToRgba(this.Alpha, this.Blue, this.Green, this.Red); }

        public ColorWithAlpha CopyWith(ColorWithAlpha other)
        {
            return new ColorWithAlpha(
                other.Red == -1 ? Red : other.Red,
                other.Green == -1 ? Green : other.Green,
                other.Blue == -1 ? Blue : other.Blue,
                other.Alpha == -1 ? Alpha : other.Alpha);
        }
    }
}
