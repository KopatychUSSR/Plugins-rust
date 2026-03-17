using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GatherMoney", "TopPlugin.ru", "2.1.23")]

    public class GatherMoney : RustPlugin
    {
		bool progressbar = false;
		
        public class ShopSettings
        {
            [JsonProperty("ID Магазина GameStores (Оставте поле пустым если у Вас магазин Moscow.ovh)")]
            public string shopid = "12345";
            [JsonProperty("Секретный ключ магазина GameStores (Оставте поле пустым если у Вас магазин Moscow.ovh)")]
            public string secretcode = "xhls574y8jav1wv8uuaf622pubcd9iyc";

            [JsonProperty("Если поля GameStores пустые, здесь должно быть true (заполняется автоматически)")]
            public bool MoscowShop;
        }

        public class MoneySettings
        {
            [JsonProperty("Сколько выдавать рублей")]
            public int MoneyFromXp = 1;
            [JsonProperty("Сколько нужно набрать XP для перевода в рубли")]
            public int XpToMoney = 100;
            [JsonProperty("Ссылка на донат-магазин")]
            public string shopurl = "playrust.ru";
            [JsonProperty("Название валюты (например RUB, RET и т.д.)")]
            public string valuta = "RUB";
            [JsonProperty("Цвет сообщений")]
            public string color = "#FF69B4";
            [JsonProperty("Уменьшать (true) или нет (false) выдаваемый опыт если ресурс/бочка добыт буром/бензопилой")]
            public bool goodtoolsdebuff = true;
            [JsonProperty("Во сколько раз уменьшаем опыт если ресурс добыт буром/бензопилой")]
            public int debuff = 4;
        }

        public class XPSettings
        {
            [JsonProperty("Сколько дает XP за: Убийство NPC ученого")]
            public double xpscientist = 2.5;
            [JsonProperty("Сколько дает XP за: Убийство игрока")]
            public double xpplayer = 5;
            [JsonProperty("Сколько дает XP за: Убийство животного")]
            public double xpanimal = 1;
            [JsonProperty("Сколько дает XP за: Уничтожение бочки")]
            public double xpbarrel = 1;
            [JsonProperty("Сколько дает XP за: Уничтожение танка")]
            public double xpbradley = 100;
            [JsonProperty("Сколько дает XP за: Подбор ресурса с земли (руда/дерево)")]
            public double xppickup = 0.5;
            [JsonProperty("Сколько дает XP за: Открытие контейнера с лутом")]
            public double xploot = 1;
            [JsonProperty("Сколько дает XP за: Фарм дерева/руды (за каждый удар)")]
            public double xpgather = 0.5;
        }


        private class PluginConfig
        {
            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            [DefaultValue(default(ShopSettings))]
            [JsonProperty("Настройки Магазина", DefaultValueHandling = DefaultValueHandling.Populate)]
            public ShopSettings ShopSetting;

            [DefaultValue(default(ShopSettings))]
            [JsonProperty("Настройки Валюты", DefaultValueHandling = DefaultValueHandling.Populate)]
            public MoneySettings MoneySetting;

            [DefaultValue(default(ShopSettings))]
            [JsonProperty("Настройки XP", DefaultValueHandling = DefaultValueHandling.Populate)]
            public XPSettings XPSetting;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    MoneySetting = new MoneySettings(),
                    ShopSetting = new ShopSettings(),
                    XPSetting = new XPSettings()
                };
            }
        }
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за покупку плагина на сайте RustPlugin.ru. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();
            Config.WriteObject(config, true);
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void UpdateConfigValues()
        {

            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                PrintWarning("Config update detected! Updating config values...");

                PrintWarning("Config update completed!");

            }

            config.PluginVersion = Version;
        }

        class PlayerInfo
        {
            public Dictionary<ulong, PlayersClasses> Players = new Dictionary<ulong, PlayersClasses>();
            public PlayerInfo() { }
        }

        class PlayersClasses
        {
            public double XP;
            public int Dengi;
        }

        PlayerInfo info;
		
        bool init = false;
		
        private void OnServerInitialized()
        {
            LoadData();
            if (plugins.Find("RustStore"))
            {
                config.ShopSetting.MoscowShop = true;
                PrintWarning("Plugin loaded, working with RustStore (Moscow.OVH)");
            }
            else if (plugins.Find("GameStoresRUST"))
            {
                config.ShopSetting.MoscowShop = false;
                PrintWarning("Plugin loaded, working with GameStores");
                if (string.IsNullOrEmpty(config.ShopSetting.shopid) || string.IsNullOrEmpty(config.ShopSetting.secretcode))
                {
                    PrintError("Please change SHOPID and SECRETCODE");
                    Interface.Oxide.UnloadPlugin(Title);
                    return;
                }
            }
            else
            {
                PrintError("Can't find store plugin! Please check for it.");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            timer.Every(360, SaveData);
            init = true;
        }

        public void GiveXp(BasePlayer player, double amount)
        {
            if (!init) return;
            info.Players[player.userID].XP += amount;
            if (info.Players[player.userID].XP > config.MoneySetting.XpToMoney)
            {
                double money = info.Players[player.userID].XP / config.MoneySetting.XpToMoney;
                double was = info.Players[player.userID].XP;
                info.Players[player.userID].XP -= (int)money * 100;
                money = (int)money * config.MoneySetting.MoneyFromXp;
                info.Players[player.userID].Dengi += (int)money;
                PrintToChat(player, $"Вы набрали <color={config.MoneySetting.color}>{was}</color> из <color={config.MoneySetting.color}>{config.MoneySetting.XpToMoney}</color> единиц опыта и получили <color={config.MoneySetting.color}>{money} {config.MoneySetting.valuta}</color>.\nДо следующего пополнения: <color={config.MoneySetting.color}>{config.MoneySetting.XpToMoney - info.Players[player.userID].XP} опыта</color>.\nИспользуйте <color={config.MoneySetting.color}>/money</color> для вывода.");
            }
			if (progressbar) ShowProgressBar(player);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (entity is BasePlayer && entity.GetComponent<NPCPlayer>() == null)
            {
                BasePlayer player = entity as BasePlayer;
                Item active = player.GetActiveItem();
                if (config.MoneySetting.goodtoolsdebuff && (active.info.shortname == "chainsaw" || active.info.shortname == "jackhammer")) GiveXp(player, config.XPSetting.xpgather / config.MoneySetting.debuff);
                else GiveXp(player, config.XPSetting.xpgather);
            }
            return;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hinfo)
        {
            try
            {
                if (hinfo.Initiator is BasePlayer && hinfo != null && hinfo.InitiatorPlayer.GetComponent<NPCPlayer>() == null)
                {
                    BasePlayer player = hinfo.InitiatorPlayer;
					
					if (!info.Players.ContainsKey(player.userID))
					{
						PrintError("Игрок не найден в data!");
						return;
					}

                    if (entity.ShortPrefabName.Contains("scientist"))
                    {
                        GiveXp(player, config.XPSetting.xpscientist);
                        return;
                    }

                    if (entity is BasePlayer && entity.GetComponent<NPCPlayer>() == null)
                    {
                        GiveXp(player, config.XPSetting.xpplayer);
                        return;
                    }

                    if (entity.PrefabName.Contains("agent"))
                    {
                        GiveXp(player, config.XPSetting.xpanimal);
                        return;
                    }

                    if (entity.PrefabName.Contains("barrel"))
                    {
                        if (config.MoneySetting.goodtoolsdebuff && player.GetActiveItem().info.shortname == "jackhammer") GiveXp(player, config.XPSetting.xpbarrel / config.MoneySetting.debuff);
                        else GiveXp(player, config.XPSetting.xpbarrel);
                        return;
                    }

                    if (entity is BradleyAPC)
                    {
                        GiveXp(player, config.XPSetting.xpbradley);
                        return;
                    }
                }
            }
            catch (NullReferenceException)
            {

            }
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            GiveXp(player, config.XPSetting.xppickup);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is LootContainer) || entity.OwnerID != 0) return;

            GiveXp(player, config.XPSetting.xploot);
            entity.OwnerID = player.userID;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!init)
			{
				timer.In(1f, () => OnPlayerConnected(player));
				return;
			}
            if (player.IsReceivingSnapshot)
            {
                timer.In(1f, () => OnPlayerConnected(player));
                return;
            }
            if (!info.Players.ContainsKey(player.userID))
            {
                info.Players.Add(player.userID, new PlayersClasses()
                {
                    XP = 0,
                    Dengi = 0
                });
            }
            else if (info.Players.ContainsKey(player.userID) && info.Players[player.userID].Dengi > 0)
            {
                PrintToChat(player, $"На Вашем счету находится {info.Players[player.userID].Dengi} {config.MoneySetting.valuta}\nИспользуйте /money для вывода");
            }
			if (progressbar)
			{
				CuiHelper.DestroyUi(player, "GatherMoney" + ".Bar");
				CuiElementContainer container = new CuiElementContainer();
				container.Add(new CuiPanel
				{
					CursorEnabled = false,
					RectTransform = { AnchorMin = "0.3445 0.003", AnchorMax = "0.4069 0.003", OffsetMax = "300 14" },
					Image = { Color = "0.7 0.7 0.7 0.3" }
				}, "Overlay", "GatherMoney" + ".Bar");
				CuiHelper.AddUi(player, container);
				ShowProgressBar(player);
			}
        }
		
        [ChatCommand("money")]
        void cmdBonus(BasePlayer player, string cmd, string[] args)
        {
            ShowMenu(player);
        }

        [ChatCommand("xp")]
        private void cmdGetBalance(BasePlayer player, string cmd, string[] args)
        {
            PrintToChat(player, $"На Вашем счету <color={config.MoneySetting.color}>{info.Players[player.userID].XP}</color> единиц опыта.");
        }
		
		private void ShowProgressBar(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, "GatherMoney.Bar.Progress");
			
			CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiElement
			{
				Name = "GatherMoney" + ".Bar" + ".Progress",
				Parent = "GatherMoney" + ".Bar",
				Components =
				{
					new CuiImageComponent { Color = "1 0 1 0.25" },
					new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"{Math.Min(0.01 + 1*((float) info.Players[player.userID].XP / config.MoneySetting.XpToMoney), 1)} 1", OffsetMax = "0 0" }
				}
			});
			
			CuiHelper.DestroyUi(player, "GatherMoney.Bar.ProgressText");
			
			container.Add(new CuiElement
			{
				Name = "GatherMoney" + ".Bar" + ".ProgressText",
				Parent = "GatherMoney" + ".Bar",
				Components =
				{
					new CuiTextComponent { FadeIn = 0.2f, Align = TextAnchor.MiddleCenter, Text = $"XP: {info.Players[player.userID].XP}/{config.MoneySetting.XpToMoney}   BALANCE: {info.Players[player.userID].Dengi} {config.MoneySetting.valuta}", FontSize = 10, Color = "1 1 1 0.3" },
					new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80 -20", OffsetMax = "80 20" }
				}
			});
            
            CuiHelper.AddUi(player, container);
		}

        private void ShowMenu(BasePlayer player)
        {
            if (!init) return;
			
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "GatherMoney");

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.3166667 0.4169213", AnchorMax = "0.6833333 0.5930786" },
                Image = { Color = "0 0 0 0" }
            }, "Hud", "GatherMoney");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100" },
                Button = { Close = "GatherMoney", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "GatherMoney");

            container.Add(new CuiElement
            {
                Parent = "GatherMoney",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0.4" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "GatherMoney",
                Name = "GatherMoney" + ".Header",
                Components =
                {
                    new CuiImageComponent { Color = "0.482 0.407 0.933 0.6" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.7752296", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "GatherMoney" + ".Header",
                Components =
                {
                    new CuiTextComponent { Text = "ВЫВОД БОНУСОВ", Align = TextAnchor.MiddleCenter, FontSize = 22, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            string helpText = $"Ваш бонусный баланс: <color={config.MoneySetting.color}>{info.Players[player.userID].Dengi} {config.MoneySetting.valuta}</color>\nДля перевода введите нужное количество {config.MoneySetting.valuta} и нажмите ENTER";
            container.Add(new CuiElement
            {
                FadeOut = 0.4f,
                Name = "GatherMoney" + ".Menu",
                Parent = "GatherMoney",
                Components =
                {
                    new CuiTextComponent { FadeIn = 0.4f, Text = helpText, Align = TextAnchor.MiddleCenter, FontSize = 17, Font = "robotocondensed-regular.ttf"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "GatherMoney",
                Name = "GatherMoney" + ".Vvod",
                Components =
                {
                    new CuiImageComponent { Color = "1 1 1 0.5" },
                    new CuiRectTransformComponent { AnchorMin = "0.02 0.09027457", AnchorMax = "0.98 0.2771558" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "GatherMoney" + ".Vvod",
                Name = "GatherMoney" + ".Vvod.Current",
                Components =
                {
                    new CuiInputFieldComponent { FontSize = 16, Align = TextAnchor.MiddleCenter, Command = "gmwithdraw "},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("gmwithdraw")]
        private void cmdgmwithdraw(ConsoleSystem.Arg args)
        {
            if (!init) return;
            if (args.Connection == null) return;
            if (args.Player() == null) return;
            if (args.Args.Length <= 0) return;
            int vivods;
            if (!Int32.TryParse(args.Args[0], out vivods))
            {
                PrintToChat(args.Player(), "<size=15>ОШИБКА! Введите число!</size>");
                CuiHelper.DestroyUi(args.Player(), "GatherMoney");
                return;
            }
            if (vivods < 1)
            {
                PrintToChat(args.Player(), "<size=15>ОШИБКА! Введите число больше ноля!</size>");
                CuiHelper.DestroyUi(args.Player(), "GatherMoney");
                return;
            }
            if (info.Players[args.Player().userID].Dengi < vivods)
            {
                PrintToChat(args.Player(), "<size=15>ОШИБКА! У Вас нет столько бонусов!</size>");
                CuiHelper.DestroyUi(args.Player(), "GatherMoney");
                return;
            }
            if ((info.Players[args.Player().userID].Dengi < vivods) || vivods < 1)
            {
                LogToFile("vivod", $"[{DateTime.Now.ToShortTimeString()}] {args.Player().displayName} ({args.Player().userID}) tried to hack (first)!", this, true);
                CuiHelper.DestroyUi(args.Player(), "GatherMoney");
                return;
            }
            if (config.ShopSetting.MoscowShop)
            {
                plugins.Find("RustStore").CallHook("APIChangeUserBalance", args.Player().userID, vivods, new Action<string>((result) =>
                {
                    if (result == "SUCCESS")
                    {
                        if (info.Players[args.Player().userID].Dengi < vivods)
                        {
                            LogToFile("vivods", $"[{DateTime.Now.ToShortTimeString()}] {args.Player().displayName} ({args.Player().userID}) tried to hack (second)!", this, true);
                            return;
                        }
                        info.Players[args.Player().userID].Dengi = info.Players[args.Player().userID].Dengi - vivods;
                        SaveData();
                        Interface.Oxide.LogDebug($"Игрок {args.Player().displayName} ({args.Player().userID}) вывел {vivods} {config.MoneySetting.valuta} в магазин");
                        LogToFile("vivod", $"[{DateTime.Now.ToShortTimeString()}] {args.Player().displayName} ({args.Player().userID}) вывел в магазин {args.Args[0]} {config.MoneySetting.valuta}. Бонусный баланс: {info.Players[args.Player().userID].Dengi}", this, true);
                        SendReply(args.Player(), $"Вы вывели <color={config.MoneySetting.color}>{args.Args[0]} {config.MoneySetting.valuta}</color> на баланс магазина <color={config.MoneySetting.color}>{config.MoneySetting.shopurl}</color>!");
                        CuiHelper.DestroyUi(args.Player(), "GatherMoney");
                        return;
                    }
                    if (result != "SUCCESS")
                    {
                        Interface.Oxide.LogDebug($"Ошибка: {result}! Игрок: {args.Player().displayName} ({args.Player().userID})");
                        LogToFile("vivod", $"[{DateTime.Now.ToShortTimeString()}] Ошибка: {result}! {args.Player().displayName} ({args.Player().userID})", this, true);
                        SendReply(args.Player(), $"Ошибка вывода! Убедитесь, что Вы авторизованы в магазине: {config.MoneySetting.shopurl}");
                        CuiHelper.DestroyUi(args.Player(), "GatherMoney");
                    }
                }));
            }
            else
            {
                webrequest.EnqueueGet($"http://gamestores.ru/api?shop_id={config.ShopSetting.shopid}&secret={config.ShopSetting.secretcode}&action=moneys&type=plus&steam_id={args.Player().userID}&amount={vivods}", (otvet, err) =>
                {
                    if (otvet == 200)
                    {
                        if (info.Players[args.Player().userID].Dengi < vivods)
                        {
                            LogToFile("vivods", $"[{DateTime.Now.ToShortTimeString()}] {args.Player().displayName} ({args.Player().userID}) tried to hack (second)!", this, true);
                            return;
                        }
                        info.Players[args.Player().userID].Dengi = info.Players[args.Player().userID].Dengi - vivods;
                        SaveData();
                        Interface.Oxide.LogDebug($"Игрок {args.Player().displayName} ({args.Player().userID}) вывел {vivods} {config.MoneySetting.valuta} в магазин");
                        LogToFile("vivod", $"[{DateTime.Now.ToShortTimeString()}] {args.Player().displayName} ({args.Player().userID}) вывел в магазин {args.Args[0]} {config.MoneySetting.valuta}. Бонусный баланс: {info.Players[args.Player().userID].Dengi}", this, true);
                        SendReply(args.Player(), $"Вы вывели <color={config.MoneySetting.color}>{args.Args[0]} {config.MoneySetting.valuta}</color> на баланс магазина <color={config.MoneySetting.color}>{config.MoneySetting.shopurl}</color>!");
                        CuiHelper.DestroyUi(args.Player(), "GatherMoney");
                        return;
                    }
                    if (otvet != 200)
                    {
                        Interface.Oxide.LogDebug($"Ошибка {otvet}: {err}! Игрок: {args.Player().displayName} ({args.Player().userID})");
                        LogToFile("vivod", $"[{DateTime.Now.ToShortTimeString()}] Ошибка {otvet}: {err}! {args.Player().displayName} ({args.Player().userID})", this, true);
                        SendReply(args.Player(), $"Ошибка вывода! Убедитесь, что Вы авторизованы в магазине: {config.MoneySetting.shopurl}");
                        CuiHelper.DestroyUi(args.Player(), "GatherMoney");
                    }
                }, this);
            }
            return;
        }

        void OnServerSave() => SaveData();

        void Unload() => SaveData();

        void SaveData()
        {
            if (info != null)
                Interface.Oxide.DataFileSystem.WriteObject("GatherMoney", info);
        }

        void LoadData()
        {
            try
            {
                info = Interface.GetMod().DataFileSystem.ReadObject<PlayerInfo>("GatherMoney");
                if (info == null)
                    info = new PlayerInfo();
            }
            catch
            {
                info = new PlayerInfo();
            }
        }
    }
}