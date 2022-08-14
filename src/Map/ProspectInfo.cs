using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace ProspectorInfo.Map
{
    internal class ProspectInfo
    {
        public readonly int X;
        public readonly int Z;
        public readonly List<OreOccurence> Values;
        [Newtonsoft.Json.JsonIgnore]
        private readonly string Message; //Used for backwards compatibility

        private readonly Regex _cleanupRegex = new Regex("<.*?>", RegexOptions.Compiled);

        [Newtonsoft.Json.JsonConstructor]
        public ProspectInfo(int x, int z, string message, List<OreOccurence> values)
        {
            X = x;
            Z = z;
            Message = message;
            Values = values;
            if (values == null)
                Values = new List<OreOccurence>();
        }

        public ProspectInfo(BlockPos pos, int chunkSize, ICoreServerAPI api, ProPickWorkSpace ppws)
        {
            X = pos.X / chunkSize;
            Z = pos.Z / chunkSize;
            Values = new List<OreOccurence>();
            GetDepositValues(pos, api, ppws);
        }

        public bool Equals(ProspectInfo other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ProspectInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public string GetMessage()
        {
            StringBuilder sb = new StringBuilder();

            if (Values.Count > 0)
            {
                sb.AppendLine(Lang.Get("propick-reading-title", Values.Count));

                string[] densityStrings = new string[] { "propick-density-verypoor", "propick-density-poor", "propick-density-decent", "propick-density-high", "propick-density-veryhigh", "propick-density-ultrahigh" };
                var sbTrace = new StringBuilder();
                int traceCount = 0;

                foreach (var elem in Values)
                {
                    if (elem.relativeDensity > RelativeDensity.Miniscule)
                    {
                        string proPickReading = Lang.Get("propick-reading", Lang.Get(densityStrings[(int)elem.relativeDensity - 2]), "ore", Lang.Get("ore-" + elem.name), elem.absoluteDensity.ToString("0.#"));
                        proPickReading = _cleanupRegex.Replace(proPickReading, string.Empty);
                        sb.AppendLine(proPickReading);
                    }
                    else
                    {
                        if (traceCount > 0)
                            sbTrace.Append(", ");
                        sbTrace.Append(Lang.Get("ore-" + elem.name));
                        traceCount++;
                    }
                }
                sb.Append(Lang.Get("Miniscule amounts of {0}", sbTrace.ToString()));
                sb.AppendLine();
            }
            else
            {
                if (Message != null)
                    sb.Append(Message);
                else
                    sb.Append(Lang.Get("propick-noreading"));
            }
            return sb.ToString();
        }

        public RelativeDensity GetValueOfOre(string oreName)
        {
            foreach (var ore in Values)
            {
                if (Lang.Get("ore-" + ore.name).ToLower() == oreName.ToLower() || ore.name.ToLower() == oreName.ToLower())
                    return ore.relativeDensity;
            }
            return RelativeDensity.Zero;
        }

        private void GetDepositValues(BlockPos pos, ICoreServerAPI api, ProPickWorkSpace ppws)
        {
            DepositVariant[] deposits = api.ModLoader.GetModSystem<GenDeposits>()?.Deposits;
            if (deposits == null) return;

            IBlockAccessor blockAccess = api.World.BlockAccessor;
            int regsize = blockAccess.RegionSize;

            IMapRegion reg = api.World.BlockAccessor.GetMapRegion(pos.X / regsize, pos.Z / regsize);
            int lx = pos.X % regsize;
            int lz = pos.Z % regsize;

            pos = pos.Copy();
            pos.Y = api.World.BlockAccessor.GetTerrainMapheightAt(pos);

            int[] blockColumn = ppws.GetRockColumn(pos.X, pos.Z);

            foreach (var val in reg.OreMaps)
            {
                IntDataMap2D map = val.Value;
                int noiseSize = map.InnerSize;

                float posXInRegionOre = (float)lx / regsize * noiseSize;
                float posZInRegionOre = (float)lz / regsize * noiseSize;

                int oreDist = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                ppws.depositsByCode[val.Key].GetPropickReading(pos, oreDist, blockColumn, out double ppt, out double totalFactor);

                if (totalFactor > 0.025)
                {
                    Values.Add(new OreOccurence(val.Key, (RelativeDensity)((int)GameMath.Clamp(totalFactor * 7.5f, 0, 5) + 2), ppt));
                }
                else if (totalFactor > 0.002)
                {
                    Values.Add(new OreOccurence(val.Key, RelativeDensity.Miniscule, totalFactor));
                }
            }
            Values.Sort(delegate (OreOccurence it, OreOccurence other) { return other.relativeDensity - it.relativeDensity; });
        }
    }

    internal struct OreOccurence
    {
        public string name;
        public RelativeDensity relativeDensity;
        public double absoluteDensity;

        public OreOccurence(string name, RelativeDensity relativeDensity, double absoluteDensity)
        {
            this.name = name;
            this.relativeDensity = relativeDensity;
            this.absoluteDensity = absoluteDensity;
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
}