using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Util;

namespace ProspectorInfo.Map
{
    internal class ProspectorMessages : Dictionary<ChunkCoordinates, ProspectInfo> 
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

    [ProtoBuf.ProtoContract(ImplicitFields = ProtoBuf.ImplicitFields.None)]
    internal struct OreOccurence
    {
        [ProtoBuf.ProtoMember(1)]
        public readonly string Name;
        [ProtoBuf.ProtoMember(2)]
        public readonly string PageCode;
        [ProtoBuf.ProtoMember(3, IsRequired = true)]
        public readonly RelativeDensity RelativeDensity;
        [ProtoBuf.ProtoMember(4)]
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

    [ProtoBuf.ProtoContract(ImplicitFields = ProtoBuf.ImplicitFields.None)]
    public struct ChunkCoordinates
    {
        [ProtoBuf.ProtoMember(1)]
        public int X;
        [ProtoBuf.ProtoMember(2)]
        public int Z;

        public ChunkCoordinates(int x, int z)
        {
            X = x;
            Z = z;
        }
    }
}
