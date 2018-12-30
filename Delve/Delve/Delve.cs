using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharpDX;
using SharpDX.Direct3D9;
using PoeHUD.Controllers;
using PoeHUD.Framework.Helpers;
using PoeHUD.Hud;
using PoeHUD.Models;
using PoeHUD.Plugins;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.RemoteMemoryObjects;
using PoeHUD.Poe.Elements;

namespace Delve
{
    public partial class Delve : BaseSettingsPlugin<DelveSettings>
    {
        private RectangleF DrawRect;

        public float CurrentDelveMapZoom = 0.635625f;

        private HashSet<EntityWrapper> DelveEntities;

        public Version version = Assembly.GetExecutingAssembly().GetName().Version;
        public string PluginVersion;
        public DateTime buildDate;
        public static int Selected;

        public static int idPop;

        public string CustomImagePath;

        public string PoeHudImageLocation;

        private string AreaName;
        private bool IsAzuriteMine => AreaName == "Azurite Mine" ? true : false;

        public FossilTiers FossilList = new FossilTiers();
        public LargeMapData LargeMapInformation { get; set; }

        public override void Initialise()
        {
            buildDate = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.Revision * 2);

            PluginName = "Delve";
            PluginVersion = $"{version}";
            DelveEntities = new HashSet<EntityWrapper>();
            PoeHudImageLocation = PluginDirectory + @"\..\..\textures\";
            CustomImagePath = PluginDirectory + @"\Resources\";

            GameController.Area.OnAreaChange += AreaChange;

            if (File.Exists($@"{PluginDirectory}\Fossil_Tiers.json"))
            {
                var jsonFile = File.ReadAllText($@"{PluginDirectory}\Fossil_Tiers.json");
                FossilList = JsonConvert.DeserializeObject<FossilTiers>(jsonFile, JsonSettings);
            }
            else
            {
                LogError("Error loading Fossil_Tiers.json, Please re download from Random Features github repository", 10);
            }
        }

        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };

        private void AreaChange(AreaController area)
        {
            DelveEntities.Clear();
            AreaName = area.CurrentArea.Name;
        }

        private void DrawData(string icon, string data)
        {
            var textSize = Graphics.MeasureText(data, 20, FontDrawFlags.Left | FontDrawFlags.VerticalCenter);

            DrawRect.Width = DrawRect.Height + textSize.Width + 10;

            Graphics.DrawBox(DrawRect, Color.Black);

            var imgRect = DrawRect;
            imgRect.Width = imgRect.Height;
            Graphics.DrawPluginImage(Path.Combine(PluginDirectory, icon), imgRect);
            Graphics.DrawFrame(DrawRect, 2, Color.Gray);

            var textPos = DrawRect.TopLeft;
            textPos.Y += DrawRect.Height / 2;
            textPos.X += DrawRect.Height + 3;

            Graphics.DrawText(data, 20, textPos, FontDrawFlags.Left | FontDrawFlags.VerticalCenter);



            DrawRect.X += DrawRect.Width + 1;
        }

        public override void Render()
        {
            base.Render();
            if (!Settings.Enable || !IsAzuriteMine) return;
            SubterraneanChart MineMap = GameController.Game.IngameState.IngameUi.MineMap;
            if (Settings.DelveMineMapConnections.Value) DrawMineMapConnections(MineMap);
            if(!MineMap.IsVisible) RenderMapImages();
            if (Settings.DebugHotkey.PressedOnce())
                Settings.DebugMode.Value = !Settings.DebugMode.Value;
            if (Settings.DebugMode.Value)
            {
                foreach (var entity in DelveEntities.ToArray())
                {
                    if (entity.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveWall") || entity.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveLight"))
                        continue;
                    if (Settings.ShouldHideOnOpen.Value && entity.GetComponent<Chest>().IsOpened)
                        continue;
                    var TextToDisplay = entity.Path.Replace("Metadata/Chests/DelveChests/", "");
                    var textBox = Graphics.MeasureText(TextToDisplay, 0, FontDrawFlags.Center);
                    var screenPosition = GameController.Game.IngameState.Camera.WorldToScreen(entity.Pos, entity);
                    Graphics.DrawBox(new RectangleF((screenPosition.X - textBox.Width/2) - 10, screenPosition.Y - textBox.Height/2, textBox.Width + 20, textBox.Height * 2), Color.White);
                    Graphics.DrawText(TextToDisplay, 20, screenPosition, Color.Black, FontDrawFlags.Center);
                }
            }
        }

        public class LargeMapData
        {
            public Camera @Camera { get; set; }
            public PoeHUD.Poe.Elements.Map @MapWindow { get; set; }
            public RectangleF @MapRec { get; set; }
            public Vector2 @PlayerPos { get; set; }
            public float @PlayerPosZ { get; set; }
            public Vector2 @ScreenCenter { get; set; }
            public float @Diag { get; set; }
            public float @K { get; set; }
            public float @Scale { get; set; }

            public LargeMapData(GameController GC)
            {
                @Camera = GC.Game.IngameState.Camera;
                @MapWindow = GC.Game.IngameState.IngameUi.Map;
                @MapRec = @MapWindow.GetClientRect();
                @PlayerPos = GC.Player.GetComponent<Positioned>().GridPos;
                @PlayerPosZ = GC.Player.GetComponent<Render>().Z;
                @ScreenCenter = new Vector2(@MapRec.Width / 2, @MapRec.Height / 2).Translate(0, -20)
                                   + new Vector2(@MapRec.X, @MapRec.Y)
                                   + new Vector2(@MapWindow.LargeMapShiftX, @MapWindow.LargeMapShiftY);
                @Diag = (float)Math.Sqrt(@Camera.Width * @Camera.Width + @Camera.Height * @Camera.Height);
                @K = @Camera.Width < 1024f ? 1120f : 1024f;
                @Scale = @K / @Camera.Height * @Camera.Width * 3f / 4f / @MapWindow.LargeMapZoom;
            }
        }

        private void DrawToLargeMiniMap(EntityWrapper entity)
        {
            var icon = GetMapIcon(entity);
            if (icon == null)
            {
                return;
            }

            var iconZ = icon.EntityWrapper.GetComponent<Render>().Z;
            var point = LargeMapInformation.ScreenCenter
                        + MapIcon.DeltaInWorldToMinimapDelta(icon.WorldPosition - LargeMapInformation.PlayerPos,
                            LargeMapInformation.Diag, LargeMapInformation.Scale,
                            (iconZ - LargeMapInformation.PlayerPosZ) /
                            (9f / LargeMapInformation.MapWindow.LargeMapZoom));

            var texture = icon.TextureIcon;
            var size = icon.Size * 2; // icon.SizeOfLargeIcon.GetValueOrDefault(icon.Size * 2);
            texture.DrawPluginImage(Graphics, new RectangleF(point.X - size / 2f, point.Y - size / 2f, size, size));
        }

        private void DrawToSmallMiniMap(EntityWrapper entity)
        {
            var icon = GetMapIcon(entity);
            if (icon == null)
            {
                return;
            }

            var smallMinimap = GameController.Game.IngameState.IngameUi.Map.SmallMinimap;
            var playerPos = GameController.Player.GetComponent<Positioned>().GridPos;
            var posZ = GameController.Player.GetComponent<Render>().Z;
            const float scale = 240f;
            var mapRect = smallMinimap.GetClientRect();
            var mapCenter = new Vector2(mapRect.X + mapRect.Width / 2, mapRect.Y + mapRect.Height / 2).Translate(0, 0);
            var diag = Math.Sqrt(mapRect.Width * mapRect.Width + mapRect.Height * mapRect.Height) / 2.0;
            var iconZ = icon.EntityWrapper.GetComponent<Render>().Z;
            var point = mapCenter + MapIcon.DeltaInWorldToMinimapDelta(icon.WorldPosition - playerPos, diag, scale, (iconZ - posZ) / 20);
            var texture = icon.TextureIcon;
            var size = icon.Size;
            var rect = new RectangleF(point.X - size / 2f, point.Y - size / 2f, size, size);
            mapRect.Contains(ref rect, out var isContain);
            if (isContain)
            {
                texture.DrawPluginImage(Graphics, rect);
            }
        }

        private MapIcon GetMapIcon(EntityWrapper e)
        {
            if (Settings.DelvePathWays)
            {
                if (e.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveLight"))
                {
                    return new MapIcon(e, new HudTexture(CustomImagePath + "abyss-crack.png", Settings.DelvePathWaysNodeColor), () => true,
                            Settings.DelvePathWaysNodeSize);
                }
            }
            if (Settings.DelveChests)
            {
                if (!e.GetComponent<Chest>().IsOpened)
                {
                    if (e.Path.EndsWith("Encounter"))
                    {
                        return null;
                    }
                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveMiningSuppliesDynamite"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Bombs.png", Settings.DelveMiningSuppliesDynamiteChestColor),
                            () => Settings.DelveMiningSuppliesDynamiteChest, Settings.DelveMiningSuppliesDynamiteChestSize);
                    }
                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteGeneric")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteArmour")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteWeapon")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteTrinkets"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Bombs.png", Settings.DelveMiningSuppliesDynamiteChestColor),
                            () => Settings.DelveMiningSuppliesDynamiteChest, Settings.DelveMiningSuppliesDynamiteChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveMiningSuppliesFlares"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Flare.png", Settings.DelveMiningSuppliesFlaresChestColor),
                            () => Settings.DelveMiningSuppliesFlaresChest, Settings.DelveMiningSuppliesFlaresChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathCurrency")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/PathCurrency")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrencySockets")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericCurrency"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Currency.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericRandomEnchant"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Enchant.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmour6LinkedUniqueBody")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeapon6LinkedTwoHanded")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourFullyLinkedBody"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "SixLink.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DynamiteCurrency"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Currency.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourBody2AdditionalSockets"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "AdditionalSockets.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericAtziriFragment"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Fragment.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericPaleCourtFragment"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "PaleCourtComplete.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericRandomEssence")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialGenericEssence"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Essence.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrencySilverCoins"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "SilverCoin.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrencyWisdomScrolls"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "WisDomCurrency.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericDivination")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourDivination")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponDivination")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsDivination")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrencyDivination"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "divinationCard.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrency"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Currency.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveAzuriteVein"))
                    {
                        int size = Settings.DelveAzuriteVeinChestSize;
                        if (e.Path.EndsWith("1_3") || e.Path.EndsWith("2_1"))
                            size *= 2;
                        return new MapIcon(e, new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelveAzuriteVeinChestColor),
                            () => Settings.DelveAzuriteVeinChest, size);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator3")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator4")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator5"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "ResonatorT1.png", Settings.DelveResonatorChestColor),
                            () => Settings.DelveResonatorChest, Settings.DelveResonatorChestSize * 0.7f);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator2"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "ResonatorT2.png", Settings.DelveResonatorChestColor),
                            () => Settings.DelveResonatorChest, Settings.DelveResonatorChestSize * 0.7f);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator1"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "ResonatorT3.png", Settings.DelveResonatorChestColor),
                            () => Settings.DelveResonatorChest, Settings.DelveResonatorChestSize * 0.7f);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourMovementSpeed"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "SpeedArmour.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueMana"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "UniqueManaFlask.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize * 1.3f);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericRandomEnchant"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Enchant.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests")
                        && (e.Path.EndsWith("FossilChest") || e.Path.EndsWith("FossilChestDynamite")))
                    {
                        foreach (var @string in FossilList.T1)
                        {
                            if (e.Path.ToLower().Contains(@string.ToLower()))
                            {
                                return new MapIcon(e, new HudTexture(CustomImagePath + "AbberantFossilT1.png", Settings.DelveFossilChestColor),
                                    () => Settings.DelveFossilChest, Settings.DelveFossilChestSize);
                            }
                        }
                        foreach (var @string in FossilList.T2)
                        {
                            if (e.Path.ToLower().Contains(@string.ToLower()))
                            {
                                return new MapIcon(e, new HudTexture(CustomImagePath + "AbberantFossilT2.png", Settings.DelveFossilChestColor),
                                    () => Settings.DelveFossilChest, Settings.DelveFossilChestSize);
                            }
                        }
                        foreach (var @string in FossilList.T3)
                        {
                            if (e.Path.ToLower().Contains(@string.ToLower()))
                            {
                                return new MapIcon(e, new HudTexture(CustomImagePath + "AbberantFossilT3.png", Settings.DelveFossilChestColor),
                                    () => Settings.DelveFossilChest, Settings.DelveFossilChestSize);
                            }
                        }


                        return new MapIcon(e, new HudTexture(CustomImagePath + "AbberantFossil.png", Settings.DelveFossilChestColor),
                            () => Settings.DelveFossilChest, Settings.DelveFossilChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityProtoVaalResonator")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/Resonator"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "ResonatorT1.png", Settings.DelveResonatorChestColor),
                            () => Settings.DelveResonatorChest, Settings.DelveResonatorChestSize * 0.7f);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrencyMaps")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestMap"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Map.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourCorrupted")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrencyVaal")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponCorrupted")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsCorrupted")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalDoubleCorrupted"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "Corrupted.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveWall"))
                    {

                        switch (e.IsAlive)
                        {
                            case false:
                                return new MapIcon(e, new HudTexture(CustomImagePath + "hidden_door.png", Settings.DelveWallColor),
                                    () => Settings.DelveWall, Settings.DelveWallSize);
                            case true:
                                return new MapIcon(e, new HudTexture(CustomImagePath + "gate.png", Settings.DelveWallColor),
                                    () => Settings.DelveWall, Settings.DelveWallSize);
                        }
                    }

                    // TODO: Add useful icons to these

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/PathGeneric")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathGeneric"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/PathArmour")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathArmour"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/PathWeapon")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathWeapon"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/PathTrinkets")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/OffPathTrinkets")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericTrinkets")
                        )
                    {
                        return new MapIcon(e,
                            new HudTexture(CustomImagePath + "rare-amulet.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/ProsperoChest"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericAdditionalUniques")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourMultipleUnique")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponMultipleUnique")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsMultipleUnique"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericAdditionalUnique")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsUnique")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourUnique")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponUnique")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniquePhysical")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueFire")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueCold")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueLightning")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueChaos")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialUniqueMana")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityAbyssUnique")
                    )
                    {
                            return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericProphecyItem"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericElderItem")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsElder")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourElder")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponElder"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericShaperItem")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponShaper")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsShaper")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourShaper"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericOffering"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericDelveUnique"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueOnslaught"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueAnarchy"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueAmbushInvasion"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueDomination"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueNemesis"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueRampage"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueBeyond"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueBloodlines"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueTorment"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueWarbands"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueTempest"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueTalisman"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeaguePerandus"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueBreach"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueHarbinger"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueAbyss"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueBestiary"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGenericLeagueIncursion"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourMultipleResists"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourLife"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourAtlas"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmourOfCrafting")
                        || e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsOfCrafting"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponPhysicalDamage"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponCaster"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponExtraQuality"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponCannotRollCaster"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeaponCannotRollAttacker"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeapon30QualityUnique"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsAmulet"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsRing"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsJewel"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsAtlas"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinketsEyesOfTheGreatwolf"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemGCP"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemHighQuality"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemHighLevel"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemHighLevelQuality"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGemLevel4Special"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCurrencyHighShards"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestMapChisels"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestMapCorrupted"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestAssortedFossils"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialArmourMinion"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialTrinketsMinion"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecialGenericMinion"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalImplicit"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalAtzoatlRare"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalUberAtziri"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityVaalDoubleCorrupted"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityAbyssStygian"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityAbyssJewels"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityAbyssHighJewel"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityProtoVaalAzurite"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityProtoVaalFossils"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestCityProtoVaalEmblem"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestSpecial"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGem"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestTrinkets"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestWeapon"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestArmour"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    if (e.Path.StartsWith("Metadata/Chests/DelveChests/DelveChestGeneric"))
                    {
                        return new MapIcon(e,
                            new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                            () => true,
                            Settings.DelvePathwayChestSize);
                    }

                    // catch missing delve chests
                    if (Settings.DelvePathwayChest)
                    {
                        if (e.Path.StartsWith("Metadata/Chests/DelveChests") && !e.Path.Contains("Encounter"))
                        {
                            return new MapIcon(e,
                                new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelvePathwayChestColor),
                                () => true,
                                Settings.DelvePathwayChestSize);
                        }
                    }
                }
            }
            return null;
        }

        public override void EntityAdded(EntityWrapper entityWrapper)
        {
            if (!IsAzuriteMine)
                return;

            if (entityWrapper.Path.StartsWith("Metadata/Chests/DelveChests/")
                || entityWrapper.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveWall")
                || entityWrapper.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveLight")
            )
            {
                DelveEntities.Add(entityWrapper);
            }
        }
        public override void EntityRemoved(EntityWrapper entityWrapper)
        {
            if (!IsAzuriteMine)
                return;

            if (DelveEntities.Contains(entityWrapper))
            {
                DelveEntities.Remove(entityWrapper);
            }
        }

        public class FossilTiers
        {
            [JsonProperty("t1")]
            public string[] T1 { get; set; }

            [JsonProperty("t2")]
            public string[] T2 { get; set; }

            [JsonProperty("t3")]
            public string[] T3 { get; set; }
        }

        private void RenderMapImages()
        {
            if (GameController.Game.IngameState.IngameUi.Map.LargeMap.IsVisible)
            {
                LargeMapInformation = new LargeMapData(GameController);
                foreach (var entity in DelveEntities.ToArray())
                {
                    if (entity is null) continue;
                    DrawToLargeMiniMap(entity);
                }
            }
            else if (GameController.Game.IngameState.IngameUi.Map.SmallMinimap.IsVisible)
            {
                foreach (var entity in DelveEntities.ToArray())
                {
                    if (entity is null) continue;
                    DrawToSmallMiniMap(entity);
                }
            }
        }

        private void DrawMineMapConnections(SubterraneanChart MineMap)
        {
            if (!MineMap.IsVisible)
                return;

            int width = 0;
            RectangleF connection_area = RectangleF.Empty;
            RectangleF MineMapArea = MineMap.GetClientRect();
            float reducedWidth = ((100 - Settings.ShowRadiusPercentage.Value) * MineMapArea.Width)/200;
            float reduceHeight = ((100 - Settings.ShowRadiusPercentage.Value) * MineMapArea.Height)/200;
            MineMapArea.Inflate(0 - reducedWidth, 0 - reduceHeight);
            foreach (var zone in MineMap.GridElement.Children)
            {
                foreach (var block in zone.Children)
                {
                    if (MineMapArea.Contains(block.GetClientRect().Center))
                    {
                        foreach (var connection in block.Children)
                        {
                            width = (int)connection.Width;
                            if ((width == 10 || width == 4))
                                Graphics.DrawFrame(connection.GetClientRect(), 1, Color.Yellow);
                        }
                    }
                }
            }
        }
    }
}
