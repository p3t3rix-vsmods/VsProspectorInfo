using Foundation.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ProspectTogether.Shared
{
    public abstract class CommonStorage<C, A>
        where C : CommonConfig
        where A : ICoreAPI
    {
        protected string ChannelName { get; } = "ProspectTogether";
        protected string FileName { get; }
        protected A Api { get; }

        protected C Config;

        private long? SaveTickListenerId = null;
        protected bool HasChangedSinceLastSave = false;

        public object Lock = new object();
        public IDictionary<ChunkCoordinate, ProspectInfo> Data = new Dictionary<ChunkCoordinate, ProspectInfo>();

        public CommonStorage(A api, C config, string fileName)
        {
            Api = api;
            Config = config;
            FileName = fileName;
        }

        public void ConfigureSaveListener()
        {
            if (SaveTickListenerId != null)
            {
                Api.World.UnregisterGameTickListener((long)SaveTickListenerId);
            }
            // Save data periodically.
            SaveTickListenerId = Api.World.RegisterGameTickListener((_) => SaveProspectingDataFile(),
                    (int)TimeSpan.FromMinutes(Config.SaveIntervalMinutes).TotalMilliseconds);
        }

        protected virtual void SaveProspectingDataFile()
        {
            lock (Lock)
            {
                if (HasChangedSinceLastSave)
                {
                    Api.SaveDataFile(FileName, new StoredData(Data.Values.ToList()));
                    HasChangedSinceLastSave = false;
                }
            }
        }

        protected virtual void LoadProspectingDataFile()
        {
            lock (Lock)
            {
                StoredData loaded = Api.LoadOrCreateDataFile<StoredData>(FileName);
                Data = loaded.ProspectInfos.ToDictionary(item => item.Chunk, item => item);
                HasChangedSinceLastSave = false;
            }
        }
    }
}
