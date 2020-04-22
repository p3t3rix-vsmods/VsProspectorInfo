using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ProspectorInfo.Map
{
    internal class ProspectorOverlayMapComponent : MapComponent
    {
        private readonly int _chunkX;
        private readonly int _chunkZ;
        private readonly string _message;
        private readonly int _chunksize;

        public ProspectorOverlayMapComponent(ICoreClientAPI clientApi, int chunkX, int chunkZ, string message) : base(clientApi)
        {
            _chunkX = chunkX;
            _chunkZ = chunkZ;
            _message = message;
            _chunksize = clientApi.World.BlockAccessor.ChunkSize;
        }

        public override void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            var worldPos = new Vec3d();
            float mouseX = (float) (args.X - mapElem.Bounds.renderX);
            float mouseY = (float) (args.Y - mapElem.Bounds.renderY);

            mapElem.TranslateViewPosToWorldPos(new Vec2f(mouseX, mouseY),  ref worldPos );

            var chunkX = (int) (worldPos.X / _chunksize);
            var chunkZ = (int) (worldPos.Z / _chunksize);
            if (chunkX == _chunkX && chunkZ == _chunkZ)
            {
                hoverText.AppendLine($"\n{_message}");
            }
        }
    }
}