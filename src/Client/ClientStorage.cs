using ProspectTogether.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

namespace ProspectTogether.Client
{
    public class ClientStorage : CommonStorage<ClientModConfig, ICoreClientAPI>
    {
        private IClientNetworkChannel ClientChannel;

        public IEnumerable<KeyValuePair<string, string>> FoundOres { get { return AllOres.Where((pair) => FoundOreNames.Contains(pair.Value)); } }
        private readonly Dictionary<string, string> AllOres = new OreNames();
        private readonly HashSet<string> FoundOreNames = new HashSet<string>();

        public event Action<ICollection<ProspectInfo>> OnChanged;

        public ClientStorage(ICoreClientAPI api, ClientModConfig config, string fileName) : base(api, config, fileName)
        {
        }

        public virtual void StartClientSide()
        {
            LoadProspectingDataFile();
            Api.Event.LeaveWorld += SaveProspectingDataFile;
            ClientChannel = Api.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<ProspectingPacket>()
                .SetMessageHandler<ProspectingPacket>(OnClientDataReceived);
            ConfigureSaveListener();
        }

        protected virtual void OnClientDataReceived(ProspectingPacket packet)
        {
            lock (Lock)
            {
                foreach (ProspectInfo info in packet.Data)
                {
                    Data[info.Chunk] = info;
                    foreach (OreOccurence ore in info.Values)
                    {
                        FoundOreNames.Add(ore.Name);
                    }
                }
                HasChangedSinceLastSave = true;
                OnChanged?.Invoke(packet.Data);
            }
            if (packet.OriginatesFromProPick && Config.AutoShare)
            {
                // It's our prospecting data and we want to share it.
                ClientChannel.SendPacket(new ProspectingPacket(packet.Data, false));
            }
        }

        public void SendAll()
        {
            lock (Lock)
            {
                ClientChannel.SendPacket(new ProspectingPacket(Data.Values.ToList(), false));
            }
        }
    }
}
