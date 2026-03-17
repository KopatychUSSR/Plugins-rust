using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("WipeBlock", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "2.0.14")]
    [Description("Блокировка предметов для вашего сервера!")]
    public class WipeBlock : RustPlugin
    {
        #region Classes
        private class Configuration
        {
            public class Interface
            {
                [JsonProperty("Сдвиг панели по вертикале (если некорректно отображается при текущих настройках)")]
                public int Margin = 0;
                [JsonProperty("Текст на первой строке")]
                public string FirstString = "БЛОКИРОВКА ПРЕДМЕТОВ";
                [JsonProperty("Текст на второй строке")]
                public string SecondString = "НАЖМИТЕ ЧТОБЫ УЗНАТЬ БОЛЬШЕ";
                [JsonProperty("Название сервера")]
                public string ServerName = "%CONFIG%";
            }

            public class Block 
            {
                [JsonProperty("Сдвиг блокировки в секундах ('1852' - на 1852 секунд вперёд, '-1852' на 1852 секунд назад)")]
                public int TimeMove = 0;
                [JsonProperty("Настройки блокировки предметов")]
                public Dictionary<int, List<string>> BlockItems;
                [JsonProperty("Названия категорий в интерфейсе")]
                public Dictionary<string, string> CategoriesName;
            }
            
            [JsonProperty("Настройки интерфейса плагина")]
            public Interface SInterface;

            [JsonProperty("Настройки текущей блокировки")]
            public Block SBlock;

            [JsonProperty("Включить поддержку экспериментальных функций")]
            public bool Experimental = true;

            public static Configuration GetDefaultConfiguration()
            {
                var newConfiguration = new Configuration();
                newConfiguration.SInterface = new Interface();
                newConfiguration.SBlock = new Block();
                newConfiguration.SBlock.CategoriesName = new Dictionary<string, string>
                {
                    ["Weapon"] = "ОРУЖИЯ",
                    ["Ammunition"] = "БОЕПРИПАСОВ",
                    ["Medical"] = "МЕДИЦИНЫ",
                    ["Food"] = "ЕДЫ",
                    ["Traps"] = "ЛОВУШЕК",
                    ["Tool"] = "ИНСТРУМЕНТОВ",
                    ["Construction"] = "КОНСТРУКЦИЙ",
                    ["Resources"] = "РЕСУРСОВ",
                    ["Items"] = "ПРЕДМЕТОВ",
                    ["Component"] = "КОМПОНЕНТОВ",
                    ["Misc"] = "ПРОЧЕГО",
                    ["Attire"] = "ОДЕЖДЫ"
                };
                newConfiguration.SBlock.BlockItems = new Dictionary<int,List<string>>
                {
                    [1800] = new List<string>
                    {
                        "pistol.revolver",
                        "shotgun.double",
                    },
                    [3600] = new List<string>
                    {
                        "flamethrower",
                        "bucket.helmet",
                        "riot.helmet",
                        "pants",
                        "hoodie",
                    },
                    [7200] = new List<string>
                    {
                        "pistol.python",
                        "pistol.semiauto",
                        "coffeecan.helmet",
                        "roadsign.jacket",
                        "roadsign.kilt",
                        "icepick.salvaged",
                        "axe.salvaged",
                        "hammer.salvaged",
                    },
                    [14400] = new List<string>
                    {
                        "shotgun.pump",
                        "shotgun.spas12",
                        "pistol.m92",
                        "smg.mp5",
                        "jackhammer",
                        "chainsaw",
                    },
                    [28800] = new List<string>
                    {
                        "smg.2",
                        "smg.thompson",
                        "rifle.semiauto",
                        "explosive.satchel",
                        "grenade.f1",
                        "grenade.beancan",
                        "surveycharge"
                    },
                    [43200] = new List<string>
                    {
                        "rifle.bolt",
                        "rifle.ak",
                        "rifle.lr300",
                        "metal.facemask",
                        "metal.plate.torso",
                        "rifle.l96",
                        "rifle.m39"
                    },
                    [64800] = new List<string>
                    {
                        "ammo.rifle.explosive",
                        "ammo.rocket.basic",
                        "ammo.rocket.fire",
                        "ammo.rocket.hv",
                        "rocket.launcher",
                        "explosive.timed"
                    },
                    [86400] = new List<string>
                    {
                        "lmg.m249",
                        "heavy.plate.helmet",
                        "heavy.plate.jacket",
                        "heavy.plate.pants",
                    }
                };
                
                return newConfiguration;
            }
        }

        #endregion
        
        #region Variables

        [PluginReference] 
        private Plugin ImageLibrary, Duel, Duels, Battles;
        private Configuration settings = null;

        private List<string> Gradients = new List<string> { "518eef","5CAD4F","5DAC4E","5EAB4E","5FAA4E","60A94E","61A84E","62A74E","63A64E","64A54E","65A44E","66A34E","67A24E","68A14E","69A04E","6A9F4E","6B9E4E","6C9D4E","6D9C4E","6E9B4E","6F9A4E","71994E","72984E","73974E","74964E","75954E","76944D","77934D","78924D","79914D","7A904D","7B8F4D","7C8E4D","7D8D4D","7E8C4D","7F8B4D","808A4D","81894D","82884D","83874D","84864D","86854D","87844D","88834D","89824D","8A814D","8B804D","8C7F4D","8D7E4D","8E7D4D","8F7C4D","907B4C","917A4C","92794C","93784C","94774C","95764C","96754C","97744C","98734C","99724C","9B714C","9C704C","9D6F4C","9E6E4C","9F6D4C","A06C4C","A16B4C","A26A4C","A3694C","A4684C","A5674C","A6664C","A7654C","A8644C","A9634C","AA624B","AB614B","AC604B","AD5F4B","AE5E4B","B05D4B","B15C4B","B25B4B","B35A4B","B4594B","B5584B","B6574B","B7564B","B8554B","B9544B","BA534B","BB524B","BC514B","BD504B","BE4F4B","BF4E4B","C04D4B","C14C4B","C24B4B","C44B4B" };
        
        private string Layer = "UI_1852InstanceBlock";
        private string LayerBlock = "UI_1852Block";
        private string LayerInfoBlock = "UI_1852InfoBlock"; 

        private string IgnorePermission = "wipeblock.ignore";

        private Dictionary<ulong, int> UITimer = new Dictionary<ulong, int>();

        private Coroutine UpdateAction;
        #endregion

        #region Initialization
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                settings = Config.ReadObject<Configuration>();
                if (settings?.SBlock == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => settings = Configuration.GetDefaultConfiguration();
        protected override void SaveConfig() => Config.WriteObject(settings);

        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("ImageLibrary not found, plugin will not work!");
                return;
            }

            foreach (string item in settings.SBlock.BlockItems.SelectMany(p => p.Value))
            {
                if (!ImageLibrary.Call<bool>("HasImage", item))
                {
                    ImageLibrary.Call("AddImage", ImageLibrary.Call<string>("GetImageURL", item), item);
                }
            }

            permission.RegisterPermission(IgnorePermission, this);

            CheckActiveBlocks();
        }

        private void Unload()
        {
            if (UpdateAction != null)
                ServerMgr.Instance.StopCoroutine(UpdateAction);

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                player.SetFlag(BaseEntity.Flags.Reserved3, false);

                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, LayerBlock);
                CuiHelper.DestroyUi(player, LayerInfoBlock);
            }
        }
        #endregion

        #region Hooks
        private object CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (playerOnDuel(player)) return null;

            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                DrawInstanceBlock(player, item);
            }

            return isBlocked;
        }

        private object CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null)
                return null;
            if (playerOnDuel(player)) return null;
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                DrawInstanceBlock(player, item);
            }

            return isBlocked;
        }

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;
            
            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
            if (isBlocked == false && !playerOnDuel(player))
            {
                List<Item> list = player.inventory.FindItemIDs(projectile.primaryMagazine.ammoType.itemid).ToList<Item>();
                if (list.Count == 0)
                {
                    List<Item> list2 = new List<Item>();
                    player.inventory.FindAmmo(list2, projectile.primaryMagazine.definition.ammoTypes);
                    if (list2.Count > 0)
                    {
                        isBlocked = IsBlocked(list2[0].info) > 0 ? false : (bool?) null;
                    }
                }

                if (isBlocked == false)
                {
                    SendReply(player, $"Вы <color=#81B67A>не можете</color> использовать этот тип боеприпасов!");
                }

                return isBlocked;
            }

            return null;
        }
        
        private object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            NextTick(() =>
            {
                var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    player.GiveItem(ItemManager.CreateByItemID(projectile.primaryMagazine.ammoType.itemid, projectile.primaryMagazine.contents, 0UL), BaseEntity.GiveItemReason.Generic);
                    projectile.primaryMagazine.contents = 0;
                    projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
                    projectile.SendNetworkUpdate();
                    player.SendNetworkUpdate();

                    PrintError($"[{DateTime.Now.ToShortTimeString()}] {player} пытался взломать систему блокировки!");
                    SendReply(player, $"<color=#81B67A>Хорошая</color> попытка, правда ваше оружие теперь сломано!");
                }
            });

            return null;
        }

        private void OnPlayerConnected(BasePlayer player, bool first = true)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player, first));
                return;
            }

            if (settings.Experimental)
            {
                if (first)
                {
                    foreach (string item in settings.SBlock.BlockItems.SelectMany(p => p.Value))
                        SendFilePng(player, item);
                }
            }

            if (!IsAnyBlocked())
                return;

            DrawBlockInfo(player);
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer)
        {
            if (inventory == null || item == null)
                return null;

            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            ItemContainer container = inventory.FindContainer(targetContainer);
            if (container == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret)
            {
                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false )
                {
                    DrawInstanceBlock(player, item);
                    return true;
                }
            }

            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (container == null || item == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret)
            {
                BasePlayer player = item.GetOwnerPlayer();
                if (player == null)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    DrawInstanceBlock(player, item);
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }

            return null;
        }
        #endregion

        #region GUI
        private void DrawBlockInfo(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, LayerInfoBlock);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-180 -35", OffsetMax = "-10 -15"},
                Image = { Color = "0 0 0 0" }
            }, "Hud", LayerInfoBlock);

            container.Add(new CuiButton
            {
                RectTransform = {  AnchorMin = "-3 0", AnchorMax = "1 1.5", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "wipeblock.ui.open" },
                Text = { Text = settings.SInterface.FirstString, Font = "robotocondensed-bold.ttf", Color = HexToRustFormat("#FFFFFF5A"), Align = TextAnchor.UpperRight, FontSize = 20 }, 
            }, LayerInfoBlock);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-3 -0.2", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "wipeblock.ui.open" },
                Text = { Text = settings.SInterface.SecondString, Font = "robotocondensed-bold.ttf", Color = HexToRustFormat("#FFFFFF5A"), Align = TextAnchor.LowerRight, FontSize = 12 }, 
            }, LayerInfoBlock);

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("wipeblock.ui.open")]
        private void cmdConsoleDrawBlock(ConsoleSystem.Arg args)
        {
            if (args.Player() == null)
                return;

            DrawBlockGUI(args.Player());
        }

        [ConsoleCommand("blockmove")]
        private void cmdConsoleMoveblock(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
                return;

            if (!args.HasArgs(1))
            {
                PrintWarning($"Введите количество секунд для перемещения!");
                return;
            }

            int newTime;
            if (!int.TryParse(args.Args[0], out newTime))
            {
                PrintWarning("Вы ввели не число!");
                return;
            }

            settings.SBlock.TimeMove += newTime;
            SaveConfig();
            PrintWarning("Время блокировки успешно изменено!");

            CheckActiveBlocks();
        }

        [ChatCommand("block")]
        private void cmdChatDrawBlock(BasePlayer player)
        {
            DrawBlockGUI(player);
        }

        [ConsoleCommand("wipeblock.ui.close")]
        private void cmdConsoleCloseUI(ConsoleSystem.Arg args)
        {
            if (args.Player() == null)
                return;

            args.Player()?.SetFlag(BaseEntity.Flags.Reserved3, false);
        }

        private void DrawBlockGUI(BasePlayer player)
        {
            if (player.HasFlag(BaseEntity.Flags.Reserved3))
                return;

            if (UITimer.ContainsKey(player.userID))
            {
                if (UITimer[player.userID] == (int)UnityEngine.Time.realtimeSinceStartup)
                    return;
                else
                    UITimer[player.userID] = (int)UnityEngine.Time.realtimeSinceStartup;
            }
            else
            {
                UITimer.Add(player.userID, (int)UnityEngine.Time.realtimeSinceStartup);
            }

            player.SetFlag(BaseEntity.Flags.Reserved3, true); 

            CuiHelper.DestroyUi(player, LayerBlock);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-441.5 {-298 + settings.SInterface.Margin - 20}", OffsetMax = $"441.5 {298 + settings.SInterface.Margin - 20}" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", LayerBlock);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9", Close = LayerBlock, Material = "assets/content/ui/uibackgroundblur.mat", Command = "wipeblock.ui.close"},
                Text = { Text = "" }
            }, LayerBlock);
            
            container.Add(new CuiElement
            {
                Parent = LayerBlock, 
                Name = LayerBlock + ".Header",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.928601852154", AnchorMax = "1.015 0.9998464", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = LayerBlock + ".Header",
                Components =
                {
                    new CuiTextComponent {Text = $"БЛОКИРОВКА ПРЕДМЕТОВ НА {settings.SInterface.ServerName}", FontSize = 30, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                } 
            });

            Dictionary<string, Dictionary<ItemDefinition, string>> blockedItemsGroups = new Dictionary<string, Dictionary<ItemDefinition, string>>();
            FillBlockedItems(blockedItemsGroups);
            var blockedItemsNew = blockedItemsGroups.OrderByDescending(p => p.Value.Count);

            int newString = 0;
            double totalUnblockTime = 0;
            for (int t = 0; t < blockedItemsNew.Count(); t++)
            {
                var blockedCategory = blockedItemsNew.ElementAt(t).Value.OrderBy(p => IsBlocked(p.Value));
                
                container.Add(new CuiElement
                {
                    Parent = LayerBlock,
                    Name = LayerBlock + ".Category",
                    Components =
                    {
                        new CuiImageComponent { Color = "0 0 0 0" },
                        new CuiRectTransformComponent { AnchorMin = $"0 {0.889  - (t) * 0.17 - newString * 0.123}", AnchorMax = $"1.015 {0.925  - (t) * 0.17 - newString * 0.123}", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = LayerBlock + ".Category",
                    Components =
                    {
                        new CuiTextComponent { Color = "1 1 1 1", Text = $"БЛОКИРОВКА {blockedItemsNew.ElementAt(t).Key}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });

                for (int i = 0; i < blockedCategory.Count(); i++)
                {
                    if (i == 12)
                    {
                        newString++;
                    }
                    float margin = Mathf.CeilToInt(blockedCategory.Count() - Mathf.CeilToInt((float) (i + 1) / 12) * 12);
                    if (margin < 0)
                    {
                        margin *= -1;
                    }
                    else
                    {
                        margin = 0;
                    }
                    
                    var blockedItem = blockedCategory.ElementAt(i);
                    container.Add(new CuiElement
                    {
                        Parent = LayerBlock,
                        Name = LayerBlock + $".{blockedItem.Key.shortname}",
                        Components =
                        {
                            new CuiImageComponent { FadeIn = 0.5f, Color = HexToRustFormat((blockedItem.Value + "96"))},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0.008608246 + i * 0.0837714 + ((float) margin / 2) * 0.0837714 - (Math.Floor((double) i / 12) * 12 * 0.0837714)}" +
                                            $" {0.7618223 - (t) * 0.17 - newString * 0.12}", 
                                
                                AnchorMax = $"{0.08415613 + i * 0.0837714 + ((float) margin / 2) * 0.0837714 - (Math.Floor((double) i / 12) * 12 * 0.0837714)}" +
                                            $" {0.8736619  - (t) * 0.17 - newString * 0.12}", OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent { Distance = "1 1", Color = "0 0 0 0.1"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerBlock + $".{blockedItem.Key.shortname}",
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 0.5f,  Png = ImageLibrary?.Call<string>("GetImage", blockedItem.Key.shortname) },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"}
                        }
                    });

                    double unblockTime = IsBlocked(blockedItem.Key);
                    totalUnblockTime += unblockTime;

                    string text = unblockTime > 0
                            ? $"<size=10>ОСТАЛОСЬ</size>\n<size=14>{TimeSpan.FromSeconds(unblockTime).ToShortString()}</size>"
                            : "<size=11>ДОСТУПНО</size>";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { FadeIn = 0.5f,Text = "", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        Button = { Color = "0 0 0 0.5"},
                    }, LayerBlock + $".{blockedItem.Key.shortname}", $"Time.{blockedItem.Key.shortname}");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { FadeIn = 0.5f,Text = text, FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter},
                        Button = { Color = "0 0 0 0" },
                    }, $"Time.{blockedItem.Key.shortname}", $"Time.{blockedItem.Key.shortname}.Update");
                }
            }

            CuiHelper.AddUi(player, container);

            if (totalUnblockTime > 0)
                ServerMgr.Instance.StartCoroutine(StartUpdate(player, totalUnblockTime));
        }

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

        private void DrawInstanceBlock(BasePlayer player, Item item)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();
            string inputText = "Предмет {name} временно заблокирован,\nподождите {1}".Replace("{name}", item.info.displayName.english).Replace("{1}", $"{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked(item.info)).TotalHours))} час {TimeSpan.FromSeconds(IsBlocked(item.info)).Minutes} минут.");
            
            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                Image = { FadeIn = 1f, Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.62 0.95" },
                CursorEnabled = false
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                FadeOut = 1f,
                Parent = Layer,
                Name = Layer + ".Hide",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Hide",
                Name = Layer + ".Destroy1",
                FadeOut = 1f,
                Components =
                {
                    new CuiImageComponent { Color = "0.4 0.4 0.4 0.7"},
                    new CuiRectTransformComponent { AnchorMin = "0 0.62", AnchorMax = "1.1 0.85" }
                }
                
            });
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Color = "0.9 0.9 0.9 1", Text = "ПРЕДМЕТ ЗАБЛОКИРОВАН", FontSize = 22, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, Layer + ".Destroy1", Layer + ".Destroy5");
            container.Add(new CuiButton
            {
                FadeOut = 1f,
                RectTransform = { AnchorMin = "0 0.29", AnchorMax = "1.1 0.61" },
                Button = {FadeIn = 1f, Color = "0.3 0.3 0.3 0.5" },
                Text = { Text = "" }
            }, Layer + ".Hide", Layer + ".Destroy2");
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Text = inputText, FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.85 0.85 0.85 1" , Font = "robotocondensed-regular.ttf"},
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "10 0.9" }
            }, Layer + ".Hide", Layer + ".Destroy3");
            CuiHelper.AddUi(player, container);

            timer.Once(3f, () =>
            {
                CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
            });
        }

        #endregion

        #region Functions
        private string GetGradient(int t)
        {
            var LeftTime = UnBlockTime(t) - CurrentTime();
            return Gradients[Math.Min(99, Math.Max(Convert.ToInt32((float) LeftTime / t * 100), 0))];
        }

        private double IsBlockedCategory(int t) => IsBlocked(settings.SBlock.BlockItems.ElementAt(t).Value.First());
        private bool IsAnyBlocked() => UnBlockTime(settings.SBlock.BlockItems.Last().Key) > CurrentTime();
        private double IsBlocked(string shortname) 
        {
            if (!settings.SBlock.BlockItems.SelectMany(p => p.Value).Contains(shortname))
                return 0;

            var blockTime = settings.SBlock.BlockItems.FirstOrDefault(p => p.Value.Contains(shortname)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            
            return lefTime > 0 ? lefTime : 0;
        }

        private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount + settings.SBlock.TimeMove;

        private double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);

        private void FillBlockedItems(Dictionary<string, Dictionary<ItemDefinition, string>> fillDictionary)
        {
            foreach (var category in settings.SBlock.BlockItems)
            {
                string categoryColor = GetGradient(category.Key);
                foreach (var item in category.Value)
                {
                    ItemDefinition definition = ItemManager.FindItemDefinition(item);
                    string catName = settings.SBlock.CategoriesName[definition.category.ToString()];
                
                    if (!fillDictionary.ContainsKey(catName))
                        fillDictionary.Add(catName, new Dictionary<ItemDefinition, string>());
                
                    if (!fillDictionary[catName].ContainsKey(definition))
                        fillDictionary[catName].Add(definition, categoryColor);
                }
            }
        }

        private void CheckActiveBlocks()
        {
            if (IsAnyBlocked())
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    OnPlayerConnected(player, false);

                UpdateAction = ServerMgr.Instance.StartCoroutine(UpdateInfoBlock());

                SubscribeHooks(true);
            }
            else
                SubscribeHooks(false);
        }
        
        private void SubscribeHooks(bool subscribe)
        {
            if (subscribe)
            {
                Subscribe(nameof(CanWearItem));
                Subscribe(nameof(CanEquipItem));
                Subscribe(nameof(OnReloadWeapon));
                Subscribe(nameof(OnReloadMagazine));
                Subscribe(nameof(CanAcceptItem));
                Subscribe(nameof(CanMoveItem));
            }
            else
            {
                Unsubscribe(nameof(CanWearItem));
                Unsubscribe(nameof(CanEquipItem));
                Unsubscribe(nameof(OnReloadWeapon));
                Unsubscribe(nameof(OnReloadMagazine));
                Unsubscribe(nameof(CanAcceptItem));
                Unsubscribe(nameof(CanMoveItem));
            }
        }
        #endregion

        #region Utils
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
        
        public static string ToShortString(TimeSpan timeSpan)
        {
            int i = 0;
            string resultText = "";
            if (timeSpan.Days > 0)
            {
                resultText += timeSpan.Days + " День";
                i++;
            }
            if (timeSpan.Hours > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Час";
                i++;
            }
            if (timeSpan.Minutes > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Мин.";
                i++;
            }
            if (timeSpan.Seconds > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Сек.";
                i++;
            }

            return resultText;
        }
        
        private void GetConfig<T>(string menu, string key, ref T varObject)
        {
            if (Config[menu, key] != null)
            {
                varObject = Config.ConvertValue<T>(Config[menu, key]);
            }
            else
            {
                Config[menu, key] = varObject;
            }
        }

        private bool playerOnDuel(BasePlayer player)
        {
            if (Duels != null)
                if (Duels.Call<bool>("inDuel", player))
                    return true;
			if (plugins.Find("OneVSOne") && (bool)plugins.Find("OneVSOne").Call("IsEventPlayer",player)) return true;

            if (Duel != null)
            {
                var result = (bool)Duel?.Call("IsPlayerOnActiveDuel", player);
                if (result)
                    return true;
            }
               
            if (Battles != null)
                if (Battles.Call<bool>("IsPlayerOnBattle", player.userID))
                    return true;

            return false;
        }

        private void SendFilePng(BasePlayer player, string imageName, ulong imageId = 0)
        {
            if (!ImageLibrary.Call<bool>("HasImage", imageName, imageId))
                return;

            uint crc = ImageLibrary.Call<uint>("GetImage", imageName, imageId);
            byte[] array = FileStorage.server.Get(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

            if (array == null)
                return;

            CommunityEntity.ServerInstance.ClientRPCEx<uint, uint, byte[]>(new Network.SendInfo(player.net.connection)
            {
                channel = 2,
                method = Network.SendMethod.Reliable
            }, null, "CL_ReceiveFilePng", crc, (uint)array.Length, array);
        }
        #endregion

        #region Coroutines
        private IEnumerator StartUpdate(BasePlayer player, double totalUnblockTime)
        {
            while (player.HasFlag(BaseEntity.Flags.Reserved3) && player.IsConnected && totalUnblockTime > 0)
            {
                totalUnblockTime = 0;

                foreach (var check in settings.SBlock.BlockItems.SelectMany(p => p.Value))
                {
                    CuiElementContainer container = new CuiElementContainer();
                    ItemDefinition blockedItem = ItemManager.FindItemDefinition(check);
                    CuiHelper.DestroyUi(player, $"Time.{blockedItem.shortname}.Update");

                    double unblockTime = IsBlocked(blockedItem);
                    totalUnblockTime += unblockTime;

                    string text = unblockTime > 0
                            ? $"<size=10>ОСТАЛОСЬ</size>\n<size=14>{TimeSpan.FromSeconds(unblockTime).ToShortString()}</size>"
                            : "<size=11>ДОСТУПНО</size>";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text          = { Text        = text, FontSize   = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter},
                        Button        = { Color     = "0 0 0 0" }, 
                    }, $"Time.{blockedItem.shortname}", $"Time.{blockedItem.shortname}.Update");

                    CuiHelper.AddUi(player, container);
                }

                yield return new WaitForSeconds(1);
            }

            player.SetFlag(BaseEntity.Flags.Reserved3, false);
            yield break;
        }

        private IEnumerator UpdateInfoBlock()
        {
            while (true)
            {
                if (!IsAnyBlocked())
                {
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                        CuiHelper.DestroyUi(player, LayerInfoBlock);

                    SubscribeHooks(false);
                    this.UpdateAction = null;
                    yield break;
                }

                yield return new WaitForSeconds(30);
            }
        }
        #endregion
    }
}