using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Util;

namespace ProspectorInfo.Map
{
    // Should be a dictionary with coordinates as key, but NewtonSoft Json does not play
    // nicely with complex objects as key...
    internal class ProspectorMessages : List<ProspectInfo> 
    {
        [JsonIgnore]
        public bool HasChanged { get; set; } = false;
    }

    internal class OreNames : Dictionary<string, string>
    {
        public OreNames()
        {
            IDictionary<string, string> oreValues = Vintagestory.API.Config.Lang.GetAllEntries();
            // game:ore-lapis is a leftover and unused so it can be removed. See https://discord.com/channels/302152934249070593/351624415039193098/1009372460568805427
            oreValues.RemoveAll((key, val) => !key.Contains(":ore-") || key.CountChars('-') != 1 || key.Contains("_") || key == "game:ore-lapis");
            foreach (var elem in oreValues)
                if (!TryGetValue(elem.Value, out string _)) // Ores with the same translation will be saved under the same tag
                    Add(elem.Value, elem.Key);
        }
    }

    internal struct OreOccurence
    {
        public readonly string Name;
        public readonly string PageCode;
        public readonly RelativeDensity RelativeDensity;
        public readonly double AbsoluteDensity;

        [Newtonsoft.Json.JsonConstructor]
        public OreOccurence(string name, string pageCode, RelativeDensity relativeDensity, double absoluteDensity)
        {
            Name = name;
            PageCode = pageCode;
            RelativeDensity = relativeDensity;
            AbsoluteDensity = absoluteDensity;
        }
    }

    internal enum RelativeDensity
    {
        Zero,
        Miniscule,
        VeryPoor,
        Poor,
        Decent,
        High,
        VeryHigh,
        UltraHigh
    }

    public enum MapMode
    {
        Default,
        Heatmap
    }
}
