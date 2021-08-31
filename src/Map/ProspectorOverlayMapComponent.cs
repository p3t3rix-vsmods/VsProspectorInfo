using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ProspectorInfo.Map
{
    public class ProspectorOverlayMapComponent : MapComponent
    {
        public readonly int ChunkX;
        public readonly int ChunkZ;

        private readonly string _message;
        private readonly int _chunksize;

        private LoadedTexture colorTexture;
        private Vec3d worldPos = new Vec3d();
        private Vec2f viewPos = new Vec2f();

        public ProspectorOverlayMapComponent(ICoreClientAPI clientApi, int chunkX, int chunkZ, string message, LoadedTexture colorTexture) : base(clientApi)
        {
            this.ChunkX = chunkX;
            this.ChunkZ = chunkZ;
            this._message = message;
            this._chunksize = clientApi.World.BlockAccessor.ChunkSize;
            this.worldPos = new Vec3d(chunkX * _chunksize, 0, chunkZ * _chunksize);
            this.colorTexture = colorTexture;
        }

        public override void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            var worldPos = new Vec3d();
            float mouseX = (float)(args.X - mapElem.Bounds.renderX);
            float mouseY = (float)(args.Y - mapElem.Bounds.renderY);

            mapElem.TranslateViewPosToWorldPos(new Vec2f(mouseX, mouseY), ref worldPos);

            var chunkX = (int)(worldPos.X / _chunksize);
            var chunkZ = (int)(worldPos.Z / _chunksize);
            if (chunkX == ChunkX && chunkZ == ChunkZ)
            {
                hoverText.AppendLine($"\n{_message}");
            }
        }

        public override void Render(GuiElementMap map, float dt)
        {
            map.TranslateWorldPosToViewPos(this.worldPos, ref this.viewPos);

            base.capi.Render.Render2DTexture(
                this.colorTexture.TextureId,
                (int)(map.Bounds.renderX + viewPos.X),
                (int)(map.Bounds.renderY + viewPos.Y),
                (int)(this.colorTexture.Width * map.ZoomLevel),
                (int)(this.colorTexture.Height * map.ZoomLevel),
                50);
        }
    }
}