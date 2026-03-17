using Newtonsoft.Json;
using ox = Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using ru = Oxide.Game.Rust;

namespace Oxide.Plugins
{
    [Info("ZetaCraft", "TopPlugin.ru", "0.0.4")]
    [Description("Крафт меню")]
    class ZetaCraft : RustPlugin
    {
        #region Plugins
        Plugin ImageLibrary => ox.Interface.Oxide.RootPluginManager.GetPlugin("ImageLibrary");
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        #endregion

        #region Config
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Список крафтов")]
            public Dictionary<string, main> recipts;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    recipts = new Dictionary<string, main>()
                };
            }
        }
        #endregion

        #region Head
        private class main
        {
            public string name;
            public int amount;
            public string image;
            public List<items> items = new List<items>();
        }
        private class items
        {
            public string name;
            public int amount;
        }

        private Dictionary<string, int> lastpage = new Dictionary<string, int>();
        private List<string> _page = new List<string>();
        private Dictionary<string, main> recipts = new Dictionary<string, main>();
        Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        public void Command(string command, params object[] args)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, command, args);
        }
        #endregion

        #region Main
        [HookMethod("OnServerInitialized")]
        private void OnServerInitialized()
        {
            LoadConfig();
            permission.RegisterPermission("zetacraft.use", this);
            recipts = config.recipts;

            ox.Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand("craft.menu", this, "consolecommand");
            ox.Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand("craft.recipt", this, "createitem");
            ox.Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand("craft.grenadeexit", this, "consolecommandexit");
            ox.Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddChatCommand("craft", this, "chatcommand");

            if (recipts.Count() == 0)
            {
                recipts.Add("multiplegrenadelauncher", new main() { name = "Многозарядный гранатомёт", amount = 1, items = new List<items> { new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 } } });
                recipts.Add("ammo.grenadelauncher.he", new main() { name = "40мм граната", amount = 50, items = new List<items> { new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 } } });
                recipts.Add("ammo.grenadelauncher.smoke", new main() { name = "40мм дымовая граната", amount = 50, items = new List<items> { new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 } } });
                recipts.Add("addgroup {steamid} vip 7d", new main() { name = "VIP на 7 дней", image = "https://gamestores.pictures/images/2017/07/03/VIP_7.jpg", amount = 1, items = new List<items> { new items { name = "wood", amount = 1000 }, new items { name = "autoturret", amount = 2 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 } } });
                recipts.Add("addgroup {steamid} vip 14d", new main() { name = "VIP на 14 дней", image = "https://gamestores.pictures/images/2017/07/03/VIP_14.jpg", amount = 1, items = new List<items> { new items { name = "wood", amount = 1000 }, new items { name = "autoturret", amount = 2 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 } } });
                recipts.Add("addgroup {steamid} vip 30d", new main() { name = "VIP на 30 дней", image = "https://gamestores.pictures/images/2017/07/03/VIP_30.jpg", amount = 1, items = new List<items> { new items { name = "wood", amount = 1000 }, new items { name = "autoturret", amount = 2 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 } } });
                recipts.Add("autoturret", new main() { name = "Автоматическая турель", amount = 1, items = new List<items> { new items { name = "wood", amount = 1000 }, new items { name = "autoturret", amount = 2 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 }, new items { name = "wood", amount = 1000 } } });

                SaveConfig();
            }
            new PluginTimers(this).Once(60f, () => image());
        }

        private void image()
        {
            if (ImageLibrary != null)
            {
                AddImage("https://files.facepunch.com/helkus/1b0411b1/mgl.png", "multiplegrenadelauncher");
                AddImage("https://rustlabs.com/img/items180/ammo.grenadelauncher.he.png", "ammo.grenadelauncher.he");
                AddImage("https://rustlabs.com/img/items180/ammo.grenadelauncher.smoke.png", "ammo.grenadelauncher.smoke");
                List<string> Keys = new List<string>();
                foreach (var z in recipts)
                {
                    if (z.Value.image != null) AddImage(z.Value.image, z.Value.image);
                    foreach (var x in z.Value.items) if (!Keys.Contains(x.name)) Keys.Add(x.name);
                }
                foreach (var z in Keys) GetImage(z);
            }
            else new PluginTimers(this).Once(60f, () => image());
        }

        [HookMethod("Unload")]
        private void Unload()
        {
            foreach (var z in BasePlayer.activePlayerList) DestroyUI(z);
        }

        private void DestroyUI(BasePlayer player)
        {
            if (_page.Contains(player.UserIDString)) _page.Remove(player.UserIDString);
            CuiHelper.DestroyUi(player, "crafteruigrenade");
            CuiHelper.DestroyUi(player, "crafteruigrenademain");
            CuiHelper.DestroyUi(player, "MiddlePanelcraft");
        }

        [HookMethod("consolecommandexit")]
        private void consolecommandexit(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            DestroyUI(player);
        }

        [HookMethod("chatcommand")]
        private void chatcommand(BasePlayer player, string cmd)
        {
            if (player == null) return;
            if(!permission.UserHasPermission(player.UserIDString, "zetacraft.use"))
            {
                GuiInfo(player, "У вас нет разрешения на доступ к этом меню! Чтобы получить доступ к этом меню, купите его в <color=yellow>Магазине</color>.", 3f);
                return;
            }
            MenuLeftUI(player);
        }

        [HookMethod("consolecommand")]
        private void consolecommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, "zetacraft.use"))
            {
                GuiInfo(player, "У вас нет разрешения на доступ к этом меню! Чтобы получить доступ к этом меню, купите его в <color=yellow>Магазине</color>.", 3f);
                return;
            }
            if (arg.HasArgs())
            {
                int itemId;
                if (int.TryParse(arg.Args[0], out itemId))
                {
                    MenuLeftUI(player, itemId);
                }
                else
                {
                    MenuLeftUI(player);
                }
            }
            else MenuLeftUI(player);
        }

        private void GuiInfo(BasePlayer player, string text, float time = 3f)
        {
            Effect.server.Run("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player.transform.position);
            if (timers.ContainsKey(player.UserIDString))
            {
                Timer ss = timers[player.UserIDString];
                new PluginTimers(this).Destroy(ref ss);
                timers.Remove(player.UserIDString);
            }

            CuiHelper.DestroyUi(player, "MiddlePanelcraft");
            var container = LMUI.CreateElementContainer("MiddlePanelcraft", "0.1 0.1 0.1 0.2", "0 0.6", "1 .7", false, "Overlay", "Assets/Icons/IconMaterial.mat");
            LMUI.CreatePanel(ref container, "MiddlePanelcraft", "0.3 0.3 0.3 0.95", "0 0", "1 1");
            LMUI.OutlineText(ref container, "MiddlePanelcraft", "1 1 1 0.5", text, 20, "0.05 0", "0.95 1", TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, container);
            timers.Add(player.UserIDString, new PluginTimers(this).Once(time, () => CuiHelper.DestroyUi(player, "MiddlePanelcraft")));
        }

        private ItemDefinition FindItem(string itemNameOrId)
        {
            ItemDefinition itemDef = ItemManager.FindItemDefinition(itemNameOrId.ToLower());
            if (itemDef == null)
            {
                int itemId;
                if (int.TryParse(itemNameOrId, out itemId))
                {
                    itemDef = ItemManager.FindItemDefinition(itemId);
                }
            }
            return itemDef;
        }

        private bool haveinventory(PlayerInventory inv, string name, int count)
        {
            List<Item> source2 = new List<Item>();
            if (FindItem(name) != null) source2 = inv.FindItemIDs(FindItem(name).itemid).ToList();
            if (source2.Count > 0)
            {
                int num3 = source2.Sum<Item>((Func<Item, int>)(x => x.amount));
                if (num3 >= count) return true;
            }
            return false;
        }

        private void ItemSpawner(Item item, Vector3 dropPosition)
        {
            ItemContainer container = new ItemContainer();
            container.Insert(item);
            DropUtil.DropItems(container, dropPosition);
        }

        void giveitem(BasePlayer player, string name, int amount)
        {
            ItemDefinition itemdef = FindItem(name);
            if (itemdef != null)
            {
                Item item = ItemManager.Create(FindItem(name));
                item.amount = amount;
                if (!player.inventory.GiveItem(item)) ItemSpawner(item, player.transform.position);
            }
            else
            {
                Command(name.Replace("{steamid}", player.UserIDString));
            }
        }

        private void revokeitem(PlayerInventory inv, string name, int amount)
        {
            List<Item> source2 = inv.FindItemIDs(FindItem(name).itemid).ToList();
            int num6 = 0;
            foreach (Item obj2 in source2)
            {
                int split_Amount = Mathf.Min(amount - num6, obj2.amount);
                (obj2.amount > split_Amount ? obj2.SplitItem(split_Amount) : obj2).DoRemove();
                num6 += split_Amount;
                if (num6 >= amount) break;
            }
        }

        [HookMethod("createitem")]
        private void createitem(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !arg.HasArgs()) return;
            if(!permission.UserHasPermission(player.UserIDString, "zetacraft.use"))
            {
                GuiInfo(player, "У вас нет разрешения на доступ к этом меню! Чтобы получить доступ к этом меню, купите его в <color=yellow>Магазине</color>.", 3f);
                return;
            }
            string name = string.Join(" ", arg.Args.Skip(0));
            if (!recipts.ContainsKey(name))
            {
                GuiInfo(player, "Рецепт не найден!");
                return;
            }
            bool have = true;
            Dictionary<string, int> rcp = new Dictionary<string, int>();
            foreach (var z in recipts[name].items)
            {
                if (rcp.ContainsKey(z.name)) rcp[z.name] += z.amount;
                else rcp.Add(z.name, z.amount);
            }
            foreach (var z in rcp)
            {
                if (!haveinventory(player.inventory, z.Key, z.Value)) have = false;
            }
            if (have)
            {
                foreach (var z in recipts[name].items) revokeitem(player.inventory, z.name, z.amount);
                giveitem(player, name, recipts[name].amount);
                GuiInfo(player, $"Рецепт {recipts[name].name} успешно скрафчен!", 1.5f);
                Effect.server.Run("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", player.transform.position);
            }
            else
            {
                GuiInfo(player, "У вас нет всех предметов нужных для крафта!", 1.5f);
                return;
            }
        }

        private void MenuLeftUI(BasePlayer player, int page = 0)
        {

            if (!_page.Contains(player.UserIDString))
            {
                _page.Add(player.UserIDString);
                if (lastpage.ContainsKey(player.UserIDString)) page = lastpage[player.UserIDString];
                CuiHelper.DestroyUi(player, "crafteruigrenade");
                var container2 = LMUI.CreateElementContainer("crafteruigrenade", "0.005 0.005 0.005 0", "0 0", "1 1", true, "Overlay");
                CuiHelper.AddUi(player, container2);
            }
            else
            {
                if (!lastpage.ContainsKey(player.UserIDString)) lastpage.Add(player.UserIDString, page);
                else lastpage[player.UserIDString] = page;
            }

            CuiHelper.DestroyUi(player, "crafteruigrenademain");
            var container = LMUI.CreateElementContainer("crafteruigrenademain", "0.005 0.005 0.005 0", "0 0", "1 1", true, "Overlay");
            LMUI.CreateButton(ref container, "crafteruigrenademain", "0.005 0.005 0.005 0.9", "", 0, "0 0", "1 1", "craft.grenadeexit");
            float start = 0.98f;
            float xline = 0.1f;
            float end = 0f;
            int i = 0;
            foreach (var z in recipts.Skip(page * 6).Take((page + 1) * 6))
            {
                end = start - 0.29f;
                LMUI.CreatePanel(ref container, "crafteruigrenademain", "0.3 0.3 0.3 0.4", xline + " " + end, (xline + 0.38f) + " " + start);
                LMUI.CreateButton(ref container, "crafteruigrenademain", "0.5 0.5 0.7 0.4", "Скрафтить", 16, (xline + 0.3f) + " " + (start - 0.05f), (xline + 0.38f) + " " + (start - 0.01f), $"craft.recipt {z.Key}");
                LMUI.CreateLabel(ref container, "crafteruigrenademain", "1 1 1 0.9", "<b>" + z.Value.name + "</b>", 19, xline + " " + (start - 0.05f), (xline + 0.3f) + " " + (start - 0.01f), TextAnchor.MiddleCenter);
                LMUI.CreatePanel(ref container, "crafteruigrenademain", "0.3 0.4 0.3 0.4", (xline + 0.15f) + " " + (start - 0.18f), (xline + 0.22f) + " " + (start - 0.07f));
                LMUI.LoadImage(ref container, "crafteruigrenademain", GetImage(z.Value.image == null ? z.Key : z.Value.image), (xline + 0.158f) + " " + (start - 0.17f), (xline + 0.212f) + " " + (start - 0.08f));
                LMUI.CreateLabel(ref container, "crafteruigrenademain", "1 1 1 0.5", "x" + z.Value.amount, 17, (xline + 0.18f) + " " + (start - 0.18f), (xline + 0.22f - 0.001f) + " " + (start - 0.07f), TextAnchor.LowerRight);


                float Xstart = 0.04f;
                float Xend = 0f;
                foreach (var x in z.Value.items)
                {
                    Xend = Xstart + 0.05f;
                    LMUI.CreatePanel(ref container, "crafteruigrenademain", "0.3 0.3 0.3 0.4", (xline + Xstart) + " " + (start - 0.28f), (xline + Xend) + " " + (start - 0.19f));
                    LMUI.LoadImage(ref container, "crafteruigrenademain", GetImage(x.name), (xline + Xstart + 0.005f) + " " + (start - 0.27f), (xline + Xend - 0.005f) + " " + (start - 0.2f));
                    LMUI.CreateLabel(ref container, "crafteruigrenademain", "1 1 1 0.5", "x" + x.amount, 14, (xline + Xstart) + " " + (start - 0.28f), (xline + Xend - 0.001f) + " " + (start - 0.19f), TextAnchor.LowerRight);
                    Xstart += Xend - Xstart + 0.01f;
                }


                if (i == 5) break;
                else if (i == 2)
                {
                    xline = 0.52f;
                    start = 0.98f;
                }
                else start -= start - end + 0.01f;
                i++;
            }
            if (page > 0) LMUI.CreateButton(ref container, "crafteruigrenademain", "0.5 0.5 0.7 0.9", "<<", 16, "0 0.5", "0.02 0.6", $"craft.menu {page - 1}");
            if (recipts.Count() > (page + 1) * 6) LMUI.CreateButton(ref container, "crafteruigrenademain", "0.5 0.5 0.7 0.9", ">>", 16, "0.98 0.5", "1 0.6", $"craft.menu {page + 1}");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region UI
        private class LMUI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false, string parent = "Overlay", string material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", float fadeout = 0f)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color, Material = material},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor,
                        FadeOut = fadeout
                    },
                    new CuiElement().Parent = parent,
                    panelName
                }
            };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false, string material = "Assets/Icons/IconMaterial.mat")
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color, Material = material },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel, CuiHelper.GetGuid());
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleLeft, float fadeIn = 0f, float fadeout = 0f)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadeIn, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    FadeOut = fadeout
                },
                panel, CuiHelper.GetGuid());

            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0f, string material = "assets/content/ui/uibackgroundblur-ingamemenu.mat")
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = fadeIn, Material = material },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel, CuiHelper.GetGuid());
            }
            static public void LoadImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
            static public void OutlineText(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleLeft, string colorout = "0 0 0 1", float fadeIn = 0f, string fadeOut = "140")
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent {Color = color, FontSize = size, Align = align, FadeIn = fadeIn, Text = text },
                        new CuiOutlineComponent { Distance = "0.6 0.6", Color = colorout },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax },
                    }
                });
            }
            static public void CreateInput(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, int chars = 100, TextAnchor align = TextAnchor.UpperLeft)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Color = color,
                            Text = text,
                            FontSize = size,
                            Command = command,
                            CharsLimit = chars,
                            Align = align,
                            IsPassword = false
                        },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
        }
        #endregion
    }
}