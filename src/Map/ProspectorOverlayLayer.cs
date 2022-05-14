using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Foundation.Extensions;
using Foundation.Utils;
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
        private readonly string[] _triggerwords;
        private readonly ProspectorMessages _messages;
        private readonly int _chunksize;
        private readonly ICoreClientAPI _clientApi;
        private readonly List<MapComponent> _components = new List<MapComponent>();
        private readonly IWorldMapManager _worldMapManager;
        private readonly Regex _cleanupRegex;
        private readonly ModConfig _config;
        private readonly LoadedTexture[] _colorTextures = new LoadedTexture[8];
        private bool _temporaryRenderOverride = false;

        public override string Title => "ProspectorOverlay";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        protected internal static List<BlockSelection> blocksSinceLastSuccessList = new List<BlockSelection>();

        public ProspectorOverlayLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
        {
            _worldMapManager = mapSink;
            _chunksize = api.World.BlockAccessor.ChunkSize;
            _messages = api.LoadOrCreateDataFile<ProspectorMessages>(Filename);
            _triggerwords = LangUtils.GetAllLanguageStringsOfKey("propick-reading-title").Select(x => x.Split().FirstOrDefault()).Where(x => !string.IsNullOrEmpty(x)).ToArray();
            _cleanupRegex = new Regex("<.*?>", RegexOptions.Compiled);

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
                default:
                    _clientApi.ShowChatMessage("Unknown subcommand!");
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
            } else
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

        private void OnChatMessage(int groupId, string message, EnumChatType chattype, string data)
        {
            if (_clientApi?.World?.Player == null)
                return;

            var pos = _clientApi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative ? blocksSinceLastSuccessList.LastOrDefault()?.Position : blocksSinceLastSuccessList.ElementAtOrDefault(blocksSinceLastSuccessList.Count - 2 - 1)?.Position;
            if (pos == null || groupId != GlobalConstants.InfoLogChatGroup || !_triggerwords.Any(triggerWord => message.StartsWith(triggerWord)))
                return;

            message = _cleanupRegex.Replace(message, string.Empty);
            var posX = pos.X / _chunksize;
            var posZ = pos.Z / _chunksize;
            _messages.RemoveAll(m => m.X == posX && m.Z == posZ);
            _messages.Add(new ProspectInfo(posX, posZ, message));
            _clientApi.SaveDataFile(Filename, _messages);

            _components.RemoveAll(component =>
            {
                var castComponent = component as ProspectorOverlayMapComponent;
                return castComponent?.ChunkX == posX && castComponent.ChunkZ == posZ;
            });
            RelativeDensity densityValue;
            if (_config.HeatMapOre == null)
                densityValue = _messages.Last().Values.First().relativeDensity;
            else
                densityValue = _messages.Last().GetValueOfOre(_config.HeatMapOre);
            var newComponent = new ProspectorOverlayMapComponent(_clientApi, posX, posZ, message, _colorTextures[(int)densityValue]);
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

            foreach (var message in _messages)
            {
                RelativeDensity densityValue;
                if (_config.HeatMapOre == null)
                    densityValue = message.Values.First().relativeDensity;
                else
                    densityValue = message.GetValueOfOre(_config.HeatMapOre);
                var component = new ProspectorOverlayMapComponent(_clientApi, message.X, message.Z, message.Message, _colorTextures[(int)densityValue]);
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