using ProspectTogether.Shared;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace ProspectTogether.Server
{
    public class ServerStorage : CommonStorage<ServerModConfig, ICoreServerAPI>
    {

        private IServerNetworkChannel ServerChannel;

        public ServerStorage(ICoreServerAPI api, ServerModConfig config, string fileName) : base(api, config, fileName)
        {
        }

        public virtual void StartServerSide()
        {
            Api.Event.SaveGameLoaded += LoadProspectingDataFile;
            Api.Event.GameWorldSave += SaveProspectingDataFile;
            Api.Event.PlayerJoin += player =>
            {
                lock (Lock)
                {
                    // Send full data to new players
                    ServerChannel?.SendPacket(new ProspectingPacket(Data.Values.ToList(), false), player);
                }
            };
            ConfigureSaveListener();

            ServerChannel = Api.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<ProspectingPacket>()
                .SetMessageHandler<ProspectingPacket>(UserSharedProspectingData);
        }

        public virtual void UserProspected(ProspectInfo newData, IServerPlayer byPlayer)
        {
            var packet = new ProspectingPacket(new List<ProspectInfo> { newData }, true);
            ServerChannel.SendPacket(packet, byPlayer);
        }

        public virtual void UserSharedProspectingData(IServerPlayer fromPlayer, ProspectingPacket packet)
        {
            lock (Lock)
            {
                foreach (ProspectInfo info in packet.Data)
                {
                    Data[info.Chunk] = info;
                }
                HasChangedSinceLastSave = true;
            }
            ServerChannel.BroadcastPacket(packet, fromPlayer);
        }
    }
}
