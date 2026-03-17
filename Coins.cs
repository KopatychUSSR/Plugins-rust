using Oxide.Core;
using System;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Coins", "http://topplugin.ru/", "1.0.0")]
    [Description("Добавляет монетки на ваш сервер")]
    public class Coins : RustPlugin
    {
        #region Config [Конфиг]

		string secret = "dd45a517253d20e7f01fbdb73ff41ef1";
		string shopId = "14613";
        bool LogsPlayer = true;

		public class ItemRecord {
			[JsonProperty(PropertyName = "Shortname")]
			public string target;

			[JsonProperty(PropertyName = "Новое имя")]
			public string name;

			[JsonProperty(PropertyName = "Новый id скина")]
			public ulong skinid;
		}

		public class Configurarion {
			[JsonProperty(PropertyName = "Список предметов для замены скинов и имени")]
			public List<ItemRecord> items;
		}

		public Configurarion config;

		protected override void LoadDefaultConfig() {
			config = new Configurarion {
				items = new List<ItemRecord> {
					new ItemRecord {
						target = "sticks",
						name   = "Серебрянная монета",
						skinid = 1555861907
					},
					new ItemRecord {
						target = "glue",
						name   = "Золотая монета",
						skinid = 1555862731
					}
				}
			};
			SaveConfig();
		}

        #endregion

        #region Methods [Методы]

		protected override void SaveConfig() => Config.WriteObject(config);

		private void Loaded() {
			try {
				config = Config.ReadObject<Configurarion>();
			} catch {
				LoadDefaultConfig();
			}
		}

		void OnItemAddedToContainer(ItemContainer container, Item item) {
			if (item == null || item.info == null) return;

			var name = item.info.shortname.ToLower();

			foreach (var configRow in config.items) {
				if (configRow.target.ToLower() != name || configRow.skinid == item.skin) continue;

				item.name = configRow.name;
				item.skin = configRow.skinid;
			}
		}

		void OnItemDropped(Item item, BaseEntity entity) { }

        private void OnLootSpawn(LootContainer lootContainer)
        {
            if (lootContainer == null) return;
            if (lootContainer.inventory == null) return;  

            #region SilverCoin

            if(UnityEngine.Random.Range(0, 100) <= 25)
            {
                if (lootContainer.ShortPrefabName == "crate_normal_2")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("sticks", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "crate_basic")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("sticks", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "crate_mine")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("sticks", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "crate_normal")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("sticks", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "loot_barrel_1")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("sticks", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "loot_barrel_2")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("sticks", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "oil_barrel")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("sticks", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "minecart")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("sticks", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }
            }

            #endregion

            #region GoldCoin

            if(UnityEngine.Random.Range(0, 100) <= 10)
            {
                if (lootContainer.ShortPrefabName == "crate_normal_2")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("glue", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "crate_basic")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("glue", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "crate_mine")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("glue", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "crate_normal")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("glue", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "loot_barrel_1")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("glue", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "loot_barrel_2")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("glue", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "oil_barrel")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("glue", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }

                if (lootContainer.ShortPrefabName == "minecart")
                {
                    int amount = UnityEngine.Random.Range(1, 1);
                    Item add = ItemManager.CreateByName("glue", amount);
                    if (add == null)
                    {
                        PrintError("Item not found!");
                    }
                    else
                    {
                        add.MoveToContainer(lootContainer.inventory);
                    }  
                }
            }
            #endregion      
        }

        void OnServerInitialized()
        {
            if (!plugins.Exists("ImageLibrary"))
            {
                PrintError("Плагин ImageLibrary не загружен");
                Unload();
                return;
            }

            ImageLibrary.Call("AddImage", "https://i.imgur.com/fgNADOO.png", "1");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/1C2sq8x.png", "2");
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "Layer");  
            }   
        }

        #endregion

        #region Addons [Референсы]

        [PluginReference] private Plugin ImageLibrary;

        #endregion

        #region ChatCommands [Команды]

        [ChatCommand("coins")]
        private void CmdCoins(BasePlayer player)
        {
            EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward), player.net.connection);
            Main(player);
        }

        [ChatCommand("trade.sticks")]
        private void CmdTradeSilver(BasePlayer player)
        {
			var sticks = player.inventory.GetAmount(642482233);//642482233 sticks
						
			if (sticks >= 1)
			{
				player.inventory.Take(null, 642482233, 100);
			}
			else
			{
			player.ChatMessage("Недостаточно монет для обмена! \nМинимальное количество для обмена - 200 шт.");
				return;
			}

			MoneyPlus(player.userID, 10);
			player.ChatMessage("Вы успешно обменяли монеты!");
			return;
        }

        [ChatCommand("trade.glue")]
        private void CmdTradeGlue(BasePlayer player)
        {
			var glue = player.inventory.GetAmount(-1899491405);//-1899491405 glue
						
			if (glue >= 100)
			{
				player.inventory.Take(null, -1899491405, 100);
			}
			else
			{
			player.ChatMessage("Недостаточно монет для обмена! \nМинимальное количество для обмена - 200 шт.");
				return;
			}

			MoneyPlus(player.userID, 30);
			player.ChatMessage("Вы успешно обменяли монеты!");
			return;
        }

        #endregion

        #region Выдача баланса

		void MoneyPlus(ulong userId, int amount) {
			ApiRequestBalance(new Dictionary<string, string>() {
				{"action", "moneys"},
				{"type", "plus"},
				{"steam_id", userId.ToString()},
				{"amount", amount.ToString()}
			});
		}

		void ApiRequestBalance(Dictionary<string, string> args) {
			string url =
				$"http://panel.gamestores.ru/api?shop_id={shopId}&secret={secret}{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
			webrequest.EnqueueGet(url,
				(i, s) => {
					if (i != 200 && i != 201) {
						PrintError($"{url}\nCODE {i}: {s}");

						if (LogsPlayer) {
							LogToFile("logError", $"({DateTime.Now.ToShortTimeString()}): {url}\nCODE {i}: {s}", this);
						}
					} else {
						if (LogsPlayer) {
							LogToFile("logWEB",
								$"({DateTime.Now.ToShortTimeString()}): "
							+ "Пополнение счета:"
							+ $"{string.Join(" ", args.Select(arg => $"{arg.Value}").ToArray()).Replace("moneys", "").Replace("plus", "")}",
								this);
						}
					}

					if (i == 201) {
						PrintWarning("Плагин не работает!");
						Interface.Oxide.UnloadPlugin(Title);
					}
				},
				this);
		}

        #endregion

        #region GUI [Цуи]

        private void Main(BasePlayer d)
        {
            string Layer = "Layer";

            CuiHelper.DestroyUi(d, Layer);

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat("#FFFFFF00") },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", Layer);
            
            /* Блюр на бэкграунд */
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent { Color = "0 0 0 0.85", FadeIn = 0.25f, Sprite = "assets/content/ui/ui.background.tiletex.psd", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent { Text = "Наш магазин...", FadeIn = 0.25f, Align = TextAnchor.MiddleCenter, FontSize = 15, Font = "RobotoCondensed-regular.ttf"},
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.7 0.7" },
                    new CuiRectTransformComponent { AnchorMin = "0.2554686 -0.06111113", AnchorMax = "0.7289063 0.1166662"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent { Text = "Выберете что вы хотите обменять.", FadeIn = 0.25f, Align = TextAnchor.MiddleCenter, FontSize = 25, Font = "RobotoCondensed-regular.ttf"},
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.7 0.7" },
                    new CuiRectTransformComponent { AnchorMin = "0.2078125 0.579167", AnchorMax = "0.778125 0.7569444"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.3593742 0.4333332", AnchorMax = "0.4765613 0.6416668"},
                Button = {Close = "", Color = HexToRustFormat("#9696967F"), Material = "assets/content/ui/ui.background.tiletex.psd" },
                Text = {Text = "", Align = TextAnchor.MiddleCenter, FontSize = 10}
            }, Layer);
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent {  Png = (string) ImageLibrary.Call("GetImage", "1"), },
                    new CuiRectTransformComponent(){AnchorMin = "0.3773429 0.4680555", AnchorMax = "0.4554679 0.6069443"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent { Text = "x100 = 10p", FadeIn = 0.25f, Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "RobotoCondensed-regular.ttf"},
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.7 0.7" },
                    new CuiRectTransformComponent { AnchorMin = "0.4039064 0.3888889", AnchorMax = "0.4820313 0.5277778"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.5156242 0.4333332", AnchorMax = "0.6328112 0.6416668"},
                Button = {Close = "", Color = HexToRustFormat("#9696967F"), Material = "assets/content/ui/ui.background.tiletex.psd" },
                Text = {Text = "", Align = TextAnchor.MiddleCenter, FontSize = 10}
            }, Layer);
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent {  Png = (string) ImageLibrary.Call("GetImage", "2"), },
                    new CuiRectTransformComponent(){AnchorMin = "0.5335929 0.4680554", AnchorMax = "0.6117179 0.6069443"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent { Text = "x100 = 30p", FadeIn = 0.25f, Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "RobotoCondensed-regular.ttf"},
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.7 0.7" },
                    new CuiRectTransformComponent { AnchorMin = "0.5585939 0.3888889", AnchorMax = "0.6367188 0.5277778"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Close = Layer, Color = HexToRustFormat("#FFFFFF00")},
                Text = {Text = "", Align = TextAnchor.MiddleCenter, FontSize = 10}
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.3593742 0.4333332", AnchorMax = "0.4765613 0.6416668"},
                Button = {Close = Layer, Command = "chat.say /trade.sticks", Color = HexToRustFormat("#FFFFFF00")},
                Text = {Text = "", Align = TextAnchor.MiddleCenter, FontSize = 10}
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.5156242 0.4333332", AnchorMax = "0.6328112 0.6416668"},
                Button = {Close = Layer, Command = "chat.say /trade.glue", Color = HexToRustFormat("#FFFFFF00")},
                Text = {Text = "", Align = TextAnchor.MiddleCenter, FontSize = 10}
            }, Layer);

            CuiHelper.AddUi(d, container);
        }

        #endregion

        #region Helpers

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion
    }
}