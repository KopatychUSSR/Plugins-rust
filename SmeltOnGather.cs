using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("SmeltOnGather", "https://topplugin.ru/", "1.0.2")]
    class SmeltOnGather : RustPlugin
    {
        #region Declarations
        const string perm = "smeltongather.use";
        #endregion

        #region Hooks
        void OnServerInitialized()
        {
            LoadData();
            permission.RegisterPermission(perm, this);
        }

        void Unload()
        {
            // Сохраняет только при выгрузке плагина для сохранения записей на диске.
            SaveData();
            //
        }

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            // Потенциальное исправление для редких непризнанных образований.
            if (player == null || item == null)
            {
                return null;
            }
            //

            if (!data.Preferences.TryGetValue(player.userID, out preference))
            {
                return null;
            }

            var cookComponent = item.info.GetComponent<ItemModCookable>();

            // Дополнительная нулевая проверка для обработки древесного угля.
            if (cookComponent == null && preference.CharcoalEnabled && item.info.GetComponent<ItemModBurnable>() != null)
            {
                player.GiveItem(ItemManager.CreateByItemID(-1938052175, item.amount));
                item.Remove(0f);

                return true;
            }
            //

            if (cookComponent == null)
            {
                // Предоставление элемента, отправленного из OnCollectiblePickup, если компонент не был найден.
                if (dispenser == null)
                {
                    player.GiveItem(item);
                }
                //

                return null;
            }

            if (dispenser != null && !preference.OreEnabled)
            {
                return null;
            }

            player.GiveItem(ItemManager.Create(cookComponent.becomeOnCooked, item.amount));
            item.Remove(0f);

            return true;
        }

        object OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            if (!data.Preferences.TryGetValue(player.userID, out preference) || !preference.PickupEnabled)
            {
                return null;
            }

            OnDispenserGather(null, player, item);

            // Крюк возвращается до того, как мировая сущность будет очищена. Это продолжение собственного кода для завершения работы.
            if (entity.pickupEffect.isValid)
            {
                Effect.server.Run(entity.pickupEffect.resourcePath, entity.transform.position);
            }

            entity.itemList = null;
            entity.Kill();
            //

            return true;
        }

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OnDispenserGather(dispenser, player, item);
            return true;
        }
        #endregion

        #region Functions

        #region Commands
        [ChatCommand("smelt")]
        void ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm))
            {
                SendReply(player, $"Unknown command: {command}");
                return;
            }

            if (args == null || args.Length == 0)
            {
                SendReply(player, "<size=20>Автоматическая Плавка</size>\nPickup - Поднимает с земли в переплавленном виде.\nOre - Добывает руду в переплавленном виде.\nCharcoal - Добывает угорь.");
                return;
            }

            if (!data.Preferences.TryGetValue(player.userID, out preference))
            {
                data.Preferences.Add(player.userID, new Preferences());
                preference = data.Preferences[player.userID];
            }

            switch (args[0])
            {
                case "pickup":
                    preference.PickupEnabled = !preference.PickupEnabled;
                    SendReply(player, $"Вы вкл/выкл {(preference.PickupEnabled ? "enabled" : "disabled")} Автопереплавку с земли.");
                    break;

                case "ore":
                    preference.OreEnabled = !preference.OreEnabled;
                    SendReply(player, $"Вы вкл/выкл {(preference.OreEnabled ? "enabled" : "disabled")} Автопереплавку руды.");
                    break;

                case "charcoal":
                    preference.CharcoalEnabled = !preference.CharcoalEnabled;
                    SendReply(player, $"Вы вкл/выкл {(preference.CharcoalEnabled ? "enabled" : "disabled")} Добычу угля.");
                    break;

                default:
                    SendReply(player, $"Запись не распознана: {args[0]}");
                    break;
            }
        }
        #endregion

        #region Data
        Data data;
        Preferences preference;

        class Data
        {
            public Dictionary<ulong, Preferences> Preferences = new Dictionary<ulong, Preferences>();
        }

        class Preferences
        {
            public bool PickupEnabled;
            public bool OreEnabled;
            public bool CharcoalEnabled;
        }

        void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>("SmeltOnGather");
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SmeltOnGather", data);
        }
        #endregion

        #endregion
    }
}