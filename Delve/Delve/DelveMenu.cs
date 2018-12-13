using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using Delve.Libs;

namespace Delve
{
	partial class Delve
	{
		public void DelveMenu(int idIn, out int idPop)
		{
			idPop = idIn;
			if (ImGui.TreeNode("Delve Map Grid - Shown only when fully zoomed in"))
			{
				ImGui.PushID(idPop);
				Settings.DelveGridMap.Value = ImGuiExtension.Checkbox(Settings.DelveGridMap.Value ? "Show" : "Hidden", Settings.DelveGridMap);
				ImGui.PopID();
				idPop++;
				ImGui.PushID(idPop);
				if (ImGui.Button("Set current Delve Zoom as the zoom you want to show grid\r\nZoom in all the way then press the button"))
				{
					Settings.DelveGridMapScale = CurrentDelveMapZoom;
				}
				ImGui.PopID();
				idPop++;
				ImGui.TreePop();
			}
			if (ImGui.TreeNode("Delve Path's"))
			{
				ImGui.PushID(idPop);
				Settings.DelvePathWays.Value = ImGuiExtension.Checkbox(Settings.DelvePathWays.Value ? "Show" : "Hidden", Settings.DelvePathWays);
				ImGui.PopID();
				idPop++;

				ImGui.Spacing();
				ImGui.PushID(idPop);
				Settings.DelvePathWaysNodeSize.Value = ImGuiExtension.IntSlider("Size", Settings.DelvePathWaysNodeSize);
				ImGui.PopID();
				idPop++;
				ImGui.PushID(idPop);
				Settings.DelvePathWaysNodeColor = ImGuiExtension.ColorPicker("Color", Settings.DelvePathWaysNodeColor);
				ImGui.PopID();
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				Settings.DelveWall.Value = ImGuiExtension.Checkbox($"Breakable Wall##{idPop}", Settings.DelveWall);
				idPop++;
				Settings.DelveWallSize.Value = ImGuiExtension.IntSlider($"Size##{idPop}", Settings.DelveWallSize);
				idPop++;
				Settings.DelveWallColor.Value = ImGuiExtension.ColorPicker($"Color##{idPop}", Settings.DelveWallColor);
				idPop++;
				ImGui.TreePop();
			}
			if (ImGui.TreeNode("Delve Chests"))
			{
				ImGui.PushID(idPop);
				Settings.DelveChests.Value = ImGuiExtension.Checkbox(Settings.DelveChests.Value ? "Show" : "Hidden", Settings.DelveChests);
				ImGui.PopID();
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				Settings.DelvePathwayChest.Value = ImGuiExtension.Checkbox($"Hidden Chests on the way##{idPop}", Settings.DelvePathwayChest);
				idPop++;
				Settings.DelvePathwayChestSize.Value = ImGuiExtension.IntSlider($"Size##{idPop}", Settings.DelvePathwayChestSize);
				idPop++;
				Settings.DelvePathwayChestColor.Value = ImGuiExtension.ColorPicker($"Color##{idPop}", Settings.DelvePathwayChestColor);
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				Settings.DelveMiningSuppliesDynamiteChest.Value = ImGuiExtension.Checkbox($"Dynamite Supplies##{idPop}", Settings.DelveMiningSuppliesDynamiteChest);
				idPop++;
				Settings.DelveMiningSuppliesDynamiteChestSize.Value = ImGuiExtension.IntSlider($"Size##{idPop}", Settings.DelveMiningSuppliesDynamiteChestSize);
				idPop++;
				Settings.DelveMiningSuppliesDynamiteChestColor.Value = ImGuiExtension.ColorPicker($"Color##{idPop}", Settings.DelveMiningSuppliesDynamiteChestColor);
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				Settings.DelveMiningSuppliesFlaresChest.Value = ImGuiExtension.Checkbox($"Flare Supplies##{idPop}", Settings.DelveMiningSuppliesFlaresChest);
				idPop++;
				Settings.DelveMiningSuppliesFlaresChestSize.Value = ImGuiExtension.IntSlider($"Size##{idPop}", Settings.DelveMiningSuppliesFlaresChestSize);
				idPop++;
				Settings.DelveMiningSuppliesFlaresChestColor.Value = ImGuiExtension.ColorPicker($"Color##{idPop}", Settings.DelveMiningSuppliesFlaresChestColor);
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				Settings.DelveCurrencyChest.Value = ImGuiExtension.Checkbox($"Currency Chests##{idPop}", Settings.DelveCurrencyChest);
				idPop++;
				Settings.DelveCurrencyChestSize.Value = ImGuiExtension.IntSlider($"Size##{idPop}", Settings.DelveCurrencyChestSize);
				idPop++;
				Settings.DelveCurrencyChestColor.Value = ImGuiExtension.ColorPicker($"Color##{idPop}", Settings.DelveCurrencyChestColor);
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				Settings.DelveAzuriteVeinChest.Value = ImGuiExtension.Checkbox($"Azurite Veins##{idPop}", Settings.DelveAzuriteVeinChest);
				idPop++;
				Settings.DelveAzuriteVeinChestSize.Value = ImGuiExtension.IntSlider($"Size##{idPop}", Settings.DelveAzuriteVeinChestSize);
				idPop++;
				Settings.DelveAzuriteVeinChestColor.Value = ImGuiExtension.ColorPicker($"Color##{idPop}", Settings.DelveAzuriteVeinChestColor);
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				Settings.DelveResonatorChest.Value = ImGuiExtension.Checkbox($"Resonator Chests##{idPop}", Settings.DelveResonatorChest);
				idPop++;
				Settings.DelveResonatorChestSize.Value = ImGuiExtension.IntSlider($"Size##{idPop}", Settings.DelveResonatorChestSize);
				idPop++;
				Settings.DelveResonatorChestColor.Value = ImGuiExtension.ColorPicker($"Color##{idPop}", Settings.DelveResonatorChestColor);
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				Settings.DelveFossilChest.Value = ImGuiExtension.Checkbox($"Fossil Chests##{idPop}", Settings.DelveFossilChest);
				idPop++;
				Settings.DelveFossilChestSize.Value = ImGuiExtension.IntSlider($"Size##{idPop}", Settings.DelveFossilChestSize);
				idPop++;
				Settings.DelveFossilChestColor.Value = ImGuiExtension.ColorPicker($"Color##{idPop}", Settings.DelveFossilChestColor);
				idPop++;
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.TreePop();
			}
			if (ImGui.TreeNode("Delve Helpers"))
			{
				ImGui.PushID(idPop);
				Settings.DelveHelpers.Value = ImGuiExtension.Checkbox(Settings.DelveHelpers.Value ? "Show" : "Hidden", Settings.DelveHelpers);
				ImGui.PopID();
				idPop++;
				ImGui.Spacing();
				ImGui.PushID(idPop);
				Settings.PosX.Value = ImGuiExtension.IntSlider($"Position X##{idPop}", Settings.PosX);
				ImGui.PopID();
				idPop++;
				ImGui.PushID(idPop);
				Settings.PosY.Value = ImGuiExtension.IntSlider($"Position Y##{idPop}", Settings.PosY);
				ImGui.PopID();
				idPop++;
				ImGui.TreePop();
			}
		}
	
		public override void DrawSettingsMenu()
		{
			ImGui.BulletText($"v{PluginVersion}");
			ImGui.BulletText($"Last Updated: {buildDate}");
			idPop = 1;
			ImGui.PushStyleVar(StyleVar.ChildRounding, 5.0f);
			ImGuiNative.igGetContentRegionAvail(out var newcontentRegionArea);
			if (ImGui.BeginChild("RightSettings", new System.Numerics.Vector2(newcontentRegionArea.X, newcontentRegionArea.Y), true, WindowFlags.Default))
			{
				DelveMenu(idPop, out var newInt);
				idPop = newInt;
			}
		}
	}
}