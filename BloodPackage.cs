using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;
using Newtonsoft.Json;
using Oxide.Core;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("BloodPackage", "https://topplugin.ru/", "1.0.6")]
    public class BloodPackage : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;
        [PluginReference] private Plugin setfps;

        void OnServerInitialized()
        {
            if (!plugins.Exists("ImageLibrary"))
            {
                PrintError("Плагин ImageLibrary не загружен");
                Interface.Oxide.UnloadPlugin("BloodPackage");
                return;
            }
            ImageLibrary.Call("AddImage", "https://i.imgur.com/hGNLb0C.png", "blood");
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
            if (!setfps)
            {
                PrintWarning("Разработка плагинов - vk.com/rustnastroika");
                return;
            }
            Dictionary<string, string> images = new Dictionary<string, string>();
        }

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Настройка")]
            public krov Blood { get; set; }

            public class krov
            {
                [JsonProperty(PropertyName = "Основные настройки")]
                public bloodkrov nadoconfig { get; set; }

                public class bloodkrov
                {
                    [JsonProperty("Сколько нужно 'пакетиков с кровью' для того, чтобы игрок 'встал'")] public int blood { get; set; }
                    [JsonProperty("Включить тряску экрана после использования 'пакетика с кровью'?")] public bool TryaskaTryaska = false;
                    [JsonProperty("Выпадение 'пакетика крови' с животного?")] public bool enable = true;
                    [JsonProperty("Шанс выпадания 'пакетика крови' с добычи животного (если работает параметр выше)")] public int chance = 10;
                    [JsonProperty("Сколько 'пакетиков с кровью' выпадает при добыче? (если включен параметр выше)")] public int kolvo = 1;

                }
            }
        }
        #endregion

        protected override void LoadConfig()
        {
            base.LoadConfig(); configData = Config.ReadObject<ConfigData>(); Config.WriteObject(configData, true);
        }
        protected override void LoadDefaultConfig()
        {
            configData = GetBaseConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private ConfigData GetBaseConfig() => new ConfigData
        {
            Blood = new ConfigData.krov
            {
                nadoconfig = new ConfigData.krov.bloodkrov
                {
                    blood = 1,
                }
            }
        };

        #region Hooks
        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
            ImageLibrary.Call("GetImage", "blood");
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

        private static List<string> Effects = new List<string>
        {
            "assets/prefabs/tools/jackhammer/effects/strike_screenshake.prefab", "assets/prefabs/weapons/doubleshotgun/effects/attack_shake.prefab", "assets/prefabs/weapons/hatchet/effects/strike_screenshake.prefab", "assets/prefabs/weapons/rock/effects/strike_screenshake.prefab", "assets/prefabs/weapons/smg/effects/attack_shake.prefab", "assets/prefabs/weapons/torch/effects/strike_screenshake.prefab"
        };

        private void Tryaska(BasePlayer player, float amount)
        {
            if (Math.Abs(amount - 0.25f * 100) < 0.5 || player.IsDead())
                return;
            Effect tryska = new Effect(Effects.GetRandom(), player, 0, new Vector3(), new Vector3()); EffectNetwork.Send(tryska, player.Connection); amount += 0.25f; timer.Once(0.25f, () => Tryaska(player, amount));
        }
        void OnPlayerWound(BasePlayer player)
        {
            if (player == null) return;
            DrawUI(player);
        }
        void OnPlayerRespawn(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BloodPackage_UI");
        }
        private void OnPlayerRespawned(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BloodPackage_UI");
        }
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (configData.Blood.nadoconfig.enable)
            {
                if (dispenser.gatherType == ResourceDispenser.GatherType.Flesh)
                {
                    if (Random.Range(0, 100) <= configData.Blood.nadoconfig.chance)
                    {
                        if (dispenser.GetComponent<BaseEntity>().ShortPrefabName == "bear.corpse" || dispenser.GetComponent<BaseEntity>().ShortPrefabName == "wolf.corpse" || dispenser.GetComponent<BaseEntity>().ShortPrefabName == "boar.corpse" || dispenser.GetComponent<BaseEntity>().ShortPrefabName == "player.corpse")
                        {
                            (entity as BasePlayer).inventory.GiveItem(ItemManager.CreateByName("blood", configData.Blood.nadoconfig.kolvo));
                            (entity as BasePlayer).SendConsoleCommand($"note.inv {item.info.itemid} 1 \"Пакетик с кровью\"");
                        }
                    }
                }
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            CuiHelper.DestroyUi(player, "BloodPackage_UI");
        }
        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "BloodPackage_UI");
            }
        }
        #endregion

        #region Commands

        [ConsoleCommand("BloodPackageUI_One")]
        private void CmdHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
            if (!player.IsWounded()) return;

            var itemcount = player.inventory.GetAmount(ItemManager.FindItemDefinition("blood").itemid);
            if (itemcount >= configData.Blood.nadoconfig.blood)
            {
                player.inventory.Take(null, ItemManager.FindItemDefinition("blood").itemid, 1);
            }
            else
            {
                SendReply(player, "У вас недостаточно пакетов с кровью");
                return;
            }

            player.StopWounded();
            CuiHelper.DestroyUi(player, "BloodPackage_UI");
            if (configData.Blood.nadoconfig.TryaskaTryaska)
            {
                Tryaska(player, 0);
            }
            SendReply(player, "Вы <color=#689656>успешно</color> использовали пакет крови");
        }
        #endregion

        #region UI
        void DrawUI(BasePlayer player)
        {
            var itemcount = player.inventory.GetAmount(ItemManager.FindItemDefinition("blood").itemid);
            string Layer = "BloodPackage_UI";
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-264 18", OffsetMax = "-204 78" }
            }, "Overlay", Layer);
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent() {  Png = (string) ImageLibrary.Call("GetImage", "blood"), Color = "0 0 0 0.4", },
                    new CuiRectTransformComponent(){  AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Button = { Color = "0.968627453 0.92451568632 0.882352948 0.03529412", Command = "BloodPackageUI_One", Close = Layer },
                Text = { Text = $"У ВАС: {itemcount}", Align = TextAnchor.MiddleCenter, FontSize = 10 }
            }, Layer);

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}