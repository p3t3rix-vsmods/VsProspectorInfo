using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using ProspectorInfo.Models;
using ProspectorInfo.Utils;
using Storage;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ProspectorInfo.Map
{
    internal class ProspectorOverlayLayer : MapLayer
    {
        private readonly int _chunksize;
        private readonly ProspectingDataStorage _storage;
        private readonly ICoreClientAPI _clientApi;
        private readonly Dictionary<ChunkCoordinate, ProspectorOverlayMapComponent> _components = new Dictionary<ChunkCoordinate, ProspectorOverlayMapComponent>();
        private readonly IWorldMapManager _worldMapManager;
        private readonly LoadedTexture[] _colorTextures = new LoadedTexture[8];
        private bool _temporaryRenderOverride = false;
        private static ModConfig _config;
        private static GuiDialog _settingsDialog;

        public override string Title => "ProspectorOverlay";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        public ProspectorOverlayLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            _worldMapManager = mapSink;
            _chunksize = api.World.BlockAccessor.ChunkSize;

            var modSystem = api.ModLoader.GetModSystem<ProspectorInfoModSystem>();
            _config = modSystem.Config;
            _storage = modSystem.Storage;
            _storage.OnChanged += UpdateMapComponents;

            if (api.Side == EnumAppSide.Client)
            {
                _clientApi = (ICoreClientAPI)api;
                _clientApi.Event.AfterActiveSlotChanged += Event_AfterActiveSlotChanged;
                _clientApi.Event.PlayerJoin += (p) =>
                {
                    if (p == _clientApi?.World.Player)
                    {
                        var invMan = p?.InventoryManager?.GetHotbarInventory();
                        invMan.SlotModified += Event_SlotModified;
                    }
                };

                _clientApi.ChatCommands.Create("pi")
                    .WithDescription("ProspectorInfo main command. Defaults to toggling the map overlay.")
                    .HandleWith(OnShowOverlayCommand)
                    .BeginSubCommand("showoverlay")
                        .WithDescription(".pi showoverlay [bool] - Shows or hides the overlay. No argument toggles instead.")
                        .WithArgs(api.ChatCommands.Parsers.OptionalBool("show"))
                        .HandleWith(OnShowOverlayCommand)
                    .EndSubCommand()
                    .BeginSubCommand("showgui")
                        .WithDescription(".pi showgui [bool] - Shows or hides the gui whenever the map is open. No argument toggles instead.")
                        .WithArgs(api.ChatCommands.Parsers.OptionalBool("show"))
                        .HandleWith(OnShowGuiCommand)
                    .EndSubCommand()
                    .BeginSubCommand("showborder")
                        .WithDescription(".pi showborder [bool] - Shows or hides the tile border. No argument toggles instead.<br/>" +
                                         "Sets the \"RenderBorder\" config option (default = true)")
                        .WithArgs(api.ChatCommands.Parsers.OptionalBool("show"))
                        .HandleWith(OnShowBorderCommand)
                    .EndSubCommand()
                    .BeginSubCommand("setcolor")
                        .WithDescription(".pi setcolor (overlay|border|lowheat|highheat) [0-255] [0-255] [0-255] [0-255]<br/>" +
                                         "Sets the given color for the specified element.<br/>" +
                                         "You can specify a color either as RGBA, RGB or only A.<br/>" +
                                         "The lowheat and highheat colors will be blended on the heatmap based on relative density.<br/>" +
                                         "Available elements and corresponding config option:<br/>" +
                                         "overlay: TextureColor (default = 150 125 150 128)<br/>" +
                                         "border: BorderColor (default = 0 0 0 200)<br/>" +
                                         "lowheat: LowHeatColor (default = 85 85 181 128)<br/>" +
                                         "highheat: HighHeatColor (default = 168 34 36 128)")
                        .WithArgs(api.ChatCommands.Parsers.WordRange("element", "overlay", "border", "lowheat", "highheat"),
                                  new ColorWithAlphaArgParser("color", true))
                        .HandleWith(OnSetColorCommand)
                    .EndSubCommand()
                    .BeginSubCommand("setborderthickness")
                        .WithDescription(".pi setborderthickness [1-5] - Sets the tile outline's thickness.<br/>" +
                                         "Sets the \"BorderThickness\" config option (default = 1)")
                        .WithArgs(api.ChatCommands.Parsers.IntRange("thickness", 1, 5))
                        .HandleWith(OnSetBorderThicknessCommand)
                    .EndSubCommand()
                    .BeginSubCommand("mode")
                        .WithDescription(".pi mode [0-1] - Sets the map mode<br/>" +
                                         "Supported modes: 0 (Default) and 1 (Heatmap)")
                        .WithArgs(api.ChatCommands.Parsers.IntRange("mode", 0, 1))
                        .HandleWith(OnSetModeCommand)
                    .EndSubCommand()
                    .BeginSubCommand("heatmapore")
                        .WithDescription(".pi heatmapore [oreName] - Changes the heatmap mode to display a specific ore<br/>" +
                                         "No argument resets the heatmap back to all ores. Can only handle the ore name in your selected language or the ore tag.<br/>" +
                                         "E.g. game:ore-emerald, game:ore-bituminouscoal, Cassiterite")
                        .WithArgs(api.ChatCommands.Parsers.OptionalWord("oreName"))
                        .HandleWith(OnHeatmapOreCommand)
                    .EndSubCommand()
                    .BeginSubCommand("setsaveintervalminutes")
                        .WithDescription(".pi setsaveintervalminutes [int] - How often should the prospecting data be saved to disk.<br/>" +
                                         "Requires leaving/reentering the world. The data is also saved when leaving a world.<br/>" +
                                         "Sets the \"SaveIntervalMinutes\" config option (default = 1)")
                        .WithArgs(api.ChatCommands.Parsers.IntRange("interval", 1, 60))
                        .HandleWith(OnSetSaveIntervalMinutes)
                    .EndSubCommand();

                for (int i = 0; i < _colorTextures.Length; i++)
                {
                    _colorTextures[i]?.Dispose();
                    _colorTextures[i] = GenerateOverlayTexture((RelativeDensity)i);
                }

                _settingsDialog = new GuiProspectorInfoSetting(_clientApi, _config, RebuildMap, _storage);
            }
        }


        #region Handling Prospecting Data

        public void UpdateMapComponents(ICollection<ProspectInfo> information)
        {
            foreach (ProspectInfo info in information)
            {
                var newComponent = new ProspectorOverlayMapComponent(_clientApi, info.Chunk, info.GetMessage(), _colorTextures[(int)GetRelativeDensity(info)]);
                _components[info.Chunk] = newComponent;
            }
        }
        #endregion

        #region Commands/Events

        private TextCommandResult OnShowOverlayCommand(TextCommandCallingArgs args)
        {
            // Parser count is 0 when only calling .pi command.
            if (args.Parsers.Count == 0 || args.Parsers[0].IsMissing)
                _config.RenderTexturesOnMap = !_config.RenderTexturesOnMap;
            else
                _config.RenderTexturesOnMap = (bool)args.Parsers[0].GetValue();
            _config.Save(api);
            RebuildMap();
            return TextCommandResult.Success($"Set RenderTexturesOnMap to {_config.RenderTexturesOnMap}.");
        }

        private TextCommandResult OnShowGuiCommand(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
                _config.ShowGui = !_config.ShowGui;
            else
                _config.ShowGui = (bool)args.Parsers[0].GetValue();
            _config.Save(api);
            if (_worldMapManager.IsOpened)
                _settingsDialog.TryOpen();
            return TextCommandResult.Success($"Set ShowGui to {_config.ShowGui}.");
        }

        private TextCommandResult OnSetColorCommand(TextCommandCallingArgs args)
        {
            ColorWithAlphaUpdate colorUpdate = (ColorWithAlphaUpdate)args.Parsers[1].GetValue();
            string changedColorSetting;
            switch ((string)args.Parsers[0].GetValue())
            {
                case "overlay":
                    colorUpdate.ApplyUpdateTo(_config.TextureColor);
                    changedColorSetting = "TextureColor";
                    break;
                case "border":
                    colorUpdate.ApplyUpdateTo(_config.BorderColor);
                    changedColorSetting = "BorderColor";
                    break;
                case "lowheat":
                    colorUpdate.ApplyUpdateTo(_config.LowHeatColor);
                    changedColorSetting = "LowHeadColor";
                    break;
                case "highheat":
                    colorUpdate.ApplyUpdateTo(_config.HighHeatColor);
                    changedColorSetting = "HighHeatColor";
                    break;
                default:
                    return TextCommandResult.Error("Unknown element to set color for.");
            }
            _config.Save(api);
            RebuildMap(true);
            return TextCommandResult.Success($"Updated color for {changedColorSetting}.");
        }

        private TextCommandResult OnSetBorderThicknessCommand(TextCommandCallingArgs args)
        {
            var newThickness = (int)args.Parsers[0].GetValue();
            _config.BorderThickness = newThickness;
            _config.Save(api);
            RebuildMap(true);
            return TextCommandResult.Success($"Set BorderThickness to {_config.BorderThickness}.");
        }

        private TextCommandResult OnShowBorderCommand(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
                _config.RenderBorder = !_config.RenderBorder;
            else
                _config.RenderBorder = (bool)args.Parsers[0].GetValue();
            _config.Save(api);

            RebuildMap(true);
            return TextCommandResult.Success($"Set RenderBorder to {_config.RenderBorder}.");
        }

        private TextCommandResult OnSetModeCommand(TextCommandCallingArgs args)
        {
            var newMode = (int)args.Parsers[0].GetValue();
            _config.MapMode = (MapMode)newMode;
            _config.Save(api);

            RebuildMap(true);
            return TextCommandResult.Success($"Set MapMode to {_config.MapMode}.");
        }

        private TextCommandResult OnHeatmapOreCommand(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
                _config.HeatMapOre = null;
            else
                _config.HeatMapOre = (string)args.Parsers[0].GetValue();
            _config.Save(api);

            RebuildMap(true);
            return TextCommandResult.Success($"Set HeatMapOre to {_config.HeatMapOre}.");
        }

        private TextCommandResult OnSetSaveIntervalMinutes(TextCommandCallingArgs args)
        {
            _config.SaveIntervalMinutes = (int)args.Parsers[0].GetValue();
            _config.Save(api);
            return TextCommandResult.Success($"Set SaveIntervalMinutes to {_config.SaveIntervalMinutes}.");
        }

        private void Event_SlotModified(int slotId)
        {
            UpdateRenderOverride();
        }
        private void Event_AfterActiveSlotChanged(ActiveSlotChangeEventArgs t1)
        {
            UpdateRenderOverride();
        }

        private void UpdateRenderOverride()
        {
            if (!_config.AutoToggle)
                return;

            _temporaryRenderOverride = ProspectingPickInHand;
        }

        private bool ProspectingPickInHand => _clientApi?.World?.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Item?.Code?
                .FirstPathPart()?.ToLower().StartsWith("prospectingpick") ?? false;

        #endregion

        #region Texture
        private LoadedTexture GenerateOverlayTexture(RelativeDensity? relativeDensity)
        {
            var colorTexture = new LoadedTexture(_clientApi, 0, _chunksize, _chunksize);
            int[] colorArray;
            if (_config.MapMode == MapMode.Heatmap)
                colorArray = Enumerable.Repeat(ColorUtil.ColorOverlay(_config.LowHeatColor.RGBA, _config.HighHeatColor.RGBA, 1 * (int)relativeDensity / 8.0f), _chunksize * _chunksize).ToArray();
            else
                colorArray = Enumerable.Repeat(_config.TextureColor.RGBA, _chunksize * _chunksize).ToArray();

            if (_config.RenderBorder)
            {
                for (int x = 0; x < _chunksize; x++)
                {
                    for (int y = 0; y < _chunksize; y++)
                    {
                        if (x < _config.BorderThickness || x > _chunksize - 1 - _config.BorderThickness)
                            colorArray[y * _chunksize + x] = ColorUtil.ColorOver(colorArray[y * _chunksize + x], _config.BorderColor.RGBA);
                        else if (y < _config.BorderThickness || y > _chunksize - 1 - _config.BorderThickness)
                            colorArray[y * _chunksize + x] = ColorUtil.ColorOver(colorArray[y * _chunksize + x], _config.BorderColor.RGBA);
                    }
                }
            }

            _clientApi.Render.LoadOrUpdateTextureFromRgba(colorArray, false, 0, ref colorTexture);
            _clientApi.Render.BindTexture2d(colorTexture.TextureId);

            return colorTexture;
        }

        public bool UserDisabledMapTextures()
        {
            return !_config.RenderTexturesOnMap;
        }
        #endregion

        public override void OnMapOpenedClient()
        {
            if (!_worldMapManager.IsOpened)
                return;

            RebuildMap();
        }

        public void RebuildMap(bool rebuildTexture = false)
        {

            if (rebuildTexture)
            {
                for (int i = 0; i < _colorTextures.Length; i++)
                {
                    _colorTextures[i]?.Dispose();
                    _colorTextures[i] = GenerateOverlayTexture((RelativeDensity)i);
                }
            }

            lock (_storage.Lock)
            {
                _components.Clear();
                UpdateMapComponents(_storage.Data.Values);
            }
        }

        private RelativeDensity GetRelativeDensity(ProspectInfo prospectInfo)
        {
            if (_config.HeatMapOre == null)
                if (prospectInfo.Values != null && prospectInfo.Values.Count > 0)
                    return prospectInfo.Values.First().RelativeDensity;
                else
                    return RelativeDensity.Zero;
            else
                return prospectInfo.GetValueOfOre(_config.HeatMapOre);
        }

        public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            lock (_storage.Lock)
            {
                foreach (var component in _components.Values)
                {
                    component.OnMouseMove(args, mapElem, hoverText);
                }
            }
        }

        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (!_temporaryRenderOverride && UserDisabledMapTextures())
                return;

            lock (_storage.Lock)
            {
                foreach (var component in _components.Values)
                {
                    component.Render(mapElem, dt);
                }
            }
        }

        public override void Dispose()
        {
            foreach (var texture in _colorTextures)
            {
                texture?.Dispose();
            }
            base.Dispose();
        }

        [HarmonyPatch(typeof(GuiDialogWorldMap), "TryClose")]
        class GuiDialogWorldMapTryClosePatch
        {
            static void Postfix()
            {
                _settingsDialog.TryClose();
            }
        }

        [HarmonyPatch(typeof(GuiDialogWorldMap), "Open")]
        class GuiDialogWorldMapOpenPatch
        {
            static void Postfix(EnumDialogType type)
            {
                if (_config.ShowGui && type == EnumDialogType.Dialog)
                    _settingsDialog.TryOpen();
                else
                    _settingsDialog.TryClose();
            }
        }


    }
}
