using Foundation.Extensions;
using Foundation.ModConfig;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using ProspectorInfo.Map;
using Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ProspectorInfo
{
    public class ProspectorInfoModSystem : ModSystem
    {
        private const string Name = "prospectorInfo";
        public const string DATAFILE = "vsprospectorinfo.data.json";
        public ModConfig Config;
        public ProspectingDataStorage Storage;

        public override void StartClientSide(ICoreClientAPI api)
        {
            Storage = new ProspectingDataStorage(Name, api, Config);
            Storage.StartClientSide(api);

            var mapManager = api.ModLoader.GetModSystem<WorldMapManager>();
            mapManager.RegisterMapLayer<ProspectorOverlayLayer>(Name);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Foundation.Extensions.ApiExtensions.MigrateOldDataIfExists(Path.Combine(GamePaths.DataPath, "ModData", api.World.Seed.ToString(),
                "PospectorInfo.prospectorMessages.json"), DATAFILE, api);

            MigrationSerializationFormatToV1(DATAFILE, api);

            Storage = new ProspectingDataStorage(Name, api, Config);
            Storage.StartServerSide(api);
        }

        private void MigrationSerializationFormatToV1(string filename, ICoreServerAPI api)
        {
            // Try to migrate old stored data
            var dataPath = Path.Combine(GamePaths.DataPath, "ModData", api.GetWorldId(), filename);
            if (!File.Exists(dataPath))
            {
                return;
            }
            try
            {
                var content = File.ReadAllText(dataPath);

                var result = JToken.Parse(content);
                if (!(result is JArray))
                {
                    return;
                }
                JArray rootArray = result as JArray;

                // Remove entries that could not be parsed in the past.
                List<JObject> toDelete = new List<JObject>();
                foreach (JObject item in rootArray)
                {
                    if (item["Values"].Type == JTokenType.Null)
                    {
                        toDelete.Add(item);
                    }
                }
                foreach (JObject item in toDelete)
                {
                    rootArray.Remove(item);
                }

                // Remove old values and group X and Z into chunk.
                foreach (JObject entry in rootArray)
                {
                    JObject chunk = new JObject
                {
                    { "X", entry["X"] },
                    { "Z", entry["Z"] }
                };
                    entry.Add("Chunk", chunk);
                    entry.Remove("Message");
                    entry.Remove("X");
                    entry.Remove("Z");
                }

                // Add version header
                JObject newRoot = new JObject();
                newRoot["Version"] = new JValue(1);
                newRoot["ProspectInfos"] = rootArray;

                File.WriteAllText(dataPath, newRoot.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception e) {
                api.World.Logger.Error($"Failed to migrate prospecting data file at '{dataPath}', with an error of '{e}'! Either delete that file or check what is causing the problem.");
                throw e;
            }
        }

        public override void Start(ICoreAPI api)
        {
            Config = api.LoadOrCreateConfig<ModConfig>(this);
            var prospectorInfoPatches = new Harmony("vsprospectorinfo.patches");
            prospectorInfoPatches.PatchAll(Assembly.GetExecutingAssembly());
        }

    }
}