using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Foundation.Extensions;
using HarmonyLib;
using ProspectorInfo.Models;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ProspectorInfo.Map
{
    internal class ProspectorOverlayLayer : MapLayer
    {
        private const string Filename = ProspectorInfoModSystem.DATAFILE;
        private readonly Regex _headerParsingRegex;
        private readonly ProspectorMessages _prospectInfos;
        private readonly int _chunksize;
        private readonly ICoreClientAPI _clientApi;
        private readonly List<MapComponent> _components = new List<MapComponent>();
        private readonly IWorldMapManager _worldMapManager;
        private readonly LoadedTexture[] _colorTextures = new LoadedTexture[8];
        private bool _temporaryRenderOverride = false;
        private static ModConfig _config;
        private static GuiDialog _settingsDialog;

        public override string Title => "ProspectorOverlay";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        protected internal static List<BlockSelection> blocksSinceLastSuccessList = new List<BlockSelection>();

        public ProspectorOverlayLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            _worldMapManager = mapSink;
            _chunksize = api.World.BlockAccessor.ChunkSize;
            _prospectInfos = api.LoadOrCreateDataFile<ProspectorMessages>(Filename);
            _headerParsingRegex = new Regex(Lang.Get("propick-reading-title", ".*?"), RegexOptions.Compiled);

            var modSystem = this.api.ModLoader.GetModSystem<ProspectorInfoModSystem>();
            _config = modSystem.Config;

            if (api.Side == EnumAppSide.Client)
            {
                _clientApi = (ICoreClientAPI)api;
                _clientApi.Event.ChatMessage += OnChatMessage;
                _clientApi.Event.AfterActiveSlotChanged += Event_AfterActiveSlotChanged;
                _clientApi.Event.PlayerJoin += (p) =>
                {
                    if (p == _clientApi?.World.Player)
                    {
                        var invMan = p?.InventoryManager?.GetHotbarInventory();
                        invMan.SlotModified += Event_SlotModified;
                    }
                };

                // Save data when leaving and periodically.
                _clientApi.Event.LeaveWorld += () => {
                    SaveProspectingData();
                };
                _clientApi.World.RegisterGameTickListener((_) => SaveProspectingData(), 
                        (int) TimeSpan.FromMinutes(_config.SaveIntervalMinutes).TotalMilliseconds);

                _clientApi.RegisterCommand("pi", "ProspectorInfo main command. Allows you to toggle the visibility of the chunk texture overlay.", "", OnPiCommand);

                for (int i = 0; i < _colorTextures.Length; i++)
                {
                    _colorTextures[i]?.Dispose();
                    _colorTextures[i] = GenerateOverlayTexture((RelativeDensity)i);
                }

                _settingsDialog = new GuiProspectorInfoSetting(_clientApi, _config, RebuildMap);
            }
        }

        private void SaveProspectingData() {
            lock(_prospectInfos)
            {
                if (_prospectInfos.HasChanged)
                {
                    _clientApi.SaveDataFile(Filename, _prospectInfos);
                    _prospectInfos.HasChanged = false;
                }
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
                    _config.MapMode = (MapMode)newMode;
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
                            _clientApi.ShowChatMessage("No argument resets the heatmap back to all ores. Can only handle the ore name in your selected language or the ore tag.");
                            _clientApi.ShowChatMessage("E.g. game:ore-emerald, game:ore-bituminouscoal, Cassiterite");
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

        private void OnChatMessage(int groupId, string message, EnumChatType chattype, string data)
        {
            if (_clientApi?.World?.Player == null)
                return;

            var pos = _clientApi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative ? blocksSinceLastSuccessList.LastOrDefault()?.Position : blocksSinceLastSuccessList.ElementAtOrDefault(blocksSinceLastSuccessList.Count - 2 - 1)?.Position;
            if (pos == null || groupId != GlobalConstants.InfoLogChatGroup || !_headerParsingRegex.IsMatch(message))
                return;

            var posX = pos.X / _chunksize;
            var posZ = pos.Z / _chunksize;
            var newProspectInfo = new ProspectInfo(posX, posZ, message);
            lock (_prospectInfos)
            {
                _prospectInfos.RemoveAll(m => m.X == posX && m.Z == posZ);
                _prospectInfos.Add(newProspectInfo);
                _prospectInfos.HasChanged = true;
            }

            _components.RemoveAll(component =>
            {
                var castComponent = component as ProspectorOverlayMapComponent;
                return castComponent?.ChunkX == posX && castComponent.ChunkZ == posZ;
            });

            var newComponent = new ProspectorOverlayMapComponent(_clientApi, posX, posZ, newProspectInfo.GetMessage(), _colorTextures[(int)GetRelativeDensity(newProspectInfo)]);
            _components.Add(newComponent);

            blocksSinceLastSuccessList.Clear();
        }

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

            foreach (var info in _prospectInfos)
            {
                var component = new ProspectorOverlayMapComponent(_clientApi, info.X, info.Z, info.GetMessage(), _colorTextures[(int)GetRelativeDensity(info)]);
                _components.Add(component);
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

        // ReSharper disable once UnusedType.Local
        [HarmonyPatch(typeof(ItemProspectingPick), "ProbeBlockDensityMode")]
        class PrintProbeResultsPatch
        {
            static void Postfix(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel)
            {
                if (world.Side != EnumAppSide.Client)
                    return;

                blocksSinceLastSuccessList.Add(blockSel);
            }
        }
    }
}
