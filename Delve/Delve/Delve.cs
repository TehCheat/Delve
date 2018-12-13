using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharpDX;
using SharpDX.Direct3D9;
using PoeHUD.Controllers;
using PoeHUD.Framework.Helpers;
using PoeHUD.Hud;
using PoeHUD.Models;
using PoeHUD.Models.Enums;
using PoeHUD.Plugins;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.RemoteMemoryObjects;

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

			GameController.Area.OnAreaChange += area => AreaChange();
		}

		private void AreaChange()
		{
			DelveEntities.Clear();
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

		private void RenderDelveHelpers()
		{
			DrawRect = new RectangleF(Settings.PosX, Settings.PosY, 70, 30);

			var playerStats = GameController.Player.GetComponent<Stats>().StatDictionary;

			int iDelveSulphiteCapacityID = GameController.Instance.Files.Stats.records["delve_sulphite_capacity"].ID;

			if (!playerStats.ContainsKey(iDelveSulphiteCapacityID))
			{
				return;
			}
			var sc = playerStats[iDelveSulphiteCapacityID];

			DrawData("Resources/Sulphite.png", GameController.Game.IngameState.ServerData.CurrentSulphiteAmount + "/" + sc);
			DrawData("Resources/Azurite.png", GameController.Game.IngameState.ServerData.CurrentAzuriteAmount.ToString());

			var buff = GameController.Player.GetComponent<Life>().Buffs.FirstOrDefault(x => x.Name == "delve_degen_buff");
			//if(buff != null)
			DrawData("Resources/Buff.png", buff == null ? "-" : buff.Charges.ToString());
		}

		public override void Render()
		{
			base.Render();
			if (!Settings.Enable) return;
			DelveMapNodes();
			RenderMapImages();
			if (Settings.DelveHelpers)
			{
				RenderDelveHelpers();
			}
		}

		private void DelveMapNodes()
		{
			if (!Settings.DelveGridMap) return;
			var delveMap = GameController.Game.IngameState.UIRoot.Children[1].Children[59];
			if (!delveMap.IsVisible) return;
			try
			{
				var largeGridList = delveMap.Children[0].Children[0].Children[2].Children.ToList();
				var scale = delveMap.Children[0].Children[0].Children[2].Scale;
				CurrentDelveMapZoom = scale;

				if (scale != Settings.DelveGridMapScale) return;
				//LogMessage($"Count: {largeGrids.Count}", 5);
				for (var i = 0; i < largeGridList.Count; i++)
				{
					var largeGrid = largeGridList[i];

					if (!largeGrid.GetClientRect().Intersects(delveMap.GetClientRect())) continue;

					var smallGridList = largeGrid.Children.ToList();
					for (var j = 0; j < smallGridList.Count - 1; j++)
					{
						var smallGrid = smallGridList[j];

						if (smallGrid.GetClientRect().Intersects(delveMap.GetClientRect()))
							Graphics.DrawFrame(smallGrid.GetClientRect(), 1, Color.DarkGray);
					}
				}
			}
			catch
			{

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
				if (e.Path.EndsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveLight"))
				{
					return new MapIcon(e, new HudTexture(CustomImagePath + "abyss-crack.png", Settings.DelvePathWaysNodeColor), () => true,
							Settings.DelvePathWaysNodeSize);
				}
			}
			if (Settings.DelveChests)
			{
				if (!e.GetComponent<Chest>().IsOpened)
				{
					if (e.Path.Contains("Metadata/Chests/DelveChests/DelveMiningSuppliesDynamite"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Bombs.png", Settings.DelveMiningSuppliesDynamiteChestColor),
							() => Settings.DelveMiningSuppliesDynamiteChest, Settings.DelveMiningSuppliesDynamiteChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests/DelveMiningSuppliesFlares"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Flare.png", Settings.DelveMiningSuppliesFlaresChestColor),
							() => Settings.DelveMiningSuppliesFlaresChest, Settings.DelveMiningSuppliesFlaresChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("PathCurrency"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Currency.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("DynamiteCurrency"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Currency.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("AdditionalSockets"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//AdditionalSockets.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("AtziriFragment"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Fragment.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("PaleCourtFragment"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//PaleCourtComplete.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("Essence"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Essence.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("SilverCoin"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//SilverCoin.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("WisdomScroll"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//WisDomCurrency.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("Divination"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//divinationCard.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests/DelveChestCurrency"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Currency.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("DelveAzuriteVein") && !e.Path.Contains("Encounter"))
					{
						return new MapIcon(e, new HudTexture(PoeHudImageLocation + "strongbox.png", Settings.DelveAzuriteVeinChestColor),
							() => Settings.DelveAzuriteVeinChest, Settings.DelveAzuriteVeinChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("Resonator3") && e.Path.Contains("Resonator4") && e.Path.Contains("Resonator5"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//ResonatorT1.png", Settings.DelveResonatorChestColor),
							() => Settings.DelveResonatorChest, Settings.DelveResonatorChestSize * 0.7f);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("Resonator2"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//ResonatorT2.png", Settings.DelveResonatorChestColor),
							() => Settings.DelveResonatorChest, Settings.DelveResonatorChestSize * 0.7f);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("Resonator1"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//ResonatorT3.png", Settings.DelveResonatorChestColor),
							() => Settings.DelveResonatorChest, Settings.DelveResonatorChestSize * 0.7f);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests/DelveChestArmourMovementSpeed"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//SpeedArmour.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests/DelveChestSpecialUniqueMana"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//UniqueManaFlask.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize * 1.3f);
					}

                    if (e.Path.Contains("RandomEnchant") && e.Path.StartsWith("Metadata/Chests/DelveChests"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "//Enchant.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("6Linked"))
                    {
                        return new MapIcon(e, new HudTexture(CustomImagePath + "//SixLink.png", Settings.DelveCurrencyChestColor),
                            () => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
                    }

                    if (e.Path.EndsWith("FossilChest") && e.Path.StartsWith("Metadata/Chests/DelveChests"))
					{
						foreach (var @string in FossilList.T1)
						{
							if (e.Path.ToLower().Contains(@string.ToLower()))
							{
								return new MapIcon(e, new HudTexture(CustomImagePath + "//AbberantFossilT1.png", Settings.DelveFossilChestColor),
									() => Settings.DelveFossilChest, Settings.DelveFossilChestSize);
							}
						}
						foreach (var @string in FossilList.T2)
						{
							if (e.Path.ToLower().Contains(@string.ToLower()))
							{
								return new MapIcon(e, new HudTexture(CustomImagePath + "//AbberantFossilT2.png", Settings.DelveFossilChestColor),
									() => Settings.DelveFossilChest, Settings.DelveFossilChestSize);
							}
						}
						foreach (var @string in FossilList.T3)
						{
							if (e.Path.ToLower().Contains(@string.ToLower()))
							{
								return new MapIcon(e, new HudTexture(CustomImagePath + "//AbberantFossilT3.png", Settings.DelveFossilChestColor),
									() => Settings.DelveFossilChest, Settings.DelveFossilChestSize);
							}
						}


						return new MapIcon(e, new HudTexture(CustomImagePath + "//AbberantFossil.png", Settings.DelveFossilChestColor),
							() => Settings.DelveFossilChest, Settings.DelveFossilChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("Resonator"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//ResonatorT1.png", Settings.DelveResonatorChestColor),
							() => Settings.DelveResonatorChest, Settings.DelveResonatorChestSize * 0.7f);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("Map"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Map.png", Settings.DelveCurrencyChestColor),
							() => Settings.DelveCurrencyChest, Settings.DelveCurrencyChestSize);
					}

					if (e.Path.Contains("Metadata/Chests/DelveChests") && e.Path.Contains("Corrupted"))
					{
						return new MapIcon(e, new HudTexture(CustomImagePath + "//Corrupted.png", Settings.DelveCurrencyChestColor),
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

					// catch missing delve chests
					if (Settings.DelvePathwayChest)
					{
						if (e.Path.Contains("Metadata/Chests/DelveChests") && !e.Path.Contains("Encounter"))
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
			if (entityWrapper.HasComponent<Chest>() || entityWrapper.Path.StartsWith("Metadata/Terrain/Leagues/Delve/Objects/DelveWall"))
			{
				DelveEntities.Add(entityWrapper);
			}
		}
		public override void EntityRemoved(EntityWrapper entityWrapper)
		{
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
			var Area = GameController.Game.IngameState.Data.CurrentArea;
			if (Area.IsTown || Area.RawName.Contains("Hideout")) return;
			if (GameController.Game.IngameState.IngameUi.Map.LargeMap.IsVisible)
			{
				LargeMapInformation = new LargeMapData(GameController);
				foreach (var entity in DelveEntities)
				{
					if (entity is null) continue;
					DrawToLargeMiniMap(entity);
				}
			}
			else if (GameController.Game.IngameState.IngameUi.Map.SmallMinimap.IsVisible)
			{
				foreach (var entity in DelveEntities)
				{
					if (entity is null) continue;
					DrawToSmallMiniMap(entity);
				}
			}
		}
	}
}
