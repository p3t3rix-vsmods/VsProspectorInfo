using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Foundation.Extensions;
using HarmonyLib;
using ProspectorInfo.Models;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ProspectorInfo.Map
{
    internal class ProspectorOverlayLayer : MapLayer
    {
        // They have to be static so the PrintProbeResultsPatch postfix can access them
        static private ICoreClientAPI _clientApi;
        static private ProspectorMessages _prospectInfos;
        static private readonly List<MapComponent> _components = new List<MapComponent>();
        static private ModConfig _config;
        static private readonly LoadedTexture[] _colorTextures = new LoadedTexture[8];
        static private GuiDialog _settingsDialog;

        private const string Filename = ProspectorInfoModSystem.DATAFILE;
        private readonly int _chunksize;
        static private IWorldMapManager _worldMapManager;
        private bool _temporaryRenderOverride = false;

        public override string Title => "ProspectorOverlay";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        public ProspectorOverlayLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            _worldMapManager = mapSink;
            _chunksize = api.World.BlockAccessor.ChunkSize;
            _prospectInfos = api.LoadOrCreateDataFile<ProspectorMessages>(Filename);

            var modSystem = this.api.ModLoader.GetModSystem<ProspectorInfoModSystem>();
            _config = modSystem.Config;

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
                _clientApi.Event.PlayerLeave += (p) =>
                {
                    if (p == _clientApi?.World.Player)
                    {
                        var invMan = p?.InventoryManager?.GetHotbarInventory();
                        invMan.SlotModified -= Event_SlotModified;
                    }
                };

                _clientApi.RegisterCommand("pi", "ProspectorInfo main command. Allows you to toggle the visibility of the chunk texture overlay.", "", OnPiCommand);
                for (int i = 0; i < _colorTextures.Length; i++)
                {
                    _colorTextures[i]?.Dispose();
                    _colorTextures[i] = GenerateOverlayTexture((RelativeDensity)i);
                }

                _settingsDialog = new GuiProspectorInfoSetting(_clientApi, _config, RebuildMap);
            }
        }


        #region Commands/Events


        private void OnPiCommand(int groupId, CmdArgs args)
        {
            switch (args.PopWord("showoverlay"))
            {
                case "showoverlay":
                    var toggleValue = args.PopBool();
                    if (toggleValue.HasValue)
                        _config.RenderTexturesOnMap = toggleValue.Value;
                    else
                        _config.RenderTexturesOnMap = !_config.RenderTexturesOnMap;

                    _config.Save(api);
                    break;
                case "showgui":
                    var guiToggleValue = args.PopBool();
                    if (guiToggleValue.HasValue)
                        _config.ShowGui = guiToggleValue.Value;
                    else
                        _config.ShowGui = !_config.ShowGui;

                    _config.Save(api);
                    if (_worldMapManager.IsOpened)
                        _settingsDialog.TryOpen();
                    break;
                case "setcolor":
                    try
                    {
                        var newColor = TrySetRGBAValues(args, _config.TextureColor);
                        _config.TextureColor = newColor;
                        _config.Save(api);

                        RebuildMap(true);
                    }
                    catch (FormatException e)
                    {
                        _clientApi.ShowChatMessage(e.Message);
                    }
                    catch (ArgumentException e)
                    {
                        _clientApi.ShowChatMessage(e.Message);
                    }
                    break;
                case "setlowheatcolor":
                    try
                    {
                        var newColor = TrySetRGBAValues(args, _config.BorderColor);
                        _config.LowHeatColor = newColor;
                        _config.Save(api);

                        RebuildMap(true);
                    }
                    catch (FormatException e)
                    {
                        _clientApi.ShowChatMessage(e.Message);
                    }
                    catch (ArgumentException e)
                    {
                        _clientApi.ShowChatMessage(e.Message);
                    }
                    break;
                case "sethighheatcolor":
                    try
                    {
                        var newColor = TrySetRGBAValues(args, _config.BorderColor);
                        _config.HighHeatColor = newColor;
                        _config.Save(api);

                        RebuildMap(true);
                    }
                    catch (FormatException e)
                    {
                        _clientApi.ShowChatMessage(e.Message);
                    }
                    catch (ArgumentException e)
                    {
                        _clientApi.ShowChatMessage(e.Message);
                    }
                    break;
                case "setbordercolor":
                    try
                    {
                        var newColor = TrySetRGBAValues(args, _config.BorderColor);
                        _config.BorderColor = newColor;
                        _config.Save(api);

                        RebuildMap(true);
                    }
                    catch (FormatException e)
                    {
                        _clientApi.ShowChatMessage(e.Message);
                    }
                    catch (ArgumentException e)
                    {
                        _clientApi.ShowChatMessage(e.Message);
                    }
                    break;
                case "setborderthickness":
                    var newThickness = args.PopInt(2).Value;
                    _config.BorderThickness = newThickness;
                    _config.Save(api);

                    RebuildMap(true);
                    break;
                case "toggleborder":
                    var toggleBorder = args.PopBool() ?? !_config.RenderBorder;
                    _config.RenderBorder = toggleBorder;
                    _config.Save(api);

                    RebuildMap(true);
                    break;
                case "mode":
                    var newMode = args.PopInt(2).Value;
                    _config.MapMode = newMode;
                    _config.Save(api);

                    RebuildMap(true);
                    break;
                case "heatmapore":
                    var oreName = args.PopAll();
                    if (oreName.Trim() == "" || oreName.Trim() == "null")
                        oreName = null;
                    _config.HeatMapOre = oreName;
                    _config.Save(api);

                    RebuildMap(true);
                    break;
                case "help":
                    switch (args.PopWord(""))
                    {
                        case "showoverlay":
                            _clientApi.ShowChatMessage(".pi showoverlay [bool] - Shows or hides the overlay. No argument toggles instead.");
                            break;
                        case "showgui":
                            _clientApi.ShowChatMessage(".pi showgui [bool] - Shows or hides the gui whenever the map is open. No argument toggles instead.");
                            break;
                        case "setcolor":
                            _clientApi.ShowChatMessage(".pi setcolor [0-255] [0-255] [0-255] [0-255] - Sets the color of the overlay tiles.");
                            _clientApi.ShowChatMessage("Command version of config \"TextureColor\". Default config: 7 52 91 50");
                            break;
                        case "setlowheatcolor":
                            _clientApi.ShowChatMessage(".pi setlowheatcolor [0-255] [0-255] [0-255] [0-255] - Sets the low heat RGBA color of the overlay tiles for the heatmap.");
                            _clientApi.ShowChatMessage("Gets blended with \"sethighheatcolor\" based on the relative density of a ore");
                            _clientApi.ShowChatMessage("Command version of config \"setlowheatcolor\". Default config: 85 85 181 128");
                            break;
                        case "sethighheatcolor":
                            _clientApi.ShowChatMessage(".pi sethighheatcolor [0-255] [0-255] [0-255] [0-255] - Sets the high heat RGBA color of the overlay tiles for the heatmap.");
                            _clientApi.ShowChatMessage("Gets blended with \"setlowheatcolor\" based on the relative density of a ore");
                            _clientApi.ShowChatMessage("Command version of config \"sethighheatcolor\". Default config: 168 34 36 128");
                            break;
                        case "setbordercolor":
                            _clientApi.ShowChatMessage(".pi setbordercolor [0-255] [0-255] [0-255] [0-255] - Sets the color of the tile outlines.");
                            _clientApi.ShowChatMessage("Command version of config \"BorderColor\". Default config: 0 0 0 200");
                            break;
                        case "setborderthickness":
                            _clientApi.ShowChatMessage(".pi setborderthickness [int] - Sets the tile outline's thickness.");
                            _clientApi.ShowChatMessage("Command version of config \"BorderThickness\". Default config: 1");
                            break;
                        case "toggleborder":
                            _clientApi.ShowChatMessage(".pi toggleborder [true,false] - Shows or hides the tile border.");
                            _clientApi.ShowChatMessage("Command version of config \"RenderBorder\". Default config: true");
                            break;
                        case "mode":
                            _clientApi.ShowChatMessage(".pi mode [0-1] - Sets the map mode");
                            _clientApi.ShowChatMessage("Supported modes: 0 (Default) and 1 (Heatmap)");
                            break;
                        case "heatmapore":
                            _clientApi.ShowChatMessage(".pi heatmapore [oreName] - Changes the heatmap mode to display a specific ore");
                            _clientApi.ShowChatMessage("No argument resets the heatmap back to all ores. Can only handle the english name of an ore without spaces.");
                            _clientApi.ShowChatMessage("E.g. cassiterite, bituminouscoal, nativecopper");
                            break;
                        default:
                            _clientApi.ShowChatMessage(".pi - Defaults to \"showoverlay\" without arguments.");
                            _clientApi.ShowChatMessage(".pi showoverlay [bool] - Shows or hides the overlay. No argument toggles instead.");
                            _clientApi.ShowChatMessage(".pi showgui [bool] - Shows or hides the gui whenever the map is open. No argument toggles instead.");
                            _clientApi.ShowChatMessage(".pi setcolor [0-255] [0-255] [0-255] [0-255] - Sets the RGBA color of the overlay tiles.");
                            _clientApi.ShowChatMessage(".pi setlowheatcolor [0-255] [0-255] [0-255] [0-255] - Sets the low heat RGBA color of the overlay tiles for the heatmap.");
                            _clientApi.ShowChatMessage(".pi sethighheatcolor [0-255] [0-255] [0-255] [0-255] - Sets the high heat RGBA color of the overlay tiles for the heatmap.");
                            _clientApi.ShowChatMessage(".pi setbordercolor [0-255] [0-255] [0-255] [0-255] - Sets the RGBA color of the tile outlines.");
                            _clientApi.ShowChatMessage(".pi setborderthickness [int] - Sets the tile outline's thickness.");
                            _clientApi.ShowChatMessage(".pi toggleborder [true,false] - Shows or hides the tile border.");
                            _clientApi.ShowChatMessage(".pi mode [0-1] - Sets the map mode");
                            _clientApi.ShowChatMessage(".pi heatmapore [oreName] - Changes the heatmap mode to display a specific ore");
                            _clientApi.ShowChatMessage(".pi help [command] - Shows command's help. Defaults to listing .pi subcommands.");
                            break;
                    }
                    break;
                default:
                    _clientApi.ShowChatMessage("Unknown subcommand! Try \"help\".");
                    break;
            }
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

        #region SetColorCommand
        private ColorWithAlpha TrySetRGBAValues(CmdArgs args, ColorWithAlpha color)
        {
            var numberOfArgs = args.Length;
            if (numberOfArgs == 4)
                SetColorAndAlpha(args, color);
            else if (numberOfArgs == 3)
                SetColor(args, color);
            else if (numberOfArgs == 1)
                SetAlpha(args, color, 0);
            else
                throw new FormatException($"Number of arguments must be 4, 3, or 1. You provided {numberOfArgs}!");

            return color;
        }

        private void SetColorAndAlpha(CmdArgs args, ColorWithAlpha color)
        {
            SetColor(args, color);
            SetAlpha(args, color, 3);
        }

        private void SetColor(CmdArgs args, ColorWithAlpha color)
        {
            color.Red = TryGetArgumentValue(args, 0);
            color.Green = TryGetArgumentValue(args, 1);
            color.Blue = TryGetArgumentValue(args, 2);
        }

        private void SetAlpha(CmdArgs args, ColorWithAlpha color, int position)
        {
            color.Alpha = TryGetArgumentValue(args, position);
        }

        private int TryGetArgumentValue(CmdArgs args, int position)
        {
            var arg = args.PopInt() ?? throw new FormatException($"{GetArgumentPositionAsString(position)} argument must be an integer!");
            if (arg > 255)
                throw new ArgumentException($"{GetArgumentPositionAsString(position)} argument must be 255 or less!");
            if (arg < 0)
                throw new ArgumentException($"{GetArgumentPositionAsString(position)} argument must be 0 or greater!");
            return arg;
        }

        private string GetArgumentPositionAsString(int position)
        {
            switch (position)
            {
                case 0:
                    return "First";
                case 1:
                    return "Second";
                case 2:
                    return "Third";
                case 3:
                    return "Fourth";
                default:
                    return "";
            }
        }
        #endregion

        #region Texture
        private LoadedTexture GenerateOverlayTexture(RelativeDensity? relativeDensity)
        {
            var colorTexture = new LoadedTexture(_clientApi, 0, _chunksize, _chunksize);
            int[] colorArray;
            if (_config.MapMode == 1)
            {
                colorArray = Enumerable.Repeat(ColorUtil.ColorOverlay(_config.LowHeatColor.RGBA, _config.HighHeatColor.RGBA, 1 * (int)relativeDensity / 8.0f), _chunksize * _chunksize).ToArray();
            }
            else
            {
                colorArray = Enumerable.Repeat(_config.TextureColor.RGBA, _chunksize * _chunksize).ToArray();
            }

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
            foreach (var component in _components)
            {
                component.Dispose();
            }

            _components.Clear();

            if (rebuildTexture)
            {
                for (int i = 0; i < _colorTextures.Length; i++)
                {
                    _colorTextures[i]?.Dispose();
                    _colorTextures[i] = GenerateOverlayTexture((RelativeDensity)i);
                }
            }

            foreach (var message in _prospectInfos)
            {
                RelativeDensity densityValue;
                if (_config.HeatMapOre == null)
                    if (message.Values != null && message.Values.Count > 0)
                        densityValue = message.Values.First().relativeDensity;
                    else
                        densityValue = RelativeDensity.Zero;
                else
                    densityValue = message.GetValueOfOre(_config.HeatMapOre);
                var component = new ProspectorOverlayMapComponent(_clientApi, message.X, message.Z, message.GetMessage(), _colorTextures[(int)densityValue]);
                _components.Add(component);
            }
        }

        public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            foreach (var component in _components)
            {
                component.OnMouseMove(args, mapElem, hoverText);
            }
        }

        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (!_temporaryRenderOverride && UserDisabledMapTextures())
                return;

            foreach (var component in _components)
            {
                component.Render(mapElem, dt);
            }
        }

        public override void Dispose()
        {
            _components.ForEach(c => c.Dispose());
            base.Dispose();
        }

        [HarmonyPatch(typeof(WorldMapManager), "ToggleMap")]
        class WorldMapManagerPatch
        {
            static void Postfix(EnumDialogType asType)
            {
                if (_settingsDialog.IsOpened()) _settingsDialog.TryClose();
                else if (_config.ShowGui) _settingsDialog.TryOpen();
            }
        }

        [HarmonyPatch(typeof(ItemProspectingPick), "ProbeBlockDensityMode")]
        class PrintProbeResultsPatch
        {
            static void Postfix(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel)
            {
                // Only needed to be compatible with https://github.com/Spoonail-Iroiro/VSOneshotPropickMod Version 1.0.0
                // TODO Should be removed once VSOneshotPropickMod gets updated and the "compatibility code" gets removed
                // Dont forget to rename RealPrintProbeResultsPatch back to PrintProbeResultsPatch
            }
        }

        [HarmonyPatch(typeof(ItemProspectingPick), "PrintProbeResults")]
        class RealPrintProbeResultsPatch
        {
            static void Postfix(IWorldAccessor world, IServerPlayer byPlayer, ItemSlot itemslot, BlockPos pos)
            {
                if (world.Side == EnumAppSide.Client || _clientApi?.World?.Player == null)
                    return;

                ProPickWorkSpace _proPickWorkSpace = ObjectCacheUtil.GetOrCreate(world.Api, "propickworkspace", () =>
                {
                    ProPickWorkSpace ppws = new ProPickWorkSpace();
                    ppws.OnLoaded(world.Api);
                    return ppws;
                });

                int _chunkSize = world.BlockAccessor.ChunkSize;
                int posX = pos.X / _chunkSize;
                int posZ = pos.Z / _chunkSize;

                _prospectInfos.RemoveAll(m => m.X == posX && m.Z == posZ);
                _prospectInfos.Add(new ProspectInfo(pos, _chunkSize, world.Api as ICoreServerAPI, _proPickWorkSpace));
                _clientApi.SaveDataFile(Filename, _prospectInfos);

                _components.RemoveAll(component =>
                {
                    var castComponent = component as ProspectorOverlayMapComponent;
                    return castComponent?.ChunkX == posX && castComponent.ChunkZ == posZ;
                });

                RelativeDensity densityValue;
                if (_config.HeatMapOre == null)
                    if (_prospectInfos.Last().Values != null && _prospectInfos.Last().Values.Count > 0)
                        densityValue = _prospectInfos.Last().Values.First().relativeDensity;
                    else
                        densityValue = RelativeDensity.Zero;
                else
                    densityValue = _prospectInfos.Last().GetValueOfOre(_config.HeatMapOre);

                var newComponent = new ProspectorOverlayMapComponent(_clientApi, posX, posZ, _prospectInfos.Last().GetMessage(), _colorTextures[(int)densityValue]);
                _components.Add(newComponent);
            }
        }
    }
}
