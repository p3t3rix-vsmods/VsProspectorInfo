using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Config;

namespace ProspectorInfo.Map
{
    [ProtoBuf.ProtoContract(ImplicitFields = ProtoBuf.ImplicitFields.None)]
    internal class ProspectInfo
    {
        public static IEnumerable<KeyValuePair<string, string>> FoundOres { get { return _allOres.Where((pair) => _foundOres.Contains(pair.Value)); } }
        
        private static readonly Dictionary<string, string> _allOres = new OreNames();
        private static readonly HashSet<string> _foundOres = new HashSet<string>();

        /// <summary>
        /// Maps relative densities to their Lang string.
        /// Note that not every RelativeDensity has such a mapping.
        /// </summary>
        private static readonly Dictionary<RelativeDensity, string> _relativeDensityToLang = new Dictionary<RelativeDensity, string>{
                { RelativeDensity.VeryPoor, "propick-density-verypoor" },
                { RelativeDensity.Poor, "propick-density-poor"},
                { RelativeDensity.Decent, "propick-density-decent" },
                { RelativeDensity.High , "propick-density-high" },
                { RelativeDensity.VeryHigh , "propick-density-veryhigh" },
                { RelativeDensity.UltraHigh , "propick-density-ultrahigh" }
            };

        private static Dictionary<string, RelativeDensity> GetDensityMappingForLang(string langCode)
        {
            return _relativeDensityToLang.ToDictionary(entry => Lang.GetL(langCode, entry.Value), entry => entry.Key);
        }
        private static readonly Dictionary<string, RelativeDensity> _translatedDensities = GetDensityMappingForLang(Lang.CurrentLocale);

        

        private static readonly Regex _cleanupRegex = new Regex("<.*?>", RegexOptions.Compiled);
        private static readonly Regex _headerParsingRegex = new Regex(Lang.Get("propick-reading-title", ".*?"), RegexOptions.Compiled);
        private static readonly Regex _readingParsingRegex = new Regex(
            Lang.Get("propick-reading", "[?<relativeDensity>.*?]", "[?<pageCode>.*?]", "[?<oreName>.*?]", "[?<absoluteDensity>.*?]")
                .Replace("/", "\\/")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("[", "(")
                .Replace("]", ")"), 
            RegexOptions.Compiled
        );

        /// <summary>
        /// A regex to extract every page code and ore name from a text. Can be used in every language
        /// </summary>
        private static readonly Regex _tracesParsingRegex = new Regex(
            "<a href=\"handbook:\\/\\/(?<pageCode>.*?)\">(?<oreName>.*?)<\\/a>",
            RegexOptions.Compiled
        );

        /// <summary>
        /// A regex to extract decimal numbers from a string.
        /// </summary>
        private static readonly Regex _absoluteDensityRegex = new Regex("([0-9]+[.,]?[0-9]*)", 
            RegexOptions.Compiled);

        [ProtoBuf.ProtoMember(1)]
        public readonly int X;
        [ProtoBuf.ProtoMember(2)]
        public readonly int Z;

        /// <summary>
        /// We don't want to change the serialization format,
        /// so we keep the coordinates in a cache.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        private ChunkCoordinates? _coordinates = null;
        [Newtonsoft.Json.JsonIgnore]
        public ChunkCoordinates ChunkCoordinates
        { 
            get {
                if (_coordinates == null)
                {
                    _coordinates = new ChunkCoordinates(X, Z);
                }
                return (ChunkCoordinates)_coordinates;
            }
        }

        /// <summary>
        /// A sorted list of all ore occurencies in this chunk. The ore with the highest relative density is first.
        /// </summary>
        [ProtoBuf.ProtoMember(3)]
        public readonly List<OreOccurence> Values;

        /// <summary>
        /// The prospecting message that was send by the server. Used for backwards compatibitly and when parsing errors occur.
        /// </summary>
        [Newtonsoft.Json.JsonProperty]
        private string Message;

        /// <summary>
        /// The return value from <see cref="GetMessage"/> if it was called at least once. Used to avoid multiple StringBuilder calls.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        private string _messageCache;

        /// <summary>
        /// Required for ProtoBuf deserialization
        /// </summary>
        private ProspectInfo()
        {
        }

        [Newtonsoft.Json.JsonConstructor]
        public ProspectInfo(int x, int z, string message, List<OreOccurence> values)
        {
            X = x;
            Z = z;
            Values = values;
            Message = message;
            if (message != null) // ProspectInfo is legacy style message
            {
                Values = new List<OreOccurence>();
                ParseLegacyMessage();
            } 
            else
            {
                foreach (var val in values)
                    _foundOres.Add(val.Name);
            }            
        }

        public ProspectInfo(int x, int z, string message)
        {
            X = x;
            Z = z;
            Values = new List<OreOccurence>();
            try
            {
                ParseMessage(message);
            }
            catch (System.Exception)
            {
                Values = null;
                // We just save the message as it will be parsed correctly on the next load anyway.
                Message = _cleanupRegex.Replace(message, string.Empty);
            }
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
        /// The absolute density values that we get from the server are formatted with the servers system/OS locale (not game locale).
        /// Thus, we might get either ',' or '.' as a decimal separator.
        /// Since we cannot know the locale in advance, we replace all `,` with `.` and parse the number
        /// using the invariant culture, i.e., one that uses `.` as a hardcoded decimal separator.
        /// This can get problematic if we encounter numbers with digit grouping, e.g., `1,000.00`,
        /// but hopefully, this will never be case.
        /// </summary>
        private static double ParseDoubleInvariant(string value)
        {
            return double.Parse(value.Replace(",", "."), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Tries to parse the given <paramref name="message"/> in the current locale. If the locale of <paramref name="message"/> is not equal to the current locale,
        /// the message is just saved as is without parsing.
        /// If any of the exceptions below is thrown, <see cref="Values"/> should be cleared and <paramref name="message"/> should be saved to <see cref="Message"/>.
        /// </summary>
        /// <param name="message">The prospecting string received from the server</param>
        /// <exception cref="KeyNotFoundException">If a parsed ore name can not be found in oreNames or the density in translatedDensities</exception>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="System.FormatException"></exception>
        private void ParseMessage(string message)
        {
            // If header can not be matched, we are receiving a different locale than our current one is.
            var headerMatch = _headerParsingRegex.Match(message);
            if (!headerMatch.Success)
                throw new System.FormatException();

            // We have to remove the header before splitting as some languages (looking at you Slovensky) have a line break in their header
            string[] splits = message.Replace(headerMatch.Value, string.Empty).Split('\n');

            foreach (var reading in splits)
            {
                Match match = _readingParsingRegex.Match(reading);
                if (match.Success)
                {
                    Values.Add(new OreOccurence(
                        _allOres[match.Groups["oreName"].Value],
                        match.Groups["pageCode"].Value,
                        _translatedDensities[match.Groups["relativeDensity"].Value],
                        ParseDoubleInvariant(match.Groups["absoluteDensity"].Value)
                    ));
                } else
                {
                    MatchCollection matches = _tracesParsingRegex.Matches(reading);
                    foreach (Match elem in matches)
                        Values.Add(new OreOccurence(
                            _allOres[elem.Groups["oreName"].Value],
                            elem.Groups["pageCode"].Value,
                            RelativeDensity.Miniscule,
                            0
                        ));
                }
            }

            foreach (var val in Values)
                _foundOres.Add(val.Name);
        }

        /// <summary>
        /// Parses the legacy style messages (From VsProspectorInfo versions < 4.0.0).
        /// Should only be called on load since it is significantly slower than <see cref="ParseMessage(string)"/>.
        /// Checks every language to parse <see cref="Message"/> and sets the field to null afterwards.
        /// If no language can be matched to the Message, nothing happens.
        /// </summary>
        private void ParseLegacyMessage()
        {
            string[] splits = null;
            string langCode = null;

            foreach (var language in Lang.AvailableLanguages.Keys)
            {
                var langRegex = new Regex(Lang.GetL(language, "propick-reading-title", ".*?"));
                var langMatch = langRegex.Match(Message);
                if (langMatch.Success)
                {
                    splits = Message.Replace(langMatch.Value, string.Empty).Split('\n');
                    langCode = language;
                    break;
                }    
            }

            if (langCode == null)
                return;

            var ores = new Dictionary<string, string>();
            foreach (var ore in _allOres)
                ores[Lang.GetL(langCode, ore.Value)] = ore.Value;

            Dictionary<string, RelativeDensity> relativeDensities = GetDensityMappingForLang(langCode);

            foreach (var reading in splits)
            {
                RelativeDensity relativeDensity = RelativeDensity.Zero;

                foreach (var density in relativeDensities)
                {
                    if (reading.Contains(density.Key))
                    {
                        relativeDensity = density.Value;
                        break;
                    }
                }

                if (relativeDensity != RelativeDensity.Zero) 
                {
                    double absoluteDensity = ParseDoubleInvariant(_absoluteDensityRegex.Match(reading).Value);
                    
                    string oreName = null;
                    foreach (var ore in ores)
                        if (reading.Contains(ore.Key))
                        {
                            oreName = ore.Value;
                            break;
                        }

                    Values.Add(new OreOccurence(oreName, null, relativeDensity, absoluteDensity));
                }
                else // if it is RelativeDensity.Zero we are checking miniscule amounts
                {
                    foreach (var ore in ores)
                        if (reading.Contains(ore.Key))
                            Values.Add(new OreOccurence(ore.Value, null, RelativeDensity.Miniscule, 0));
                }
            }

            foreach (var val in Values)
                _foundOres.Add(val.Name);

            Message = null;
        }

        public string GetMessage()
        {
            if (_messageCache != null)
                return _messageCache;

            StringBuilder sb = new StringBuilder();

            if (Values != null && Values.Count > 0)
            {
                sb.AppendLine(Lang.Get("propick-reading-title", Values.Count));

                var sbTrace = new StringBuilder();
                int traceCount = 0;

                foreach (var elem in Values)
                {
                    if (elem.RelativeDensity > RelativeDensity.Miniscule)
                    {
                        string proPickReading = Lang.Get("propick-reading", Lang.Get(_relativeDensityToLang[elem.RelativeDensity]), elem.PageCode, Lang.Get(elem.Name), elem.AbsoluteDensity.ToString("0.#"));
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

            _messageCache = sb.ToString();
            return _messageCache;
        }

        public RelativeDensity GetValueOfOre(string oreName)
        {
            if (Values != null)
                foreach (var ore in Values)
                {
                    if (Lang.Get(ore.Name).ToLower() == oreName.ToLower() || ore.Name.ToLower() == oreName.ToLower())
                        return ore.RelativeDensity;
                }
            return RelativeDensity.Zero;
        }
    }
}
