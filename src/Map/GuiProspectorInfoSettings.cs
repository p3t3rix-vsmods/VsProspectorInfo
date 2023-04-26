using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

namespace ProspectorInfo.Map
{
    public class GuiProspectorInfoSetting : GuiDialog
    {
        public override string ToggleKeyCombinationCode => "prospectorinfosettings";
        private readonly ModConfig _config;
        private readonly Action<bool> _rebuildMap;
        private List<KeyValuePair<string, string>> _ores;

        public GuiProspectorInfoSetting(ICoreClientAPI capi, ModConfig config, Action<bool> rebuildMap) : base(capi)
        {
            _config = config;
            _rebuildMap = rebuildMap;
            _ores = ProspectInfo.FoundOres.OrderBy((pair) => pair.Key).ToList();
            _ores.Insert(0, new KeyValuePair<string, string>("All ores", null));
            SetupDialog();
        }

        public override bool TryOpen()
        {
            if (_ores.Count != ProspectInfo.FoundOres.Count() + 1)
            {
                _ores = ProspectInfo.FoundOres.OrderBy((pair) => pair.Key).ToList();
                _ores.Insert(0, new KeyValuePair<string, string>("All ores", null));
                SetupDialog();
            }
            
            return base.TryOpen();
        }

        private void SetupDialog()
        {
            // Auto-sized dialog at the center of the screen
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle);
            ElementBounds backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds dialogContainerBounds = ElementBounds.Fixed(0, 40, 150, 150);
            backgroundBounds.BothSizing = ElementSizing.FitToChildren;
            backgroundBounds.WithChildren(dialogContainerBounds);

            ElementBounds showOverlayTextBounds = ElementBounds.Fixed(35, 55, 120, 40);
            ElementBounds switchBounds = ElementBounds.Fixed(125, 50);
            ElementBounds mapModeBounds = ElementBounds.Fixed(35, 100, 120, 20);
            ElementBounds oreBounds = ElementBounds.Fixed(35, 130, 120, 20);

            var currentHeatmapOreIndex = 0;
            if (_config.HeatMapOre != null)
            {
                currentHeatmapOreIndex = _ores.FindIndex((pair) => pair.Value != null && pair.Value.Contains(_config.HeatMapOre));
                if (currentHeatmapOreIndex == -1) // config.HeatMapOre is not a valid ore name -> reset to all ores
                    currentHeatmapOreIndex = 0;
            }

            SingleComposer = capi.Gui.CreateCompo("ProspectorInfo Settings", dialogBounds)
                .AddShadedDialogBG(backgroundBounds)
                .AddDialogTitleBar("ProspectorInfo", OnCloseTitleBar)
                .AddStaticText("Show overlay", CairoFont.WhiteDetailText(), showOverlayTextBounds)
                .AddSwitch(OnSwitchOverlay, switchBounds, "showOverlaySwitch")
                .AddDropDown(new string[] { "0", "1" }, new string[] { "Default", "Heatmap" }, (int)_config.MapMode, OnMapModeSelected, mapModeBounds)
                .AddDropDown(_ores.Select((pair) => pair.Value).ToArray(), _ores.Select((pair) => pair.Key).ToArray(), currentHeatmapOreIndex, OnHeatmapOreSelected, oreBounds)
                .Compose();

            SingleComposer.GetSwitch("showOverlaySwitch").On = _config.RenderTexturesOnMap;
        }

        private void OnCloseTitleBar()
        {
            _config.ShowGui = false;
            _config.Save(capi);
            TryClose();
        }

        private void OnSwitchOverlay(bool value)
        {
            _config.RenderTexturesOnMap = value;
            _config.Save(capi);
            _rebuildMap(true);
        }

        private void OnMapModeSelected(string code, bool selected)
        {
            _config.MapMode = (MapMode)int.Parse(code);
            _config.Save(capi);
            _rebuildMap(true);
        }

        private void OnHeatmapOreSelected(string code, bool selected)
        {
            _config.HeatMapOre = code;
            _config.Save(capi);
            _rebuildMap(true);
        }
    }
}
