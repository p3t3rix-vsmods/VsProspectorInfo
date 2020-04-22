using ProspectorInfo.Map;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ProspectorInfo
{
    public class ProspectorInfoModSystem : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

        private const string MapLayerName = "prospectorInfo";

        public override void StartClientSide(ICoreClientAPI api)
        {
            var mapManager = api.ModLoader.GetModSystem<WorldMapManager>();
            mapManager.RegisterMapLayer<ProspectorOverlayLayer>(MapLayerName);
        }
    }
}