using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Config;

namespace ProspectorInfo.Map
{
    internal class ProspectInfo
    {
        public static IEnumerable<KeyValuePair<string, string>> FoundOres { get { return AllOres.Where((pair) => foundOres.Contains(pair.Value)); } }
        
        private static readonly Dictionary<string, string> AllOres = new OreNames();
        private static readonly HashSet<string> foundOres = new HashSet<string>();
        private static readonly List<string> densityStrings = new List<string>{ 
            "propick-density-verypoor",
            "propick-density-poor",
            "propick-density-decent",
            "propick-density-high",
            "propick-density-veryhigh",
            "propick-density-ultrahigh"
        };
        private static readonly Dictionary<string, RelativeDensity> translatedDensity = new Dictionary<string, RelativeDensity>{
            { Lang.Get("propick-density-verypoor"), RelativeDensity.VeryPoor },
            { Lang.Get("propick-density-poor"), RelativeDensity.Poor },
            { Lang.Get("propick-density-decent"), RelativeDensity.Decent },
            { Lang.Get("propick-density-high"), RelativeDensity.High },
            { Lang.Get("propick-density-veryhigh"), RelativeDensity.VeryHigh },
            { Lang.Get("propick-density-ultrahigh"), RelativeDensity.UltraHigh }
        };
        private static readonly Regex _cleanupRegex = new Regex("<.*?>", RegexOptions.Compiled);
        private static readonly Regex _readingParsingRegex = new Regex(
            Lang.Get("propick-reading", "[?<relativeDensity>.*?]", "[?<pageCode>.*?]", "[?<oreName>.*?]", "[?<absoluteDensity>.*?]")
                .Replace("/", "\\/")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("[", "(")
                .Replace("]", ")"), 
            RegexOptions.Compiled
        );
        private static readonly Regex _tracesParsingRegex = new Regex(
            "<a href=\"handbook:\\/\\/(?<pageCode>.*?)\">(?<oreName>.*?)<\\/a>",
            RegexOptions.Compiled
        );

        public readonly int X;
        public readonly int Z;
        public readonly List<OreOccurence> Values;
        private readonly string Message; //Used for backwards compatibility

        [Newtonsoft.Json.JsonConstructor]
        public ProspectInfo(int x, int z, string message, List<OreOccurence> values)
        {
            X = x;
            Z = z;
            Values = values;
            Message = message;
            if (values == null)
                Values = new List<OreOccurence>();
            else
                foreach (var val in values)
                    foundOres.Add(val.Name);
        }

        public ProspectInfo(int x, int z, string message)
        {
            X = x;
            Z = z;
            Values = new List<OreOccurence>();
            ParseMessage(message);
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

        private void ParseMessage(string message)
        {
            var splits = message.Split('\n');
            for (var i = 1; i < splits.Length - 1; i++)
            {
                Match match = _readingParsingRegex.Match(splits[i]);
                if (match.Success)
                {
                    if (!translatedDensity.TryGetValue(match.Groups["relativeDensity"].Value, out RelativeDensity relativeDensity))
                        relativeDensity = RelativeDensity.Zero;

                    Values.Add(new OreOccurence(
                        AllOres[match.Groups["oreName"].Value],
                        match.Groups["pageCode"].Value,
                        relativeDensity,
                        double.Parse(match.Groups["absoluteDensity"].Value)
                    ));
                } else
                {
                    MatchCollection matches = _tracesParsingRegex.Matches(splits[i]);
                    foreach (Match elem in matches)
                        Values.Add(new OreOccurence(
                            AllOres[elem.Groups["oreName"].Value],
                            elem.Groups["pageCode"].Value,
                            RelativeDensity.Miniscule,
                            0
                        ));
                }
            }

            foreach (var val in Values)
                foundOres.Add(val.Name);
        }

        public string GetMessage()
        {
            StringBuilder sb = new StringBuilder();

            if (Values.Count > 0)
            {
                sb.AppendLine(Lang.Get("propick-reading-title", Values.Count));

                var sbTrace = new StringBuilder();
                int traceCount = 0;

                foreach (var elem in Values)
                {
                    if (elem.RelativeDensity > RelativeDensity.Miniscule)
                    {
                        string proPickReading = Lang.Get("propick-reading", Lang.Get(densityStrings[(int)elem.RelativeDensity - 2]), elem.PageCode, Lang.Get(elem.Name), elem.AbsoluteDensity.ToString("0.#"));
                        proPickReading = _cleanupRegex.Replace(proPickReading, string.Empty);
                        sb.AppendLine(proPickReading);
                    }
                    else
                    {
                        if (traceCount > 0)
                            sbTrace.Append(", ");
                        sbTrace.Append(Lang.Get(elem.Name));
                        traceCount++;
                    }
                }
                if (sbTrace.Length != 0)
                {
                    sb.Append(Lang.Get("Miniscule amounts of {0}", sbTrace.ToString()));
                    sb.AppendLine();
                }
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
                if (Lang.Get(ore.Name).ToLower() == oreName.ToLower() || ore.Name.ToLower() == oreName.ToLower())
                    return ore.RelativeDensity;
            }
            return RelativeDensity.Zero;
        }
    }
}
