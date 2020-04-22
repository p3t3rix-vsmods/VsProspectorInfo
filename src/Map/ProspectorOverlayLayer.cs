using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Foundation.Util.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace ProspectorInfo.Map
{
    internal class ProspectorOverlayLayer : MapLayer
    {
        private const string Filename = "PospectorInfo.prospectorMessages.json";
        private readonly string _triggerword;
        private readonly ProspectorMessages _messages;
        private readonly int _chunksize;
        private readonly ICoreClientAPI _clientApi;
        private readonly List<MapComponent> _components = new List<MapComponent>();
        private readonly IWorldMapManager _worldMapManager;
        private readonly Regex _cleanupRegex;

        public override string Title => "ProspectorOverlay";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        public ProspectorOverlayLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            _worldMapManager = mapSink;
            _chunksize = api.World.BlockAccessor.ChunkSize;
            _messages = api.LoadOrCreateDataFile<ProspectorMessages>(Filename);
            _triggerword = Lang.GetUnformatted("propick-reading-title")?.Split().FirstOrDefault();
            _cleanupRegex = new Regex("<.*?>", RegexOptions.Compiled);
            if (api.Side == EnumAppSide.Client)
            {
                _clientApi = (ICoreClientAPI)api;
                _clientApi.Event.ChatMessage += OnChatMessage;
            }
        }

        private void OnChatMessage(int groupId, string message, EnumChatType chattype, string data)
        {
            var pos = _clientApi.World.Player.CurrentBlockSelection?.Position ?? _clientApi.World.Player.Entity.Pos.AsBlockPos;
            if (pos != null && groupId == GlobalConstants.InfoLogChatGroup && message.StartsWith(_triggerword))
            {
                message = _cleanupRegex.Replace(message, string.Empty);
                var posX = pos.X / _chunksize;
                var posZ = pos.Z / _chunksize;
                _messages.RemoveAll(m => m.X == posX && m.Z == posZ);
                _messages.Add(new ProspectInfo(posX, posZ, message));
                _clientApi.SaveDataFile(Filename, _messages);
            }
        }

        public override void OnMapOpenedClient()
        {
            foreach (var component in _components)
            {
                _worldMapManager.RemoveMapData(component);
                component.Dispose();
            }

            foreach (var message in _messages)
            {
                var component = new ProspectorOverlayMapComponent(_clientApi, message.X, message.Z, message.Message);
                _components.Add(component);
                _worldMapManager.AddMapData(component);
            }
        }

        public override void Dispose()
        {
            _components.ForEach(c => c.Dispose());
            base.Dispose();
        }
    }
}