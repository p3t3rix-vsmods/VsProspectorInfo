using System.Collections.Generic;

namespace ProspectorInfo.Map
{
    internal class ProspectInfo
    {
        public readonly int X;
        public readonly int Z;
        public string Message;
        public List<OreOccurence> Values;

        public ProspectInfo(int x, int z, string message)
        {
            X = x;
            Z = z;
            Message = message;
            Values = new List<OreOccurence>();
            ParseMessage();
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

        /// <summary>
        /// Parses Message into a list of OreOccurence. Works only for English. 
        /// //TODO find a way to get the data multi lingual
        /// </summary>
        public void ParseMessage()
        {
            string[] seperator = { "\r\n" };
            string[] split = Message.Split(seperator, System.StringSplitOptions.RemoveEmptyEntries);
            string[] keySeperator = { ", ", " (", "‰" };
            for (int i = 1; i < split.Length; i++)
            {
                if (split[i].StartsWith("Miniscule"))
                {
                    string[] keys = split[i].Substring(21).Split(keySeperator, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var key in keys)
                    {
                        Values.Add(new OreOccurence(key, RelativeDensity.Miniscule, 0));
                    }
                }
                else if (split[i].StartsWith("Very poor"))
                {
                    string[] key = split[i].Substring(10).Split(keySeperator, System.StringSplitOptions.RemoveEmptyEntries);
                    if (!System.Single.TryParse(key[1].Replace(',', '.'), out float absolute))
                        absolute = -1;

                    Values.Add(new OreOccurence(key[0], RelativeDensity.VeryPoor, absolute));
                }
                else if (split[i].StartsWith("Poor"))
                {
                    string[] key = split[i].Substring(5).Split(keySeperator, System.StringSplitOptions.RemoveEmptyEntries);
                    if (!System.Single.TryParse(key[1].Replace(',', '.'), out float absolute))
                        absolute = -1;

                    Values.Add(new OreOccurence(key[0], RelativeDensity.Poor, absolute));
                }
                else if (split[i].StartsWith("Decent"))
                {
                    string[] key = split[i].Substring(7).Split(keySeperator, System.StringSplitOptions.RemoveEmptyEntries);
                    if (!System.Single.TryParse(key[1].Replace(',', '.'), out float absolute))
                        absolute = -1;

                    Values.Add(new OreOccurence(key[0], RelativeDensity.Decent, absolute));
                }
                else if (split[i].StartsWith("High"))
                {
                    string[] key = split[i].Substring(5).Split(keySeperator, System.StringSplitOptions.RemoveEmptyEntries);
                    if (!System.Single.TryParse(key[1].Replace(',', '.'), out float absolute))
                        absolute = -1;

                    Values.Add(new OreOccurence(key[0], RelativeDensity.High, absolute));
                }
                else if (split[i].StartsWith("Very high"))
                {
                    string[] key = split[i].Substring(10).Split(keySeperator, System.StringSplitOptions.RemoveEmptyEntries);
                    if (!System.Single.TryParse(key[1].Replace(',', '.'), out float absolute))
                        absolute = -1;

                    Values.Add(new OreOccurence(key[0], RelativeDensity.VeryHigh, absolute));
                }
                else if (split[i].StartsWith("Ultra high"))
                {
                    string[] key = split[i].Substring(11).Split(keySeperator, System.StringSplitOptions.RemoveEmptyEntries);
                    if (!System.Single.TryParse(key[1].Replace(',', '.'), out float absolute))
                        absolute = -1;

                    Values.Add(new OreOccurence(key[0], RelativeDensity.UltraHigh, absolute));
                }
            }
        }

        public RelativeDensity GetValueOfOre(string oreName)
        {
            foreach (var ore in Values)
            {
                if (ore.name == oreName)
                    return ore.relativeDensity;
            }
            return RelativeDensity.Zero;
        }
    }

    internal struct OreOccurence
    {
        public string name;
        public RelativeDensity relativeDensity;
        public float absoluteDensity;

        public OreOccurence(string name, RelativeDensity relativeDensity, float absoluteDensity)
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