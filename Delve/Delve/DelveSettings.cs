using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PoeHUD.Hud.Settings;
using PoeHUD.Plugins;
using SharpDX;

namespace Delve
{
    public class DelveSettings : SettingsBase
    {
        public DelveSettings()
        {
            Enable = true;
        }

        // Delve Pathways
        public ToggleNode DelvePathWays = true;
        public RangeNode<int> DelvePathWaysNodeSize = new RangeNode<int>(7, 1, 200);
        public ColorBGRA DelvePathWaysNodeColor = new ColorBGRA(255, 140, 0, 255);

        public ToggleNode DelveWall { get; set; } = true;
        public RangeNode<int> DelveWallSize { get; set; } = new RangeNode<int>(18, 1, 200);
        public ColorNode DelveWallColor { get; set; } = new ColorBGRA(255, 255, 255, 255);

        // Delve Chests
        public ToggleNode DelveChests = true;

        public ToggleNode DelveMiningSuppliesDynamiteChest { get; set; } = true;
        public RangeNode<int> DelveMiningSuppliesDynamiteChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveMiningSuppliesDynamiteChestColor { get; set; } = new ColorBGRA(255, 255, 255, 255);

        public ToggleNode DelveMiningSuppliesFlaresChest { get; set; } = true;
        public RangeNode<int> DelveMiningSuppliesFlaresChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveMiningSuppliesFlaresChestColor { get; set; } = new ColorBGRA(255, 255, 255, 255);

        public ToggleNode DelveAzuriteVeinChest { get; set; } = true;
        public RangeNode<int> DelveAzuriteVeinChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveAzuriteVeinChestColor { get; set; } = new ColorBGRA(0, 115, 255, 255);

        public ToggleNode DelveCurrencyChest { get; set; } = true;
        public RangeNode<int> DelveCurrencyChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveCurrencyChestColor { get; set; } = new ColorBGRA(255, 255, 255, 255);

        public ToggleNode DelveResonatorChest { get; set; } = true;
        public RangeNode<int> DelveResonatorChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveResonatorChestColor { get; set; } = new ColorBGRA(255, 255, 255, 255);

        public ToggleNode DelveFossilChest { get; set; } = true;
        public RangeNode<int> DelveFossilChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveFossilChestColor { get; set; } = new ColorBGRA(255, 255, 255, 255);

        // Catch all remaining Delve chests
        public ToggleNode DelvePathwayChest { get; set; } = true;
        public RangeNode<int> DelvePathwayChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelvePathwayChestColor { get; set; } = new ColorBGRA(0, 131, 0, 255);

        // Delve Mine Map Connections
        public ToggleNode DelveMineMapConnections { get; set; } = true;
        public RangeNode<int> ShowRadiusPercentage { get; set; } = new RangeNode<int>(80, 0, 100);

        public ToggleNode DebugMode { get; set; } = false;
        public ToggleNode ShouldHideOnOpen { get; set; } = false;
        public HotkeyNode DebugHotkey { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.Menu);
    }
}
