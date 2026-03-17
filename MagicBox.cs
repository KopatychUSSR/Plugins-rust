using System;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("MagicBox", "topplugin.ru", "1.0.2")]
    public class MagicBox : RustPlugin
    {
        #region Additional [Дополнительные листы и так далее]

        [PluginReference] private Plugin ImageLibrary;
        private List<CraftItem> CraftItems = new List<CraftItem>();
        public List<uint> Boxes = new List<uint>();
        public List<uint> ActiveBoxes = new List<uint>();
        public List<ulong> LeavedPlayers = new List<ulong>();
        public Dictionary<ulong, Dictionary<string, int>> DepositItem = new Dictionary<ulong, Dictionary<string, int>>();

        #endregion

        #region Data [Работа с датой]

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("MagicBox/Data/Boxes", Boxes);
            Interface.Oxide.DataFileSystem.WriteObject("MagicBox/Data/ActiveBoxes", ActiveBoxes);
            Interface.Oxide.DataFileSystem.WriteObject("MagicBox/Data/Deposits", DepositItem);
            Interface.Oxide.DataFileSystem.WriteObject("MagicBox/Data/LeavedPlayers", LeavedPlayers);
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("MagicBox/Data/Boxes"))
                Boxes = Interface.Oxide.DataFileSystem.ReadObject<List<uint>>("MagicBox/Data/Boxes");
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("MagicBox/Data/ActiveBoxes"))
                ActiveBoxes = Interface.Oxide.DataFileSystem.ReadObject<List<uint>>("MagicBox/Data/ActiveBoxes");
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("MagicBox/Data/Deposits"))
                DepositItem = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, int>>>("MagicBox/Data/Deposits");
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("MagicBox/Data/LeavedPlayers"))
                LeavedPlayers = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("MagicBox/Data/LeavedPlayers");
        }

        private void WipeData()
        {
            foreach (var boxes in BaseEntity.serverEntities)
            {
                if (boxes == null) continue;
                if (Boxes.Contains(boxes.net.ID))
                {
                    boxes.GetComponent<BaseEntity>().skinID = 0;
                    var storage = boxes.GetComponent<StorageContainer>();
                    if (storage == null) continue;
                    if (storage.inventory.capacity == 0 || storage.inventory.capacity == 1) boxes.GetComponent<StorageContainer>().inventory.capacity = 12;
                    boxes.SendNetworkUpdate();
                }
            }
            Boxes.Clear();
            DepositItem.Clear();
            ActiveBoxes.Clear();
            LeavedPlayers.Clear();
        }

        #endregion

        #region Config [Конфигурация плагина]

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Основные настройки плагина")]
            public Settings PluginSets = new Settings();

            public class Settings
            {
                [JsonProperty("СкинID ящика")]
                public ulong skinid = 1915614851;
                [JsonProperty("Максимальное количество предмета для старта магического процесса")]
                public int maxitemamount = 1000;
                [JsonProperty("Время через которое закончится магический процесс (секунды)")]
                public int time = 60;
                [JsonProperty("Список разрешенных предметов (в данной версии желательно использовать только ресурсы)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> Items = new List<string>()
                {
                    { "sulfur" },
                    { "wood" },
                    { "stones" },
                    { "metal.fragments" },
                    { "metal.refined" },
                    { "cloth" },
                    { "leather" },
                    { "charcoal" }
                };
            }
        }

        public class CraftItem
        {
            [JsonProperty("Шортнейм предмета")]
            public string Shortname;
            [JsonProperty("Количество предмета, необходимое для крафта")]
            public int Amount;
            [JsonProperty("URL картинки")]
            public string URL;
        }

        private void Init()
        {
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        #endregion

        #region Hooks [Хуки, методы]

        void OnServerSave()
        {
            SaveData();
        }

        void Unload()
        {
            ActiveBoxes.Clear();
            DepositItem.Clear();
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "Pending");
                CuiHelper.DestroyUi(player, "Text");
                CuiHelper.DestroyUi(player, "Craft");
            }
        }

        void OnServerInitialized()
        {
            LoadData();
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("MagicBox/CraftItems"))
            {
                CraftItems.Add(new CraftItem
                {
                    Shortname = "rifle.ak",
                    Amount = 1,
                    URL = "https://www.rustedit.io/images/imagelibrary/rifle.ak.png",
                });
                CraftItems.Add(new CraftItem
                {
                    Shortname = "stones",
                    Amount = 1000,
                    URL = "https://www.rustedit.io/images/imagelibrary/stones.png",
                });

                Interface.Oxide.DataFileSystem.WriteObject("MagicBox/CraftItems", CraftItems);
                NextTick(OnServerInitialized);
                return;
            }
            CraftItems = Interface.Oxide.DataFileSystem.ReadObject<List<CraftItem>>("MagicBox/CraftItems");
            if (CraftItems.Count > 10)
            {
                PrintError("В списке предметов для крафта обнаружено более 10 предметов. Выгружаю плагин..");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            if (!plugins.Exists("ImageLibrary"))
            {
                PrintError("Плагин ImageLibrary не загружен, дальнейшая работа плагина невозможна!");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            foreach (var check in CraftItems.Select((i, t) => new { A = i, B = t })) ImageLibrary.Call("AddImage", check.A.URL, check.A.Shortname);
            ImageLibrary.Call("AddImage", "https://i.imgur.com/w7FWEbo.png", "MagicBox");

            UpdateBoxes();
        }

        void UpdateBoxes()
        {
            DepositItem.Clear();
            foreach (var boxes in BaseEntity.serverEntities)
            {
                if (boxes == null) continue;
                if (Boxes.Contains(boxes.net.ID))
                {
                    var storagecontainer = boxes.GetComponent<StorageContainer>();
                    if (storagecontainer == null) continue;
                    storagecontainer.inventory.capacity = 1;
                    boxes.SendNetworkUpdate();
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity.name.Contains("woodbox_deployed") && entity.GetComponent<BaseEntity>().skinID == cfg.PluginSets.skinid)
            {
                var box = entity.GetComponent<StorageContainer>();
                if (box == null) return;
                box.inventory.capacity = 1;
                Boxes.Add(box.net.ID);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
			if (DepositItem==null) return;
            if (DepositItem.ContainsKey(player.userID)) LeavedPlayers.Add(player.userID);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
			
            if (LeavedPlayers.Contains(player.userID))
            {
                if (LeavedPlayers.Contains(player.userID)) LeavedPlayers.Remove(player.userID);
                player.ChatMessage("<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nВы вышли во время магического процесса. Он успешно завершён, но возможно ваши ресурсы уже украли игроки, или магический ящик вовсе сгнил. Бегом проверять!");
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || entity.net==null) return;
			if (!entity.name.Contains("woodbox_deployed"))return;
            if (Boxes.Contains(entity.net.ID)) Boxes.Remove(entity.net.ID);
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
			if (!entity.name.Contains("woodbox_deployed"))return;
            if (Boxes.Contains(entity.net.ID)) Boxes.Remove(entity.net.ID);
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;
			if (!entity.name.Contains("woodbox_deployed"))return;
            if (Boxes.Contains(entity.net.ID)) DrawText(player);
        }

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (player == null || entity == null) return;
			if (!entity.name.Contains("woodbox_deployed"))return;
            if (Boxes.Contains(entity.net.ID))
            {
                CuiHelper.DestroyUi(player, "Text");
                CuiHelper.DestroyUi(player, "Pending");
            }
        }

        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (container == null) return null;
            if (Boxes.Contains(container.uid - 1))
            {
                var player = item.GetOwnerPlayer();
                if (player == null) return null;
                if (cfg.PluginSets.Items.Contains(item.info.shortname))
                {
                    if (targetPos != 0) return ItemContainer.CanAcceptResult.CannotAccept;
                    if (DepositItem.ContainsKey(player.userID))
                    {
                        player.ChatMessage("<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nВы уже вкладывали ресурсы в магический ящик\nПодождите пока пройдет некоторое время, чтобы вложить новые");
                        return ItemContainer.CanAcceptResult.CannotAccept;
                    }
                    ItemTreatment(player, item.info.shortname, item.amount, container.uid);
                    return ItemContainer.CanAcceptResult.CanAccept;
                }
                else
                {
                    player.ChatMessage("<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nВы не можете использовать данный предмет для запуска магического процесса");
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }
            }
            return null;
        }

        void ItemTreatment(BasePlayer player, string shortname, int amount, uint containeruid)
        {
            if (!cfg.PluginSets.Items.Contains(shortname) || amount < 1) return;
            DrawPendingUI(player, containeruid, shortname, amount);
        }

        void GiveMagicBox(BasePlayer player)
        {
            if (player == null) return;
            Item item = ItemManager.CreateByName("box.wooden", 1);
            if (item == null) return;
            item.skin = cfg.PluginSets.skinid;
            item.name = "Магический ящик";
            player.GiveItem(item);
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || entity.net==null) return null;
			if (!entity is BoxStorage) return null;
			if (!entity.name.Contains("woodbox_deployed"))return null;
            if (ActiveBoxes.Contains(entity.net.ID)) return false;
            return null;
        }

        bool CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return false;
            if (ActiveBoxes.Contains(entity.net.ID)) return false;
            return true;
        }


        #endregion

        #region Fixes [Фиксы сплита и т.д.]

        private Item OnItemSplit(Item item, int amount)
        {
            if (item != null && item.skin == cfg.PluginSets.skinid)
            {
                Item x = ItemManager.CreateByPartialName("box.wooden", amount);
                x.name = "Магический ящик";
                x.skin = cfg.PluginSets.skinid;
                x.amount = amount;

                item.amount -= amount;
                item.MarkDirty();
                return x;
            }
            return null;
        }

        private bool? CanStackItem(Item item, Item targetItem)
        {
            if (item.skin != targetItem.skin)
                return false;

            return null;
        }

        private bool? CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.GetItem() == null || targetItem.GetItem() == null)
                return null;

            if (item.GetItem().skin != targetItem.GetItem().skin)
                return false;

            return null;
        }

        #endregion

        #region Commands [Консольные и чат команды]

        [ConsoleCommand("magicbox_takeitem")]
        private void TakeItem(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null && !args.HasArgs(3)) return;
            if (DepositItem.ContainsKey(player.userID))
            {
                player.ChatMessage("<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nВы уже вкладывали ресурсы в магический ящик\nПодождите пока пройдет магический процесс, чтобы вложить новые");
                return;
            }
            var containeruid = uint.Parse(args.Args[0]);
            var shortname = args.Args[1];
            var amount = int.Parse(args.Args[2]);
            var container = BaseEntity.serverEntities.Find(containeruid - 1);
            if (container == null) return;
            var storagecontainer = container.GetComponent<StorageContainer>();
            if (storagecontainer == null) return;
            if (amount > cfg.PluginSets.maxitemamount)
            {
                player.ChatMessage($"<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nДостигнут лимит вложения ресурсов\nМаксимальное количество: {cfg.PluginSets.maxitemamount}");
                return;
            }
            if (storagecontainer.health < 150)
            {
                player.ChatMessage($"<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nМы не можем начать магический процесс, так как у ящика недостаточно очков здоровья");
                return;
            }
            if (storagecontainer.inventory.itemList.Count == 0) return;
            foreach (var item in storagecontainer.inventory.itemList)
            {
                if (item == null) return;
                if (item.info.shortname != shortname) return;
                DepositItem.Add(player.userID, new Dictionary<string, int> { { shortname, amount } });
                item.UseItem(amount);
            }
            storagecontainer.inventory.capacity = 0;
            CuiHelper.DestroyUi(player, "Pending");
            ActiveBoxes.Add(storagecontainer.net.ID);
            player.ChatMessage($"<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nМагический процесс запущен, ожидайте..");
            timer.Once(cfg.PluginSets.time, () =>
            {
                if (container == null) return;
                Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", container.transform.position);
                storagecontainer.inventory.capacity = 1;
                foreach (var element in DepositItem[player.userID])
                {
                    Item newitem = ItemManager.CreateByName(element.Key, element.Value * 2);
                    if (newitem == null || storagecontainer.inventory.IsFull()) return;
                    storagecontainer.inventory.Insert(newitem);
                }
                if (ActiveBoxes.Contains(storagecontainer.net.ID)) ActiveBoxes.Remove(storagecontainer.net.ID);
                if (player == null) return;
                if (DepositItem.ContainsKey(player.userID)) DepositItem.Remove(player.userID);
                player.ChatMessage("<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nМагический процесс завершен. Вы можете забрать ресурсы, которые вкладывали в ящик");
            });
        }

        [ChatCommand("magicbox")]
        void DrawCraftUIToPlayer(BasePlayer player)
        {
            if (player == null) return;
            DrawCraftUI(player);
        }


        [ConsoleCommand("craftmagicbox")]
        void CraftMagicBox(ConsoleSystem.Arg arg)
        {
            bool enough = true;
            var player = arg.Player();
            if (player == null) return;
            foreach (var items in CraftItems)
            {
                var haveCount = player.inventory.GetAmount(ItemManager.FindItemDefinition(items.Shortname).itemid);
                if (haveCount >= items.Amount) continue;
                enough = false;
            }
            if (!enough) return;
            foreach (var elem in CraftItems)
            {
                player.inventory.Take(null, ItemManager.FindItemDefinition(elem.Shortname).itemid, elem.Amount);
            }
            Item item = ItemManager.CreateByName("box.wooden", 1);
            if (item == null) return;
            item.skin = cfg.PluginSets.skinid;
            item.name = "Магический ящик";
            player.GiveItem(item);
            player.ChatMessage("<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nВы успешно скрафтили магический ящик");
        }

        [ChatCommand("givemagicbox")]
        void GiveAdminMagicBox(BasePlayer player)
        {
            if (player == null || !player.IsAdmin) return;
            Item item = ItemManager.CreateByName("box.wooden", 5);
            if (item == null) return;
            item.skin = cfg.PluginSets.skinid;
            item.name = "Магический ящик";
            player.GiveItem(item);
        }

        [ChatCommand("magicboxwipe")]
        void WipeData(BasePlayer player)
        {
            if (player == null || !player.IsAdmin) return;
            WipeData();
            player.ChatMessage("<color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color>\nОчистка даты была произведена успешно. Перезагрузите плагин");
        }

        [ConsoleCommand("magicbox.givebox")]
        private void CmdGiveBox(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            if (!arg.HasArgs(1)) return;
            var player = BasePlayer.Find(arg.Args[0]);
            GiveMagicBox(player);
        }

        #endregion

        #region UI [Интерфейс плагина]

        private void DrawText(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Text");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0", FadeIn = 0.15f },
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-445 285", OffsetMax = "-65 345" },
                CursorEnabled = false,
            }, "Overlay", "Text");

            container.Add(new CuiElement
            {
                Parent = "Text",
                Components =
                {
                    new CuiTextComponent { Text = "<b><color=#a751cf>МАГИЧЕСКИЙ ЯЩИК</color></b>\nЕсли вы положите сюда ресурсы, то по истечению магического процесса вы получите эти же ресурсы, но увеличенные в два раза!", FadeIn = 0.15f, Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                    new CuiOutlineComponent { Color = "0 0 0 0.75", Distance = "0.5 0.5" }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        private void DrawPendingUI(BasePlayer player, uint containeruid, string shortname, int amount)
        {
            CuiHelper.DestroyUi(player, "Pending");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat", FadeIn = 0.15f },
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-445 100", OffsetMax = "-65 240" },
                CursorEnabled = true,
            }, "Overlay", "Pending");

            container.Add(new CuiElement
            {
                Parent = "Pending",
                Components =
                {
                    new CuiTextComponent { Text = "Вы уверены что хотите вложить данный ресурс?", FadeIn = 0.15f, Align = TextAnchor.UpperCenter, FontSize = 14, Font = "RobotoCondensed-regular.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 0.8900002"},
                    new CuiOutlineComponent { Color = "0 0 0 0.75", Distance = "0.5 0.5" }
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2486867 0.1850002", AnchorMax = "0.4238172 0.6850003" },
                Button = { Command = $"magicbox_takeitem {containeruid} {shortname} {amount}", FadeIn = 0.15f, Color = HexToRustFormat("#A7F56A76") },
                Text = { Text = "ДА", Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-regular.ttf" }
            }, "Pending");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5744292 0.1850002", AnchorMax = "0.7495596 0.6850003" },
                Button = { Close = "Pending", FadeIn = 0.15f, Color = HexToRustFormat("#BA383876") },
                Text = { Text = "НЕТ", Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-regular.ttf" }
            }, "Pending");

            CuiHelper.AddUi(player, container);
        }

        private void DrawCraftUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Craft");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.7", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", "Craft");

            container.Add(new CuiElement
            {
                Parent = "Craft",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#252A23F2") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "Craft",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#a751cfFF"), Text = "МАГИЧЕСКИЙ ЯЩИК", Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.4617186 0.6333336", AnchorMax = "0.8273436 0.6861113"},
                    new CuiOutlineComponent { Color = "0 0 0 0.75", Distance = "0.5 0.5" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "Craft",
                Components =
                {
                    new CuiTextComponent { Text = "Если вы положите в данный ящик ящик ресурсы, то по истечению магического процесса вы получите эти же ресурсы, но увеличенные в два раза!", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-regular.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.4390625 0.5625", AnchorMax = "0.8531247 0.6625"},
                    new CuiOutlineComponent { Color = "0 0 0 0.75", Distance = "0.5 0.5" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "Craft",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF28") },
                    new CuiRectTransformComponent { AnchorMin = "0.19375 0.2833337", AnchorMax = "0.428125 0.6999997" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "Craft",
                Components =
                    {
                        new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", "MagicBox")},
                        new CuiRectTransformComponent {AnchorMin = "0.19375 0.2833337", AnchorMax = "0.428125 0.6999997"}
                    }
            });

            foreach (var check in CraftItems.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiElement
                {
                    Parent = "Craft",
                    Name = "Craft" + ".panels",
                    Components =
                    {
                        new CuiImageComponent {Color = HexToRustFormat("#FFFFFF28")},
                        new CuiRectTransformComponent {AnchorMin = $"{0.4421877 + check.B * 0.08203 - Math.Floor((double) check.B / 5) * 5 * 0.08203} {0.4291653 - Math.Floor((double) check.B / 5) * 0.145832}", AnchorMax = $"{0.5203124 + check.B * 0.0820301 - Math.Floor((double) check.B / 5) * 5 * 0.0820301} {0.5680559 - Math.Floor((double) check.B / 5) * 0.1458337}",}
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = "Craft" + ".panels",
                    Components =
                    {
                        new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", check.A.Shortname)},
                        new CuiRectTransformComponent {AnchorMin = "0.02 0.02", AnchorMax = "0.98 0.98"}
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = "Craft" + ".panels",
                    Components =
                    {
                        new CuiTextComponent { Text = $"x{check.A.Amount}", Align = TextAnchor.MiddleRight, FontSize = 12, Font = "RobotoCondensed-regular.ttf" },
                        new CuiRectTransformComponent {AnchorMin = "0.02 0", AnchorMax = "0.96 0.22"},
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "0.5 0.5" }
                    }
                });
            }

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Close = "Craft" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, "Craft");

            bool enough = true;
            foreach (var items in CraftItems)
            {
                var haveCount = player.inventory.GetAmount(ItemManager.FindItemDefinition(items.Shortname).itemid);
                if (haveCount >= items.Amount) continue;
                enough = false;
            }
            if (enough)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = HexToRustFormat("#A751CFFF"), Close = "Craft", Command = "craftmagicbox" },
                    RectTransform = { AnchorMin = "0.19375 0.2361111", AnchorMax = "0.428125 0.2777779" },
                    Text = { Text = "СКРАФТИТЬ", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#6A3782FF"), FontSize = 18, Font = "RobotoCondensed-bold.ttf" }
                }, "Craft");
            }
            else
            {
                container.Add(new CuiButton
                {
                    Button = { Color = HexToRustFormat("#B83434FF"), Close = "Craft" },
                    RectTransform = { AnchorMin = "0.19375 0.2361111", AnchorMax = "0.428125 0.2777779" },
                    Text = { Text = "НЕДОСТАТОЧНО РЕСУРСОВ", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#763434FF"), FontSize = 18, Font = "RobotoCondensed-bold.ttf" }
                }, "Craft");
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Helpers [Вспомагательные методы для интерфейса]

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

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion
    }
}