using Foundation.Extensions;
using ProspectorInfo;
using ProspectorInfo.Models;
using ProspectorInfo.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Storage
{
    public class ProspectingDataStorage
    {
        public string ChannelName { get; }
        public ICoreAPI Api { get; }
        private IServerNetworkChannel ServerChannel;
        private IClientNetworkChannel ClientChannel;
        private readonly ModConfig Config;
        
        public object Lock = new object(); 
        public IDictionary<ChunkCoordinate, ProspectInfo> Data = new Dictionary<ChunkCoordinate, ProspectInfo>();

        public IEnumerable<KeyValuePair<string, string>> FoundOres { get { return AllOres.Where((pair) => FoundOreNames.Contains(pair.Value)); } }
        private readonly Dictionary<string, string> AllOres = new OreNames();
        private readonly HashSet<string> FoundOreNames = new HashSet<string>();

        private bool HasChangedSinceLastSave = false;

        public event System.Action<ICollection<ProspectInfo>> OnChanged;


        public ProspectingDataStorage(string name, ICoreAPI api, ModConfig config)
        {
            ChannelName = name;
            Api = api;
            Config = config;
        }

        #region Server
        public virtual void StartServerSide(ICoreServerAPI api)
        {
            api.Event.SaveGameLoaded += () => OnSaveGameLoading(api);
            api.Event.GameWorldSave += () => OnSaveGameSaving(api);
            api.Event.PlayerJoin += player =>
            {
                lock (Lock)
                {
                    // Send full data to new players
                    SendDataTo(player, Data.Values.ToList());
                }
            };
            // Save data periodically.
            api.World.RegisterGameTickListener((_) => OnSaveGameSaving(api),
                    (int)TimeSpan.FromMinutes(Config.SaveIntervalMinutes).TotalMilliseconds);

            ServerChannel = api.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<List<ProspectInfo>>();
        }
        protected virtual void OnSaveGameSaving(ICoreServerAPI api)
        {
            lock (Lock)
            {
                if (HasChangedSinceLastSave)
                {
                    api.SaveDataFile(ProspectorInfoModSystem.DATAFILE, new StoredData(1, Data.Values.ToList()));
                    HasChangedSinceLastSave = false;
                }
            }
        }

        protected virtual void OnSaveGameLoading(ICoreServerAPI api)
        {
            StoredData loaded = api.LoadOrCreateDataFile<StoredData>(ProspectorInfoModSystem.DATAFILE);
            Data = loaded.ProspectInfos.ToDictionary(item => item.Chunk, item => item);
        }

        public virtual void DataProspected(ProspectInfo newData)
        {
            var list = new List<ProspectInfo> { newData };
            lock (Lock)
            {
                UpdateData(list);
            }
            SendDataToClients(list);
        }

        protected virtual void SendDataToClients(List<ProspectInfo> msg)
        {
            if (Api is ICoreServerAPI)
            {
                ServerChannel?.BroadcastPacket(msg);
            }
        }
        protected virtual void SendDataTo(IServerPlayer player, List<ProspectInfo> msg)
        {
            if (Api is ICoreServerAPI)
            {
                ServerChannel?.SendPacket(msg, player);
            }
        }
        #endregion

        #region Client

        public virtual void StartClientSide(ICoreClientAPI api)
        {
            ClientChannel = api.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<List<ProspectInfo>>()
                .SetMessageHandler<List<ProspectInfo>>(OnClientDataReceived);
        }

        protected virtual void OnClientDataReceived(List<ProspectInfo> msg)
        {
            lock (Lock)
            {
                UpdateData(msg);
                OnChanged?.Invoke(msg);
            }
        }

        private void UpdateData(List<ProspectInfo> msg)
        {
            foreach (ProspectInfo info in msg)
            {
                Data[info.Chunk] = info;
                foreach (OreOccurence ore in info.Values)
                {
                    FoundOreNames.Add(ore.Name);
                }
            }
            HasChangedSinceLastSave = true;
        }
        #endregion
    }
}
