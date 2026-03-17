using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.CaravanExtensionMethods;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using static BaseAIBrain;

namespace Oxide.Plugins
{
    [Info("Caravan", "Adem", "1.1.7")]
    class Caravan : RustPlugin
    {
        #region Variables
        const bool en = false;
        static Caravan ins;
        [PluginReference] Plugin NpcSpawn, GUIAnnouncements, DiscordMessages, Notify, PveMode, Economics, ServerRewards, IQEconomic, DynamicPVP, ArmoredTrain;
        HashSet<string> subscribeMetods = new HashSet<string>
        {
            "OnEntityTakeDamage",
            "OnPlayerDeath",
            "OnEntityDeath",
            "OnEntityKill",
            "OnCorpsePopulate",
            "CanMountEntity",
            "CanDismountEntity",
            "OnHorseLead",
            "CanLootEntity",
            "CanHackCrate",
            "OnLootEntity",
            "OnLootEntityEnd",
            "OnSamSiteTarget",
            "CanEntityBeTargeted",
            "CanEntityTakeDamage",
            "OnCustomNpcTarget",
            "CanPopulateLoot",
            "OnCustomLootContainer",
            "OnCustomLootNPC",
            "SetOwnerPveMode",
            "ClearOwnerPveMode",
            "OnPlayerViolation"
        };
        EventController eventController;
        #endregion Variables

        #region Hooks
        void Init()
        {
            Unsubscribes();
        }

        void OnServerInitialized()
        {
            ins = this;

            if (!NpcSpawnManager.IsNpcSpawnReady())
                return;

            UpdateConfig();
            LoadDefaultMessages();
            GuiManager.LoadImages();
            LootManager.InitialLootManagerUpdate();
            PathManager.StartCachingRouts();
            EventLauncher.AutoStartEvent();
        }

        void Unload()
        {
            EventLauncher.StopEvent(true);
            PathManager.OnPluginUnloaded();
            ins = null;
        }

        object OnEntityTakeDamage(RidableHorse ridableHorse, HitInfo info)
        {
            if (!ridableHorse.IsExists() || info == null || ridableHorse.net == null || info.Initiator == null)
                return null;

            HorseCarriage horseCarriage = HorseCarriage.GetCarriageByHorseNetID(ridableHorse.net.ID.Value);
            Horseman horseman = Horseman.GetHorsemanByNetID(ridableHorse.net.ID.Value);

            if (horseCarriage != null || horseman != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, ridableHorse, shoudSendMessages: true) && !PveModeManager.IsPveModeBlockAction(info.InitiatorPlayer))
                    {
                        eventController.OnEventAttacked(info.InitiatorPlayer);
                        return null;
                    }
                    else
                        return true;
                }
                else
                    return true;
            }

            return null;
        }

        object OnEntityTakeDamage(ModularCar modularCar, HitInfo info)
        {
            if (!modularCar.IsExists() || info == null || modularCar.net == null || info.Initiator == null)
                return null;

            HorseCarriage horseCarriage = HorseCarriage.GetCarriageByModularCarNetID(modularCar.net.ID.Value);

            if (horseCarriage != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, modularCar, shoudSendMessages: true) && !PveModeManager.IsPveModeBlockAction(info.InitiatorPlayer))
                    {
                        eventController.OnEventAttacked(info.InitiatorPlayer);
                        return null;
                    }
                    else
                        return true;
                }
                else
                    return true;
            }

            return null;
        }

        object OnEntityTakeDamage(BaseVehicleModule baseVehicleModule, HitInfo info)
        {
            if (!baseVehicleModule.IsExists() || info == null || baseVehicleModule.net == null || info.Initiator == null)
                return null;

            BaseModularVehicle modularCar = baseVehicleModule.Vehicle;

            if (modularCar == null || modularCar.net == null)
                return null;

            HorseCarriage horseCarriage = HorseCarriage.GetCarriageByModularCarNetID(modularCar.net.ID.Value);

            if (horseCarriage != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, modularCar, shoudSendMessages: true))
                    {
                        eventController.OnEventAttacked(info.InitiatorPlayer);
                        return null;
                    }
                    else
                        return true;
                }
                else
                    return true;
            }

            return null;
        }

        object OnEntityTakeDamage(HotAirBalloon hotAirBalloon, HitInfo info)
        {
            if (!hotAirBalloon.IsExists() || info == null || hotAirBalloon.net == null || info.Initiator == null)
                return null;

            AirBalloon airBalloon = AirBalloon.GetAirBalloonByNetID(hotAirBalloon.net.ID.Value);

            if (airBalloon != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, hotAirBalloon, shoudSendMessages: true) && !PveModeManager.IsPveModeBlockAction(info.InitiatorPlayer))
                    {
                        eventController.OnEventAttacked(info.InitiatorPlayer);
                        return null;
                    }
                    else
                        return true;
                }

                SamSite samSite = info.Initiator as SamSite;

                if (samSite != null)
                {
                    if (samSite.ShortPrefabName == "sam_static")
                        return true;
                    else if (ins._config.damageConfig.isPlayerSamSiteCanAttackEvent)
                        return null;
                }

                return true;
            }

            return null;
        }

        object OnEntityTakeDamage(ScientistNPC scientistNPC, HitInfo info)
        {
            if (!scientistNPC.IsExists() || info == null || scientistNPC.net == null || !info.InitiatorPlayer.IsRealPlayer())
                return null;

            if (NpcSpawnManager.GetScientistByNetId(scientistNPC.net.ID.Value) == null)
                return null;

            if (info.InitiatorPlayer.IsRealPlayer())
            {
                if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, scientistNPC, shoudSendMessages: true))
                {
                    if (scientistNPC.isMounted)
                        info.damageTypes.ScaleAll(10);

                    eventController.OnEventAttacked(info.InitiatorPlayer);
                    return null;
                }
                else
                    return true;
            }

            return null;
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null)
                return;

            if (ZoneController.IsPlayerInZone(player.userID))
                ZoneController.OnPlayerLeaveZone(player);
        }

        void OnEntityDeath(ScientistNPC scientistNPC, HitInfo info)
        {
            if (scientistNPC == null || info == null || scientistNPC.net == null)
                return;

            if (info.InitiatorPlayer.IsRealPlayer() && NpcSpawnManager.IsEventNpc(scientistNPC))
            {
                Horseman horseman = Horseman.GetHorsemanByNpcNetId(scientistNPC.net.ID.Value);

                if (horseman != null)
                    EconomyManager.AddBalance(info.InitiatorPlayer.userID, _config.economicsConfig.horsemanPoint);
                else
                    EconomyManager.AddBalance(info.InitiatorPlayer.userID, _config.economicsConfig.npcPoint);
            }
        }

        void OnEntityDeath(RidableHorse ridableHorse, HitInfo info)
        {
            if (ridableHorse == null || info == null || ridableHorse.net == null)
                return;

            if (info.InitiatorPlayer.IsRealPlayer())
            {
                Horseman horseman = Horseman.GetHorsemanByHorseNetId(ridableHorse.net.ID.Value);

                if (horseman != null)
                    EconomyManager.AddBalance(info.InitiatorPlayer.userID, _config.economicsConfig.horsemanPoint);
            }
        }

        void OnEntityKill(RidableHorse ridableHorse)
        {
            if (!ridableHorse.IsExists() || ridableHorse.net == null)
                return;

            HorseCarriage horseCarriage = HorseCarriage.GetCarriageByHorseNetID(ridableHorse.net.ID.Value);

            if (horseCarriage != null)
                ins.NextTick(() => eventController.EventPassingCheck());
        }

        void OnCorpsePopulate(ScientistNPC scientistNPC, NPCPlayerCorpse corpse)
        {
            if (scientistNPC == null || corpse == null)
                return;

            NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName);

            if (npcConfig != null)
            {
                ins.NextTick(() =>
                {
                    if (corpse == null)
                        return;

                    if (!corpse.containers.IsNullOrEmpty() && corpse.containers[0] != null)
                        LootManager.UpdateItemContainer(corpse.containers[0], npcConfig.lootTableConfig, npcConfig.lootTableConfig.clearDefaultItemList);

                    if (npcConfig.deleteCorpse && !corpse.IsDestroyed)
                        corpse.Kill();
                });
            }
        }

        object CanMountEntity(BasePlayer player, BaseMountable baseMountable)
        {
            if (!player.IsRealPlayer() || baseMountable == null)
                return null;

            BaseVehicle baseVehicle = baseMountable.VehicleParent();

            if (!baseVehicle.IsExists() || baseVehicle.net == null)
                return null;

            if (baseVehicle is RidableHorse)
            {
                Horseman horseman = Horseman.GetHorsemanByNetID(baseVehicle.net.ID.Value);
                HorseCarriage horseCarriage = HorseCarriage.GetCarriageByHorseNetID(baseVehicle.net.ID.Value);

                if (horseCarriage != null || horseman != null)
                    return true;
            }

            return null;
        }

        object CanDismountEntity(BasePlayer player, BaseMountable baseMountable)
        {
            if (baseMountable == null || player == null || player.userID.IsSteamId())
                return null;

            BaseVehicle baseVehicle = baseMountable.VehicleParent();

            if (!baseVehicle.IsExists() || baseVehicle.net == null)
                return null;

            if (baseVehicle is RidableHorse)
            {
                Horseman horseman = Horseman.GetHorsemanByNetID(baseVehicle.net.ID.Value);

                if (horseman != null)
                    return true;
            }

            return null;
        }

        object OnHorseLead(RidableHorse ridableHorse, BasePlayer player)
        {
            if (!ridableHorse.IsExists() || ridableHorse.net == null)
                return null;

            HorseCarriage horseCarriage = HorseCarriage.GetCarriageByHorseNetID(ridableHorse.net.ID.Value);
            Horseman horseman = Horseman.GetHorsemanByNetID(ridableHorse.net.ID.Value);

            if (horseCarriage != null || horseman != null)
                return true;

            return null;
        }

        object CanLootEntity(BasePlayer player, LootContainer lootContainer)
        {
            if (player == null || lootContainer == null || lootContainer.net == null)
                return null;

            if (LootManager.GetContainerDataByNetId(lootContainer.net.ID.Value) != null)
            {
                if (!ins.eventController.IsPlayerCanLootCaravan(player, true))
                {
                    eventController.MakeCaravanAgressive();
                    return true;
                }
            }

            return null;
        }

        object CanLootEntity(BasePlayer player, StorageContainer storageContainer)
        {
            if (player == null || storageContainer == null || storageContainer.net == null)
                return null;

            if (LootManager.GetContainerDataByNetId(storageContainer.net.ID.Value) != null)
            {
                if (!ins.eventController.IsPlayerCanLootCaravan(player, true))
                {
                    eventController.MakeCaravanAgressive();
                    return true;
                }
            }

            return null;
        }

        object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null || crate.net == null)
                return null;

            StorageContainerData storageContainerData = LootManager.GetContainerDataByNetId(crate.net.ID.Value);

            if (storageContainerData != null)
            {
                if (!ins.eventController.IsPlayerCanLootCaravan(player, true))
                {
                    eventController.MakeCaravanAgressive();
                    return true;
                }

                LootManager.UpdateCrateHackTime(crate, storageContainerData.presetName);
            }

            return null;
        }

        void OnLootEntity(BasePlayer player, LootContainer lootContainer)
        {
            if (player == null || lootContainer == null || lootContainer.net == null)
                return;

            if (LootManager.IsEventCrate(lootContainer.net.ID.Value))
            {
                LootManager.OnEventCrateLooted(lootContainer, player.userID);
                eventController.EventPassingCheck();
            }
        }

        void OnLootEntity(BasePlayer player, StorageContainer storageContainer)
        {
            if (player == null || storageContainer == null || storageContainer.net == null)
                return;

            if (LootManager.IsEventCrate(storageContainer.net.ID.Value))
            {
                LootManager.OnEventCrateLooted(storageContainer, player.userID);
                eventController.EventPassingCheck();
            }
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer storageContainer)
        {
            if (storageContainer == null || storageContainer.net == null || !player.IsRealPlayer())
                return;

            if (LootManager.GetContainerDataByNetId(storageContainer.net.ID.Value) != null)
                if (storageContainer.inventory.IsEmpty())
                    storageContainer.Kill();
        }

        object OnSamSiteTarget(SamSite samSite, HotAirBalloon hotAirBalloon)
        {
            if (samSite == null || hotAirBalloon == null || hotAirBalloon.net == null)
                return null;

            AirBalloon airBalloon = AirBalloon.GetAirBalloonByNetID(hotAirBalloon.net.ID.Value);

            if (airBalloon != null)
            {
                if (samSite.ShortPrefabName == "sam_static")
                    return true;
                else if (!ins._config.damageConfig.isPlayerSamSiteCanAttackEvent)
                    return true;
            }
            return null;
        }

        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (player == null)
                return null;

            if (type == AntiHackType.FlyHack && eventController.IsCaravanLooted())
                return true;

            return null;
        }
        #region OtherPlugins
        object CanEntityBeTargeted(HotAirBalloon hotAirBalloon, SamSite samSite)
        {
            if (samSite == null || hotAirBalloon == null || hotAirBalloon.net == null)
                return null;

            AirBalloon airBalloon = AirBalloon.GetAirBalloonByNetID(hotAirBalloon.net.ID.Value);

            if (airBalloon != null)
            {
                if (samSite.ShortPrefabName == "sam_static")
                    return false;
                else if (ins._config.damageConfig.isPlayerSamSiteCanAttackEvent)
                    return true;
                else
                    return false;
            }
            return null;
        }

        object CanEntityTakeDamage(RidableHorse ridableHorse, HitInfo info)
        {
            if (eventController == null || info == null || !ridableHorse.IsExists() || ridableHorse.net == null || info.Initiator == null)
                return null;

            HorseCarriage horseCarriage = HorseCarriage.GetCarriageByHorseNetID(ridableHorse.net.ID.Value);
            Horseman horseman = Horseman.GetHorsemanByNetID(ridableHorse.net.ID.Value);

            if (horseCarriage != null || horseman != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, ridableHorse, shoudSendMessages: true) && !PveModeManager.IsPveModeBlockAction(info.InitiatorPlayer))
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }

            return null;
        }

        object CanEntityTakeDamage(ModularCar modularCar, HitInfo info)
        {
            if (eventController == null || info == null || !modularCar.IsExists() || modularCar.net == null || info.Initiator == null)
                return null;

            HorseCarriage horseCarriage = HorseCarriage.GetCarriageByModularCarNetID(modularCar.net.ID.Value);

            if (horseCarriage != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, modularCar, shoudSendMessages: true) && !PveModeManager.IsPveModeBlockAction(info.InitiatorPlayer))
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }

            return null;
        }

        object CanEntityTakeDamage(BaseVehicleModule baseVehicleModule, HitInfo info)
        {
            if (eventController == null || info == null || !baseVehicleModule.IsExists() || baseVehicleModule.net == null || info.Initiator == null)
                return null;

            BaseModularVehicle modularCar = baseVehicleModule.Vehicle;

            if (modularCar == null || modularCar.net == null)
                return null;

            HorseCarriage horseCarriage = HorseCarriage.GetCarriageByModularCarNetID(modularCar.net.ID.Value);

            if (horseCarriage != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, modularCar, shoudSendMessages: true) && !PveModeManager.IsPveModeBlockAction(info.InitiatorPlayer))
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }

            return null;
        }

        object CanEntityTakeDamage(HotAirBalloon hotAirBalloon, HitInfo info)
        {
            if (eventController == null || info == null || !hotAirBalloon.IsExists() || hotAirBalloon.net == null || info.Initiator == null)
                return null;

            AirBalloon airBalloon = AirBalloon.GetAirBalloonByNetID(hotAirBalloon.net.ID.Value);

            if (airBalloon != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, hotAirBalloon, shoudSendMessages: true) && !PveModeManager.IsPveModeBlockAction(info.InitiatorPlayer))
                        return true;
                    else
                        return false;
                }

                SamSite samSite = info.Initiator as SamSite;

                if (samSite != null)
                {
                    if (samSite.ShortPrefabName == "sam_static")
                        return false;
                    else if (ins._config.damageConfig.isPlayerSamSiteCanAttackEvent)
                        return true;
                }
                else
                    return false;
            }

            return null;
        }

        object CanEntityTakeDamage(ScientistNPC scientistNPC, HitInfo info)
        {
            if (eventController == null || info == null || !scientistNPC.IsExists() || scientistNPC.net == null || info.Initiator == null)
                return null;

            NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName);

            if (npcConfig != null)
            {
                if (info.InitiatorPlayer.IsRealPlayer())
                {
                    if (eventController.IsPlayerCanDealDamage(info.InitiatorPlayer, scientistNPC, shoudSendMessages: true))
                        return null;
                    else
                        return false;
                }
            }

            return null;
        }

        object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (eventController == null || hitinfo == null || !victim.IsRealPlayer())
                return null;

            if (_config.zoneConfig.isPVPZone && !_config.supportedPluginsConfig.pveMode.enable)
            {
                if (hitinfo.InitiatorPlayer.IsRealPlayer() && ZoneController.IsPlayerInZone(hitinfo.InitiatorPlayer.userID) && ZoneController.IsPlayerInZone(victim.userID))
                    return true;
            }

            return null;
        }

        object OnCustomNpcTarget(ScientistNPC scientistNPC, BasePlayer player)
        {
            if (scientistNPC == null)
                return null;

            if (ins._config.behaviorConfig.agressiveTime < 0)
                return null;

            NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName);

            if (npcConfig != null)
            {
                if (eventController.IsCaravanAgressive())
                    return null;
                else
                    return false;
            }

            return null;
        }

        object CanPopulateLoot(LootContainer lootContainer)
        {
            if (eventController == null || lootContainer == null || lootContainer.net == null)
                return null;

            StorageContainerData storageContainerData = LootManager.GetContainerDataByNetId(lootContainer.net.ID.Value);

            if (storageContainerData != null)
            {
                CrateConfig crateConfig = LootManager.GetCrateConfigByPresetName(storageContainerData.presetName);

                if (crateConfig != null && !crateConfig.lootTableConfig.isAlphaLoot)
                    return true;
            }

            return null;
        }

        object CanPopulateLoot(ScientistNPC scientistNPC, NPCPlayerCorpse corpse)
        {
            if (eventController == null || scientistNPC == null || scientistNPC.net == null)
                return null;

            NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName);

            if (npcConfig != null && !npcConfig.lootTableConfig.isAlphaLoot)
                return true;

            return null;
        }

        object OnCustomLootContainer(NetworkableId netID)
        {
            if (eventController == null || netID == null)
                return null;

            StorageContainerData storageContainerData = LootManager.GetContainerDataByNetId(netID.Value);

            if (storageContainerData != null)
            {
                CrateConfig crateConfig = LootManager.GetCrateConfigByPresetName(storageContainerData.presetName);

                if (crateConfig != null && !crateConfig.lootTableConfig.isCustomLoot)
                    return true;
            }

            return null;
        }

        object OnCustomLootNPC(NetworkableId netID)
        {
            if (eventController == null || netID == null)
                return null;

            ScientistNPC scientistNPC = NpcSpawnManager.GetScientistByNetId(netID.Value);

            if (scientistNPC != null)
            {
                NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName);

                if (npcConfig != null && !npcConfig.lootTableConfig.isCustomLoot)
                    return true;
            }

            return null;
        }

        void SetOwnerPveMode(string shortname, BasePlayer player)
        {
            if (eventController == null || string.IsNullOrEmpty(shortname) || shortname != Name || !player.IsRealPlayer())
                return;

            if (shortname == Name)
                PveModeManager.OnNewOwnerSet(player);
        }

        void ClearOwnerPveMode(string shortname)
        {
            if (eventController == null || string.IsNullOrEmpty(shortname))
                return;

            if (shortname == Name)
                PveModeManager.OnOwnerDeleted();

        }
        #endregion OtherPlugins
        #endregion Hooks

        #region Commands
        [ChatCommand("caravanstart")]
        void ChatStartEventCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            if (arg == null || arg.Length == 0)
                EventLauncher.DelayStartEvent(false, player);
            else
            {
                string eventPresetName = arg[0];
                EventLauncher.DelayStartEvent(false, player, eventPresetName);
            }
        }
        [ConsoleCommand("caravanstart")]
        void ConsoleStartEventCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            if (arg == null || arg.Args == null || arg.Args.Length == 0)
            {
                EventLauncher.DelayStartEvent();
            }
            else
            {
                string eventPresetName = arg.Args[0];
                EventLauncher.DelayStartEvent(presetName: eventPresetName);
            }
        }

        [ChatCommand("caravanstop")]
        void ChatStopEventCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            EventLauncher.StopEvent();
        }

        [ConsoleCommand("caravanstop")]
        void ConsoleStopEventCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            EventLauncher.StopEvent();
        }

        [ChatCommand("caravanpathstart")]
        void ChatPathStartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            PathRecorder.StartRecordingRoute(player);
        }

        [ChatCommand("caravanpathsave")]
        void ChatPathSaveCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            if (arg.Length == 0)
            {
                NotifyManager.SendMessageToPlayer(player, "CustomRouteDescription", ins._config.notifyConfig.prefix);
                return;
            }

            string pathName = arg[0];
            PathRecorder.TrySaveRoute(player.userID, pathName);
        }

        [ChatCommand("caravanpathcancel")]
        void ChatPathCancelCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            PathRecorder.TryCancelRoute(player.userID);
        }

        [ChatCommand("showroute")]
        void ChatShowRouteCommand(BasePlayer player, string command, string[] arg)
        {
            PathManager.DdrawPath(PathManager.currentPath, player);
        }

        [ConsoleCommand("savecaravancart")]
        void ConsoleSaveCustomWagonCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            string wagonShortPrefabName = arg.Args[0];
            CarCustomizator.MapSaver.CreateOrAddNewWagonToData(wagonShortPrefabName);
        }

        [ChatCommand("caravanroadblock")]
        void ChatRoadBlockCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            PathList blockRoad = TerrainMeta.Path.Roads.FirstOrDefault(x => x.Path.Points.Any(y => Vector3.Distance(player.transform.position, y) < 10));

            if (blockRoad == null)
            {
                NotifyManager.SendMessageToPlayer(player, $"{_config.notifyConfig.prefix} Road <color=#ce3f27>not found</color>. Step onto the required road and enter the command again.");
                return;
            }

            int index = TerrainMeta.Path.Roads.IndexOf(blockRoad);

            if (_config.pathConfig.blockRoads.Contains(index))
            {
                NotifyManager.SendMessageToPlayer(player, $"{_config.notifyConfig.prefix} The road is already <color=#ce3f27>blocked</color>");
                return;
            }

            _config.pathConfig.blockRoads.Add(index);
            SaveConfig();

            NotifyManager.SendMessageToPlayer(player, $"{_config.notifyConfig.prefix} The road with the index <color=#738d43>{index}</color> is <color=#ce3f27>blocked</color>");
        }

        [ChatCommand("caravantest")]
        void ChatTestkCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            BasicCar baseEntity = BuildManager.SpawnRegularEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", player.transform.position, Quaternion.identity) as BasicCar;

            foreach (TriggerBase triggerBase in baseEntity.gameObject.GetComponentsInChildren<TriggerBase>())
                Debug(triggerBase.GetType());

            baseEntity.rigidBody.detectCollisions = false;
        }
        #endregion Commands

        #region Methods
        void UpdateConfig()
        {
            if (IsFrontierMap())
            {
                List<string> frontierMapRoutesNames = new List<string>
                {
                    "Frontier_1",
                    "Frontier_2",
                    "Frontier_3",
                    "Frontier_4"
                };

                if (_config.pathConfig.pathType != 2)
                {
                    PrintError("There are no roads on this map! The plugin can only work with custom routes!\n1. Upload the data files that come with the map to the oxide/data/Caravan/Custom routes folder \n2. Change 'Type of caravan routes' to custom in config");
                    ins.NextTick(() => ins.Server.Command($"o.unload {ins.Name}"));
                }
                else if (!_config.pathConfig.customPathConfig.customRoutesPresets.Any(x => x.Contains("Frontier")))
                    foreach (string name in frontierMapRoutesNames)
                        if (!_config.pathConfig.customPathConfig.customRoutesPresets.Contains(name))
                            _config.pathConfig.customPathConfig.customRoutesPresets.Add(name);
            }

            if (_config.version != Version)
            {
                if (_config.version.Minor == 0)
                {
                    if (_config.version.Patch <= 1)
                    {
                        _config.supportedPluginsConfig.pveMode.scaleDamage = new Dictionary<string, float>
                        {
                            ["Npc"] = 1f,
                        };
                    }
                    if (_config.version.Patch <= 6)
                    {
                        _config.supportedPluginsConfig.pveMode.showEventOwnerNameOnMap = true;
                    }
                    _config.version = new VersionNumber(1, 1, 0);
                }

                if (_config.version.Minor == 1)
                {
                    if (_config.version.Patch == 0)
                    {
                        _config.zoneConfig.isColoredBorder = false;
                        _config.zoneConfig.brightness = 5;
                        _config.zoneConfig.borderColor = 2;

                        _config.notifyConfig.redefinedMessages = new HashSet<RedefinedMessageConfig>
                        {
                            new RedefinedMessageConfig
                            {
                                langKey = "Replace it with the required lang key",
                                isEnable = true,
                                chatConfig = new ChatConfig
                                {
                                    isEnabled = false,
                                },
                                gameTipConfig = new GameTipConfig
                                {
                                    isEnabled = true,
                                    style = 2,
                                },
                                guiAnnouncementsConfig = new GUIAnnouncementsConfig
                                {
                                    isEnabled = false,
                                    bannerColor = "Grey",
                                    textColor = "White",
                                    apiAdjustVPosition = 0.03f
                                },
                                notifyPluginConfig = new NotifyPluginConfig
                                {
                                    isEnabled = false,
                                    type = 0
                                },
                            }
                        };
                    }
                }

                _config.version = Version;
            }

            UpdateLootTables();
            SaveConfig();
        }

        bool IsFrontierMap()
        {
            return BaseNetworkable.serverEntities.OfType<RANDSwitch>().Any(x => x != null && x.transform != null && x.transform.position.x + x.transform.position.y + x.transform.position.z == 557);
        }

        void UpdateLootTables()
        {
            foreach (CrateConfig crateConfig in _config.crateConfigs)
                UpdateBaseLootTable(crateConfig.lootTableConfig);
        }

        void UpdateBaseLootTable(BaseLootTableConfig baseLootTableConfig)
        {
            for (int i = 0; i < baseLootTableConfig.randomItemsConfig.items.Count; i++)
            {
                LootItemConfig lootItemConfig = baseLootTableConfig.randomItemsConfig.items[i];

                if (lootItemConfig.chance <= 0)
                    baseLootTableConfig.randomItemsConfig.items.RemoveAt(i);
            }

            baseLootTableConfig.randomItemsConfig.items = baseLootTableConfig.randomItemsConfig.items.OrderByQuickSort(x => x.chance);

            if (baseLootTableConfig.randomItemsConfig.maxItemsAmount > baseLootTableConfig.randomItemsConfig.items.Count)
                baseLootTableConfig.randomItemsConfig.maxItemsAmount = baseLootTableConfig.randomItemsConfig.items.Count;

            if (baseLootTableConfig.randomItemsConfig.minItemsAmount > baseLootTableConfig.randomItemsConfig.maxItemsAmount)
                baseLootTableConfig.randomItemsConfig.minItemsAmount = baseLootTableConfig.randomItemsConfig.maxItemsAmount;
        }

        void Unsubscribes()
        {
            foreach (string hook in subscribeMetods)
                Unsubscribe(hook);
        }

        void Subscribes()
        {
            foreach (string hook in subscribeMetods)
                Subscribe(hook);
        }

        static void Debug(params object[] arg)
        {
            string result = "";

            foreach (object obj in arg)
                if (obj != null)
                    result += obj.ToString() + " ";

            ins.Puts(result);
        }
        #endregion Methods  

        #region Classes
        static class EventLauncher
        {
            static Coroutine autoEventCoroutine;
            static Coroutine delayedEventStartCorountine;

            internal static bool IsEventActive()
            {
                return ins != null && ins.eventController != null;
            }

            internal static void AutoStartEvent()
            {
                if (!ins._config.mainConfig.isAutoEvent)
                    return;

                if (autoEventCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(autoEventCoroutine);

                autoEventCoroutine = ServerMgr.Instance.StartCoroutine(AutoEventCorountine());
            }

            internal static void DelayStartEvent(bool isAutoActivated = false, BasePlayer activator = null, string presetName = "")
            {
                if (IsEventActive() || delayedEventStartCorountine != null)
                {
                    NotifyManager.PrintError(activator, "EventActive_Exeption");
                    return;
                }

                if (autoEventCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(autoEventCoroutine);

                EventConfig eventConfig = DefineEventConfig(presetName);
                if (eventConfig == null)
                {
                    NotifyManager.PrintError(activator, "ConfigurationNotFound_Exeption");
                    StopEvent();
                    return;
                }

                PathManager.GenerateNewPath();

                if (PathManager.currentPath == null)
                    return;

                delayedEventStartCorountine = ServerMgr.Instance.StartCoroutine(DelayedStartEventCorountine(eventConfig));

                if (!isAutoActivated)
                    NotifyManager.PrintInfoMessage(activator, "SuccessfullyLaunched");
            }

            static IEnumerator AutoEventCorountine()
            {
                yield return CoroutineEx.waitForSeconds(UnityEngine.Random.Range(ins._config.mainConfig.minTimeBetweenEvents, ins._config.mainConfig.maxTimeBetweenEvents));
                DelayStartEvent(true);
            }

            static IEnumerator DelayedStartEventCorountine(EventConfig eventConfig)
            {
                if (ins._config.mainConfig.preStartTime > 0)
                    NotifyManager.SendMessageToAll("PreStart", ins._config.notifyConfig.prefix, ins._config.mainConfig.preStartTime);

                yield return CoroutineEx.waitForSeconds(ins._config.mainConfig.preStartTime);

                if (PathManager.currentPath != null)
                    StartEvent(eventConfig);
                else
                    StopEvent();
            }

            static void StartEvent(EventConfig eventConfig)
            {
                GameObject gameObject = new GameObject();
                ins.eventController = gameObject.AddComponent<EventController>();
                ins.eventController.Init(eventConfig);

                if (ins._config.mainConfig.enableStartStopLogs)
                    NotifyManager.PrintLogMessage("EventStart_Log", eventConfig.presetName);

                Interface.CallHook("OnCaravanStart");
            }

            static EventConfig DefineEventConfig(string eventPresetName = "")
            {
                if (eventPresetName != "")
                    return ins._config.eventConfigs.FirstOrDefault(x => x.presetName == eventPresetName);

                HashSet<EventConfig> suitableEventConfigs = ins._config.eventConfigs.Where(x => x.chance > 0 && x.isAutoStart && IsEventConfigSuitableByTime(x));

                if (suitableEventConfigs == null || suitableEventConfigs.Count == 0)
                    return null;

                float sumChance = 0;
                foreach (EventConfig eventConfig in suitableEventConfigs)
                    sumChance += eventConfig.chance;

                float random = UnityEngine.Random.Range(0, sumChance);

                foreach (EventConfig eventConfig in suitableEventConfigs)
                {
                    random -= eventConfig.chance;

                    if (random <= 0)
                        return eventConfig;
                }

                return null;
            }

            static bool IsEventConfigSuitableByTime(EventConfig eventConfig)
            {
                if (eventConfig.minTimeAfterWipe <= 0 && eventConfig.maxTimeAfterWipe <= 0)
                    return true;

                int timeScienceWipe = GetTimeScienceLastWipe();

                if (timeScienceWipe < eventConfig.minTimeAfterWipe)
                    return false;
                if (eventConfig.maxTimeAfterWipe > 0 && timeScienceWipe > eventConfig.maxTimeAfterWipe)
                    return false;

                return true;
            }

            static int GetTimeScienceLastWipe()
            {
                DateTime startTime = new DateTime(2019, 1, 1, 0, 0, 0);

                double realTime = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                double wipeTime = SaveRestore.SaveCreatedTime.Subtract(startTime).TotalSeconds;

                return Convert.ToInt32(realTime - wipeTime);
            }

            internal static void StopEvent(bool isPluginUnloading = false)
            {
                if (IsEventActive())
                {
                    ins.Unsubscribes();
                    ins.eventController.DeleteController();

                    Horseman.KillAllHorseman();
                    HorseCarriage.KillAllCarriages();
                    CaravanNpc.KillAllNpcs();
                    AirBalloon.KillAllBalloons();
                    HorseBrain.ClearData();

                    ZoneController.TryDeleteZone();
                    EventMapMarker.DeleteMapMarker();
                    NpcSpawnManager.ClearData();
                    PveModeManager.OnEventEnd();
                    GuiManager.DestroyAllGui();
                    LootManager.ClearLootData();
                    EconomyManager.OnEventEnd();
                    NotifyManager.SendMessageToAll("Finish", ins._config.notifyConfig.prefix);
                    Interface.CallHook("OnCaravanStop");

                    if (ins._config.mainConfig.enableStartStopLogs)
                        NotifyManager.PrintLogMessage("EventStop_Log");

                    if (!isPluginUnloading)
                        AutoStartEvent();
                }

                if (delayedEventStartCorountine != null)
                {
                    ServerMgr.Instance.StopCoroutine(delayedEventStartCorountine);
                    delayedEventStartCorountine = null;
                }
            }
        }

        class EventController : FacepunchBehaviour
        {
            internal EventConfig eventConfig;
            Coroutine spawnCorountine;
            Coroutine eventCorountine;
            internal List<CaravanHorse> caravanHorses = new List<CaravanHorse>();
            int eventTime;
            int stopTime;
            int agressiveTime;
            bool isEventLooted;

            internal int GetEventTime()
            {
                return eventTime;
            }

            internal Vector3 GetEventPosition()
            {
                int counter = 0;
                Vector3 resultPositon = Vector3.zero;

                foreach (CaravanHorse caravanHorse in caravanHorses)
                {
                    if (caravanHorse != null && caravanHorse.ridableHorse.IsExists())
                    {
                        resultPositon += caravanHorse.transform.position;
                        counter++;
                    }
                }

                return resultPositon / counter;
            }

            internal int GetEventGuardsCount()
            {
                int result = 0;
                foreach (CaravanHorse caravanHorse in caravanHorses)
                {
                    if (caravanHorse != null)
                    {
                        HorseBrain horseBrain = caravanHorse.GetBrain();

                        if (horseBrain != null)
                            result += horseBrain.GetGuardNpcCount();
                    }
                }
                return result;
            }

            internal bool IsCaravanAgressive()
            {
                return ins._config.behaviorConfig.agressiveTime < 0 || agressiveTime > 0;
            }

            internal bool IsStopped()
            {
                return stopTime > 0 || isEventLooted;
            }

            internal bool IsCaravanLooted()
            {
                return isEventLooted;
            }

            internal bool IsPlayerCanDealDamage(BasePlayer player, BaseEntity caravanEntity, bool shoudSendMessages)
            {
                if (spawnCorountine != null)
                    return false;

                if (eventConfig.maxGroundDamageDistance > 0)
                {
                    Vector3 playerGroundPosition = new Vector3(player.transform.position.x, 0, player.transform.position.z);
                    Vector3 entityGroundPosition = new Vector3(caravanEntity.transform.position.x, 0, caravanEntity.transform.position.z);
                    float distance = Vector3.Distance(playerGroundPosition, entityGroundPosition);

                    float distanceMultiplicator = 1;

                    if (caravanEntity is HotAirBalloon)
                        distanceMultiplicator = 1.5f;
                    else
                    {
                        BaseEntity parentEntity = caravanEntity.GetParentEntity();

                        if (parentEntity != null && parentEntity is HotAirBalloon)
                            distanceMultiplicator = 1.5f;
                    }

                    if (distance > eventConfig.maxGroundDamageDistance * distanceMultiplicator)
                    {
                        if (shoudSendMessages)
                            NotifyManager.SendMessageToPlayer(player, "DamageDistance", ins._config.notifyConfig.prefix);

                        return false;
                    }
                }

                if (PveModeManager.IsPveModeBlockInterract(player))
                {
                    NotifyManager.SendMessageToPlayer(player, "PveMode_BlockAction", ins._config.notifyConfig.prefix);
                    return false;
                }

                return true;
            }

            internal bool IsPlayerCanLootCaravan(BasePlayer player, bool shoudSendMessages)
            {
                if (IsLootBlockedByThisPlugin())
                {
                    NotifyManager.SendMessageToPlayer(player, "CantLoot", ins._config.notifyConfig.prefix);
                    return false;
                }

                if (PveModeManager.IsPveModeBlockInterract(player))
                {
                    if (shoudSendMessages)
                        NotifyManager.SendMessageToPlayer(player, "PveMode_BlockAction", ins._config.notifyConfig.prefix);

                    return false;
                }

                if (PveModeManager.IsPveModeBlockLooting(player))
                {
                    if (shoudSendMessages)
                        NotifyManager.SendMessageToPlayer(player, "PveMode_YouAreNoOwner", ins._config.notifyConfig.prefix);

                    return false;
                }

                return true;
            }

            bool IsLootBlockedByThisPlugin()
            {
                if (spawnCorountine != null)
                    return true;

                if (agressiveTime <= 0 && ins._config.behaviorConfig.agressiveTime > 0)
                    return true;

                if (ins._config.lootConfig.blockLootingByMove && !IsStopped())
                    return true;

                if (ins._config.lootConfig.blockLootingByNpcs && GetEventGuardsCount() > 0)
                    return true;

                return false;
            }

            internal bool IsPlayerCanStopEvent(BasePlayer player, bool shoudSendMessages)
            {
                if (PveModeManager.IsPveModeBlockInterract(player))
                {
                    NotifyManager.SendMessageToPlayer(player, "PveMode_BlockAction", ins._config.notifyConfig.prefix);
                    return false;
                }

                return true;
            }

            internal void OnEventAttacked(BasePlayer player)
            {
                MakeCaravanAgressive();

                if (!IsPlayerCanStopEvent(player, true))
                    return;

                if ((agressiveTime <= 0 && ins._config.behaviorConfig.agressiveTime > 0) || stopTime <= 0)
                    NotifyManager.SendMessageToAll("CaravanAttacked", ins._config.notifyConfig.prefix, player.displayName, eventConfig.displayName.ToLower());

                StopMoving(player);
            }

            internal void MakeCaravanAgressive()
            {
                agressiveTime = ins._config.behaviorConfig.agressiveTime;
            }

            internal void EventPassingCheck()
            {
                if (isEventLooted)
                    return;

                LootManager.UpdateCountOfUnlootedCrates();
                int countOfUnlootedCrates = LootManager.GetCountOfUnlootedCrates();

                if (countOfUnlootedCrates == 0)
                {
                    isEventLooted = true;
                    StopMoving(null);

                    if (eventTime > ins._config.mainConfig.endAfterDeathTime)
                        eventTime = ins._config.mainConfig.endAfterDeathTime;

                    NotifyManager.SendMessageToAll("Looted", ins._config.notifyConfig.prefix, eventConfig.displayName);
                }
            }

            internal void OnCaravanRotated()
            {
                foreach (CaravanHorse caravanHorse in caravanHorses)
                {
                    if (caravanHorse == null)
                        continue;

                    HorseBrain horseBrain = caravanHorse.GetBrain();

                    if (horseBrain == null)
                        continue;

                    horseBrain.OnCaravanRotated();
                }

                CaravanNpc.OnCaravanRotated();
            }

            internal void Init(EventConfig eventConfig)
            {
                this.eventConfig = eventConfig;

                ins.Subscribes();
                SpawnCaravan();
            }

            void SpawnCaravan()
            {
                spawnCorountine = ServerMgr.Instance.StartCoroutine(SpawnCorountine());
            }

            IEnumerator SpawnCorountine()
            {
                PathPoint startPathPoint = PathManager.currentPath.startPathPoint;
                CaravanHorse lastCaravanHorse = null;

                foreach (CaravanVehicleConfig caravanVehicleConfig in eventConfig.vehicleOrder)
                {
                    while (lastCaravanHorse != null && Vector3.Distance(lastCaravanHorse.transform.position, startPathPoint.position) < 15)
                        yield return CoroutineEx.waitForSeconds(1);

                    CaravanHorse caravanHorse = SpawnCaravanVehicle(caravanVehicleConfig);

                    if (caravanHorse != null)
                    {
                        lastCaravanHorse = caravanHorse;
                        caravanHorses.Add(caravanHorse);
                    }

                    yield return CoroutineEx.waitForSeconds(1);
                }

                OnSpawnFinished();
            }

            CaravanHorse SpawnCaravanVehicle(CaravanVehicleConfig caravanVehicleConfig)
            {
                CarriageConfig horseCarriageConfig = ins._config.carriageConfigs.FirstOrDefault(x => x.presetName == caravanVehicleConfig.presetName);

                if (horseCarriageConfig != null)
                    return HorseCarriage.CreateRouteCarriage(horseCarriageConfig, caravanVehicleConfig);

                return null;
            }

            void OnSpawnFinished()
            {
                PathPoint startPathPoint = PathManager.currentPath.startPathPoint;
                NotifyManager.SendMessageToAll("EventStart", ins._config.notifyConfig.prefix, eventConfig.displayName, MapHelper.PositionToString(startPathPoint.position));
                eventTime = eventConfig.eventTime;
                eventCorountine = ServerMgr.Instance.StartCoroutine(EventCorountine());
                EventMapMarker.CreateMarker();
                LootManager.UpdateCountOfUnlootedCrates();

                if (spawnCorountine != null)
                {
                    ServerMgr.Instance.StopCoroutine(spawnCorountine);
                    spawnCorountine = null;
                }
            }

            IEnumerator EventCorountine()
            {
                while (eventTime > 0)
                {
                    if (stopTime > 0)
                    {
                        stopTime--;

                        if (stopTime <= 0)
                            StartMoving();
                    }

                    if (agressiveTime > 0)
                        agressiveTime--;

                    eventTime--;

                    if (eventTime % 30 == 0)
                        EventPassingCheck();

                    yield return CoroutineEx.waitForSeconds(1);
                }

                EventLauncher.StopEvent();
            }

            internal void StartMoving()
            {
                if (isEventLooted)
                    return;

                NotifyManager.SendMessageToAll("StartMoving", ins._config.notifyConfig.prefix, eventConfig.displayName);

                foreach (CaravanHorse caravanHorse in caravanHorses)
                    if (caravanHorse != null)
                        caravanHorse.StartMoving();

                ZoneController.TryDeleteZone();
            }

            internal void StopMoving(BasePlayer attacker)
            {
                if (stopTime <= 0)
                {
                    foreach (CaravanHorse caravanHorse in caravanHorses)
                        if (caravanHorse != null)
                            caravanHorse.StopMoving();

                    ZoneController.CreateZone(attacker);
                }

                stopTime = ins._config.behaviorConfig.stopTime;
            }

            internal void DeleteController()
            {
                if (spawnCorountine != null)
                    ServerMgr.Instance.StopCoroutine(spawnCorountine);

                if (eventCorountine != null)
                    ServerMgr.Instance.StopCoroutine(eventCorountine);

                GameObject.Destroy(this);
            }
        }

        class Horseman : CaravanHorse
        {
            static HashSet<Horseman> horsemen = new HashSet<Horseman>();
            HorsemanConfig horsemanConfig;
            ScientistNPC driverNpc;

            internal static Horseman GetHorsemanByNetID(ulong netID)
            {
                return horsemen.FirstOrDefault(x => x != null && x.ridableHorse.IsExists() && x.ridableHorse.net.ID.Value == netID);
            }

            internal static Horseman GetHorsemanByNpcNetId(ulong netID)
            {
                return horsemen.FirstOrDefault(x => x != null && x.ridableHorse.IsExists() && x.driverNpc != null && x.driverNpc.net.ID.Value == netID);
            }

            internal static Horseman GetHorsemanByHorseNetId(ulong netID)
            {
                return horsemen.FirstOrDefault(x => x != null && x.ridableHorse.IsExists() && x.ridableHorse.net.ID.Value == netID);
            }

            internal static Horseman CreateFollowerHorseman(HorsemanConfig horsemanConfig, HorseBrain foollowingHorseBrain, Vector3 spawnPosition)
            {
                Horseman horseman = CreateHorseman(spawnPosition, Quaternion.identity, horsemanConfig, null, foollowingHorseBrain);
                return horseman;
            }

            static Horseman CreateHorseman(Vector3 position, Quaternion rotation, HorsemanConfig horsemanConfig, CaravanVehicleConfig caravanVehicleConfig, HorseBrain foollowingHorseBrain)
            {
                RidableHorse ridableHorse = BuildManager.SpawnRegularEntity("assets/rust.ai/nextai/testridablehorse.prefab", position, rotation) as RidableHorse;
                RegularHorseManager.UpdateRegularHorse(ridableHorse, horsemanConfig.horsePresetName, 1);

                Horseman horseman = ridableHorse.gameObject.AddComponent<Horseman>();
                horseman.Init(ridableHorse, horsemanConfig, caravanVehicleConfig, foollowingHorseBrain);
                horsemen.Add(horseman);
                return horseman;
            }

            internal ScientistNPC GetDriver()
            {
                return driverNpc;
            }

            internal void UpdateDriversLookRotation()
            {
                if (driverNpc.IsExists())
                    UpdateNpcLookRotation(driverNpc);
            }

            void Init(RidableHorse ridableHorse, HorsemanConfig horsemanConfig, CaravanVehicleConfig caravanVehicleConfig, HorseBrain foollowingHorseBrain)
            {
                base.Init(ridableHorse, caravanVehicleConfig, foollowingHorseBrain);
                this.horsemanConfig = horsemanConfig;
                horseBrain = HorsemanBrain.AttachBrainToHorseman(this, ridableHorse, caravanVehicleConfig, foollowingHorseBrain, horsemanConfig);
                CreateNpcs();
            }

            void CreateNpcs()
            {
                driverNpc = NpcSpawnManager.SpawnScientistNpc(horsemanConfig.frontNpcPresetName, ridableHorse.transform.position, true);
                ridableHorse.AttemptMount(driverNpc, false);
            }

            void FixedUpdate()
            {
                UpdateNpcs();
            }

            void UpdateNpcs()
            {
                if (!driverNpc.IsExists())
                    Destroy();
            }

            void UpdateNpcLookRotation(ScientistNPC scientistNpc)
            {
                scientistNpc.OverrideViewAngles(ridableHorse.transform.eulerAngles);
            }

            internal static void KillAllHorseman()
            {
                foreach (Horseman horseman in horsemen)
                    if (horseman != null)
                        horseman.Destroy();

                horsemen.Clear();
            }

            void Destroy()
            {
                ridableHorse.Kill();
            }

            void OnDestroy()
            {
                if (driverNpc.IsExists())
                    driverNpc.Kill();
            }
        }

        class HorseCarriage : CaravanHorse
        {
            static HashSet<HorseCarriage> horseCarriages = new HashSet<HorseCarriage>();
            CarriageConfig carriageConfig;
            ModularCar modularCar;
            SpringJoint springJoint;
            Coroutine carPositionChecker;
            HashSet<ScientistNPC> mountedNpcs = new HashSet<ScientistNPC>();
            HashSet<StorageContainer> lootContainers = new HashSet<StorageContainer>();

            internal static HorseCarriage GetCarriageByHorseNetID(ulong netID)
            {
                return horseCarriages.FirstOrDefault(x => x != null && x.ridableHorse != null && x.ridableHorse.net != null && x.ridableHorse.net.ID.Value == netID);
            }

            internal static HorseCarriage GetCarriageByModularCarNetID(ulong netID)
            {
                return horseCarriages.FirstOrDefault(x => x != null && x.modularCar != null && x.modularCar.net != null && x.modularCar.net.ID.Value == netID);
            }

            internal static HorseCarriage GetCarriageByModularChildEntity(BaseEntity childEntity)
            {
                BaseEntity parentEntity = childEntity.GetParentEntity();

                if (parentEntity == null || parentEntity is not ModularCar || parentEntity.net == null)
                    return null;

                return GetCarriageByModularCarNetID(parentEntity.net.ID.Value);
            }

            internal static HorseCarriage CreateRouteCarriage(CarriageConfig horseCarriageConfig, CaravanVehicleConfig caravanVehicleConfig)
            {
                Quaternion startRotation = Quaternion.LookRotation(PathManager.currentPath.spawnRotation);
                Vector3 spawnPosition = PathManager.currentPath.startPathPoint.position;

                RidableHorse ridableHorse = BuildManager.SpawnRegularEntity("assets/rust.ai/nextai/testridablehorse.prefab", spawnPosition, startRotation) as RidableHorse;
                RegularHorseManager.UpdateRegularHorse(ridableHorse, horseCarriageConfig.horsePresetName, 0);
                HorseCarriage horseCarriage = ridableHorse.gameObject.AddComponent<HorseCarriage>();
                horseCarriages.Add(horseCarriage);
                horseCarriage.Init(ridableHorse, horseCarriageConfig, caravanVehicleConfig);

                return horseCarriage;
            }

            internal float GetCarriageLength()
            {
                if (modularCar.ShortPrefabName.Contains("2mod"))
                    return 3.6f;

                if (modularCar.ShortPrefabName.Contains("3mod"))
                    return 5f;

                return 6.3f;
            }

            void Init(RidableHorse ridableHorse, CarriageConfig horseCarriageConfig, CaravanVehicleConfig caravanVehicleConfig)
            {
                this.ridableHorse = ridableHorse;
                this.carriageConfig = horseCarriageConfig;
                base.Init(ridableHorse, caravanVehicleConfig, null);

                CreateCarriage();
                horseBrain = HorseBrain.AttachBrainToHorse(this, ridableHorse, caravanVehicleConfig);
                carPositionChecker = ServerMgr.Instance.StartCoroutine(CarPositionChecker());
            }

            void CreateCarriage()
            {
                CreateModularCar();
                SpawnCrates();
                SpawnNpcs();
                ConnectCarriageToHorse();
            }

            void CreateModularCar()
            {
                float offsetScale = carriageConfig.modules.Count == 2 ? 1.8f : carriageConfig.modules.Count == 3 ? 3.75f : 4.5f;

                Vector3 spawnPosition = ridableHorse.transform.position - ridableHorse.transform.forward * offsetScale;
                spawnPosition = PositionDefiner.GetGroundPositionInPoint(spawnPosition) + Vector3.up;

                Quaternion spawnRotation = ridableHorse.transform.rotation;

                modularCar = ModularCarManager.SpawnModularCar(spawnPosition, spawnRotation, carriageConfig.modules);

                ModularCarManager.UpdateCarriageWheel(modularCar.wheelFR);
                ModularCarManager.UpdateCarriageWheel(modularCar.wheelFL);
                ModularCarManager.UpdateCarriageWheel(modularCar.wheelRR);
                ModularCarManager.UpdateCarriageWheel(modularCar.wheelRL);

                CarCustomizator.DecorateModularCar(modularCar, carriageConfig.customizationPresetName);
                modularCar.SetFlag(BaseEntity.Flags.Locked, true);
                CollideController collideController = modularCar.gameObject.AddComponent<CollideController>();
                collideController.Init(modularCar.rigidBody);

                modularCar.rigidBody.drag = 100;
                modularCar.rigidBody.angularDrag = 1f;
                modularCar.carSettings.canSleep = false;
                BaseMountable.AllMountables.Remove(modularCar);
                CollisionDisabler.AttachCollisonDisabler(modularCar);
            }

            void SpawnCrates()
            {
                foreach (PresetLocationConfig presetLocationConfig in carriageConfig.crateLocations)
                    SpawnCrate(presetLocationConfig);
            }

            void SpawnCrate(PresetLocationConfig presetLocationConfig)
            {
                CrateConfig crateConfig = ins._config.crateConfigs.FirstOrDefault(x => x.presetName == presetLocationConfig.presetName);

                if (crateConfig == null)
                {
                    NotifyManager.PrintError(null, "PresetNotFound_Exeption", crateConfig.presetName);
                    return;
                }

                Vector3 localPosition = presetLocationConfig.position.ToVector3();
                Vector3 localRotation = presetLocationConfig.rotation.ToVector3();

                BaseEntity crateEntity = BuildManager.SpawnChildEntity(modularCar, crateConfig.prefabName, localPosition, localRotation, crateConfig.skin, false);

                if (crateEntity == null)
                    return;

                LootContainer lootContainer = crateEntity as LootContainer;

                if (lootContainer != null)
                {
                    LootManager.UpdateLootContainer(lootContainer, crateConfig);
                    lootContainers.Add(lootContainer);
                    HackableLockedCrate hackableLockedCrate = lootContainer as HackableLockedCrate;

                    if (hackableLockedCrate != null)
                        hackableLockedCrate.InvokeRepeating(() => UpdateCrate(hackableLockedCrate), 1, 1);

                    return;
                }

                StorageContainer storageContainer = crateEntity as StorageContainer;

                if (storageContainer != null)
                {
                    LootManager.UpdateStorageContainer(storageContainer, crateConfig);
                    lootContainers.Add(storageContainer);
                    return;
                }
            }

            static void UpdateCrate(HackableLockedCrate hackableLockedCrate)
            {
                hackableLockedCrate.SendNetworkUpdate();
            }

            void SpawnNpcs()
            {
                foreach (NpcLocationConfig npcLocationConfig in carriageConfig.npcs)
                    SpawnMountedNpc(npcLocationConfig);
            }

            void SpawnMountedNpc(NpcLocationConfig npcLocationConfig)
            {
                Vector3 localPosition = npcLocationConfig.position.ToVector3();
                Vector3 localRotation = npcLocationConfig.rotation.ToVector3();

                BaseMountable droverBaseMountable = MovableBaseMountable.CreateMovableBaseMountable(modularCar, localPosition, localRotation, npcLocationConfig.pose);
                ScientistNPC scientistNPC = NpcSpawnManager.SpawnScientistNpc(npcLocationConfig.presetName, droverBaseMountable.transform.position, true);

                if (scientistNPC != null)
                {
                    droverBaseMountable.MountPlayer(scientistNPC);
                    mountedNpcs.Add(scientistNPC);
                }
            }

            void ConnectCarriageToHorse()
            {
                CreateSpringJoint();

                if (carriageConfig.addVisualJoint)
                    CreateVisualJoints();
            }

            void CreateVisualJoints()
            {
                CreateVisualJoint(new Vector3(1.15f, -0.371f, -0.65f), new Vector3(90, 30, 0));
                CreateVisualJoint(new Vector3(1.15f, 0.371f, -0.65f), new Vector3(270, 30, 0));
            }

            void CreateSpringJoint()
            {
                springJoint = ridableHorse.gameObject.AddComponent<SpringJoint>();
                springJoint.connectedBody = modularCar.rigidBody;
                springJoint.autoConfigureConnectedAnchor = false;

                springJoint.breakForce = float.MaxValue;
                springJoint.breakTorque = float.MaxValue;
                springJoint.connectedAnchor = new Vector3(0, 0.25f, GetCarriageLength() / 2.2f);
                springJoint.anchor = new Vector3(0, 0, 0);

                springJoint.enableCollision = false;
                springJoint.spring = 500000;

                springJoint.minDistance = 1.25f;
                springJoint.maxDistance = 1.75f;
            }

            void CreateVisualJoint(Vector3 localPosition, Vector3 localRotation)
            {
                BaseEntity entity = BuildManager.CreateEntity("assets/prefabs/weapons/paddle/paddle.entity.prefab", localPosition, Quaternion.identity, 0, false);
                entity.SetParent(ridableHorse, "spine_2", false, true);
                entity.transform.localPosition = localPosition;
                entity.transform.localEulerAngles = localRotation;
                entity.Spawn();
            }

            internal void RotateCarriage()
            {
                DisconnectCarriage();

                Vector3 horseTransformPosition = ridableHorse.transform.position;

                ridableHorse.transform.position = modularCar.transform.position;
                modularCar.transform.position = horseTransformPosition;

                ridableHorse.transform.Rotate(modularCar.transform.up, 180);
                modularCar.transform.Rotate(modularCar.transform.up, 180);

                CreateSpringJoint();
            }

            void DisconnectCarriage()
            {
                if (springJoint != null)
                    UnityEngine.GameObject.DestroyImmediate(springJoint);
            }

            void FixedUpdate()
            {
                CheckAndUpdateModularCar();
            }

            void CheckAndUpdateModularCar()
            {
                if (!modularCar.IsExists())
                {
                    DestroyCarriage();
                    return;
                }

                modularCar.timeSinceLastPush = 0f;
                modularCar.carPhysics.FixedUpdate(Time.fixedDeltaTime, 1);
                modularCar.rigidBody.drag = 100;
            }

            IEnumerator CarPositionChecker()
            {
                while (modularCar.IsExists() && ridableHorse.IsExists())
                {
                    float distanceToCar = Vector3.Distance(modularCar.transform.position, ridableHorse.transform.position);
                    float angle = Vector3.Angle(modularCar.transform.up, Vector3.up);

                    if (!ins.eventController.IsStopped() && (distanceToCar > 10f || angle >= 80))
                        ResetCarPosition();

                    modularCar.SetPrivateFieldValue("lastMovingTime", Time.realtimeSinceStartup);
                    yield return CoroutineEx.waitForSeconds(5f);
                }
            }

            void ResetCarPosition()
            {
                float carLength = GetCarriageLength();
                Vector3 idealCarPosition = PositionDefiner.GetGroundPositionInPoint(ridableHorse.transform.position - ridableHorse.transform.forward * (carLength + 1) / 2) + Vector3.up;
                DisconnectCarriage();
                modularCar.transform.rotation = ridableHorse.transform.rotation;
                modularCar.transform.position = idealCarPosition;
                CreateSpringJoint();
            }

            internal static void KillAllCarriages()
            {
                foreach (HorseCarriage horseCarriage in horseCarriages)
                    if (horseCarriage != null)
                        horseCarriage.DestroyCarriage();

                horseCarriages.Clear();
            }

            void DestroyCarriage()
            {
                ridableHorse.Kill();
                KillMounteNpcs();
            }

            void OnDestroy()
            {
                if (ins != null && ins.eventController != null && ins._config.lootConfig.dropLoot)
                    DropCrates();

                if (modularCar.IsExists())
                    modularCar.Kill();

                if (carPositionChecker != null)
                    ServerMgr.Instance.StopCoroutine(carPositionChecker);

                KillMounteNpcs();
            }

            void DropCrates()
            {
                foreach (StorageContainer storageContainer in lootContainers)
                    if (storageContainer != null)
                        DropCrate(storageContainer);
            }

            void DropCrate(StorageContainer storageContainer)
            {
                Vector3 position = storageContainer.transform.position;

                DroppedItemContainer droppedItemContainer = BuildManager.SpawnRegularEntity("assets/prefabs/misc/item drop/item_drop_buoyant.prefab", position, Quaternion.identity) as DroppedItemContainer;
                droppedItemContainer.TakeFrom(new ItemContainer[] { storageContainer.inventory }, ins._config.lootConfig.lootLossPercent);
            }

            void KillMounteNpcs()
            {
                foreach (ScientistNPC scientistNPC in mountedNpcs)
                    if (scientistNPC.IsExists())
                        scientistNPC.Kill();
            }

            class MovableBaseMountable : BaseMountable
            {
                internal static MovableBaseMountable CreateMovableBaseMountable(BaseEntity parentEntity, Vector3 localPosition, Vector3 localRotation, int seatType)
                {
                    string seatPrefab = seatType == 0 ? "assets/prefabs/vehicle/seats/workcartdriver.prefab" : "assets/prefabs/vehicle/seats/testseat.prefab";

                    BaseMountable baseMountable = GameManager.server.CreateEntity(seatPrefab, parentEntity.transform.position) as BaseMountable;
                    baseMountable.enableSaving = false;
                    MovableBaseMountable movableBaseMountable = baseMountable.gameObject.AddComponent<MovableBaseMountable>();
                    BuildManager.CopySerializableFields(baseMountable, movableBaseMountable);
                    baseMountable.StopAllCoroutines();
                    UnityEngine.GameObject.DestroyImmediate(baseMountable, true);

                    BuildManager.SetParent(parentEntity, movableBaseMountable, localPosition, localRotation);
                    movableBaseMountable.Spawn();
                    return movableBaseMountable;
                }

                public override void DismountAllPlayers()
                {
                }

                public override bool GetDismountPosition(BasePlayer player, out Vector3 res, bool silent = false)
                {
                    res = player.transform.position;
                    return true;
                }
            }

            class CollideController : FacepunchBehaviour
            {
                Rigidbody rigidbody;
                bool isTransparent;

                internal void Init(Rigidbody rigidbody)
                {
                    this.rigidbody = rigidbody;

                }

                void OnTriggerEnter(Collider other)
                {
                    if (isTransparent)
                        return;

                    BaseEntity entity = other.ToBaseEntity();

                    if (entity == null)
                        return;

                    if (entity is BradleyAPC || entity is BasicCar || entity is TrainCar)
                    {
                        MakeCarTransparent();
                        return;
                    }

                    ModularCar modularCar = entity as ModularCar;

                    if (modularCar != null)
                    {
                        BasePlayer driver = modularCar.GetDriver();

                        if (driver != null && driver is ScientistNPC)
                        {
                            MakeCarTransparent();
                            return;
                        }
                    }
                }

                void MakeCarTransparent()
                {
                    if (isTransparent)
                        return;

                    rigidbody.detectCollisions = false;
                    isTransparent = true;
                    Invoke(() => MakeCarregular(), 30f);
                }

                void MakeCarregular()
                {
                    rigidbody.detectCollisions = true;
                    isTransparent = false;
                }
            }
        }

        class CaravanHorse : FacepunchBehaviour
        {
            protected HorseBrain horseBrain;
            internal RidableHorse ridableHorse;

            protected void Init(RidableHorse ridableHorse, CaravanVehicleConfig caravanVehicleConfig, HorseBrain foollowingHorseBrain)
            {
                this.ridableHorse = ridableHorse;
            }

            internal HorseBrain GetBrain()
            {
                return horseBrain;
            }

            internal void StopMoving()
            {

            }

            internal void StartMoving()
            {

            }
        }

        class HorsemanBrain : HorseBrain
        {
            Horseman horseman;
            BasePlayer target;
            bool isMainHorse;
            HorseBrain foollowingHorseBrain;

            internal static HorseBrain AttachBrainToHorseman(Horseman horseman, RidableHorse ridableHorse, CaravanVehicleConfig caravanVehicleConfig, HorseBrain foollowingHorseBrain, HorsemanConfig horsemanConfig)
            {
                HorsemanBrain horsemanBrain = ridableHorse.gameObject.AddComponent<HorsemanBrain>();
                horsemanBrain.Init(horseman, ridableHorse, caravanVehicleConfig, foollowingHorseBrain, horsemanConfig);
                return horsemanBrain;
            }

            internal BasePlayer GetTarget()
            {
                return target;
            }

            internal bool IsRouteHorseMan()
            {
                return isMainHorse;
            }

            void Init(Horseman horseman, RidableHorse ridableHorse, CaravanVehicleConfig caravanVehicleConfig, HorseBrain foollowingHorseBrain, HorsemanConfig horsemanConfig)
            {
                this.horseman = horseman;
                this.foollowingHorseBrain = foollowingHorseBrain;
                this.homePosition = ridableHorse.transform.position;
                this.isMainHorse = foollowingHorseBrain == null;
                base.Init(horseman, ridableHorse, caravanVehicleConfig);

                AddHorsemanStates(horsemanConfig);
            }

            void AddHorsemanStates(HorsemanConfig horsemanConfig)
            {
                ChaseHorseState chaseState = caravanHorse.ridableHorse.gameObject.AddComponent<ChaseHorseState>();
                chaseState.Init(horseman, this, caravanHorse.ridableHorse, horsemanConfig.chaseRange, horsemanConfig.targetDistance);
                states.Add(chaseState);

                RoamHorseState roamState = caravanHorse.ridableHorse.gameObject.AddComponent<RoamHorseState>();
                roamState.Init(horseman, this, caravanHorse.ridableHorse, horsemanConfig.roamRadius);
                states.Add(roamState);

                if (foollowingHorseBrain != null)
                {
                    FollowHorseState followHorseState = caravanHorse.ridableHorse.gameObject.AddComponent<FollowHorseState>();
                    followHorseState.Init(horseman, this, caravanHorse.ridableHorse, foollowingHorseBrain);
                    states.Add(followHorseState);
                }
                else
                    AddRouteState();
            }

            protected override void BrainThink()
            {
                base.BrainThink();
                DefineTarget();
            }

            void DefineTarget()
            {
                ScientistNPC driver = horseman.GetDriver();

                if (driver != null)
                    target = (BasePlayer)ins.NpcSpawn.Call("GetCurrentTarget", driver);
                else
                    target = null;

                if (target == null)
                    horseman.UpdateDriversLookRotation();
            }
        }

        class HorseBrain : FacepunchBehaviour
        {
            protected CaravanHorse caravanHorse;
            protected List<BaseHorseState> states = new List<BaseHorseState>();
            protected BaseHorseState currentState;
            protected BaseNavigator baseNavigator;
            internal Vector3 homePosition;
            protected List<BaseEntity> guardEntities = new List<BaseEntity>();
            protected HashSet<HotAirBalloon> guardAirBallons = new HashSet<HotAirBalloon>();
            protected bool stopped = false;
            protected float lastStateUpdateTime = UnityEngine.Time.realtimeSinceStartup;
            static float brainUpdatePeriod = 0.5f;

            internal static HorseBrain AttachBrainToHorse(CaravanHorse caravanHorse, RidableHorse ridableHorse, CaravanVehicleConfig caravanVehicleConfig)
            {
                HorseBrain horseBrain = ridableHorse.gameObject.AddComponent<HorseBrain>();
                horseBrain.Init(caravanHorse, ridableHorse, caravanVehicleConfig);
                return horseBrain;
            }

            protected void Init(CaravanHorse caravanHorse, RidableHorse ridableHorse, CaravanVehicleConfig caravanVehicleConfig)
            {
                this.caravanHorse = caravanHorse;

                SpawnGuardEntities(caravanVehicleConfig);
                UpdateHomePosition(ridableHorse.transform.position);
                CreateNavigation();

                if (caravanHorse is HorseCarriage)
                    AddRouteState();
            }

            internal int GetGuardNpcCount()
            {
                return guardEntities.Where(x => x.IsExists()).Count;
            }

            void CreateNavigation()
            {
                NavMeshAgent navMeshAgent = caravanHorse.ridableHorse.gameObject.AddComponent<NavMeshAgent>();
                navMeshAgent.baseOffset = 0f;
                navMeshAgent.areaMask = 1;

                baseNavigator = caravanHorse.ridableHorse.gameObject.AddComponent<BaseNavigator>();
                baseNavigator.Init(caravanHorse.ridableHorse, navMeshAgent);

                if (caravanHorse is HorseCarriage)
                {
                    baseNavigator.TurnSpeed = 30;
                    navMeshAgent.angularSpeed = 15;
                }
                else
                {
                    navMeshAgent.angularSpeed = 150;
                    baseNavigator.TurnSpeed = 150;
                }
            }

            protected void AddRouteState()
            {
                RouteHorseState routeMoveState = caravanHorse.ridableHorse.gameObject.AddComponent<RouteHorseState>();
                routeMoveState.Init(this, caravanHorse, caravanHorse.ridableHorse);
                states.Add(routeMoveState);
            }

            void SpawnGuardEntities(CaravanVehicleConfig caravanVehicleConfig)
            {
                if (caravanVehicleConfig == null)
                    return;

                foreach (string guardEntityPresetName in caravanVehicleConfig.guardEntityPresets)
                {
                    int indexOfNewGuardEntity = guardEntities.Count;

                    Vector3 localPosition = Vector3.zero;
                    Vector3 spawnPosition = PositionDefiner.GetGlobalPosition(caravanHorse.ridableHorse.transform, localPosition);

                    NavMeshHit navMeshHit;
                    if (PositionDefiner.GetNavmeshInPoint(spawnPosition, 2, out navMeshHit))
                        spawnPosition = navMeshHit.position;

                    HorsemanConfig horsemanConfig = ins._config.horsemanConfigs.FirstOrDefault(x => x.presetName == guardEntityPresetName);

                    if (horsemanConfig != null)
                    {
                        Horseman horseman = Horseman.CreateFollowerHorseman(horsemanConfig, this, spawnPosition);
                        ins.eventController.caravanHorses.Add(horseman);
                        continue;
                    }

                    NpcConfig npcConfig = ins._config.npcConfigs.FirstOrDefault(x => x.presetName == guardEntityPresetName);

                    if (npcConfig != null)
                    {
                        CaravanNpc.CreateCaravanNpc(npcConfig, this, spawnPosition);
                        continue;
                    }
                }

                if (caravanVehicleConfig.balloonPresetName != "")
                {
                    AirBalloonConfig airBalloonConfig = ins._config.airBalloonConfigs.FirstOrDefault(x => x.presetName == caravanVehicleConfig.balloonPresetName);

                    if (airBalloonConfig == null)
                    {
                        NotifyManager.PrintError(null, "PresetNotFound_Exeption", caravanVehicleConfig.balloonPresetName);
                        return;
                    }

                    AirBalloon.CreateHotAirBalloon(airBalloonConfig, this);
                }
            }

            internal Vector3 GetLocalPositionForGuardEntity(BaseEntity entity)
            {
                int entityIndex = guardEntities.IndexOf(entity);
                return GetLocalPositionForGuardEntityByIndex(entityIndex);
            }

            Vector3 GetLocalPositionForGuardEntityByIndex(int indexOfGuardEntity)
            {
                float xstep = 2;
                float zstep = 4;

                float xpos = 0;
                float zpos = 1;

                int guardsInRow = 2;

                int rowNumber = caravanHorse is HorseCarriage ? 3 : 1;
                int guardsInStack = rowNumber * guardsInRow;
                int fullStackCount = (indexOfGuardEntity) / guardsInStack;

                int collumnIndex = indexOfGuardEntity / guardsInStack;
                int rowIndex = (indexOfGuardEntity - fullStackCount * guardsInStack) / guardsInRow;

                xpos += indexOfGuardEntity % 2 == 0 ? (collumnIndex + 1) * xstep : (collumnIndex + 1) * -xstep;
                zpos += -rowIndex * zstep;

                Vector3 position = new Vector3(xpos, 0, zpos);
                return position;
            }

            internal void AddNewGuardEntity(BaseEntity baseEntity)
            {
                int index = -1;

                for (int i = 0; i < guardEntities.Count; i++)
                {
                    BaseEntity guardEntity = guardEntities[i];

                    if (!guardEntity.IsExists())
                    {
                        index = i;
                        break;
                    }
                }

                if (index == -1)
                    guardEntities.Add(baseEntity);
                else
                    guardEntities[index] = baseEntity;
            }

            internal void AddNewGuardEntity(HotAirBalloon hotAirBalloon)
            {
                guardAirBallons.RemoveWhere(x => x == null);
                guardAirBallons.Add(hotAirBalloon);
            }

            internal static HorseBrain GetNewFollowingVehicleBrain()
            {
                HorseBrain result = null;

                foreach (CaravanHorse caravanHorse in ins.eventController.caravanHorses)
                {
                    if (caravanHorse == null)
                        continue;

                    HorseBrain newHorseBrain = caravanHorse.GetBrain();

                    if (newHorseBrain == null)
                        continue;

                    HorsemanBrain horsemanBrain = newHorseBrain as HorsemanBrain;

                    if (horsemanBrain != null && !horsemanBrain.IsRouteHorseMan())
                        continue;

                    if (result == null)
                        result = newHorseBrain;
                    else if (newHorseBrain.guardEntities.Count < result.guardEntities.Count)
                        result = newHorseBrain;
                }

                return result;
            }

            internal static HorseBrain GetNewFollowingVehicleBrainForAirBalloon()
            {
                HorseBrain result = null;

                foreach (CaravanHorse caravanHorse in ins.eventController.caravanHorses)
                {
                    if (caravanHorse == null)
                        continue;

                    HorseBrain newHorseBrain = caravanHorse.GetBrain();

                    if (newHorseBrain == null)
                        continue;

                    HorsemanBrain horsemanBrain = newHorseBrain as HorsemanBrain;

                    if (horsemanBrain != null && !horsemanBrain.IsRouteHorseMan())
                        continue;

                    newHorseBrain.guardAirBallons.RemoveWhere(x => x == null);

                    if (result == null)
                        result = newHorseBrain;
                    else if (newHorseBrain.guardAirBallons.Count < result.guardAirBallons.Count)
                        result = newHorseBrain;
                }

                return result;
            }

            internal RidableHorse GetHorse()
            {
                return caravanHorse.ridableHorse;
            }

            void FixedUpdate()
            {
                if (UnityEngine.Time.realtimeSinceStartup - lastStateUpdateTime < brainUpdatePeriod)
                    return;

                BrainThink();
            }

            protected virtual void BrainThink()
            {
                lastStateUpdateTime = UnityEngine.Time.realtimeSinceStartup;
                UpdateStates();
                caravanHorse.ridableHorse.BudgetedUpdate();
            }

            void UpdateStates()
            {
                lastStateUpdateTime = UnityEngine.Time.realtimeSinceStartup;

                BaseHorseState newState = states.Max(x => x.GetStateWeight());
                if (newState != null && newState != currentState)
                {
                    if (currentState != null)
                        currentState.StateLeave();

                    currentState = newState;
                    currentState.StateEnter();
                }

                if (currentState != null)
                    currentState.StateThink();
            }

            internal void SetDestination(Vector3 position, float radius = 2f)
            {
                Vector3 idealPosition = GetIdealPosition(position, radius);
                idealPosition.y += 2f;

                if (!idealPosition.IsEqualVector3(baseNavigator.Destination))
                    baseNavigator.SetDestination(idealPosition, BaseNavigator.NavigationSpeed.Slow);
            }

            internal void SetSpeed(float speed)
            {
                float deltaSpeed = Math.Abs(baseNavigator.Agent.speed - speed);

                if (deltaSpeed > 0.25f)
                    baseNavigator.Agent.speed = speed;
            }

            internal void StopMoving()
            {
                if (!stopped)
                {
                    baseNavigator.Pause();
                    stopped = true;
                }
            }

            internal void StartMoving()
            {
                if (stopped)
                {
                    baseNavigator.Resume();
                    stopped = false;
                }
            }

            internal bool IsStopped()
            {
                return stopped;
            }

            Vector3 GetIdealPosition(Vector3 position, float radius)
            {
                NavMeshHit navMeshHit;

                if (NavMesh.SamplePosition(position, out navMeshHit, radius, baseNavigator.Agent.areaMask))
                {
                    NavMeshPath path = new NavMeshPath();

                    if (NavMesh.CalculatePath(transform.position, navMeshHit.position, baseNavigator.Agent.areaMask, path))
                    {
                        if (path.status == NavMeshPathStatus.PathComplete)
                            return navMeshHit.position;
                        else
                            return path.corners.Last();
                    }
                }

                return position;
            }

            internal void OnCaravanRotated()
            {
                if (guardEntities.Count > 0)
                    guardEntities.Reverse();

                foreach (BaseHorseState baseHorseState in states)
                    baseHorseState.OnCaravanRotated();

                UpdateStates();
            }

            internal void OnCaravanSpeedChange()
            {
                currentState.UpdateSpeed();
            }

            internal virtual void UpdateHomePosition(Vector3 newHomePosition)
            {
                homePosition = newHomePosition;
            }

            internal static void ClearData()
            {
                RouteHorseState.ClearData();
            }
        }

        class ChaseHorseState : BaseHorseState
        {
            Horseman horseman;
            BasePlayer target;
            HorsemanBrain horsemanBrain;
            bool isDriverReadyToFire;
            float chaseRange;
            float targetDistance;

            internal void Init(Horseman horseman, HorsemanBrain horsemanBrain, RidableHorse ridableHorse, float chaseRange, float targetDistance)
            {
                this.horseman = horseman;
                this.horsemanBrain = horsemanBrain;
                this.chaseRange = chaseRange;
                this.targetDistance = targetDistance * UnityEngine.Random.Range(0.9f, 1.1f);
                base.Init(horsemanBrain, horseman, ridableHorse, 2.5f);
            }

            internal override float GetStateWeight()
            {
                if (!ins.eventController.IsStopped())
                    return 0;

                target = horsemanBrain.GetTarget();

                if (target == null)
                    return 0;

                float distanceFromHome = Vector3.Distance(ridableHorse.transform.position, horseBrain.homePosition);
                isDriverReadyToFire = horseman.GetDriver().Brain.CurrentState.GetType().ToString().Contains("Combat");

                if (!isDriverReadyToFire && distanceFromHome > chaseRange)
                    return 0;

                return 100;
            }

            internal override void StateEnter()
            {
                isDriverReadyToFire = false;
                base.StateEnter();
            }

            internal override void StateThink()
            {
                if (target == null)
                    return;

                float distanceToTarget = Vector3.Distance(target.transform.position, ridableHorse.transform.position);

                if (isDriverReadyToFire && distanceToTarget < targetDistance)
                {
                    horseBrain.StopMoving();
                    return;
                }
                else
                    horseBrain.StartMoving();
                FollowTarget();
            }

            void FollowTarget()
            {
                if (target != null)
                    horseBrain.SetDestination(target.transform.position, 2);
            }
        }

        class RoamHorseState : BaseHorseState
        {
            HorsemanBrain horsemanBrain;
            float roamRadius;
            float nextRoamPositionDefineTime;

            internal void Init(Horseman horseman, HorsemanBrain horsemanBrain, RidableHorse ridableHorse, float roamRadius)
            {
                this.horsemanBrain = horsemanBrain;
                this.roamRadius = roamRadius;
                base.Init(horsemanBrain, horseman, ridableHorse, 1);
            }

            internal override float GetStateWeight()
            {
                if (!ins.eventController.IsStopped())
                    return 0;

                return 20;
            }

            internal override void StateEnter()
            {
                SetRandomRoamDestination();
                base.StateEnter();
            }

            internal override void StateLeave()
            {
            }

            internal override void StateThink()
            {
                if (nextRoamPositionDefineTime < UnityEngine.Time.realtimeSinceStartup)
                    SetRandomRoamDestination();
            }

            void SetRandomRoamDestination()
            {
                Vector3 randomPosition = GetRandomRoamPosition();
                horseBrain.SetDestination(randomPosition, 2);
                nextRoamPositionDefineTime = UnityEngine.Time.realtimeSinceStartup + UnityEngine.Random.Range(3f, 10f);
            }

            Vector3 GetRandomRoamPosition()
            {
                Vector2 randomVector2 = UnityEngine.Random.insideUnitCircle * roamRadius;
                return horsemanBrain.homePosition + new Vector3(randomVector2.x, 0f, randomVector2.y);
            }
        }

        class FollowHorseState : BaseHorseState
        {
            Vector3 offset;
            RidableHorse followingHorse;
            bool followEntityNotFound;
            HorsemanBrain horsemanBrain;
            HorseBrain followingHorseBrain;

            internal void Init(Horseman horseman, HorsemanBrain horsemanBrain, RidableHorse ridableHorse, HorseBrain followingHorseBrain)
            {
                this.followingHorseBrain = followingHorseBrain;
                this.horsemanBrain = horsemanBrain;
                this.followingHorse = followingHorseBrain.GetHorse();
                base.Init(horsemanBrain, horseman, ridableHorse, 1.5f);
                followingHorseBrain.AddNewGuardEntity(ridableHorse);
                offset = followingHorseBrain.GetLocalPositionForGuardEntity(ridableHorse);
            }

            internal override float GetStateWeight()
            {
                if (followEntityNotFound || ins.eventController.IsStopped())
                    return 0f;
                else
                    return 25;
            }

            internal override void StateThink()
            {
                if (!EventLauncher.IsEventActive())
                    return;

                if (!followingHorse.IsExists())
                {
                    OnFollowerVehicleDied();
                    return;
                }

                Vector3 targetPosition = PositionDefiner.GetGlobalPosition(followingHorse.transform, offset);

                horseBrain.SetDestination(targetPosition);
                horsemanBrain.UpdateHomePosition(targetPosition);
            }

            internal override void OnCaravanRotated()
            {
                if (followingHorseBrain != null)
                    offset = followingHorseBrain.GetLocalPositionForGuardEntity(ridableHorse);
            }

            void OnFollowerVehicleDied()
            {
                HorseBrain newFollowerBrain = HorseBrain.GetNewFollowingVehicleBrain();

                if (newFollowerBrain == null)
                {
                    followEntityNotFound = true;
                    return;
                }

                newFollowerBrain.AddNewGuardEntity(ridableHorse);
                offset = newFollowerBrain.GetLocalPositionForGuardEntity(ridableHorse);
                this.followingHorse = newFollowerBrain.GetHorse();
            }
        }

        class RouteHorseState : BaseHorseState
        {
            static List<RouteHorseState> vehicleOrder = new List<RouteHorseState>();
            static bool justRotated = false;
            RouteHorseState frontVehicle;
            RouteHorseState backRouteState;
            PathPoint nextPathPoint;
            PathPoint previousPathPoint;
            float idealBackDistance;
            bool shoudBackToLastPoint = false;
            float lastPointTimer;

            internal void Init(HorseBrain horseBrain, CaravanHorse caravanHorse, RidableHorse ridableHorse)
            {
                base.Init(horseBrain, caravanHorse, ridableHorse, 1f);
                nextPathPoint = PathManager.currentPath.startPathPoint;
                vehicleOrder.Add(this);
                UpdateVehiclesOrder();
                DetermineBackDistance();
            }

            void DetermineBackDistance()
            {
                idealBackDistance = 2;
                HorseCarriage horseCarriage = caravanHorse as HorseCarriage;

                if (horseCarriage != null)
                    idealBackDistance = 5 + horseCarriage.GetCarriageLength();
            }

            static void UpdateVehiclesOrder()
            {
                for (int i = 0; i < vehicleOrder.Count; i++)
                {
                    RouteHorseState routeMoveHorseState = vehicleOrder[i];

                    if (routeMoveHorseState == null)
                        vehicleOrder.Remove(routeMoveHorseState);
                    else
                        routeMoveHorseState.DefineFollowEntity();
                }
            }

            void DefineFollowEntity()
            {
                frontVehicle = null;
                backRouteState = null;
                RouteHorseState frontState = GetFrontRouteState();
                frontVehicle = frontState;

                if (frontState != null)
                    frontState.backRouteState = this;
            }

            RouteHorseState GetFrontRouteState()
            {
                int thisStateIndex = vehicleOrder.IndexOf(this);
                int srontStateIndex = thisStateIndex - 1;

                if (srontStateIndex < 0)
                    return null;

                return vehicleOrder[srontStateIndex];
            }

            internal override float GetStateWeight()
            {
                return 25;
            }

            internal override void StateEnter()
            {
                base.StateEnter();
                shoudBackToLastPoint = true;
            }

            internal override void StateThink()
            {
                if (IsEntityShoudStop() || ins.eventController.IsStopped())
                {
                    horseBrain.StopMoving();
                    return;
                }
                else if (horseBrain.IsStopped())
                {
                    horseBrain.StartMoving();
                }


                UpdateMovement();
            }

            bool IsEntityShoudStop()
            {
                if (shoudBackToLastPoint)
                    return false;

                if (IsShoudWaitFrontEntity())
                    return true;

                if (IsShoudWaitBackEntity())
                    return true;

                if (vehicleOrder.Any(x => x != null && x.shoudBackToLastPoint))
                    return true;

                return false;
            }

            bool IsShoudWaitFrontEntity()
            {
                if (frontVehicle == null || !frontVehicle.ridableHorse.IsExists())
                    return false;

                if (Vector3.Distance(ridableHorse.transform.position, frontVehicle.ridableHorse.transform.position) < frontVehicle.idealBackDistance)
                    return true;

                return false;
            }

            bool IsShoudWaitBackEntity()
            {
                if (backRouteState == null || !backRouteState.ridableHorse.IsExists())
                    return false;

                float distance = Vector3.Distance(backRouteState.ridableHorse.transform.position, ridableHorse.transform.position);

                if (distance > idealBackDistance * 2.5f && distance > 20)
                    return true;

                return false;
            }

            void UpdateMovement()
            {
                if (nextPathPoint == null)
                {
                    if (frontVehicle == null)
                        RotateCaravan();

                    return;
                }

                UpdateSpeed();

                if (Vector3.Distance(nextPathPoint.position, ridableHorse.transform.position) < 6f)
                    SetNextTargetPoint();

                if (nextPathPoint != null)
                {
                    horseBrain.SetDestination(nextPathPoint.position, 5);
                    horseBrain.UpdateHomePosition(nextPathPoint.position);
                }
            }

            internal override void UpdateSpeed()
            {
                if (frontVehicle == null)
                {
                    base.UpdateSpeed();
                    return;
                }

                float baseSpeed = GetBaseSpeed();
                float idealDistance = frontVehicle.idealBackDistance + 2f;
                float actualDistatnce = Vector3.Distance(this.transform.position, frontVehicle.transform.position);
                float routeSpeed = actualDistatnce < idealDistance / 2 ? 0 : baseSpeed * Mathf.Lerp(0.5f, 1.5f, (actualDistatnce / idealDistance) / 2);

                horseBrain.SetSpeed(routeSpeed);
            }

            void SetNextTargetPoint()
            {
                int frontEntityRoadInxed = -1;

                if (frontVehicle != null && frontVehicle.nextPathPoint != null)
                    frontEntityRoadInxed = frontVehicle.nextPathPoint.roadIndex;

                PathPoint newNextPathPoint = null;

                if (frontEntityRoadInxed > 0)
                    newNextPathPoint = nextPathPoint.connectedPoints.FirstOrDefault(x => (previousPathPoint == null || x != previousPathPoint) && !x.disabled && x.roadIndex == frontEntityRoadInxed);

                if (newNextPathPoint == null && nextPathPoint != null)
                {
                    List<PathPoint> pathPoints = Pool.Get<List<PathPoint>>();

                    foreach (PathPoint pathPoint in nextPathPoint.connectedPoints)
                        if ((previousPathPoint == null || pathPoint != previousPathPoint) && !pathPoint.disabled && pathPoint.connectedPoints.Count > 1)
                            pathPoints.Add(pathPoint);
                    pathPoints = pathPoints.Shuffle();

                    if (pathPoints.Count > 0)
                        newNextPathPoint = pathPoints.Max(x => Time.realtimeSinceStartup - x.lastVisitTime);

                    Pool.FreeUnmanaged(ref pathPoints);

                    if (newNextPathPoint != null)
                        newNextPathPoint.lastVisitTime = Time.realtimeSinceStartup;
                }

                if (frontVehicle == null)
                {
                    foreach (PathPoint point in nextPathPoint.connectedPoints)
                        if (!point.disabled && (newNextPathPoint == null || point.position != newNextPathPoint.position) && (previousPathPoint == null || point.position != previousPathPoint.position))
                            point.disabled = true;

                    if (newNextPathPoint != null)
                        foreach (PathPoint point in newNextPathPoint.connectedPoints)
                            point.disabled = false;
                }

                previousPathPoint = nextPathPoint;
                nextPathPoint = newNextPathPoint;
                lastPointTimer = Time.realtimeSinceStartup;

                if (justRotated && !vehicleOrder.Any(x => x != null && ((x.frontVehicle != null && x.nextPathPoint != null && x.frontVehicle.nextPathPoint != null && x.nextPathPoint.roadIndex != x.frontVehicle.nextPathPoint.roadIndex) || x.previousPathPoint == null)))
                {
                    justRotated = false;

                    foreach (PathPoint point in PathManager.currentPath.points)
                        point.disabled = false;
                }
            }

            static void RotateCaravan()
            {
                justRotated = true;
                vehicleOrder.Reverse();

                foreach (RouteHorseState routeMoveState in vehicleOrder)
                    routeMoveState.Rotate();

                UpdateVehiclesOrder();

                foreach (RouteHorseState routeMoveState in vehicleOrder)
                    routeMoveState.SetNextTargetPoint();

                ins.eventController.OnCaravanRotated();
            }

            void Rotate()
            {
                HorseCarriage horseCarriage = caravanHorse as HorseCarriage;

                if (horseCarriage != null)
                    horseCarriage.RotateCarriage();

                int prevoiusPointIndex = PathManager.currentPath.points.IndexOf(nextPathPoint);
                nextPathPoint = previousPathPoint;

                if (prevoiusPointIndex >= 0)
                    previousPathPoint = PathManager.currentPath.points[prevoiusPointIndex];
                else
                    previousPathPoint = null;
            }

            void OnDestroy()
            {
                if (EventLauncher.IsEventActive())
                {
                    vehicleOrder.Remove(this);
                    UpdateVehiclesOrder();
                }
            }

            internal static void ClearData()
            {
                vehicleOrder.Clear();
            }
        }

        abstract class BaseHorseState : BaseState
        {
            protected HorseBrain horseBrain;
            protected RidableHorse ridableHorse;
            protected CaravanHorse caravanHorse;
            protected float stateSpeedScale;

            internal void Init(HorseBrain horseBrain, CaravanHorse caravanHorse, RidableHorse ridableHorse, float stateSpeedScale)
            {
                this.horseBrain = horseBrain;
                this.ridableHorse = ridableHorse;
                this.caravanHorse = caravanHorse;
                this.stateSpeedScale = stateSpeedScale;
            }

            internal override void StateEnter()
            {
                UpdateSpeed();
            }

            internal virtual void UpdateSpeed()
            {
                float baseSpeed = GetBaseSpeed();
                horseBrain.SetSpeed(baseSpeed);
            }

            protected float GetBaseSpeed()
            {
                return ins.eventController.eventConfig.baseSpeed * stateSpeedScale;
            }

            internal virtual void OnCaravanRotated()
            {
            }
        }

        class CaravanNpc : FacepunchBehaviour
        {
            internal static HashSet<CaravanNpc> caravanNpcs = new HashSet<CaravanNpc>();
            ScientistNPC scientistNPC;
            CaravanRouteState caravanRouteState;
            HorseBrain foollowingHorseBrain;

            internal static void CreateCaravanNpc(NpcConfig npcConfig, HorseBrain foollowingHorseBrain, Vector3 spawnPosition)
            {
                ScientistNPC scientistNPC = NpcSpawnManager.SpawnScientistNpc(npcConfig, spawnPosition, false, false);
                CaravanNpc caravanNpc = scientistNPC.gameObject.AddComponent<CaravanNpc>();
                caravanNpc.Init(scientistNPC, foollowingHorseBrain);
                foollowingHorseBrain.AddNewGuardEntity(scientistNPC);
                caravanNpcs.Add(caravanNpc);
            }

            void Init(ScientistNPC scientistNPC, HorseBrain foollowingHorseBrain)
            {
                this.scientistNPC = scientistNPC;
                this.foollowingHorseBrain = foollowingHorseBrain;
                scientistNPC.Invoke(() => AttachStates(), 0.5f);
            }

            void AttachStates()
            {
                if (scientistNPC == null || scientistNPC.Brain == null)
                    return;

                caravanRouteState = new CaravanRouteState(scientistNPC, foollowingHorseBrain);
                scientistNPC.Brain.AddState(caravanRouteState);
            }

            internal static void OnCaravanRotated()
            {
                foreach (CaravanNpc caravanNpc in caravanNpcs)
                    if (caravanNpc != null)
                        caravanNpc.RotateNpc();
            }

            void RotateNpc()
            {
                caravanRouteState.InverseFollowingPoint();
            }

            internal static void KillAllNpcs()
            {
                foreach (CaravanNpc caravanNpc in caravanNpcs)
                    if (caravanNpc != null)
                        caravanNpc.KillNpc();

                caravanNpcs.Clear();
            }

            void KillNpc()
            {
                if (scientistNPC.IsExists())
                    scientistNPC.Kill();
            }

            public class CaravanRouteState : BasicAIState
            {
                ScientistNPC scientistNpc;
                Vector3 offset;
                Coroutine followCorountine;
                HorseBrain followingHorseBrain;
                RidableHorse followingHorse;
                bool followEntityNotFound;
                float regularSpeed;
                float lastStateLeaveTime;

                internal CaravanRouteState(ScientistNPC scientistNpc, HorseBrain followingHorseBrain) : base(AIState.Cooldown)
                {
                    this.scientistNpc = scientistNpc;

                    if (followingHorseBrain == null)
                    {
                        OnFollowerVehicleDied();
                        return;
                    }
                    else
                    {
                        this.followingHorseBrain = followingHorseBrain;
                        this.followingHorse = followingHorseBrain.GetHorse();
                        offset = followingHorseBrain.GetLocalPositionForGuardEntity(scientistNpc);
                    }
                }

                public override float GetWeight()
                {
                    if (followEntityNotFound)
                        return 0f;
                    if (ins.eventController.IsStopped())
                        return 0f;

                    Vector3 newRoamPosition = followingHorse != null ? PositionDefiner.GetGlobalPosition(followingHorse.transform, offset) : scientistNpc.transform.position;

                    if (followingHorse.IsExists() && Vector3.Distance(scientistNpc.transform.position, newRoamPosition) > 5 && Time.realtimeSinceStartup - lastStateLeaveTime > 7)
                        return 110f;

                    return 51f;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    regularSpeed = scientistNpc.Brain.Navigator.Speed;
                    scientistNpc.Brain.Navigator.Speed = ins.eventController.eventConfig.baseSpeed * 1.5f;

                    if (followCorountine == null)
                        followCorountine = ServerMgr.Instance.StartCoroutine(FollowCorountine());
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    if (followCorountine != null)
                    {
                        ServerMgr.Instance.StopCoroutine(followCorountine);
                        followCorountine = null;
                    }

                    Vector3 newRoamPosition = followingHorse != null ? PositionDefiner.GetGlobalPosition(followingHorse.transform, offset) : scientistNpc.transform.position;
                    ins.NpcSpawn.Call("SetHomePosition", scientistNpc, newRoamPosition);
                    scientistNpc.Brain.Navigator.Speed = regularSpeed;
                    lastStateLeaveTime = Time.realtimeSinceStartup;
                }

                IEnumerator FollowCorountine()
                {
                    while (scientistNpc.IsExists())
                    {
                        if (!EventLauncher.IsEventActive())
                            break;

                        if (!followingHorse.IsExists())
                        {
                            OnFollowerVehicleDied();
                        }
                        else
                        {
                            Vector3 targetPosition = PositionDefiner.GetGlobalPosition(followingHorse.transform, offset);
                            SetDestination(targetPosition, 1, BaseNavigator.NavigationSpeed.Fast);
                        }

                        yield return CoroutineEx.waitForSeconds(0.1f);
                    }
                }

                void OnFollowerVehicleDied()
                {
                    HorseBrain newFollowerBrain = HorseBrain.GetNewFollowingVehicleBrain();

                    if (newFollowerBrain == null)
                    {
                        followEntityNotFound = true;
                        return;
                    }

                    newFollowerBrain.AddNewGuardEntity(scientistNpc);
                    offset = newFollowerBrain.GetLocalPositionForGuardEntity(scientistNpc);
                    this.followingHorse = newFollowerBrain.GetHorse();
                }

                void SetDestination(Vector3 position, float radius, BaseNavigator.NavigationSpeed speed)
                {
                    Vector3 idealPosition = GetIdealPosition(position, radius);
                    idealPosition.y += 2f;

                    if (!idealPosition.IsEqualVector3(scientistNpc.Brain.Navigator.Destination))
                    {
                        scientistNpc.Brain.Navigator.SetDestination(idealPosition, speed);
                    }
                }

                Vector3 GetIdealPosition(Vector3 position, float radius)
                {
                    NavMeshHit navMeshHit;

                    if (NavMesh.SamplePosition(position, out navMeshHit, radius, scientistNpc.NavAgent.areaMask))
                    {
                        NavMeshPath path = new NavMeshPath();

                        if (NavMesh.CalculatePath(scientistNpc.transform.position, navMeshHit.position, scientistNpc.NavAgent.areaMask, path))
                        {
                            if (path.status == NavMeshPathStatus.PathComplete)
                                return navMeshHit.position;
                            else
                                return path.corners.Last();
                        }
                    }

                    return position;
                }

                internal void InverseFollowingPoint()
                {
                    if (followingHorseBrain != null)
                        offset = followingHorseBrain.GetLocalPositionForGuardEntity(scientistNpc);
                }
            }
        }

        class AirBalloon : FacepunchBehaviour
        {
            static HashSet<AirBalloon> hotAirBalloons = new HashSet<AirBalloon>();
            static float idealHeight = 25;
            HotAirBalloon hotAirBalloon;
            AirBalloonConfig airBalloonConfig;
            HashSet<ScientistNPC> scientists = new HashSet<ScientistNPC>();
            bool isSelfDestructs = false;
            HashSet<BaseAiBalloonState> states = new HashSet<BaseAiBalloonState>();
            BaseAiBalloonState currentState;
            static float brainUpdatePeriod = 1f;
            float lastStateUpdateTime;
            float currentSpeedScale = 1f;
            Vector3 targetPosition;
            internal Vector3 homePosition;

            internal static AirBalloon GetAirBalloonByNetID(ulong netID)
            {
                return hotAirBalloons.FirstOrDefault(x => x != null && x.hotAirBalloon.IsExists() && x.hotAirBalloon.net.ID.Value == netID);
            }

            internal static AirBalloon CreateHotAirBalloon(AirBalloonConfig airBalloonConfig, HorseBrain followingHorseBrain)
            {
                Vector3 spawnPosition = followingHorseBrain.transform.position + Vector3.up * idealHeight;
                HotAirBalloon hotAirBalloon = BuildManager.SpawnRegularEntity("assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", spawnPosition, Quaternion.identity) as HotAirBalloon;
                AirBalloon airBalloon = hotAirBalloon.gameObject.AddComponent<AirBalloon>();
                airBalloon.Init(hotAirBalloon, airBalloonConfig, followingHorseBrain);
                hotAirBalloons.Add(airBalloon);
                return airBalloon;
            }

            internal void UpdateHomePosition(Vector3 homePosition)
            {
                this.homePosition = homePosition;
            }

            internal void UpdateSpeedScale(float speedScale)
            {
                currentSpeedScale = speedScale;
            }

            void Init(HotAirBalloon hotAirBalloon, AirBalloonConfig airBalloonConfig, HorseBrain followingHorseBrain)
            {
                this.hotAirBalloon = hotAirBalloon;
                this.airBalloonConfig = airBalloonConfig;

                UpdateAirBalloon();
                SpawnNpcs();
                CreateStates(followingHorseBrain);
                UpdateHomePosition(hotAirBalloon.transform.position);
            }

            void UpdateAirBalloon()
            {
                EntityFuelSystem entityFuelSystem = hotAirBalloon.GetFuelSystem() as EntityFuelSystem;

                entityFuelSystem.cachedHasFuel = true;
                entityFuelSystem.nextFuelCheckTime = float.MaxValue;
                hotAirBalloon.SetFlag(BaseEntity.Flags.On, true);
                hotAirBalloon.CancelInvoke(hotAirBalloon.ScheduleOff);
                hotAirBalloon.windForce = 0;
                hotAirBalloon.liftAmount = 0;
                hotAirBalloon.InitializeHealth(airBalloonConfig.health, airBalloonConfig.health);

                foreach (TriggerBase triggerBase in hotAirBalloon.GetComponentsInChildren<TriggerBase>())
                    UnityEngine.GameObject.Destroy(triggerBase);

                if (airBalloonConfig.armorType == 1)
                    BuildManager.SpawnChildEntity(hotAirBalloon, "assets/prefabs/deployable/hot air balloon/hotairballoon_armor_t1.prefab", Vector3.zero, Vector3.zero, 0);

                hotAirBalloon.SendNetworkUpdateImmediate();
            }

            void SpawnNpcs()
            {
                foreach (PresetLocationConfig presetLocationConfig in airBalloonConfig.npcLocations)
                    SpawnNpc(presetLocationConfig);
            }

            void SpawnNpc(PresetLocationConfig presetLocationConfig)
            {
                Vector3 localPosition = presetLocationConfig.position.ToVector3();
                Vector3 localRotation = presetLocationConfig.rotation.ToVector3();

                ScientistNPC scientistNPC = NpcSpawnManager.SpawnScientistNpc(presetLocationConfig.presetName, hotAirBalloon.transform.position, true);
                scientists.Add(scientistNPC);
                BuildManager.SetParent(hotAirBalloon, scientistNPC, localPosition, localRotation);
            }

            void CreateStates(HorseBrain followingHorseBrain)
            {
                GuardState guardState = hotAirBalloon.gameObject.AddComponent<GuardState>();
                guardState.Init(this, hotAirBalloon, followingHorseBrain);
                states.Add(guardState);

                RoamState roamState = hotAirBalloon.gameObject.AddComponent<RoamState>();
                roamState.Init(this, hotAirBalloon, airBalloonConfig.roamRadius);
                states.Add(roamState);
            }

            void OnCollisionEnter(Collision collisison)
            {
                if (isSelfDestructs)
                {
                    if (collisison == null || collisison.collider == null || collisison.collider.gameObject == null)
                        return;

                    if (collisison.collider.gameObject.layer != 9)
                        DestroyBalloonWithEffect();
                }
            }

            void FixedUpdate()
            {
                if (isSelfDestructs)
                    return;

                UpdateStates();
                CheckDrivers();
                ControlHeight();
                ControlDirection();
            }

            void UpdateStates()
            {
                if (UnityEngine.Time.realtimeSinceStartup - lastStateUpdateTime < brainUpdatePeriod)
                    return;

                lastStateUpdateTime = UnityEngine.Time.realtimeSinceStartup;

                BaseAiBalloonState newState = states.Max(x => x.GetStateWeight());
                if (newState != null && newState != currentState)
                {
                    if (currentState != null)
                        currentState.StateLeave();

                    currentState = newState;
                    currentState.StateEnter();
                }

                if (currentState != null)
                    currentState.StateThink();
            }

            void CheckDrivers()
            {
                if (!scientists.Any(x => x.IsExists()))
                {
                    isSelfDestructs = true;
                    hotAirBalloon.SetFlag(BaseEntity.Flags.On, false);
                }
            }

            internal void SetDestination(Vector3 position)
            {
                targetPosition = position;
            }

            void ControlHeight()
            {
                float currentHeight = GetCurrentHeight();
                float deltaHeight = idealHeight - currentHeight;

                if (deltaHeight < 0)
                    return;

                if (hotAirBalloon.myRigidbody.velocity.y > 1)
                    return;

                float upForceMultiplicator = Mathf.Lerp(0, 10000, deltaHeight / 10);
                hotAirBalloon.myRigidbody.AddForce(Vector3.up * upForceMultiplicator);
            }

            float GetCurrentHeight()
            {
                RaycastHit raycastHit;
                Physics.Raycast(hotAirBalloon.transform.position, Vector3.down, out raycastHit, 500, 1 << 16 | 1 << 23);
                return raycastHit.distance;
            }

            void ControlDirection()
            {
                if (targetPosition == null)
                    return;

                Vector3 targetGroundPosition = new Vector3(targetPosition.x, 0, targetPosition.z);
                Vector3 hotAirBalloonGroundPosition = new Vector3(hotAirBalloon.transform.position.x, 0, hotAirBalloon.transform.position.z);
                Vector3 direction = (targetGroundPosition - hotAirBalloonGroundPosition).normalized;
                float distance = Vector3.Distance(hotAirBalloonGroundPosition, targetGroundPosition);

                if (distance > 10)
                    distance = 10;

                hotAirBalloon.myRigidbody.AddForce(direction * distance * currentSpeedScale / 10 * 1000);
            }

            internal static void KillAllBalloons()
            {
                foreach (AirBalloon airBalloon in hotAirBalloons)
                    if (airBalloon != null)
                        airBalloon.DestroyBalloon();

                hotAirBalloons.Clear();
            }

            void DestroyBalloonWithEffect()
            {
                Effect.server.Run("assets/prefabs/weapons/beancan grenade/effects/beancan_grenade_explosion.prefab", hotAirBalloon.transform.position + hotAirBalloon.transform.up);
                hotAirBalloon.Kill(BaseNetworkable.DestroyMode.Gib);
            }

            void DestroyBalloon()
            {
                hotAirBalloon.Kill();
            }

            void OnDestroy()
            {
                foreach (ScientistNPC scientistNPC in scientists)
                    if (scientistNPC.IsExists())
                        scientistNPC.Kill();
            }

            class GuardState : BaseAiBalloonState
            {
                HorseBrain followingHorseBrain;
                bool followEntityNotFound;

                internal void Init(AirBalloon airBalloon, HotAirBalloon hotAirBalloon, HorseBrain followingHorseBrain)
                {
                    base.Init(airBalloon, hotAirBalloon);
                    this.followingHorseBrain = followingHorseBrain;
                    followingHorseBrain.AddNewGuardEntity(airBalloon.hotAirBalloon);
                }

                internal override float GetStateWeight()
                {
                    if (ins.eventController.IsStopped() || followEntityNotFound)
                        return 0;
                    else
                        return 25;
                }

                internal override void StateEnter()
                {
                    airBalloon.UpdateSpeedScale(1f);
                }

                internal override void StateThink()
                {
                    if (followingHorseBrain == null)
                    {
                        OnFollowerVehicleDied();
                        return;
                    }

                    Vector3 targetPosition = followingHorseBrain.transform.position;
                    airBalloon.SetDestination(targetPosition);
                    followingHorseBrain.UpdateHomePosition(targetPosition);
                    airBalloon.UpdateHomePosition(targetPosition);
                }

                void OnFollowerVehicleDied()
                {
                    followingHorseBrain = HorseBrain.GetNewFollowingVehicleBrainForAirBalloon();

                    if (followingHorseBrain == null)
                    {
                        followEntityNotFound = true;
                        return;
                    }

                    followingHorseBrain.AddNewGuardEntity(hotAirBalloon);
                }
            }

            class RoamState : BaseAiBalloonState
            {
                Vector3 roamPoint;
                float roamRadius;

                internal void Init(AirBalloon airBalloon, HotAirBalloon hotAirBalloon, float roamRadius)
                {
                    this.roamRadius = roamRadius;
                    base.Init(airBalloon, hotAirBalloon);
                }

                internal override float GetStateWeight()
                {
                    return 10;
                }

                internal override void StateEnter()
                {
                    base.StateEnter();
                    SetNewTargetDestination();
                    airBalloon.UpdateSpeedScale(0.3f);
                }

                internal override void StateThink()
                {
                    Vector2 targetGroundPosition = new Vector2(roamPoint.x, roamPoint.z);
                    Vector2 hotAirBalloonGroundPosition = new Vector2(hotAirBalloon.transform.position.x, hotAirBalloon.transform.position.z);
                    float distanceToRoamPoint = Vector2.Distance(targetGroundPosition, hotAirBalloonGroundPosition);

                    if (distanceToRoamPoint <= 2)
                        SetNewTargetDestination();
                }

                void SetNewTargetDestination()
                {
                    Vector3 newTargetDist = GetRandomRoamPosition();
                    roamPoint = newTargetDist;
                    airBalloon.SetDestination(roamPoint);
                }

                Vector3 GetRandomRoamPosition()
                {
                    Vector2 randomVector2 = UnityEngine.Random.insideUnitCircle * roamRadius;
                    return airBalloon.homePosition + new Vector3(randomVector2.x, 0f, randomVector2.y);
                }
            }

            abstract class BaseAiBalloonState : BaseState
            {
                protected HotAirBalloon hotAirBalloon;
                protected AirBalloon airBalloon;

                protected virtual void Init(AirBalloon airBalloon, HotAirBalloon hotAirBalloon)
                {
                    this.airBalloon = airBalloon;
                    this.hotAirBalloon = hotAirBalloon;
                }
            }
        }

        class BaseState : FacepunchBehaviour
        {
            internal virtual float GetStateWeight()
            {
                return 0;
            }

            internal virtual void StateEnter() { }

            internal virtual void StateLeave() { }

            internal virtual void StateThink() { }
        }

        class EventMapMarker : FacepunchBehaviour
        {
            static EventMapMarker eventMapMarker;

            MapMarkerGenericRadius radiusMarker;
            VendingMachineMapMarker vendingMarker;
            Coroutine updateCounter;

            internal static EventMapMarker CreateMarker()
            {
                GameObject gameObject = new GameObject();
                gameObject.layer = (int)Rust.Layer.Reserved1;
                eventMapMarker = gameObject.AddComponent<EventMapMarker>();
                eventMapMarker.Init();
                return eventMapMarker;
            }

            void Init()
            {
                Vector3 eventPosition = ins.eventController.GetEventPosition();
                CreateRadiusMarker(eventPosition);
                CreateVendingMarker(eventPosition);
                updateCounter = ServerMgr.Instance.StartCoroutine(MarkerUpdateCounter());
            }

            void CreateRadiusMarker(Vector3 position)
            {
                if (!ins._config.markerConfig.isRingMarker)
                    return;

                radiusMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
                radiusMarker.enableSaving = false;
                radiusMarker.Spawn();
                radiusMarker.radius = ins._config.markerConfig.radius;
                radiusMarker.alpha = ins._config.markerConfig.alpha;
                radiusMarker.color1 = new Color(ins._config.markerConfig.color1.r, ins._config.markerConfig.color1.g, ins._config.markerConfig.color1.b);
                radiusMarker.color2 = new Color(ins._config.markerConfig.color2.r, ins._config.markerConfig.color2.g, ins._config.markerConfig.color2.b);
            }

            void CreateVendingMarker(Vector3 position)
            {
                if (!ins._config.markerConfig.isShopMarker)
                    return;

                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position) as VendingMachineMapMarker;
                vendingMarker.Spawn();
                vendingMarker.markerShopName = $"{ins.eventController.eventConfig.displayName} ({NotifyManager.GetTimeMessage(null, ins.eventController.GetEventTime())})";
            }

            IEnumerator MarkerUpdateCounter()
            {
                while (EventLauncher.IsEventActive())
                {
                    Vector3 position = ins.eventController.GetEventPosition();
                    UpdateVendingMarker(position);
                    UpdateRadiusMarker(position);
                    yield return CoroutineEx.waitForSeconds(5f);
                }
            }

            void UpdateRadiusMarker(Vector3 position)
            {
                if (!radiusMarker.IsExists())
                    return;

                radiusMarker.transform.position = position;
                radiusMarker.SendUpdate();
                radiusMarker.SendNetworkUpdate();
            }

            void UpdateVendingMarker(Vector3 position)
            {
                if (!vendingMarker.IsExists())
                    return;

                vendingMarker.transform.position = position;
                BasePlayer pveModeEventOwner = PveModeManager.UpdateAndGetEventOwner();
                string displayEventOwnerName = ins._config.supportedPluginsConfig.pveMode.showEventOwnerNameOnMap && pveModeEventOwner != null ? GetMessage("Marker_EventOwner", null, pveModeEventOwner.displayName) : "";
                vendingMarker.markerShopName = $"{ins.eventController.eventConfig.displayName} ({NotifyManager.GetTimeMessage(null, ins.eventController.GetEventTime())}) {displayEventOwnerName}";
                vendingMarker.SetFlag(BaseEntity.Flags.Busy, pveModeEventOwner == null);
                vendingMarker.SendNetworkUpdate();
            }

            internal static void DeleteMapMarker()
            {
                if (eventMapMarker != null)
                    eventMapMarker.Delete();
            }

            void Delete()
            {
                if (radiusMarker.IsExists())
                    radiusMarker.Kill();

                if (vendingMarker.IsExists())
                    vendingMarker.Kill();

                if (updateCounter != null)
                    ServerMgr.Instance.StopCoroutine(updateCounter);

                Destroy(eventMapMarker.gameObject);
            }
        }

        class ZoneController : FacepunchBehaviour
        {
            static ZoneController zoneController;
            SphereCollider sphereCollider;
            Coroutine zoneUpdateCorountine;
            HashSet<BaseEntity> spheres = new HashSet<BaseEntity>();
            HashSet<BasePlayer> playersInZone = new HashSet<BasePlayer>();

            internal static void CreateZone(BasePlayer externalOwner = null)
            {
                TryDeleteZone();
                Vector3 position = ins.eventController.GetEventPosition();

                if (position == Vector3.zero)
                    return;

                GameObject gameObject = new GameObject();
                gameObject.transform.position = position;
                gameObject.layer = (int)Rust.Layer.Reserved1;

                zoneController = gameObject.AddComponent<ZoneController>();
                zoneController.Init(externalOwner);
            }

            internal static bool IsZoneCreated()
            {
                return zoneController != null;
            }

            internal static bool IsPlayerInZone(ulong userID)
            {
                return zoneController != null && zoneController.playersInZone.Any(x => x != null && x.userID == userID);
            }

            internal static bool IsAnyPlayerInEventZone()
            {
                return zoneController != null && zoneController.playersInZone.Any(x => x.IsExists() && !x.IsSleeping());
            }

            internal static void OnPlayerLeaveZone(BasePlayer player)
            {
                if (zoneController == null)
                    return;

                Interface.CallHook($"OnPlayerExit{ins.Name}", player);
                zoneController.playersInZone.Remove(player);
                GuiManager.DestroyGui(player);

                if (ins._config.zoneConfig.isPVPZone)
                {
                    if (ins.plugins.Exists("DynamicPVP") && (bool)ins.DynamicPVP.Call("IsPlayerInPVPDelay", (ulong)player.userID))
                        return;

                    NotifyManager.SendMessageToPlayer(player, "ExitPVP", ins._config.notifyConfig.prefix);
                }
            }

            internal static bool IsEventPosition(Vector3 position)
            {
                Vector3 eventPosition = ins.eventController.GetEventPosition();
                return Vector3.Distance(position, eventPosition) < ins.eventController.eventConfig.zoneRadius;
            }

            internal static HashSet<BasePlayer> GetAllPlayersInZone()
            {
                if (zoneController == null)
                    return new HashSet<BasePlayer>();
                else
                    return zoneController.playersInZone;
            }

            void Init(BasePlayer externalOwner)
            {
                CreateTriggerSphere();
                CreateSpheres();

                if (PveModeManager.IsPveModeReady())
                    PveModeManager.CreatePveModeZone(this.transform.position, externalOwner);

                zoneUpdateCorountine = ServerMgr.Instance.StartCoroutine(ZoneUpdateCorountine());
            }

            void CreateTriggerSphere()
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = ins.eventController.eventConfig.zoneRadius;
            }

            void CreateSpheres()
            {
                if (ins._config.zoneConfig.darkening > 0)
                    for (int i = 0; i < ins._config.zoneConfig.darkening; i++)
                        CreateSphere("assets/prefabs/visualization/sphere.prefab");

                if (ins._config.zoneConfig.isColoredBorder)
                {
                    string spherePrefab = ins._config.zoneConfig.borderColor == 0 ? "assets/bundled/prefabs/modding/events/twitch/br_sphere.prefab" : ins._config.zoneConfig.borderColor == 1 ? "assets/bundled/prefabs/modding/events/twitch/br_sphere_green.prefab" :
                         ins._config.zoneConfig.borderColor == 2 ? "assets/bundled/prefabs/modding/events/twitch/br_sphere_purple.prefab" : "assets/bundled/prefabs/modding/events/twitch/br_sphere_red.prefab";

                    for (int i = 0; i < ins._config.zoneConfig.brightness; i++)
                        CreateSphere(spherePrefab);
                }
            }

            void CreateSphere(string prefabName)
            {
                BaseEntity sphere = GameManager.server.CreateEntity(prefabName, gameObject.transform.position);
                SphereEntity entity = sphere.GetComponent<SphereEntity>();
                entity.currentRadius = ins.eventController.eventConfig.zoneRadius * 2;
                entity.lerpSpeed = 0f;
                sphere.enableSaving = false;
                sphere.Spawn();
                spheres.Add(sphere);
            }

            void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsRealPlayer())
                {
                    Interface.CallHook($"OnPlayerEnter{ins.Name}", player);
                    playersInZone.Add(player);

                    if (ins._config.zoneConfig.isPVPZone)
                        NotifyManager.SendMessageToPlayer(player, "EnterPVP", ins._config.notifyConfig.prefix);

                    GuiManager.CreateGui(player, NotifyManager.GetTimeMessage(player.UserIDString, ins.eventController.GetEventTime()), LootManager.GetCountOfUnlootedCrates().ToString(), ins.eventController.GetEventGuardsCount().ToString());
                }
            }

            void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();

                if (player.IsRealPlayer())
                    OnPlayerLeaveZone(player);
            }

            IEnumerator ZoneUpdateCorountine()
            {
                while (zoneController != null)
                {
                    int countOfCrates = LootManager.GetCountOfUnlootedCrates();
                    int countOfGuardNpc = ins.eventController.GetEventGuardsCount();

                    foreach (BasePlayer player in playersInZone)
                        if (player != null)
                            GuiManager.CreateGui(player, NotifyManager.GetTimeMessage(player.UserIDString, ins.eventController.GetEventTime()), countOfCrates.ToString(), countOfGuardNpc.ToString());

                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            internal static void TryDeleteZone()
            {
                if (zoneController != null)
                    zoneController.DeleteZone();
            }

            void DeleteZone()
            {
                foreach (BaseEntity sphere in spheres)
                    if (sphere != null && !sphere.IsDestroyed)
                        sphere.Kill();

                if (zoneUpdateCorountine != null)
                    ServerMgr.Instance.StopCoroutine(zoneUpdateCorountine);

                GuiManager.DestroyAllGui();
                PveModeManager.DeletePveModeZone();
                UnityEngine.GameObject.Destroy(gameObject);
            }
        }

        static class PveModeManager
        {
            static HashSet<ulong> pveModeOwners = new HashSet<ulong>();
            static BasePlayer owner;
            static float lastZoneDeleteTime;

            internal static bool IsPveModeReady()
            {
                return ins._config.supportedPluginsConfig.pveMode.enable && ins.plugins.Exists("PveMode");
            }

            internal static BasePlayer UpdateAndGetEventOwner()
            {
                if (ins.eventController.IsStopped())
                    return owner;

                float timeScienceLastZoneDelete = Time.realtimeSinceStartup - lastZoneDeleteTime;

                if (timeScienceLastZoneDelete > ins._config.supportedPluginsConfig.pveMode.timeExitOwner)
                    owner = null;

                return owner;
            }

            internal static void CreatePveModeZone(Vector3 position, BasePlayer externalOwner)
            {
                Dictionary<string, object> config = GetPveModeConfig();

                HashSet<ulong> npcs = NpcSpawnManager.GetEventNpcNetIds();
                HashSet<ulong> bradleys = new HashSet<ulong>();
                HashSet<ulong> helicopters = new HashSet<ulong>();
                HashSet<ulong> crates = LootManager.GetEventCratesNetIDs();
                HashSet<ulong> turrets = new HashSet<ulong>();

                BasePlayer playerOwner = GetEventOwner();

                if (playerOwner == null)
                    playerOwner = externalOwner;

                ins.PveMode.Call("EventAddPveMode", ins.Name, config, position, ins.eventController.eventConfig.zoneRadius, crates, npcs, bradleys, helicopters, turrets, pveModeOwners, playerOwner);
            }

            static BasePlayer GetEventOwner()
            {
                BasePlayer playerOwner = null;

                float timeScienceLastZoneDelete = Time.realtimeSinceStartup - lastZoneDeleteTime;

                if (owner != null && (ins.eventController.IsStopped() || timeScienceLastZoneDelete < ins._config.supportedPluginsConfig.pveMode.timeExitOwner))
                    playerOwner = owner;

                return playerOwner;
            }

            static Dictionary<string, object> GetPveModeConfig()
            {
                return new Dictionary<string, object>
                {
                    ["Damage"] = ins._config.supportedPluginsConfig.pveMode.damage,
                    ["ScaleDamage"] = ins._config.supportedPluginsConfig.pveMode.scaleDamage,
                    ["LootCrate"] = ins._config.supportedPluginsConfig.pveMode.lootCrate,
                    ["HackCrate"] = ins._config.supportedPluginsConfig.pveMode.hackCrate,
                    ["LootNpc"] = ins._config.supportedPluginsConfig.pveMode.lootNpc,
                    ["DamageNpc"] = ins._config.supportedPluginsConfig.pveMode.damageNpc,
                    ["DamageTank"] = false,
                    ["DamageHelicopter"] = false,
                    ["DamageTurret"] = false,
                    ["TargetNpc"] = ins._config.supportedPluginsConfig.pveMode.targetNpc,
                    ["TargetTank"] = false,
                    ["TargetHelicopter"] = false,
                    ["TargetTurret"] = false,
                    ["CanEnter"] = ins._config.supportedPluginsConfig.pveMode.canEnter,
                    ["CanEnterCooldownPlayer"] = ins._config.supportedPluginsConfig.pveMode.canEnterCooldownPlayer,
                    ["TimeExitOwner"] = ins._config.supportedPluginsConfig.pveMode.timeExitOwner,
                    ["AlertTime"] = ins._config.supportedPluginsConfig.pveMode.alertTime,
                    ["RestoreUponDeath"] = ins._config.supportedPluginsConfig.pveMode.restoreUponDeath,
                    ["CooldownOwner"] = ins._config.supportedPluginsConfig.pveMode.cooldown,
                    ["Darkening"] = 0
                };
            }

            internal static void DeletePveModeZone()
            {
                if (!IsPveModeReady())
                    return;

                lastZoneDeleteTime = Time.realtimeSinceStartup;
                pveModeOwners = (HashSet<ulong>)ins.PveMode.Call("GetEventOwners", ins.Name);

                if (pveModeOwners == null)
                    pveModeOwners = new HashSet<ulong>();

                ulong userId = (ulong)ins.PveMode.Call("GetEventOwner", ins.Name);
                OnNewOwnerSet(userId);

                ins.PveMode.Call("EventRemovePveMode", ins.Name, false);
            }

            static void OnNewOwnerSet(ulong userId)
            {
                if (userId == 0)
                    return;

                BasePlayer player = BasePlayer.FindByID(userId);
                OnNewOwnerSet(player);
            }

            internal static void OnNewOwnerSet(BasePlayer player)
            {
                owner = player;
            }

            internal static void OnOwnerDeleted()
            {
                owner = null;
            }

            internal static void OnEventEnd()
            {
                if (IsPveModeReady())
                    ins.PveMode.Call("EventAddCooldown", ins.Name, pveModeOwners, ins._config.supportedPluginsConfig.pveMode.cooldown);

                lastZoneDeleteTime = 0;
                pveModeOwners.Clear();
                owner = null;
            }

            internal static bool IsPveModeBlockAction(BasePlayer player)
            {
                if (IsPveModeReady())
                    return ins.PveMode.Call("CanActionEvent", ins.Name, player) != null;

                return false;
            }

            internal static bool IsPveModeBlockInterract(BasePlayer player)
            {
                if (!IsPveModeReady())
                    return false;

                BasePlayer eventOwner = GetEventOwner();

                if ((ins._config.supportedPluginsConfig.pveMode.noInterractIfCooldownAndNoOwners && eventOwner == null) || ins._config.supportedPluginsConfig.pveMode.noDealDamageIfCooldownAndTeamOwner)
                    return !(bool)ins.PveMode.Call("CanTimeOwner", ins.Name, (ulong)player.userID, ins._config.supportedPluginsConfig.pveMode.cooldown);

                return false;
            }

            internal static bool IsPveModeBlockLooting(BasePlayer player)
            {
                if (!IsPveModeReady())
                    return false;

                BasePlayer eventOwner = GetEventOwner();

                if (eventOwner == null)
                    return false;

                if (ins._config.supportedPluginsConfig.pveMode.canLootOnlyOwner && !IsTeam(player, eventOwner.userID))
                    return true;

                return false;
            }

            static bool IsTeam(BasePlayer player, ulong targetId)
            {
                if (player.userID == targetId)
                    return true;

                if (player.currentTeam != 0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);

                    if (playerTeam == null)
                        return false;

                    if (playerTeam.members.Contains(targetId))
                        return true;
                }
                return false;
            }
        }

        static class GuiManager
        {
            static bool isLoadingImageFailed;
            const float tabWidth = 109;
            const float tabHeigth = 25;
            static ImageInfo tabImageInfo = new ImageInfo("Tab_Adem");
            static List<ImageInfo> iconImageInfos = new List<ImageInfo>
            {
                new ImageInfo("Clock_Adem"),
                new ImageInfo("Crates_Adem"),
                new ImageInfo("Cowboys_Adem"),
            };

            internal static void LoadImages()
            {
                ServerMgr.Instance.StartCoroutine(LoadImagesCoroutine());
            }

            static IEnumerator LoadImagesCoroutine()
            {
                yield return LoadTabCoroutine();

                if (!isLoadingImageFailed)
                    yield return LoadIconsCoroutine();
            }

            static IEnumerator LoadTabCoroutine()
            {
                string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "Images/" + tabImageInfo.imageName + ".png";

                using (WWW www = new WWW(url))
                {
                    yield return www;

                    if (www.error != null)
                    {
                        OnImageSaveFailed(tabImageInfo.imageName);
                        isLoadingImageFailed = true;
                    }
                    else
                    {
                        Texture2D texture = www.texture;
                        uint imageId = FileStorage.server.Store(texture.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                        tabImageInfo.imageId = imageId.ToString();
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
            }

            static IEnumerator LoadIconsCoroutine()
            {
                for (int i = 0; i < iconImageInfos.Count; i++)
                {
                    ImageInfo imageInfo = iconImageInfos[i];
                    string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "Images/" + imageInfo.imageName + ".png";

                    using (WWW www = new WWW(url))
                    {
                        yield return www;

                        if (www.error != null)
                        {
                            OnImageSaveFailed(imageInfo.imageName);
                            break;
                        }
                        else
                        {
                            Texture2D texture = www.texture;
                            uint imageId = FileStorage.server.Store(texture.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                            imageInfo.imageId = imageId.ToString();
                            UnityEngine.Object.DestroyImmediate(texture);
                        }
                    }
                }
            }

            static void OnImageSaveFailed(string imageName)
            {
                NotifyManager.PrintError(null, $"Image {imageName} was not found. Maybe you didn't upload it to the .../oxide/data/Images/ folder");
                Interface.Oxide.UnloadPlugin(ins.Name);
            }

            internal static void CreateGui(BasePlayer player, params string[] args)
            {
                if (!ins._config.guiConfig.isEnable)
                    return;

                CuiHelper.DestroyUi(player, "Tabs_Adem");
                CuiElementContainer container = new CuiElementContainer();
                float halfWidth = tabWidth / 2 + tabWidth / 2 * (iconImageInfos.Count - 1);

                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{-halfWidth} {ins._config.guiConfig.offsetMinY}", OffsetMax = $"{halfWidth} {ins._config.guiConfig.offsetMinY + tabHeigth}" },
                    CursorEnabled = false,
                }, "Under", "Tabs_Adem");

                float xmin = 0;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    DrawTab(ref container, i, arg, xmin);
                    xmin += tabWidth;
                }

                CuiHelper.AddUi(player, container);
            }

            static void DrawTab(ref CuiElementContainer container, int index, string text, float xmin)
            {
                ImageInfo imageInfo = iconImageInfos[index];

                container.Add(new CuiElement
                {
                    Name = $"Tab_{index}_Adem",
                    Parent = "Tabs_Adem",
                    Components =
                    {
                        new CuiRawImageComponent { Png = tabImageInfo.imageId },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{xmin} 0", OffsetMax = $"{xmin + tabWidth} {tabHeigth}" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{index}_Adem",
                    Components =
                    {
                        new CuiRawImageComponent { Png = imageInfo.imageId },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "9 5", OffsetMax = "23 19" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{index}_Adem",
                    Components =
                    {
                        new CuiTextComponent() { Color = "1 1 1 1", Text = text, Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "23 5", OffsetMax = $"{tabWidth - 9} 19" }
                    }
                });
            }

            internal static void DestroyAllGui()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (player != null)
                        DestroyGui(player);
            }

            internal static void DestroyGui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "Tabs_Adem");
            }

            class ImageInfo
            {
                public string imageName;
                public string imageId;

                internal ImageInfo(string imageName)
                {
                    this.imageName = imageName;
                }
            }
        }

        static class PathManager
        {
            internal static EventPath currentPath;
            static HashSet<RoadMonumentData> roadMonumentDatas = new HashSet<RoadMonumentData>
            {
                new RoadMonumentData
                {
                    name = "assets/bundled/prefabs/autospawn/monument/roadside/radtown_1.prefab",
                    localPathPoints = new List<Vector3>
                    {
                        new Vector3(-44.502f, 0, -0.247f),
                        new Vector3(-37.827f, 0, -3.054f),
                        new Vector3(-31.451f, 0, -4.384f),
                        new Vector3(-24.0621f, 0, -7.598f),
                        new Vector3(-14.619f, 0, -5.652f),
                        new Vector3(-7.505f, 0, -0.728f),
                        new Vector3(4.770f, 0, -0.499f),
                        new Vector3(13.913f, 0, 2.828f),
                        new Vector3(18.432f, 0, 4.635f),
                        new Vector3(23.489f, 0, 3.804f),
                        new Vector3(32.881f, 0, -4.063f),
                        new Vector3(47f, 0, -0.293f),
                    },
                    monumentSize = new Vector3(49.2f, 0, 11f),
                    monuments = new HashSet<MonumentInfo>()
                }
            };

            internal static void DdrawPath(EventPath eventPath, BasePlayer player)
            {
                foreach (PathPoint pathPoint in eventPath.points)
                    player.SendConsoleCommand("ddraw.text", 10, Color.white, pathPoint.position, $"<size=50>{pathPoint.connectedPoints.Count}</size>");
            }

            internal static void StartCachingRouts()
            {
                foreach (RoadMonumentData roadMonumentData in roadMonumentDatas)
                    roadMonumentData.monuments = TerrainMeta.Path.Monuments.Where(x => x.name == "assets/bundled/prefabs/autospawn/monument/roadside/radtown_1.prefab");

                roadMonumentDatas.RemoveWhere(x => x.monuments.Count == 0);

                if (ins._config.pathConfig.pathType == 1)
                    ComplexPathGenerator.StartCachingPaths();
            }

            internal static void GenerateNewPath()
            {
                currentPath = null;

                if (ins._config.pathConfig.pathType == 1)
                    currentPath = ComplexPathGenerator.GetRandomPath();
                else if (ins._config.pathConfig.pathType == 2)
                    currentPath = CustomPathGenerator.GetCustomPath();

                if (currentPath == null)
                    currentPath = RegularPathGenerator.GetRegularPath();

                if (currentPath != null)
                {
                    currentPath.startPathPoint = DefineStartPoint();
                    currentPath.spawnRotation = DefineSpawnRotation();
                }

                if (currentPath == null || currentPath.startPathPoint == null)
                {
                    currentPath = null;
                    NotifyManager.PrintError(null, "RouteNotFound_Exeption");
                }
            }

            static int GetRoadIndex(PathList road)
            {
                return TerrainMeta.Path.Roads.IndexOf(road);
            }

            static bool IsRoadRound(Vector3[] road)
            {
                return Vector3.Distance(road[0], road[road.Length - 1]) < 5f;
            }

            static PathPoint DefineStartPoint()
            {
                PathPoint newStartPoint = null;
                NavMeshHit navMeshHit;

                BasePlayer testPlayer = BasePlayer.activePlayerList.FirstOrDefault(x => x != null && x.userID == 76561198999206146);

                if (currentPath.isRoundRoad)
                    newStartPoint = currentPath.points.Where(x => PositionDefiner.GetNavmeshInPoint(x.position, 2, out navMeshHit)).ToList().GetRandom();
                else if (testPlayer != null)
                    newStartPoint = currentPath.points.Where(x => x.connectedPoints.Count == 1).ToList().Min(x => Vector3.Distance(x.position, testPlayer.transform.position));
                else
                    newStartPoint = currentPath.points.Where(x => x.connectedPoints.Count == 1).ToList().GetRandom();

                if (newStartPoint == null)
                    newStartPoint = currentPath.points[0];

                if (PositionDefiner.GetNavmeshInPoint(newStartPoint.position, 2, out navMeshHit))
                    newStartPoint.position = navMeshHit.position;
                else
                    return null;

                return newStartPoint;
            }

            static Vector3 DefineSpawnRotation()
            {
                PathPoint secondPoint = null;

                for (int i = 0; i < currentPath.startPathPoint.connectedPoints.Count; i++)
                {
                    if (i == 0)
                    {
                        currentPath.startPathPoint.connectedPoints[i].disabled = false;
                        secondPoint = currentPath.startPathPoint.connectedPoints[i];
                    }
                    else
                        currentPath.startPathPoint.connectedPoints[i].disabled = true;
                }

                return (secondPoint.position - currentPath.startPathPoint.position).normalized;
            }

            internal static void OnSpawnFinish()
            {
                for (int i = 0; i < currentPath.startPathPoint.connectedPoints.Count; i++)
                    currentPath.startPathPoint.connectedPoints[i].disabled = false;
            }

            internal static void OnPluginUnloaded()
            {
                ComplexPathGenerator.StopPathGenerating();
            }

            internal static MonumentInfo GetRoadMonumentInPosition(Vector3 position)
            {
                foreach (RoadMonumentData roadMonumentData in roadMonumentDatas)
                {
                    foreach (MonumentInfo monumentInfo in roadMonumentData.monuments)
                    {
                        Vector3 localPosition = PositionDefiner.GetLocalPosition(monumentInfo.transform, position);

                        if (Math.Abs(localPosition.x) < roadMonumentData.monumentSize.x && Math.Abs(localPosition.z) < roadMonumentData.monumentSize.z)
                            return monumentInfo;
                    }
                }

                return null;
            }

            internal static void TryContinuePaThrough(MonumentInfo monumentInfo, Vector3 position, int roadIndex, ref PathPoint previousPoint, ref EventPath eventPath)
            {
                RoadMonumentData roadMonumentData = roadMonumentDatas.FirstOrDefault(x => x.name == monumentInfo.name);
                Vector3 startGlobalPosition = PositionDefiner.GetGlobalPosition(monumentInfo.transform, roadMonumentData.localPathPoints[0]);
                Vector3 endGlobalPosition = PositionDefiner.GetGlobalPosition(monumentInfo.transform, roadMonumentData.localPathPoints[roadMonumentData.localPathPoints.Count - 1]);

                if (Vector3.Distance(position, startGlobalPosition) < Vector3.Distance(position, endGlobalPosition))
                {
                    PathPoint monumentStartPathPoint = new PathPoint(startGlobalPosition, roadIndex);

                    if (previousPoint != null)
                    {
                        monumentStartPathPoint.ConnectPoint(previousPoint);
                        previousPoint.ConnectPoint(monumentStartPathPoint);
                    }

                    previousPoint = monumentStartPathPoint;

                    for (int i = 0; i < roadMonumentData.localPathPoints.Count; i++)
                    {
                        Vector3 localMonumentPosition = roadMonumentData.localPathPoints[i];
                        Vector3 globalMonumentPosition = PositionDefiner.GetGlobalPosition(monumentInfo.transform, localMonumentPosition);
                        PathPoint monumentPathPoint = new PathPoint(globalMonumentPosition, roadIndex);
                        monumentPathPoint.ConnectPoint(previousPoint);
                        previousPoint.ConnectPoint(monumentPathPoint);
                        eventPath.points.Add(monumentPathPoint);
                        previousPoint = monumentPathPoint;
                    }
                }
                else
                {
                    PathPoint monumentStartPathPoint = new PathPoint(endGlobalPosition, roadIndex);

                    if (previousPoint != null)
                    {
                        monumentStartPathPoint.ConnectPoint(previousPoint);
                        previousPoint.ConnectPoint(monumentStartPathPoint);
                    }

                    previousPoint = monumentStartPathPoint;

                    for (int i = roadMonumentData.localPathPoints.Count - 1; i >= 0; i--)
                    {
                        Vector3 localMonumentPosition = roadMonumentData.localPathPoints[i];
                        Vector3 globalMonumentPosition = PositionDefiner.GetGlobalPosition(monumentInfo.transform, localMonumentPosition);
                        PathPoint monumentPathPoint = new PathPoint(globalMonumentPosition, roadIndex);
                        monumentPathPoint.ConnectPoint(previousPoint);
                        previousPoint.ConnectPoint(monumentPathPoint);
                        eventPath.points.Add(monumentPathPoint);
                        previousPoint = monumentPathPoint;
                    }
                }
            }

            static class RegularPathGenerator
            {
                internal static EventPath GetRegularPath()
                {
                    PathList road = null;

                    if (ins._config.pathConfig.regularPathConfig.isRingRoad)
                        road = GetRoundRoadPathList();

                    if (road == null)
                        road = GetRegularRoadPathList();

                    if (road == null)
                        return null;

                    EventPath caravanPath = GetPathFromRegularRoad(road);
                    return caravanPath;
                }

                static PathList GetRoundRoadPathList()
                {
                    return TerrainMeta.Path.Roads.FirstOrDefault(x => !ins._config.pathConfig.blockRoads.Contains(TerrainMeta.Path.Roads.IndexOf(x)) && IsRoadRound(x.Path.Points) && x.Path.Length > ins._config.pathConfig.minRoadLength);
                }

                static PathList GetRegularRoadPathList()
                {
                    List<PathList> suitablePathList = TerrainMeta.Path.Roads.Where(x => !ins._config.pathConfig.blockRoads.Contains(TerrainMeta.Path.Roads.IndexOf(x)) && x.Path.Length > ins._config.pathConfig.minRoadLength).ToList();

                    if (suitablePathList != null && suitablePathList.Count > 0)
                        return suitablePathList.GetRandom();
                    //return suitablePathList.Min(x => Vector3.Distance(BasePlayer.activePlayerList[0].transform.position, x.Path.Points[0]));
                    else
                        return null;
                }

                static EventPath GetPathFromRegularRoad(PathList road)
                {
                    bool isRound = IsRoadRound(road.Path.Points);
                    EventPath caravanPath = new EventPath(isRound);
                    PathPoint previousPoint = null;
                    int roadIndex = GetRoadIndex(road);

                    bool isOnMonument = false;

                    foreach (Vector3 position in road.Path.Points)
                    {
                        if (isOnMonument)
                        {
                            if (GetRoadMonumentInPosition(position) == null)
                                isOnMonument = false;
                            else
                                continue;
                        }

                        MonumentInfo monumentInfo = GetRoadMonumentInPosition(position);

                        if (monumentInfo != null)
                        {
                            isOnMonument = true;

                            TryContinuePaThrough(monumentInfo, position, roadIndex, ref previousPoint, ref caravanPath);
                            continue;
                        }

                        PathPoint newPathPoint = new PathPoint(position, roadIndex);

                        if (previousPoint != null)
                        {
                            newPathPoint.ConnectPoint(previousPoint);
                            previousPoint.ConnectPoint(newPathPoint);
                        }

                        caravanPath.points.Add(newPathPoint);
                        previousPoint = newPathPoint;
                    }

                    if (isRound)
                    {
                        caravanPath.isRoundRoad = true;

                        PathPoint firstPoint = caravanPath.points.First();
                        PathPoint lastPoint = caravanPath.points.Last();
                        firstPoint.ConnectPoint(lastPoint);
                        lastPoint.ConnectPoint(firstPoint);
                    }

                    return caravanPath;
                }
            }

            static class CustomPathGenerator
            {
                internal static EventPath GetCustomPath()
                {
                    string pathName = ins._config.pathConfig.customPathConfig.customRoutesPresets.GetRandom();

                    if (pathName == null)
                        return null;

                    string filePath = $"{ins.Name}/Custom routes/{pathName}";
                    CustomRouteData customRouteData = Interface.Oxide.DataFileSystem.ReadObject<CustomRouteData>(filePath);

                    if (customRouteData == null || customRouteData.points == null || customRouteData.points.Count == 0)
                    {
                        NotifyManager.PrintError(null, "FileNotFound_Exeption", filePath);
                        return null;
                    }

                    EventPath caravanPath = GetCaravanPathFromCustomRouteData(customRouteData);

                    return caravanPath;
                }

                static EventPath GetCaravanPathFromCustomRouteData(CustomRouteData customRouteData)
                {
                    List<Vector3> points = new List<Vector3>();

                    foreach (string stringPoint in customRouteData.points)
                        points.Add(stringPoint.ToVector3());

                    if (points.Count == 0)
                        return null;

                    EventPath caravanPath = new EventPath(false);
                    PathPoint previousPoint = null;

                    NavMeshHit navMeshHit;

                    foreach (Vector3 position in points)
                    {
                        if (!PositionDefiner.GetNavmeshInPoint(position, 2, out navMeshHit))
                            return null;

                        PathPoint newPathPoint = new PathPoint(position, -1);

                        if (previousPoint != null)
                        {
                            newPathPoint.ConnectPoint(previousPoint);
                            previousPoint.ConnectPoint(newPathPoint);
                        }

                        caravanPath.points.Add(newPathPoint);
                        previousPoint = newPathPoint;
                    }

                    return caravanPath;
                }
            }

            static class ComplexPathGenerator
            {
                static bool isGenerationFinished;
                static List<EventPath> complexPaths = new List<EventPath>();
                static Coroutine cachingCorountine;
                static HashSet<Vector3> endPoints = new HashSet<Vector3>();

                internal static EventPath GetRandomPath()
                {
                    if (!isGenerationFinished)
                        return null;
                    else if (complexPaths.Count == 0)
                        return null;

                    EventPath caravanPath = null;

                    if (ins._config.pathConfig.complexPathConfig.chooseLongestRoute)
                        caravanPath = complexPaths.Max(x => x.includedRoadIndexes.Count);

                    if (caravanPath == null)
                        return complexPaths.GetRandom();

                    return caravanPath;
                }

                internal static void StartCachingPaths()
                {
                    CachceEndPoints();
                    cachingCorountine = ServerMgr.Instance.StartCoroutine(CachingCoroutine());
                }

                static void CachceEndPoints()
                {
                    foreach (PathList road in TerrainMeta.Path.Roads)
                    {
                        endPoints.Add(road.Path.Points[0]);
                        endPoints.Add(road.Path.Points[road.Path.Points.Length - 1]);
                    }
                }

                internal static void StopPathGenerating()
                {
                    if (cachingCorountine != null)
                        ServerMgr.Instance.StopCoroutine(cachingCorountine);
                }

                static IEnumerator CachingCoroutine()
                {
                    NotifyManager.PrintLogMessage("RouteСachingStart_Log");
                    complexPaths.Clear();

                    for (int roadIndex = 0; roadIndex < TerrainMeta.Path.Roads.Count; roadIndex++)
                    {
                        if (ins._config.pathConfig.blockRoads.Contains(roadIndex))
                            continue;

                        PathList roadPathList = TerrainMeta.Path.Roads[roadIndex];

                        if (roadPathList.Path.Length < ins._config.pathConfig.minRoadLength)
                            continue;

                        EventPath caravanPath = new EventPath(false);
                        complexPaths.Add(caravanPath);

                        yield return CachingRoad(roadIndex, 0, -1);
                    }

                    endPoints.Clear();
                    UpdateCaravanPathList();
                    NotifyManager.PrintWarningMessage("RouteСachingStop_Log", complexPaths.Count);
                    isGenerationFinished = true;
                }

                static void UpdateCaravanPathList()
                {
                    List<EventPath> clonePath = new List<EventPath>();

                    for (int i = 0; i < complexPaths.Count; i++)
                    {
                        EventPath caravanPath = complexPaths[i];

                        if (caravanPath == null || caravanPath.includedRoadIndexes.Count < ins._config.pathConfig.complexPathConfig.minRoadCount)
                            continue;

                        if (complexPaths.Any(x => x.points.Count > caravanPath.points.Count && !caravanPath.includedRoadIndexes.Any(y => !x.includedRoadIndexes.Contains(y))))
                            continue;

                        clonePath.Add(caravanPath);
                    }

                    complexPaths = clonePath;
                }

                static IEnumerator CachingRoad(int roadIndex, int startPointIndex, int pathPointForConnectionIndex)
                {
                    EventPath caravanPath = complexPaths.Last();
                    caravanPath.includedRoadIndexes.Add(roadIndex);
                    PathList road = TerrainMeta.Path.Roads[roadIndex];

                    List<PathConnectedData> pathConnectedDatas = new List<PathConnectedData>();
                    PathPoint pointForConnection = pathPointForConnectionIndex > 0 ? caravanPath.points[pathPointForConnectionIndex] : null;

                    bool isOnMonument = false;

                    for (int pointIndex = startPointIndex + 1; pointIndex < road.Path.Points.Length; pointIndex++)
                    {
                        Vector3 position = road.Path.Points[pointIndex];

                        if (isOnMonument)
                        {
                            if (GetRoadMonumentInPosition(position) == null)
                                isOnMonument = false;
                            else
                                continue;
                        }

                        MonumentInfo monumentInfo = GetRoadMonumentInPosition(position);

                        if (monumentInfo != null)
                        {
                            isOnMonument = true;

                            TryContinuePaThrough(monumentInfo, position, roadIndex, ref pointForConnection, ref caravanPath);
                            continue;
                        }


                        PathConnectedData pathConnectedData;
                        pointForConnection = CachingPoint(roadIndex, pointIndex, pointForConnection, out pathConnectedData);

                        if (pathConnectedData != null)
                            pathConnectedDatas.Add(pathConnectedData);

                        if (pointIndex % 50 == 0)
                            yield return null;
                    }

                    isOnMonument = false;
                    pointForConnection = pathPointForConnectionIndex > 0 ? caravanPath.points[pathPointForConnectionIndex] : null;

                    for (int pointIndex = startPointIndex - 1; pointIndex >= 0; pointIndex--)
                    {
                        Vector3 position = road.Path.Points[pointIndex];

                        if (isOnMonument)
                        {
                            if (GetRoadMonumentInPosition(position) == null)
                                isOnMonument = false;
                            else
                                continue;
                        }

                        MonumentInfo monumentInfo = GetRoadMonumentInPosition(position);

                        if (monumentInfo != null)
                        {
                            isOnMonument = true;

                            TryContinuePaThrough(monumentInfo, position, roadIndex, ref pointForConnection, ref caravanPath);
                            continue;
                        }


                        PathConnectedData pathConnectedData;
                        pointForConnection = CachingPoint(roadIndex, pointIndex, pointForConnection, out pathConnectedData);

                        if (pathConnectedData != null)
                            pathConnectedDatas.Add(pathConnectedData);

                        if (pointIndex % 50 == 0)
                            yield return null;
                    }

                    for (int i = 0; i < pathConnectedDatas.Count; i++)
                    {
                        PathConnectedData pathConnectedData = pathConnectedDatas[i];

                        if (caravanPath.includedRoadIndexes.Contains(pathConnectedData.newRoadIndex))
                            continue;

                        Vector3 currentRoadPoint = road.Path.Points[pathConnectedData.pathPointIndex];
                        PathList newRoadPathList = TerrainMeta.Path.Roads[pathConnectedData.newRoadIndex];
                        Vector3 closestPathPoint = newRoadPathList.Path.Points.Min(x => Vector3.Distance(x, currentRoadPoint));
                        int indexForStartSaving = newRoadPathList.Path.Points.ToList().IndexOf(closestPathPoint);

                        yield return CachingRoad(pathConnectedData.newRoadIndex, indexForStartSaving, pathConnectedData.pointForConnectionIndex);
                    }
                }

                static PathPoint CachingPoint(int roadIndex, int pointIndex, PathPoint lastPathPoint, out PathConnectedData pathConnectedData)
                {
                    EventPath caravanPath = complexPaths.Last();
                    PathList road = TerrainMeta.Path.Roads[roadIndex];
                    Vector3 point = road.Path.Points[pointIndex];
                    PathPoint newPathPoint = new PathPoint(point, roadIndex);

                    if (lastPathPoint != null)
                    {
                        newPathPoint.ConnectPoint(lastPathPoint);
                        lastPathPoint.ConnectPoint(newPathPoint);
                    }
                    if (pointIndex == road.Path.Points.Length - 1 && IsRingRoad(road))
                    {
                        Vector3 startPoint = road.Path.Points[1];
                        PathPoint startPathPoint = caravanPath.points.FirstOrDefault(x => x.position.IsEqualVector3(startPoint));

                        if (startPathPoint != null)
                        {
                            newPathPoint.ConnectPoint(startPathPoint);
                            startPathPoint.ConnectPoint(newPathPoint);
                        }
                    }

                    caravanPath.points.Add(newPathPoint);

                    PathList newRoad = null;
                    pathConnectedData = null;

                    if (pointIndex == 0 || pointIndex == road.Path.Points.Length - 1)
                        newRoad = TerrainMeta.Path.Roads.FirstOrDefault(x => !ins._config.pathConfig.blockRoads.Contains(TerrainMeta.Path.Roads.IndexOf(x)) && x.Path.Length > ins._config.pathConfig.minRoadLength && !caravanPath.includedRoadIndexes.Contains(GetRoadIndex(x)) && (Vector3.Distance(x.Path.Points[0], point) < 7.5f || Vector3.Distance(x.Path.Points[x.Path.Points.Length - 1], point) < 7.5f));
                    else if (endPoints.Any(x => Vector3.Distance(x, point) < 7.5f))
                        newRoad = TerrainMeta.Path.Roads.FirstOrDefault(x => !ins._config.pathConfig.blockRoads.Contains(TerrainMeta.Path.Roads.IndexOf(x)) && x.Path.Length > ins._config.pathConfig.minRoadLength && !caravanPath.includedRoadIndexes.Contains(GetRoadIndex(x)) && x.Path.Points.Any(y => Vector3.Distance(y, point) < 7.5f));

                    if (newRoad != null)
                    {
                        int newRoadIndex = GetRoadIndex(newRoad);
                        int pointForConnectionIndex = caravanPath.points.IndexOf(newPathPoint);

                        pathConnectedData = new PathConnectedData
                        {
                            pathRoadIndex = roadIndex,
                            pathPointIndex = pointIndex,
                            newRoadIndex = newRoadIndex,
                            pointForConnectionIndex = pointForConnectionIndex
                        };
                    }

                    return newPathPoint;
                }

                static bool IsRingRoad(PathList road)
                {
                    return road.Hierarchy == 0 && Vector3.Distance(road.Path.Points[0], road.Path.Points[road.Path.Points.Length - 1]) < 2f;
                }

                class PathConnectedData
                {
                    internal int pathRoadIndex;
                    internal int pathPointIndex;
                    internal int newRoadIndex;
                    internal int pointForConnectionIndex;
                }
            }

            class RoadMonumentData
            {
                public string name;
                public List<Vector3> localPathPoints;
                public Vector3 monumentSize;
                public HashSet<MonumentInfo> monuments;
            }
        }

        class EventPath
        {
            internal List<PathPoint> points = new List<PathPoint>();
            internal List<int> includedRoadIndexes = new List<int>();
            internal bool isRoundRoad;
            internal PathPoint startPathPoint;
            internal Vector3 spawnRotation;

            internal EventPath(bool isRoundRoad)
            {
                this.isRoundRoad = isRoundRoad;
            }
        }

        class PathPoint
        {
            internal Vector3 position;
            internal List<PathPoint> connectedPoints = new List<PathPoint>();
            internal bool disabled;
            internal int roadIndex;
            internal float lastVisitTime;

            internal PathPoint(Vector3 position, int roadIndex)
            {
                this.position = position;
                this.roadIndex = roadIndex;
            }

            internal void ConnectPoint(PathPoint pathPoint)
            {
                connectedPoints.Add(pathPoint);
            }
        }


        static class ModularCarManager
        {
            internal static ModularCar SpawnModularCar(Vector3 position, Quaternion rotation, List<string> moduleShortnames)
            {
                int carLength = GetRequiredCarLength(moduleShortnames);

                string prefabName = $"assets/content/vehicles/modularcar/{carLength}module_car_spawned.entity.prefab";

                ModularCar modularCar = BuildManager.CreateEntity(prefabName, position, rotation, 0, false) as ModularCar;
                modularCar.spawnSettings.useSpawnSettings = false;
                modularCar.Spawn();
                CreateCarModules(modularCar, moduleShortnames);
                modularCar.Invoke(() => DelayedCarUpdate(modularCar), 1f);

                return modularCar;
            }

            static int GetRequiredCarLength(List<string> moduleShortnameList)
            {
                int doubleModulesCount = moduleShortnameList.Where(x => x.Contains("2mod")).Count;

                int count = doubleModulesCount + moduleShortnameList.Count; ;

                if (count < 2)
                    count = 2;
                else if (count > 4)
                    count = 4;

                return count;
            }

            static void CreateCarModules(ModularCar modularCar, List<string> modules)
            {
                int lastAddedModuleIndex = -1;

                for (int socketIndex = 0; socketIndex < modularCar.TotalSockets; socketIndex++)
                {
                    int newModuleIndex = lastAddedModuleIndex + 1;

                    if (newModuleIndex >= modules.Count)
                        return;

                    lastAddedModuleIndex = newModuleIndex;

                    string itemShortname = modules[newModuleIndex];

                    if (itemShortname == "")
                        continue;

                    Item moduleItem = ItemManager.CreateByName(itemShortname);
                    if (moduleItem == null)
                        continue;

                    if (!modularCar.TryAddModule(moduleItem, socketIndex))
                        moduleItem.Remove();
                    else if (itemShortname.Contains("2mod"))
                        ++socketIndex;
                }
            }

            internal static void UpdateCarriageWheel(VisualCarWheel visualCarWheel)
            {
                visualCarWheel.tyreFriction = 10f;
                visualCarWheel.wheelCollider.wheelDampingRate = 0;
                visualCarWheel.powerWheel = false;
                visualCarWheel.brakeWheel = true;
            }

            static void DelayedCarUpdate(ModularCar modularCar)
            {
                if (modularCar == null || modularCar.rigidBody == null)
                    return;

                modularCar.rigidBody.mass = 3000;
                modularCar.rigidBody.angularDrag = 1;
                modularCar.SetFlag(BaseEntity.Flags.Locked, true);

                foreach (TriggerBase triggerBase in modularCar.GetComponentsInChildren<TriggerBase>())
                {
                    UnityEngine.GameObject.Destroy(triggerBase);
                }
            }
        }

        static class RegularHorseManager
        {
            internal static void UpdateRegularHorse(RidableHorse ridableHorse, string horsePresetName, int seatsAmount = 0)
            {
                HorseConfig horseConfig = GetHorseConfigByName(horsePresetName);
                if (horseConfig == null)
                {
                    NotifyManager.PrintError(null, "PresetNotFound_Exeption", horsePresetName);
                    return;
                }

                if (horseConfig.saddleType > seatsAmount)
                    seatsAmount = horseConfig.saddleType;

                UpdateHealth(ridableHorse, horseConfig.health);
                UpdateBreed(ridableHorse, horseConfig.breed);
                UpdateSaddle(ridableHorse, seatsAmount);
                UpdateArmor(ridableHorse, horseConfig.armorType);

                ridableHorse.SetFlag(BaseEntity.Flags.Busy, true);
            }

            static HorseConfig GetHorseConfigByName(string horsePresetName)
            {
                return ins._config.horseConfigs.FirstOrDefault(x => x.presetName == horsePresetName);
            }

            static void UpdateHealth(RidableHorse ridableHorse, float health)
            {
                ridableHorse.startHealth = health;
                ridableHorse.InitializeHealth(health, health);
            }

            static void UpdateBreed(RidableHorse ridableHorse, int breed)
            {
                int horseBread = breed == 0 ? UnityEngine.Random.Range(0, ridableHorse.breeds.Length) : breed - 1;

                if (horseBread < 0)
                    horseBread = 0;

                if (horseBread >= ridableHorse.breeds.Length)
                    horseBread = ridableHorse.breeds.Length - 1;

                ridableHorse.SetBreed(horseBread);
            }

            static void UpdateSaddle(RidableHorse ridableHorse, int sadleType)
            {
                ridableHorse.SetFlag(RidableHorse.Flag_HasSingleSaddle, false);
                ridableHorse.SetFlag(RidableHorse.Flag_HasDoubleSaddle, false);

                if (sadleType == 0)
                    return;

                string saddleItemShortname = sadleType == 1 ? "horse.saddle.single" : "horse.saddle.double";
                AddItemToHorse(ridableHorse, saddleItemShortname);
            }

            static void UpdateArmor(RidableHorse ridableHorse, int armorType)
            {
                if (armorType == 0)
                    return;

                string armorItemShortname = armorType == 1 ? "horse.armor.wood" : "horse.armor.roadsign";
                AddItemToHorse(ridableHorse, armorItemShortname);
            }

            static void AddItemToHorse(RidableHorse ridableHorse, string itemShortname)
            {
                Item item = ItemManager.CreateByName(itemShortname);

                if (item != null)
                    item.MoveToContainer(ridableHorse.equipmentInventory);
            }
        }

        static class NpcSpawnManager
        {
            internal static HashSet<ScientistNPC> eventNpcs = new HashSet<ScientistNPC>();

            internal static HashSet<ulong> GetEventNpcNetIds()
            {
                HashSet<ulong> result = new HashSet<ulong>();

                foreach (ScientistNPC scientistNPC in eventNpcs)
                    if (scientistNPC != null && scientistNPC.net != null)
                        result.Add(scientistNPC.net.ID.Value);

                return result;
            }

            internal static ScientistNPC GetScientistByNetId(ulong netId)
            {
                return eventNpcs.FirstOrDefault(x => x != null && x.net != null && x.net.ID.Value == netId);
            }

            internal static bool IsNpcSpawnReady()
            {
                if (!ins.plugins.Exists("NpcSpawn"))
                {
                    ins.PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt. NPCs will not spawn!");
                    ins.NextTick(() => Interface.Oxide.UnloadPlugin(ins.Name));
                    return false;
                }
                else
                    return true;
            }

            internal static bool IsEventNpc(ScientistNPC scientistNPC)
            {
                return scientistNPC != null && ins._config.npcConfigs.Any(x => x.displayName == scientistNPC.displayName);
            }

            internal static ScientistNPC SpawnScientistNpc(string npcPresetName, Vector3 position, bool isStationary, bool isPassive = false)
            {
                NpcConfig npcConfig = GetNpcConfigByPresetName(npcPresetName);
                if (npcConfig == null)
                {
                    NotifyManager.PrintError(null, "PresetNotFound_Exeption", npcPresetName);
                    return null;
                }

                ScientistNPC scientistNPC = SpawnScientistNpc(npcConfig, position, isStationary, isPassive);

                if (isStationary)
                    UpdateClothesWeight(scientistNPC);

                return scientistNPC;
            }

            internal static ScientistNPC SpawnScientistNpc(NpcConfig npcConfig, Vector3 position, bool isStationary, bool isPassive)
            {
                JObject baseNpcConfigObj = GetBaseNpcConfig(npcConfig, isStationary, isPassive);
                ScientistNPC scientistNPC = (ScientistNPC)ins.NpcSpawn.Call("SpawnNpc", position, baseNpcConfigObj, isPassive);
                eventNpcs.Add(scientistNPC);
                return scientistNPC;
            }

            internal static NpcConfig GetNpcConfigByDisplayName(string displayName)
            {
                return ins._config.npcConfigs.FirstOrDefault(x => x.displayName == displayName);
            }

            static NpcConfig GetNpcConfigByPresetName(string npcPresetName)
            {
                return ins._config.npcConfigs.FirstOrDefault(x => x.presetName == npcPresetName);
            }

            static JObject GetBaseNpcConfig(NpcConfig config, bool isStationary, bool isPassive)
            {
                return new JObject
                {
                    ["Name"] = config.displayName,
                    ["WearItems"] = new JArray
                    {
                        config.wearItems.Select(x => new JObject
                        {
                            ["ShortName"] = x.shortName,
                            ["SkinID"] = x.skinID
                        })
                    },
                    ["BeltItems"] = isPassive ? new JArray() : new JArray { config.beltItems.Select(x => new JObject { ["ShortName"] = x.shortName, ["Amount"] = x.amount, ["SkinID"] = x.skinID, ["mods"] = new JArray { x.mods.ToHashSet() }, ["Ammo"] = x.ammo }) },
                    ["Kit"] = config.kit,
                    ["Health"] = config.health,
                    ["RoamRange"] = isStationary ? 0 : config.roamRange,
                    ["ChaseRange"] = isStationary ? 0 : config.chaseRange,
                    ["SenseRange"] = config.senseRange,
                    ["ListenRange"] = config.senseRange / 2,
                    ["AttackRangeMultiplier"] = config.attackRangeMultiplier,
                    ["VisionCone"] = config.visionCone,
                    ["DamageScale"] = config.damageScale,
                    ["TurretDamageScale"] = 1f,
                    ["AimConeScale"] = config.aimConeScale,
                    ["DisableRadio"] = config.disableRadio,
                    ["CanRunAwayWater"] = false,
                    ["CanSleep"] = false,
                    ["Speed"] = isStationary ? 0 : config.speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = config.memoryDuration,
                    ["States"] = isPassive ? new JArray() : isStationary ? new JArray { "IdleState", "CombatStationaryState" } : new JArray { "RoamState", "ChaseState", "CombatState" }
                };
            }

            static void UpdateClothesWeight(ScientistNPC scientistNPC)
            {
                foreach (Item item in scientistNPC.inventory.containerWear.itemList)
                {
                    ItemModWearable component = item.info.GetComponent<ItemModWearable>();

                    if (component != null)
                        component.weight = 0;
                }
            }

            internal static void ClearData()
            {
                eventNpcs.Clear();
            }
        }

        static class PositionDefiner
        {
            internal static Vector3 GetGlobalPosition(Transform parentTransform, Vector3 position)
            {
                return parentTransform.transform.TransformPoint(position);
            }

            internal static Vector3 GetLocalPosition(Transform parentTransform, Vector3 globalPosition)
            {
                return parentTransform.transform.InverseTransformPoint(globalPosition);
            }

            internal static Vector3 GetGroundPositionInPoint(Vector3 position)
            {
                position.y = 100;
                RaycastHit raycastHit;
                Physics.Raycast(position, Vector3.down, out raycastHit, 500, 1 << 16 | 1 << 23);
                position.y = raycastHit.point.y;
                return position;
            }

            internal static bool GetNavmeshInPoint(Vector3 position, float radius, out NavMeshHit navMeshHit)
            {
                return NavMesh.SamplePosition(position, out navMeshHit, radius, 1);
            }
        }

        static class BuildManager
        {
            internal static BaseEntity SpawnRegularEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, enableSaving);
                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnStaticEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, false);
                DestroyUnnessesaryComponents(entity);

                StabilityEntity stabilityEntity = entity as StabilityEntity;
                if (stabilityEntity != null)
                    stabilityEntity.grounded = true;

                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                if (baseCombatEntity != null)
                    baseCombatEntity.pickup.enabled = false;

                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnChildEntity(BaseEntity parrentEntity, string prefabName, Vector3 localPosition, Vector3 localRotation, ulong skinId = 0, bool isDecor = true, bool enableSaving = false)
            {
                BaseEntity entity = isDecor ? CreateDecorEntity(prefabName, parrentEntity.transform.position, Quaternion.identity, skinId) : CreateEntity(prefabName, parrentEntity.transform.position, Quaternion.identity, skinId, enableSaving);
                SetParent(parrentEntity, entity, localPosition, localRotation);

                DestroyUnnessesaryComponents(entity);

                if (isDecor)
                    DestroyDecorComponents(entity);

                entity.Spawn();
                return entity;
            }

            internal static void UpdateEntityMaxHealth(BaseCombatEntity baseCombatEntity, float maxHealth)
            {
                baseCombatEntity.startHealth = maxHealth;
                baseCombatEntity.InitializeHealth(maxHealth, maxHealth);
            }

            internal static BaseEntity CreateEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId, bool enableSaving)
            {
                BaseEntity entity = GameManager.server.CreateEntity(prefabName, position, rotation);
                entity.enableSaving = enableSaving;
                entity.skinID = skinId;

                BradleyAPC bradleyAPC = entity as BradleyAPC;

                if (bradleyAPC != null)
                    bradleyAPC.ScientistSpawnCount = 0;

                return entity;
            }

            static BaseEntity CreateDecorEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, enableSaving);

                BaseEntity trueBaseEntity = entity.gameObject.AddComponent<BaseEntity>();
                CopySerializableFields(entity, trueBaseEntity);
                UnityEngine.Object.DestroyImmediate(entity, true);

                return trueBaseEntity;
            }

            internal static void SetParent(BaseEntity parrentEntity, BaseEntity childEntity, Vector3 localPosition, Vector3 localRotation)
            {
                childEntity.SetParent(parrentEntity, true, false);
                childEntity.transform.localPosition = localPosition;
                childEntity.transform.localEulerAngles = localRotation;
            }

            static void DestroyDecorComponents(BaseEntity entity)
            {
                Component[] components = entity.GetComponentsInChildren<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];

                    EntityCollisionMessage entityCollisionMessage = component as EntityCollisionMessage;

                    if (entityCollisionMessage != null || (component != null && component.name != entity.PrefabName))
                    {
                        Transform transform = component as Transform;
                        if (transform != null)
                            continue;

                        Collider collider = component as Collider;
                        if (collider != null && collider is MeshCollider == false)
                            continue;

                        if (component is Model)
                            continue;

                        UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
                    }
                }
            }

            static void DestroyUnnessesaryComponents(BaseEntity entity)
            {
                DestroyEntityConponent<GroundWatch>(entity);
                DestroyEntityConponent<DestroyOnGroundMissing>(entity);
                DestroyEntityConponent<TriggerHurtEx>(entity);

                if (entity is BradleyAPC == false)
                    DestroyEntityConponent<Rigidbody>(entity);
            }

            internal static void DestroyEntityConponent<TypeForDestroy>(BaseEntity entity)
            {
                if (entity == null)
                    return;

                TypeForDestroy component = entity.GetComponent<TypeForDestroy>();
                if (component != null)
                    UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
            }

            internal static void DestroyEntityConponents<TypeForDestroy>(BaseEntity entity)
            {
                if (entity == null)
                    return;

                TypeForDestroy[] components = entity.GetComponentsInChildren<TypeForDestroy>();

                for (int i = 0; i < components.Length; i++)
                {
                    TypeForDestroy component = components[i];

                    if (component != null)
                        UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
                }
            }

            internal static void CopySerializableFields<T>(T src, T dst)
            {
                FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in srcFields)
                {
                    object value = field.GetValue(src);
                    field.SetValue(dst, value);
                }
            }
        }

        static class NotifyManager
        {
            internal static void PrintInfoMessage(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null)
                    ins.PrintWarning(ClearColorAndSize(GetMessage(langKey, null, args)));
                else
                    ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static void PrintError(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null)
                    ins.PrintError(ClearColorAndSize(GetMessage(langKey, null, args)));
                else
                    ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }



            internal static void PrintLogMessage(string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is int)
                        args[i] = GetTimeMessage(null, (int)args[i]);

                ins.Puts(ClearColorAndSize(GetMessage(langKey, null, args)));
            }

            internal static void PrintWarningMessage(string langKey, params object[] args)
            {
                ins.PrintWarning(ClearColorAndSize(GetMessage(langKey, null, args)));
            }

            internal static string ClearColorAndSize(string message)
            {
                message = message.Replace("</color>", string.Empty);
                message = message.Replace("</size>", string.Empty);
                while (message.Contains("<color="))
                {
                    int index = message.IndexOf("<color=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                while (message.Contains("<size="))
                {
                    int index = message.IndexOf("<size=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                return message;
            }

            internal static void SendMessageToAll(string langKey, params object[] args)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (player != null)
                        SendMessageToPlayer(player, langKey, args);

                TrySendDiscordMessage(langKey, args);
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is int)
                        args[i] = GetTimeMessage(player.UserIDString, (int)args[i]);

                RedefinedMessageConfig redefinedMessageConfig = GetRedefinedMessageConfig(langKey);

                if (redefinedMessageConfig != null && !redefinedMessageConfig.isEnable)
                    return;

                string playerMessage = GetMessage(langKey, player.UserIDString, args);

                if (redefinedMessageConfig != null)
                    SendMessage(redefinedMessageConfig, player, playerMessage);
                else
                    SendMessage(ins._config.notifyConfig, player, playerMessage);
            }

            static void SendMessage(BaseMessageConfig baseMessageConfig, BasePlayer player, string playerMessage)
            {
                if (baseMessageConfig.chatConfig.isEnabled)
                    ins.PrintToChat(player, playerMessage);

                if (baseMessageConfig.gameTipConfig.isEnabled)
                    player.SendConsoleCommand("gametip.showtoast", baseMessageConfig.gameTipConfig.style, ClearColorAndSize(playerMessage), string.Empty);

                if (baseMessageConfig.guiAnnouncementsConfig.isEnabled && ins.plugins.Exists("guiAnnouncementsConfig"))
                    ins.GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(playerMessage), baseMessageConfig.guiAnnouncementsConfig.bannerColor, baseMessageConfig.guiAnnouncementsConfig.textColor, player, baseMessageConfig.guiAnnouncementsConfig.apiAdjustVPosition);

                if (baseMessageConfig.notifyPluginConfig.isEnabled && ins.plugins.Exists("Notify"))
                    ins.Notify?.Call("SendNotify", player, baseMessageConfig.notifyPluginConfig.type, ClearColorAndSize(playerMessage));
            }

            static RedefinedMessageConfig GetRedefinedMessageConfig(string langKey)
            {
                return ins._config.notifyConfig.redefinedMessages.FirstOrDefault(x => x.langKey == langKey);
            }

            internal static string GetTimeMessage(string userIDString, int seconds)
            {
                string message = "";

                TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
                if (timeSpan.Hours > 0) message += $" {timeSpan.Hours} {GetMessage("Hours", userIDString)}";
                if (timeSpan.Minutes > 0) message += $" {timeSpan.Minutes} {GetMessage("Minutes", userIDString)}";
                if (message == "") message += $" {timeSpan.Seconds} {GetMessage("Seconds", userIDString)}";

                return message;
            }

            static void TrySendDiscordMessage(string langKey, params object[] args)
            {
                if (CanSendDiscordMessage(langKey))
                {
                    for (int i = 0; i < args.Length; i++)
                        if (args[i] is int)
                            args[i] = GetTimeMessage(null, (int)args[i]);

                    object fields = new[] { new { name = ins.Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                    ins.DiscordMessages?.Call("API_SendFancyMessage", ins._config.notifyConfig.discordMessagesConfig.webhookUrl, "", ins._config.notifyConfig.discordMessagesConfig.embedColor, JsonConvert.SerializeObject(fields), null, ins);
                }
            }

            static bool CanSendDiscordMessage(string langKey)
            {
                return ins._config.notifyConfig.discordMessagesConfig.keys.Contains(langKey) && ins._config.notifyConfig.discordMessagesConfig.isEnabled && !string.IsNullOrEmpty(ins._config.notifyConfig.discordMessagesConfig.webhookUrl) && ins._config.notifyConfig.discordMessagesConfig.webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
            }
        }

        static class LootManager
        {
            static HashSet<ulong> lootedContainersUids = new HashSet<ulong>();
            static HashSet<StorageContainerData> storageContainers = new HashSet<StorageContainerData>();
            static int countOfUnlootedCrates;

            internal static int GetCountOfUnlootedCrates()
            {
                return countOfUnlootedCrates;
            }

            internal static void UpdateCountOfUnlootedCrates()
            {
                countOfUnlootedCrates = storageContainers.Where(x => x != null && x.storageContainer.IsExists() && x.storageContainer.net != null && !IsCrateLooted(x.storageContainer.net.ID.Value)).Count;
            }

            internal static void OnEventCrateLooted(StorageContainer storageContainer, ulong userId)
            {
                if (storageContainer.net == null)
                    return;

                if (!IsCrateLooted(storageContainer.net.ID.Value))
                {
                    double cratePoint;

                    if (ins._config.economicsConfig.crates.TryGetValue(storageContainer.PrefabName, out cratePoint))
                        EconomyManager.AddBalance(userId, cratePoint);

                    lootedContainersUids.Add(storageContainer.net.ID.Value);
                }

                UpdateCountOfUnlootedCrates();
            }

            internal static bool IsCrateLooted(ulong netID)
            {
                return lootedContainersUids.Contains(netID);
            }

            internal static bool IsEventCrate(ulong netID)
            {
                return GetContainerDataByNetId(netID) != null;
            }

            internal static StorageContainerData GetContainerDataByNetId(ulong netID)
            {
                return storageContainers.FirstOrDefault(x => x != null && x.storageContainer.IsExists() && x.storageContainer.net != null && x.storageContainer.net.ID.Value == netID);
            }

            internal static HashSet<ulong> GetEventCratesNetIDs()
            {
                HashSet<ulong> eventCrates = new HashSet<ulong>();

                foreach (StorageContainerData storageContainerData in storageContainers)
                    if (storageContainerData != null && storageContainerData.storageContainer != null && storageContainerData.storageContainer.net != null)
                        eventCrates.Add(storageContainerData.storageContainer.net.ID.Value);

                return eventCrates;
            }

            internal static CrateConfig GetCrateConfigByPresetName(string presetName)
            {
                return ins._config.crateConfigs.FirstOrDefault(x => x.presetName == presetName);
            }

            internal static void InitialLootManagerUpdate()
            {
                LootPrefabController.FindPrefabs();
            }

            internal static void UpdateItemContainer(ItemContainer itemContainer, BaseLootTableConfig lootTableConfig, bool deleteItems = false)
            {
                UpdateLootTable(itemContainer, lootTableConfig, deleteItems);
            }

            internal static void UpdateStorageContainer(StorageContainer storageContainer, CrateConfig crateConfig)
            {
                storageContainer.onlyAcceptCategory = ItemCategory.All;
                UpdateLootTable(storageContainer.inventory, crateConfig.lootTableConfig, false);
                storageContainers.Add(new StorageContainerData(storageContainer, crateConfig.presetName));
            }

            internal static void UpdateLootContainer(LootContainer lootContainer, CrateConfig crateConfig)
            {
                HackableLockedCrate hackableLockedCrate = lootContainer as HackableLockedCrate;
                if (hackableLockedCrate != null)
                {
                    if (hackableLockedCrate.mapMarkerInstance.IsExists())
                    {
                        hackableLockedCrate.mapMarkerInstance.Kill();
                        hackableLockedCrate.mapMarkerInstance = null;
                    }

                    hackableLockedCrate.Invoke(() => DelayUpdateHackableLockedCrate(hackableLockedCrate, crateConfig), 1f);
                }

                SupplyDrop supplyDrop = lootContainer as SupplyDrop;
                if (supplyDrop != null)
                {
                    supplyDrop.RemoveParachute();
                    supplyDrop.MakeLootable();
                }

                FreeableLootContainer freeableLootContainer = lootContainer as FreeableLootContainer;
                if (freeableLootContainer != null)
                    freeableLootContainer.SetFlag(BaseEntity.Flags.Reserved8, false);

                lootContainer.Invoke(() => UpdateLootTable(lootContainer.inventory, crateConfig.lootTableConfig, crateConfig.lootTableConfig.clearDefaultItemList), 2f);
                storageContainers.Add(new StorageContainerData(lootContainer, crateConfig.presetName));
            }

            static void DelayUpdateHackableLockedCrate(HackableLockedCrate hackableLockedCrate, CrateConfig crateConfig)
            {
                if (hackableLockedCrate == null || crateConfig.hackTime < 0)
                    return;

                hackableLockedCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - crateConfig.hackTime;
                UpdateLootTable(hackableLockedCrate.inventory, crateConfig.lootTableConfig, crateConfig.lootTableConfig.clearDefaultItemList);
                hackableLockedCrate.InvokeRepeating(() => hackableLockedCrate.SendNetworkUpdate(), 1f, 1f);
            }

            internal static void UpdateCrateHackTime(HackableLockedCrate hackableLockedCrate, string cratePresetName)
            {
                CrateConfig crateConfig = GetCrateConfigByPresetName(cratePresetName);

                if (crateConfig.hackTime < 0)
                    return;

                hackableLockedCrate.Invoke(() => hackableLockedCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - crateConfig.hackTime, 1.1f);
            }

            static void UpdateLootTable(ItemContainer itemContainer, BaseLootTableConfig lootTableConfig, bool clearContainer)
            {
                if (itemContainer == null)
                    return;

                if (clearContainer)
                    ClearItemsContainer(itemContainer);

                LootPrefabController.TryAddLootFromPrefabs(itemContainer, lootTableConfig.prefabConfigs);
                RandomItemsFiller.TryAddItemsToContainer(itemContainer, lootTableConfig.randomItemsConfig);

                if (itemContainer.capacity < itemContainer.itemList.Count)
                    itemContainer.capacity = itemContainer.itemList.Count;
            }

            static void ClearItemsContainer(ItemContainer container)
            {
                for (int i = container.itemList.Count - 1; i >= 0; i--)
                {
                    Item item = container.itemList[i];
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }

            internal static void ClearLootData(bool shoudKillCrates = false)
            {
                if (shoudKillCrates)
                    foreach (StorageContainerData storageContainerData in storageContainers)
                        if (storageContainerData != null && storageContainerData.storageContainer.IsExists())
                            storageContainerData.storageContainer.Kill();

                lootedContainersUids.Clear();
                storageContainers.Clear();
            }

            class LootPrefabController
            {
                static HashSet<LootPrefabController> lootPrefabDatas = new HashSet<LootPrefabController>();

                string prefabName;
                LootContainer.LootSpawnSlot[] lootSpawnSlot;
                LootSpawn lootDefinition;
                int maxDefinitionsToSpawn;
                int scrapAmount;

                internal static void TryAddLootFromPrefabs(ItemContainer itemContainer, PrefabLootTableConfigs prefabLootTableConfig)
                {
                    if (!prefabLootTableConfig.isEnable)
                        return;

                    PrefabConfig prefabConfig = prefabLootTableConfig.prefabs.GetRandom();

                    if (prefabConfig == null)
                        return;

                    int multiplicator = UnityEngine.Random.Range(prefabConfig.minLootScale, prefabConfig.maxLootScale + 1);
                    TryFillContainerByPrefab(itemContainer, prefabConfig.prefabName, multiplicator);
                }

                internal static void FindPrefabs()
                {
                    foreach (CrateConfig crateConfig in ins._config.crateConfigs.Where(x => x.lootTableConfig.prefabConfigs.isEnable))
                        foreach (PrefabConfig prefabConfig in crateConfig.lootTableConfig.prefabConfigs.prefabs)
                            TrySaveLootPrefab(prefabConfig.prefabName);

                    foreach (NpcConfig npcConfig in ins._config.npcConfigs.Where(x => x.lootTableConfig.prefabConfigs.isEnable))
                        foreach (PrefabConfig prefabConfig in npcConfig.lootTableConfig.prefabConfigs.prefabs)
                            TrySaveLootPrefab(prefabConfig.prefabName);
                }

                internal static void TrySaveLootPrefab(string prefabName)
                {
                    if (lootPrefabDatas.Any(x => x.prefabName == prefabName))
                        return;

                    GameObject gameObject = GameManager.server.FindPrefab(prefabName);

                    if (gameObject == null)
                        return;

                    LootContainer lootContainer = gameObject.GetComponent<LootContainer>();

                    if (lootContainer != null)
                    {
                        SaveLootPrefabData(prefabName, lootContainer.LootSpawnSlots, lootContainer.scrapAmount, lootContainer.lootDefinition, lootContainer.maxDefinitionsToSpawn);
                        return;
                    }

                    global::HumanNPC humanNPC = gameObject.GetComponent<global::HumanNPC>();

                    if (humanNPC != null && humanNPC.LootSpawnSlots.Length > 0)
                    {
                        SaveLootPrefabData(prefabName, humanNPC.LootSpawnSlots, 0);
                        return;
                    }

                    ScarecrowNPC scarecrowNPC = gameObject.GetComponent<ScarecrowNPC>();

                    if (scarecrowNPC != null && scarecrowNPC.LootSpawnSlots.Length > 0)
                    {
                        SaveLootPrefabData(prefabName, scarecrowNPC.LootSpawnSlots, 0);
                        return;
                    }
                }

                internal static void SaveLootPrefabData(string prefabName, LootContainer.LootSpawnSlot[] lootSpawnSlot, int scrapAmount, LootSpawn lootDefinition = null, int maxDefinitionsToSpawn = 0)
                {
                    LootPrefabController lootPrefabData = new LootPrefabController
                    {
                        prefabName = prefabName,
                        lootSpawnSlot = lootSpawnSlot,
                        lootDefinition = lootDefinition,
                        maxDefinitionsToSpawn = maxDefinitionsToSpawn,
                        scrapAmount = scrapAmount
                    };

                    lootPrefabDatas.Add(lootPrefabData);
                }

                internal static void TryFillContainerByPrefab(ItemContainer itemContainer, string prefabName, int multiplicator)
                {
                    LootPrefabController lootPrefabData = GetDataForPrefabName(prefabName);

                    if (lootPrefabData != null)
                        for (int i = 0; i < multiplicator; i++)
                            lootPrefabData.SpawnPrefabLootInCrate(itemContainer);
                }

                static LootPrefabController GetDataForPrefabName(string prefabName)
                {
                    return lootPrefabDatas.FirstOrDefault(x => x.prefabName == prefabName);
                }

                void SpawnPrefabLootInCrate(ItemContainer itemContainer)
                {
                    if (lootSpawnSlot != null && lootSpawnSlot.Length > 0)
                    {
                        foreach (LootContainer.LootSpawnSlot lootSpawnSlot in lootSpawnSlot)
                            for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                                if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                                    lootSpawnSlot.definition.SpawnIntoContainer(itemContainer);
                    }
                    else if (lootDefinition != null)
                    {
                        for (int i = 0; i < maxDefinitionsToSpawn; i++)
                            lootDefinition.SpawnIntoContainer(itemContainer);
                    }

                    GenerateScrap(itemContainer);
                }

                void GenerateScrap(ItemContainer itemContainer)
                {
                    if (scrapAmount <= 0)
                        return;

                    Item item = ItemManager.CreateByName("scrap", scrapAmount, 0);

                    if (item == null)
                        return;

                    if (!item.MoveToContainer(itemContainer))
                        item.Remove();
                }
            }

            static class RandomItemsFiller
            {
                static Dictionary<char, GrowableGenetics.GeneType> charToGene = new Dictionary<char, GrowableGenetics.GeneType>
                {
                    ['g'] = GrowableGenetics.GeneType.GrowthSpeed,
                    ['y'] = GrowableGenetics.GeneType.Yield,
                    ['h'] = GrowableGenetics.GeneType.Hardiness,
                    ['w'] = GrowableGenetics.GeneType.WaterRequirement,
                };

                internal static void TryAddItemsToContainer(ItemContainer itemContainer, RandomItemsConfig randomItemsConfig)
                {
                    if (!randomItemsConfig.isEnable)
                        return;

                    HashSet<int> includeItemIndexes = new HashSet<int>();
                    int targetItemsCount = UnityEngine.Random.Range(randomItemsConfig.minItemsAmount, randomItemsConfig.maxItemsAmount + 1);

                    while (includeItemIndexes.Count < targetItemsCount || randomItemsConfig.useRandomItemsNumber)
                    {
                        if (!randomItemsConfig.items.Any(x => x.chance >= 0.1f && !includeItemIndexes.Contains(randomItemsConfig.items.IndexOf(x))))
                            break;

                        for (int i = 0; i < randomItemsConfig.items.Count; i++)
                        {
                            if (includeItemIndexes.Contains(i))
                                continue;

                            LootItemConfig lootItemConfig = randomItemsConfig.items[i];
                            float chance = UnityEngine.Random.Range(0.0f, 100.0f);

                            if (chance <= lootItemConfig.chance)
                            {
                                Item item = CreateItem(lootItemConfig);
                                includeItemIndexes.Add(i);

                                if (item == null || !item.MoveToContainer(itemContainer))
                                    item.Remove();

                                if (!randomItemsConfig.useRandomItemsNumber && includeItemIndexes.Count == targetItemsCount)
                                    return;
                            }
                        }

                        if (randomItemsConfig.useRandomItemsNumber)
                            break;
                    }
                }

                internal static Item CreateItem(LootItemConfig lootItemConfig)
                {
                    int amount = UnityEngine.Random.Range(lootItemConfig.minAmount, lootItemConfig.maxAmount + 1);
                    return CreateItem(lootItemConfig, amount);
                }

                internal static Item CreateItem(ItemConfig itemConfig, int amount)
                {
                    Item item = null;

                    if (itemConfig.isBlueprint)
                    {
                        item = ItemManager.CreateByName("blueprintbase");
                        item.blueprintTarget = ItemManager.FindItemDefinition(itemConfig.shortname).itemid;
                    }
                    else
                        item = ItemManager.CreateByName(itemConfig.shortname, amount, itemConfig.skin);

                    if (item == null)
                    {
                        ins.PrintWarning($"Failed to create item! ({itemConfig.shortname})");
                        return null;
                    }

                    if (!string.IsNullOrEmpty(itemConfig.name))
                        item.name = itemConfig.name;

                    if (itemConfig.genomes != null && itemConfig.genomes.Count > 0)
                    {
                        string genome = itemConfig.genomes.GetRandom();
                        UpdateGenome(item, genome);
                    }

                    return item;
                }

                static void UpdateGenome(Item item, string genome)
                {
                    genome = genome.ToLower();
                    GrowableGenes growableGenes = new GrowableGenes();

                    for (int i = 0; i < 6 && i < genome.Length; ++i)
                    {
                        GrowableGenetics.GeneType geneType;

                        if (!charToGene.TryGetValue(genome[i], out geneType))
                            geneType = GrowableGenetics.GeneType.Empty;

                        growableGenes.Genes[i].Set(geneType, true);
                        GrowableGeneEncoding.EncodeGenesToItem(GrowableGeneEncoding.EncodeGenesToInt(growableGenes), item);
                    }

                }
            }
        }

        class StorageContainerData
        {
            public string presetName;
            public StorageContainer storageContainer;

            public StorageContainerData(StorageContainer storageContainer, string presetName)
            {
                this.presetName = presetName;
                this.storageContainer = storageContainer;
            }
        }

        static class EconomyManager
        {
            static string winHookName = "OnCaravanEventWin";
            static readonly Dictionary<ulong, double> playersBalance = new Dictionary<ulong, double>();

            internal static void AddBalance(ulong playerId, double balance)
            {
                if (balance == 0 || playerId == 0)
                    return;

                if (playersBalance.ContainsKey(playerId))
                    playersBalance[playerId] += balance;
                else
                    playersBalance.Add(playerId, balance);
            }

            internal static void OnEventEnd()
            {
                DefineEventWinner();

                if (!ins._config.economicsConfig.enable || playersBalance.Count == 0)
                {
                    playersBalance.Clear();
                    return;
                }

                SendBalanceToPlayers();
                playersBalance.Clear();
            }

            static void DefineEventWinner()
            {
                var winnerPair = playersBalance.Max(x => (float)x.Value);

                if (winnerPair.Value > 0)
                    Interface.CallHook(winHookName, winnerPair.Key);

                if (winnerPair.Value >= ins._config.economicsConfig.minCommandPoint)
                    foreach (string command in ins._config.economicsConfig.commands)
                        ins.Server.Command(command.Replace("{steamid}", $"{winnerPair.Key}"));
            }

            static void SendBalanceToPlayers()
            {
                foreach (KeyValuePair<ulong, double> pair in playersBalance)
                    SendBalanceToPlayer(pair.Key, pair.Value);
            }

            static void SendBalanceToPlayer(ulong userID, double amount)
            {
                if (amount < ins._config.economicsConfig.minEconomyPiont)
                    return;

                int intAmount = Convert.ToInt32(amount);

                if (intAmount <= 0)
                    return;

                if (ins._config.economicsConfig.plugins.Contains("Economics") && ins.plugins.Exists("Economics"))
                    ins.Economics.Call("Deposit", userID.ToString(), amount);

                if (ins._config.economicsConfig.plugins.Contains("Server Rewards") && ins.plugins.Exists("ServerRewards"))
                    ins.ServerRewards.Call("AddPoints", userID, intAmount);

                if (ins._config.economicsConfig.plugins.Contains("IQEconomic") && ins.plugins.Exists("IQEconomic"))
                    ins.IQEconomic.Call("API_SET_BALANCE", userID, intAmount);

                BasePlayer player = BasePlayer.FindByID(userID);
                if (player != null)
                    NotifyManager.SendMessageToPlayer(player, "SendEconomy", ins._config.notifyConfig.prefix, amount);
            }
        }

        class CarCustomizator : FacepunchBehaviour
        {
            ModularCar modularCar;

            internal static void DecorateModularCar(ModularCar modularCar, string customizationPreset)
            {
                if (customizationPreset == "")
                    return;

                CarCustomizationData carCustomizationData = LoadCustomizationProfile(customizationPreset);

                if (carCustomizationData == null || carCustomizationData.decorEntityConfigs == null)
                {
                    NotifyManager.PrintError(null, "DataFileNotFound_Exeption", customizationPreset);
                    return;
                }

                CarCustomizator carCustomizator = modularCar.gameObject.AddComponent<CarCustomizator>();
                carCustomizator.Init(modularCar, carCustomizationData);
            }

            static CarCustomizationData LoadCustomizationProfile(string profileName)
            {
                string filePath = $"{ins.Name}/Carriages/{profileName}";
                return Interface.Oxide.DataFileSystem.ReadObject<CarCustomizationData>(filePath);
            }

            internal void Init(ModularCar modularCar, CarCustomizationData carCustomizationData)
            {
                this.modularCar = modularCar;
                DecorateCar(carCustomizationData);
            }

            void DecorateCar(CarCustomizationData carCustomizationData)
            {
                if (carCustomizationData.decorEntityConfigs != null)
                {
                    List<DecorEntityData> decorEntityConfigList = carCustomizationData.decorEntityConfigs.ToList();

                    for (int i = 0; i < decorEntityConfigList.Count; i++)
                    {
                        DecorEntityData decorEntityConfig = decorEntityConfigList[i];
                        SpawnDecorEntity(decorEntityConfig);
                    }
                }
            }

            void SpawnDecorEntity(DecorEntityData decorEntityConfig)
            {
                Vector3 localPosition = decorEntityConfig.position.ToVector3();
                Vector3 localRotation = decorEntityConfig.rotation.ToVector3();

                BaseEntity entity = BuildManager.SpawnChildEntity(modularCar, decorEntityConfig.prefabName, localPosition, localRotation, decorEntityConfig.skin, decorEntityConfig.prefabName.Contains("fridge") ? false : true);

                if (entity != null)
                    UpdateDecorEntity(entity, decorEntityConfig);
            }

            void UpdateDecorEntity(BaseEntity entity, DecorEntityData decorEntityConfig)
            {
                if (!decorEntityConfig.prefabName.Contains("fridge"))
                {
                    entity.gameObject.layer = 0;
                    entity.SetFlag(BaseEntity.Flags.Busy, true);
                }

                entity.SetFlag(BaseEntity.Flags.Locked, true);

                if (entity.ShortPrefabName == "skulltrophy.deployed")
                    entity.SetFlag(BaseEntity.Flags.Reserved1, true);

                if (entity.PrefabName.Contains("tools") || entity.PrefabName.Contains("weapons"))
                    entity.SetFlag(BaseEntity.Flags.Reserved8, true);

                entity.SendNetworkUpdate();
            }

            internal static class MapSaver
            {
                static Dictionary<string, string> colliderPrefabNames = new Dictionary<string, string>
                {
                    ["fence_a"] = "assets/prefabs/misc/xmas/icewalls/icewall.prefab",
                    ["christmas_present_LOD0"] = "assets/prefabs/misc/xmas/sleigh/presentdrop.prefab",
                    ["snowman_LOD1"] = "assets/prefabs/misc/xmas/snowman/snowman.deployed.prefab",
                    ["giftbox_LOD0"] = "assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab"
                };

                internal static void CreateOrAddNewWagonToData(string customizationPresetName)
                {
                    CarCustomizationData carCustomizationData = SaveCarFromMap(customizationPresetName);
                    SaveProfile(carCustomizationData, customizationPresetName);
                }

                static CarCustomizationData SaveCarFromMap(string customizationPresetName)
                {
                    CarCustomizationData carCustomizationData = new CarCustomizationData
                    {
                        decorEntityConfigs = new HashSet<DecorEntityData>(),
                    };

                    CheckAndSaveColliders(ref carCustomizationData, customizationPresetName);
                    return carCustomizationData;
                }

                static void CheckAndSaveColliders(ref CarCustomizationData carCustomizationData, string customizationPresetName)
                {
                    List<Collider> colliders = Physics.OverlapSphere(Vector3.zero, 10).OrderBy(x => x.transform.position.z);

                    foreach (Collider collider in colliders)
                        TrySaveCollder(collider, ref carCustomizationData, customizationPresetName);
                }

                static void TrySaveCollder(Collider collider, ref CarCustomizationData carCustomizationData, string customizationPresetName)
                {
                    BaseEntity entity = collider.ToBaseEntity();

                    if (entity == null)
                        SaveCollider(collider, ref carCustomizationData);
                    else if (IsCustomizingEntity(entity, customizationPresetName))
                        SaveRegularEntity(entity, ref carCustomizationData);
                }

                static bool IsCustomizingEntity(BaseEntity entity, string customizationPresetName)
                {
                    if (entity == null)
                        return false;
                    else if (entity is ResourceEntity || entity is BasePlayer)
                        return false;

                    return true;
                }

                static void SaveRegularEntity(BaseEntity entity, ref CarCustomizationData wagonCustomizationData)
                {
                    DecorEntityData decorLocationConfig = GetDecorEntityConfig(entity);

                    if (decorLocationConfig != null && !wagonCustomizationData.decorEntityConfigs.Any(x => x.prefabName == decorLocationConfig.prefabName && x.position == decorLocationConfig.position && x.rotation == decorLocationConfig.rotation))
                        wagonCustomizationData.decorEntityConfigs.Add(decorLocationConfig);
                }

                static DecorEntityData GetDecorEntityConfig(BaseEntity entity)
                {
                    ulong skin = entity.skinID;

                    return new DecorEntityData
                    {
                        prefabName = entity.PrefabName,
                        skin = skin,
                        position = $"({entity.transform.position.x}, {entity.transform.position.y}, {entity.transform.position.z})",
                        rotation = entity.transform.eulerAngles.ToString()
                    };
                }

                static void SaveCollider(Collider collider, ref CarCustomizationData wagonCustomizationData)
                {
                    DecorEntityData colliderEntityConfig = GetColliderConfigAsBaseEntity(collider);

                    if (colliderEntityConfig != null && !wagonCustomizationData.decorEntityConfigs.Any(x => x.prefabName == colliderEntityConfig.prefabName && x.position == colliderEntityConfig.position && x.rotation == colliderEntityConfig.rotation))
                        wagonCustomizationData.decorEntityConfigs.Add(colliderEntityConfig);
                }

                static DecorEntityData GetColliderConfigAsBaseEntity(Collider collider)
                {
                    string prefabName = "";

                    if (!colliderPrefabNames.TryGetValue(collider.name, out prefabName))
                        return null;

                    return new DecorEntityData
                    {
                        prefabName = prefabName,
                        skin = 0,
                        position = $"({collider.transform.position.x}, {collider.transform.position.y}, {collider.transform.position.z})",
                        rotation = collider.transform.eulerAngles.ToString()
                    };
                }

                static void SaveProfile(CarCustomizationData customizeData, string name)
                {
                    Interface.Oxide.DataFileSystem.WriteObject($"{ins.Name}/{name}", customizeData);
                }
            }
        }

        class PathRecorder : FacepunchBehaviour
        {
            static HashSet<PathRecorder> customRouteSavers = new HashSet<PathRecorder>();
            BasePlayer player;
            RidableHorse ridableHorse;
            List<Vector3> positions = new List<Vector3>();

            static PathRecorder GetCustomRouteSavingByUserId(ulong userId)
            {
                return customRouteSavers.FirstOrDefault(x => x != null && x.ridableHorse.IsExists() && x.player != null && x.player.userID == userId);
            }

            internal static void StartRecordingRoute(BasePlayer player)
            {
                if (GetCustomRouteSavingByUserId(player.userID) != null)
                    return;

                RidableHorse ridableHorse = BuildManager.SpawnRegularEntity("assets/rust.ai/nextai/testridablehorse.prefab", player.transform.position, player.eyes.GetLookRotation()) as RidableHorse;
                ridableHorse.AttemptMount(player);
                PathRecorder customRouteSaving = ridableHorse.gameObject.AddComponent<PathRecorder>();
                customRouteSaving.Init(player, ridableHorse);
                customRouteSavers.Add(customRouteSaving);
            }

            internal static void TrySaveRoute(ulong userId, string pathName)
            {
                PathRecorder customRouteSaving = GetCustomRouteSavingByUserId(userId);

                if (customRouteSaving != null)
                    customRouteSaving.SavePath(pathName);
            }

            internal static void TryCancelRoute(ulong userId)
            {
                PathRecorder customRouteSaving = GetCustomRouteSavingByUserId(userId);

                if (customRouteSaving != null)
                    customRouteSaving.KillHorse();
            }

            void Init(BasePlayer player, RidableHorse ridableHorse)
            {
                this.player = player;
                this.ridableHorse = ridableHorse;

                TryAddFindPositionOrDestroy();
            }

            void FixedUpdate()
            {
                if (player == null || !player.isMounted)
                {
                    KillHorse();
                    return;
                }

                Vector3 lastPosition = positions.Last();
                float distance = Vector3.Distance(lastPosition, ridableHorse.transform.position);

                if (distance > 10)
                    TryAddFindPositionOrDestroy();
            }

            void TryAddFindPositionOrDestroy()
            {
                Vector3 newPosition = ridableHorse.transform.position;

                NavMeshHit navMeshHit;
                if (!PositionDefiner.GetNavmeshInPoint(newPosition, 2, out navMeshHit))
                {
                    NotifyManager.PrintError(player, "NavMesh_Exeption");
                    KillHorse();
                    return;
                }

                else
                    positions.Add(navMeshHit.position);
            }

            void SavePath(string pathName)
            {
                float pathLength = GetPathLength();
                if (pathLength < ins._config.pathConfig.minRoadLength)
                {
                    NotifyManager.SendMessageToPlayer(player, "CustomRouteTooShort", ins._config.notifyConfig.prefix);
                    return;
                }
                List<string> path = new List<string>();

                foreach (Vector3 point in positions)
                    path.Add(point.ToString());

                CustomRouteData customRouteData = new CustomRouteData
                {
                    points = path
                };

                Interface.Oxide.DataFileSystem.WriteObject($"{ins.Name}/Custom routes/{pathName}", customRouteData);
                NotifyManager.SendMessageToPlayer(player, "CustomRouteSuccess", ins._config.notifyConfig.prefix);
                ins._config.pathConfig.customPathConfig.customRoutesPresets.Add(pathName);
                KillHorse();
                ins.SaveConfig();
            }

            float GetPathLength()
            {
                float length = 0;

                for (int i = 0; i < positions.Count - 1; i++)
                {
                    Vector3 thisPoint = positions[i];
                    Vector3 nextPoint = positions[i + 1];
                    float distance = Vector3.Distance(thisPoint, nextPoint);
                    length += distance;
                }

                return length;
            }

            void KillHorse()
            {
                if (ridableHorse.IsExists())
                    ridableHorse.Kill();
            }
        }

        class CollisionDisabler : FacepunchBehaviour
        {
            HashSet<Collider> colliders = new HashSet<Collider>();

            internal static void AttachCollisonDisabler(BaseEntity baseEntity)
            {
                baseEntity.Invoke(() =>
                {
                    CollisionDisabler collisionDisabler = baseEntity.gameObject.AddComponent<CollisionDisabler>();

                    foreach (Collider collider in baseEntity.gameObject.GetComponentsInChildren<Collider>())
                        if (collider != null)
                            collisionDisabler.colliders.Add(collider);
                }, 1f);
            }

            void OnCollisionEnter(Collision collision)
            {
                if (collision == null || collision.collider == null)
                    return;

                BaseEntity entity = collision.GetEntity();
                if (entity == null)
                {
                    if (collision.collider.name != "Terrain" && collision.collider.name != "Road Mesh")
                        IgnoreCollider(collision.collider);

                    return;
                }

                if (entity.net != null && ((entity is HelicopterDebris or LootContainer or TravellingVendor or DroppedItemContainer or RidableHorse) || HorseCarriage.GetCarriageByModularCarNetID(entity.net.ID.Value) != null || HorseCarriage.GetCarriageByModularChildEntity(entity) != null))
                {
                    IgnoreCollider(collision.collider);
                    return;
                }

                if (entity is TreeEntity or ResourceEntity or JunkPile or Barricade or HotAirBalloon or BasePortal)
                {
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    return;
                }

                BaseVehicle baseVehicle = entity as BaseVehicle;
                if (baseVehicle == null)
                    baseVehicle = entity.GetParentEntity() as BaseVehicle;

                BaseVehicleModule baseVehicleModule = entity as BaseVehicleModule;
                if (baseVehicleModule != null)
                    baseVehicle = baseVehicleModule.Vehicle;

                if (baseVehicle != null && entity is not TimedExplosive)
                {

                    BasePlayer driver = baseVehicle.GetDriver();

                    if (driver.IsRealPlayer())
                        ins.eventController.OnEventAttacked(driver);

                    if (baseVehicle is TrainEngine && baseVehicle.net != null)
                    {
                        IgnoreCollider(collision.collider);

                        if (ins.plugins.Exists("ArmoredTrain") && (bool)ins.ArmoredTrain.Call("IsTrainWagon", baseVehicle.net.ID.Value))
                            ins.eventController.OnEventAttacked(null);
                    }
                    else
                    {
                        ModularCar modularCar = baseVehicle as ModularCar;

                        if (modularCar != null)
                        {
                            StorageContainer storageContainer = modularCar.GetComponentsInChildren<StorageContainer>().FirstOrDefault(x => x.name == "modular_car_fuel_storage");

                            if (!BaseMountable.AllMountables.Contains(modularCar) || !modularCar.HasAnyEngines())
                            {
                                IgnoreCollider(collision.collider);
                                return;
                            }

                            if (storageContainer != null)
                                storageContainer.inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab", storageContainer.transform.position, storageContainer.transform.rotation, 0);
                        }

                        baseVehicle.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                }
            }

            void IgnoreCollider(Collider otherCollider)
            {
                foreach (Collider collider in colliders)
                    if (collider != null)
                        Physics.IgnoreCollision(collider, otherCollider);
            }
        }
        #endregion Classes 

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "Ивент в данный момент активен, сначала завершите текущий ивент (<color=#ce3f27>/caravanstop</color>)!",
                ["ConfigurationNotFound_Exeption"] = "<color=#ce3f27>Не удалось</color> найти конфигурацию ивента!",
                ["PresetNotFound_Exeption"] = "Пресет {0} <color=#ce3f27>не найден</color> в конфиге!",
                ["NavMesh_Exeption"] = "Навигационная сетка не найдена!",

                ["SuccessfullyLaunched"] = "Ивент <color=#738d43>успешно</color> запущен!",
                ["PreStart"] = "{0} Осталось <color=#738d43>{1}</color> до начала ивента!",
                ["EventStart"] = "{0} <color=#738d43>{1}</color> был обнаружен в квадрате <color=#738d43>{2}</color>",
                ["Finish"] = "{0} Ивент <color=#ce3f27>окончен</color>!",

                ["DamageDistance"] = "{0} Подойдите <color=#ce3f27>ближе</color>!",
                ["CaravanAttacked"] = "{0} {1} <color=#ce3f27>остановил</color> {2}!",
                ["StartMoving"] = "{0} <color=#738d43>{1}</color> возобновил движение!",
                ["CantLoot"] = "{0} Для того чтобы залутать караван убейте <color=#ce3f27>всех нпс</color>!",
                ["Looted"] = "{0} <color=#738d43>{1}</color> был <color=#ce3f27>ограблен</color>!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",

                ["Hours"] = "ч.",
                ["Minutes"] = "м.",
                ["Seconds"] = "с.",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",
                ["CustomRouteDescription"] = "{0} Для записи кастомного маршрута встаньте на землю и введите команду /caravanpathstart. Двигайтесь по маршруту и введите команду /caravanpathsave <routeName>. Для отмены маршрута используйте команду /caravanpathcancel",
                ["CustomRouteTooShort"] = "{0} Кастомный маршрут слишком короткий",
                ["CustomRouteSuccess"] = "{0} Кастомный маршрут успешно сохранен",

                ["PveMode_BlockAction"] = "{0} Вы <color=#ce3f27>не можете</color> взаимодействовать с ивентом из-за кулдауна!",
                ["PveMode_YouAreNoOwner"] = "{0} Вы <color=#ce3f27>не являетесь</color> владельцем ивента!",

            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "This event is active now. Finish the current event! (<color=#ce3f27>/caravanstop</color>)!",
                ["ConfigurationNotFound_Exeption"] = "The event configuration <color=#ce3f27>could not</color> be found!",
                ["PresetNotFound_Exeption"] = "{0} preset was <color=#ce3f27>not found</color> in the config!",
                ["FileNotFound_Exeption"] = "Data file not found or corrupted! ({0}.json)!",
                ["DataFileNotFound_Exeption"] = "Could not find a data file for customization ({0}.json). Empty the [Customization preset] in the config or upload the data file",
                ["RouteNotFound_Exeption"] = "The route could not be generated! Try to increase the minimum road length or change the route type!",
                ["NavMesh_Exeption"] = "The navigation grid was not found!",

                ["SuccessfullyLaunched"] = "The event has been <color=#738d43>successfully</color> launched!",
                ["PreStart"] = "{0} The event will start in <color=#738d43>{1}</color>",
                ["EventStart"] = "{0} <color=#738d43>{1}</color> is spawned at grid <color=#738d43>{2}</color>",
                ["Finish"] = "{0} The event has <color=#ce3f27>ended</color>!",

                ["DamageDistance"] = "{0} Come <color=#ce3f27>closer</color>!",
                ["CaravanAttacked"] = "{0} {1} <color=#ce3f27>stopped</color> {2}!",
                ["StartMoving"] = "{0} <color=#738d43>{1}</color> resumed movement!",
                ["CantLoot"] = "{0} It is necessary to stop the caravan and kill the <color=#ce3f27>guards</color>!",
                ["Looted"] = "{0} <color=#738d43>{1}</color> has been <color=#ce3f27>looted</color>!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have gone out</color> the PVP zone, now other players <color=#738d43>can’t damage</color> you!",

                ["Hours"] = "h.",
                ["Minutes"] = "m.",
                ["Seconds"] = "s.",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> a reward of <color=#55aaff>{1}</color> for participating in the event",
                ["Marker_EventOwner"] = "Event Owner: {0}",
                ["CustomRouteDescription"] = "{0} To record a custom route, stand on the ground and enter the command /caravanpathstart. Follow the route and enter the command /caravanpathsave <routeName>. To cancel the route, use the command /caravanpathcancel",
                ["CustomRouteTooShort"] = "{0} The custom route is too short",
                ["CustomRouteSuccess"] = "{0} Custom route successfully saved",

                ["EventStart_Log"] = "The event has begun! (Preset displayName - {0})",
                ["EventStop_Log"] = "The event has ended!",
                ["RouteСachingStart_Log"] = "Route caching has started!",
                ["RouteСachingStop_Log"] = "Route caching has ended! The number of routes: {0}",

                ["PveMode_BlockAction"] = "{0} You <color=#ce3f27>can't interact</color> with the event because of the cooldown!",
                ["PveMode_YouAreNoOwner"] = "{0} You are not the <color=#ce3f27>owner</color> of the event!",
            }, this);
        }

        internal static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        internal static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Data
        public class CarCustomizationData
        {
            [JsonProperty("List of decorations")] public HashSet<DecorEntityData> decorEntityConfigs { get; set; }
        }

        public class DecorEntityData
        {
            [JsonProperty("Prefab")] public string prefabName { get; set; }
            [JsonProperty("Skin")] public ulong skin { get; set; }
            [JsonProperty("Position")] public string position { get; set; }
            [JsonProperty("Rotation")] public string rotation { get; set; }
        }

        public class CustomRouteData
        {
            [JsonProperty("Points")] public List<string> points { get; set; }
        }
        #endregion Data

        #region Config
        private PluginConfig _config;

        protected override void LoadDefaultConfig()
        {
            _config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        public class MainConfig
        {
            [JsonProperty(en ? "Enable automatic event holding [true/false]" : "Включить автоматическое проведение ивента [true/false]")] public bool isAutoEvent { get; set; }
            [JsonProperty(en ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]")] public int minTimeBetweenEvents { get; set; }
            [JsonProperty(en ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]")] public int maxTimeBetweenEvents { get; set; }
            [JsonProperty(en ? "The time between receiving a chat notification and the start of the event [sec.]" : "Время до начала ивента после сообщения в чате [sec.]")] public int preStartTime { get; set; }
            [JsonProperty(en ? "The time until the end of the event after the destruction or looting of all carriages [sec.]" : "Время до завершения ивента после уничтожения или лутания всех повозок [sec.]")] public int endAfterDeathTime { get; set; }
            [JsonProperty(en ? "Enable logging of the start and end of the event? [true/false]" : "Включить логирование начала и окончания ивента? [true/false]")] public bool enableStartStopLogs { get; set; }
        }

        public class BehaviorConfig
        {
            [JsonProperty(en ? "The time for which the caravan becomes aggressive after it has been attacked (-1 - the caravan is always aggressive)" : "Время на которое караван становится агрессивным после того как был атакован (-1 - караван всегда агрессивен)")] public int agressiveTime { get; set; }
            [JsonProperty(en ? "The duration of the stop after the attack" : "Длительность остановки после нападения")] public int stopTime { get; set; }
        }

        public class DamageConfig
        {
            [JsonProperty(en ? "Allow player SamSites to attack the balloons of the event? [true/false]" : "Разрешить SamSite игроков атаковать воздушные шары ивента? [true/false]")] public bool isPlayerSamSiteCanAttackEvent { get; set; }
        }

        public class LootConfig
        {
            [JsonProperty(en ? "When the carriage is destroyed, loot falls to the ground [true/false]" : "При уничтожении повозки лут будет падать на землю [true/false]")] public bool dropLoot { get; set; }
            [JsonProperty(en ? "Percentage of loot loss when destroying a carriage [0.0-1.0]" : "Процент потери лута при уничтожении повозки [0.0-1.0]")] public float lootLossPercent { get; set; }
            [JsonProperty(en ? "Prohibit looting crates if the caravan is moving [true/false]" : "Запретить лутать ящики, если караван движется [true/false]")] public bool blockLootingByMove { get; set; }
            [JsonProperty(en ? "Prohibit looting crates if NPCs/riders are alive [true/false]" : "Запретить лутать ящики, если живы нпс/всадники [true/false]")] public bool blockLootingByNpcs { get; set; }
        }

        public class PathConfig
        {
            [JsonProperty(en ? "Type of caravan routes (0 - standard (fast generation), 1 - experimental (multiple roads are used), 2 - custom)" : "Тип маршрутов каравана (0 - стандартный (быстрая генерация), 1 - экспериментальный (используется несколько дорог), 2 - кастомый)")] public int pathType { get; set; }
            [JsonProperty(en ? "Minimum road length" : "Минимальная длина дороги")] public int minRoadLength { get; set; }
            [JsonProperty(en ? "List of excluded roads (/caravanroadblock)" : "Список исключенных дорог (/caravanroadblock)")] public HashSet<int> blockRoads { get; set; }
            [JsonProperty(en ? "Setting up the standard route type" : "Настройка стандартного режима маршрутов")] public RegularPathConfig regularPathConfig { get; set; }
            [JsonProperty(en ? "Setting up a experimental type" : "Настройка экспериментального режима маршрутов")] public ComplexPathConfig complexPathConfig { get; set; }
            [JsonProperty(en ? "Setting up a custom route type" : "Настройка кастомного режима маршрутов")] public CustomPathConfig customPathConfig { get; set; }

        }

        public class RegularPathConfig
        {
            [JsonProperty(en ? "If there is a ring road on the map, then the caravan will always spawn here" : "Если на карте есть кольцевая дорога, то караван будет двигаться только по ней")] public bool isRingRoad { get; set; }
        }

        public class ComplexPathConfig
        {
            [JsonProperty(en ? "Always choose the longest route? [true/false]" : "Всегда выбирать самый длинный маршрут? [true/false]")] public bool chooseLongestRoute { get; set; }
            [JsonProperty(en ? "The minimum number of roads in a complex route" : "Минимальное количество дорог в комплексом маршруте")] public int minRoadCount { get; set; }
        }

        public class CustomPathConfig
        {
            [JsonProperty(en ? "List of presets for custom routes" : "Список пресетов кастомных маршрутов")] public List<string> customRoutesPresets { get; set; }
        }

        public class EventConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Display name" : "Отображаемое название")] public string displayName { get; set; }
            [JsonProperty(en ? "Allow automatic startup? [true/false]" : "Разрешить автоматический запуск? [true/false]")] public bool isAutoStart { get; set; }
            [JsonProperty(en ? "Preset probability [0.0-100.0]" : "Вероятность пресета [0.0-100.0]")] public float chance { get; set; }
            [JsonProperty(en ? "The minimum time after the server's wipe when this preset can be selected automatically [sec]" : "Минимальное время после вайпа сервера, когда этот пресет может быть выбран автоматически [sec]")] public int minTimeAfterWipe { get; set; }
            [JsonProperty(en ? "The maximum time after the server's wipe when this preset can be selected automatically [sec] (-1 - do not use this parameter)" : "Максимальное время после вайпа сервера, когда этот пресет может быть выбран автоматически [sec] (-1 - не использовать)")] public int maxTimeAfterWipe { get; set; }
            [JsonProperty(en ? "Duration of the event [sec.]" : "Длительность ивента [sec.]")] public int eventTime { get; set; }
            [JsonProperty(en ? "Caravan speed [1.0-5.0]" : "Скорость каравана [1.0-5.0]")] public float baseSpeed { get; set; }
            [JsonProperty(en ? "Radius of the event zone" : "Радиус зоны ивента")] public float zoneRadius { get; set; }
            [JsonProperty(en ? "The maximum distance for dealing damage to the caravan (-1 - do not limit)" : "Максимальное расстояние для нанесения урона по каравану (-1 - не ограничивать)")] public float maxGroundDamageDistance { get; set; }
            [JsonProperty(en ? "List of caravan entities" : "Список участиков каравана")] public List<CaravanVehicleConfig> vehicleOrder { get; set; }
        }

        public class CaravanVehicleConfig
        {
            [JsonProperty(en ? "Сarriage Preset Name" : "Название пресета повозки")] public string presetName { get; set; }
            [JsonProperty(en ? "Preset of the accompanying balloon" : "Пресет сопровождающего воздушного шара")] public string balloonPresetName { get; set; }
            [JsonProperty(en ? "List of escort presets NPC/Horseman (the maximum number is 6)" : "Список пресетов сопровождающих NPC/Всадников (максимальное количество - 6)")] public List<string> guardEntityPresets { get; set; }
        }

        public class HorsemanConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Horse Preset" : "Пресет лошади")] public string horsePresetName { get; set; }
            [JsonProperty(en ? "NPC Preset" : "Пресет NPC")] public string frontNpcPresetName { get; set; }
            [JsonProperty(en ? "Patrol radius" : "Радиус патрулирования")] public float roamRadius { get; set; }
            [JsonProperty(en ? "Chase range" : "Дальность преследования цели")] public float chaseRange { get; set; }
            [JsonProperty(en ? "Optimal distance to the target" : "Оптимальная дистанция до цели")] public float targetDistance { get; set; }
        }

        public class CarriageConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Customization preset" : "Пресет кастомизации")] public string customizationPresetName { get; set; }
            [JsonProperty(en ? "Add a visual connection between a cart and a horse? [true/false]" : "Добавить визуальное соединение повозки и лошади? [true/false]")] public bool addVisualJoint { get; set; }
            [JsonProperty(en ? "Horse Preset" : "Пресет лошади")] public string horsePresetName { get; set; }
            [JsonProperty(en ? "List of modules (add a vehicle module, or add empty fields to increase frame size, up to 4 module entries)" : "Список модулей")] public List<string> modules { get; set; }
            [JsonProperty(en ? "Decorative NPCs" : "Декоративные НПС")] public HashSet<NpcLocationConfig> npcs { get; set; }
            [JsonProperty(en ? "Crates" : "Крейты")] public HashSet<PresetLocationConfig> crateLocations { get; set; }
        }

        public class HorseConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Health" : "Здоровье")] public int health { get; set; }
            [JsonProperty(en ? "Horse breed (0 - random; 1 - 10 - definite)" : "Порода лошади (0 - случайная; 1 - 10 - определенная)")] public int breed { get; set; }
            [JsonProperty(en ? "Armor type (0 - 2)" : "Тип брони (0 - 2)")] public int armorType { get; set; }
            [JsonProperty(en ? "Saddle type (0 - 2)" : "Тип седла (0 - 2)")] public int saddleType { get; set; }
        }

        public class AirBalloonConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Health" : "Здоровье")] public int health { get; set; }
            [JsonProperty(en ? "Armor type (0 - 1)" : "Тип брони (0 - 1)")] public int armorType { get; set; }
            [JsonProperty(en ? "Patrol radius" : "Радиус патрулирования")] public float roamRadius { get; set; }
            [JsonProperty("NPCs")] public HashSet<PresetLocationConfig> npcLocations { get; set; }
        }

        public class NpcLocationConfig : LocationConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Position (0 - sitting, 1 - standing)" : "Положение (0 - сидит, 1 - стоит)")] public int pose { get; set; }
        }

        public class PresetLocationConfig : LocationConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
        }

        public class LocationConfig
        {
            [JsonProperty(en ? "Position" : "Позиция")] public string position { get; set; }
            [JsonProperty(en ? "Rotation" : "Вращение")] public string rotation { get; set; }
        }

        public class NpcConfig
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Name [Must be unique]" : "Название [Должно быть уникальным]")] public string displayName { get; set; }
            [JsonProperty(en ? "Health" : "Кол-во ХП")] public float health { get; set; }
            [JsonProperty(en ? "Should remove the corpse?" : "Удалять труп?")] public bool deleteCorpse { get; set; }
            [JsonProperty(en ? "Roam Range" : "Дальность патрулирования местности")] public float roamRange { get; set; }
            [JsonProperty(en ? "Chase Range" : "Дальность погони за целью")] public float chaseRange { get; set; }
            [JsonProperty(en ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float attackRangeMultiplier { get; set; }
            [JsonProperty(en ? "Sense Range" : "Радиус обнаружения цели")] public float senseRange { get; set; }
            [JsonProperty(en ? "Memory duration [sec.]" : "Длительность памяти цели [sec.]")] public float memoryDuration { get; set; }
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float damageScale { get; set; }
            [JsonProperty(en ? "Aim Cone Scale" : "Множитель разброса")] public float aimConeScale { get; set; }
            [JsonProperty(en ? "Detect the target only in the NPC's viewing vision cone?" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool checkVisionCone { get; set; }
            [JsonProperty(en ? "Vision Cone" : "Угол обзора")] public float visionCone { get; set; }
            [JsonProperty(en ? "Speed" : "Скорость")] public float speed { get; set; }
            [JsonProperty(en ? "Wear items" : "Одежда")] public List<NpcWearItemConfig> wearItems { get; set; }
            [JsonProperty(en ? "Belt items" : "Быстрые слоты")] public List<NpcBeltItemConfig> beltItems { get; set; }
            [JsonProperty(en ? "Kit" : "Kit")] public string kit { get; set; }
            [JsonProperty(en ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool disableRadio { get; set; }
            [JsonProperty(en ? "Loot table" : "Лутовая таблица")] public LootTableConfig lootTableConfig { get; set; }
        }

        public class NpcWearItemConfig
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "SkinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
        }

        public class NpcBeltItemConfig
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "Amount" : "Кол-во")] public int amount { get; set; }
            [JsonProperty(en ? "SkinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
            [JsonProperty(en ? "Mods" : "Модификации на оружие")] public List<string> mods { get; set; }
            [JsonProperty(en ? "Ammo" : "Патроны")] public string ammo { get; set; }
        }

        public class CrateConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Prefab name" : "Название префаба")] public string prefabName { get; set; }
            [JsonProperty(en ? "SkinID (0 - default)" : "Скин")] public ulong skin { get; set; }
            [JsonProperty(en ? "Time of hacking of a locked crate (-1 - do not change)" : "Время взлома заблокированного ящика (-1 - не изменять)")] public int hackTime { get; set; }
            [JsonProperty(en ? "Loot table" : "Лутовая таблица")] public LootTableConfig lootTableConfig { get; set; }
        }

        public class LootTableConfig : BaseLootTableConfig
        {
            [JsonProperty(en ? "Clear the standard content of the crate" : "Отчистить стандартное содержимое крейта")] public bool clearDefaultItemList { get; set; }
            [JsonProperty(en ? "Allow the AlphaLoot plugin to spawn items in this crate" : "Разрешить плагину AlphaLoot спавнить предметы в этом ящике")] public bool isAlphaLoot { get; set; }
            [JsonProperty(en ? "Allow the CustomLoot plugin to spawn items in this crate" : "Разрешить плагину CustomLoot спавнить предметы в этом ящике")] public bool isCustomLoot { get; set; }
        }

        public class BaseLootTableConfig
        {
            [JsonProperty(en ? "Setting up loot from standard prefabs" : "Настройка лута из стандартных крейтов")] public PrefabLootTableConfigs prefabConfigs { get; set; }
            [JsonProperty(en ? "Setting up loot from the loot table" : "Настройка лута из лутовой таблицы")] public RandomItemsConfig randomItemsConfig { get; set; }
        }

        public class PrefabLootTableConfigs
        {
            [JsonProperty(en ? "Enable spawn loot from prefabs" : "Включить спавн лута из префабов")] public bool isEnable { get; set; }
            [JsonProperty(en ? "List of prefabs (one is randomly selected)" : "Список префабов (выбирается один рандомно)")] public List<PrefabConfig> prefabs { get; set; }
        }

        public class PrefabConfig
        {
            [JsonProperty(en ? "Prefab name" : "Название префаба")] public string prefabName { get; set; }
            [JsonProperty(en ? "Minimum Loot multiplier" : "Минимальный множитель лута")] public int minLootScale { get; set; }
            [JsonProperty(en ? "Maximum Loot multiplier" : "Максимальный множитель лута")] public int maxLootScale { get; set; }
        }

        public class RandomItemsConfig
        {
            [JsonProperty(en ? "Enable spawn of items from the list" : "Включить спавн предметов из списка")] public bool isEnable { get; set; }
            [JsonProperty(en ? "Random number of items (Do not take into account the following 2 parameters)" : "Случайное количество предметов (Не учитывать следующие 2 параметра)")] public bool useRandomItemsNumber { get; set; }
            [JsonProperty(en ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int minItemsAmount { get; set; }
            [JsonProperty(en ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int maxItemsAmount { get; set; }
            [JsonProperty(en ? "List of items" : "Список предметов")] public List<LootItemConfig> items { get; set; }
        }

        public class LootItemConfig : ItemConfig
        {
            [JsonProperty(en ? "Minimum" : "Минимальное кол-во", Order = 100)] public int minAmount { get; set; }
            [JsonProperty(en ? "Maximum" : "Максимальное кол-во", Order = 101)] public int maxAmount { get; set; }
            [JsonProperty(en ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]", Order = 102)] public float chance { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty("ShortName")] public string shortname { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong skin { get; set; }
            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")] public string name { get; set; }
            [JsonProperty(en ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool isBlueprint { get; set; }
            [JsonProperty(en ? "List of genomes" : "Список геномов")] public List<string> genomes { get; set; }
        }

        public class ZoneConfig
        {
            [JsonProperty(en ? "Create a PVP zone? (only for those who use the TruePVE plugin)[true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool isPVPZone { get; set; }
            [JsonProperty(en ? "Darkening the dome" : "Затемнение купола")] public int darkening { get; set; }
            [JsonProperty(en ? "Use a colored border? [true/false]" : "Использовать цветную границу? [true/false]")] public bool isColoredBorder { get; set; }
            [JsonProperty(en ? "Border color (0 - blue, 1 - green, 2 - purple, 3 - red)" : "Цвет границы (0 - синий, 1 - зеленый, 2 - фиолетовый, 3 - красный)")] public int borderColor { get; set; }
            [JsonProperty(en ? "Brightness of the color border" : "Яркость цветной границы")] public int brightness { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(en ? "Use a vending marker? [true/false]" : "Добавить маркер магазина? [true/false]")] public bool isShopMarker { get; set; }
            [JsonProperty(en ? "Use a circular marker? [true/false]" : "Добавить круговой маркер? [true/false]")] public bool isRingMarker { get; set; }
            [JsonProperty(en ? "Radius" : "Радиус")] public float radius { get; set; }
            [JsonProperty(en ? "Alpha" : "Прозрачность")] public float alpha { get; set; }
            [JsonProperty(en ? "Marker color" : "Цвет маркера")] public ColorConfig color1 { get; set; }
            [JsonProperty(en ? "Outline color" : "Цвет контура")] public ColorConfig color2 { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float r { get; set; }
            [JsonProperty("g")] public float g { get; set; }
            [JsonProperty("b")] public float b { get; set; }
        }

        public class NotifyConfig : BaseMessageConfig
        {
            [JsonProperty(en ? "Prefix" : "Префикс сообщений", Order = 0)] public string prefix { get; set; }
            [JsonProperty(en ? "Discord setting (only for DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин Discord Messages)", Order = 100)] public DiscordMessagesConfig discordMessagesConfig { get; set; }
            [JsonProperty(en ? "Redefined messages" : "Переопределенные сообщения )", Order = 101)] public HashSet<RedefinedMessageConfig> redefinedMessages { get; set; }
        }

        public class RedefinedMessageConfig : BaseMessageConfig
        {
            [JsonProperty(en ? "Enable this message? [true/false]" : "Включить сообщение? [true/false]", Order = 1)] public bool isEnable { get; set; }
            [JsonProperty("Lang Key", Order = 1)] public string langKey { get; set; }
        }

        public class BaseMessageConfig
        {
            [JsonProperty(en ? "Chat Message setting" : "Настройки сообщений в чате", Order = 1)] public ChatConfig chatConfig { get; set; }
            [JsonProperty(en ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip", Order = 2)] public GameTipConfig gameTipConfig { get; set; }
            [JsonProperty(en ? "GUI Announcements setting (only for GUIAnnouncements plugin)" : "Настройка GUI Announcements (только для тех, кто использует плагин GUI Announcements)", Order = 3)] public GUIAnnouncementsConfig guiAnnouncementsConfig { get; set; }
            [JsonProperty(en ? "Notify setting (only for Notify plugin)" : "Настройка Notify (только для тех, кто использует плагин Notify)", Order = 4)] public NotifyPluginConfig notifyPluginConfig { get; set; }
        }

        public class ChatConfig
        {
            [JsonProperty(en ? "Use chat notifications? [true/false]" : "Использовать ли чат? [true/false]")] public bool isEnabled { get; set; }
        }

        public class GameTipConfig
        {
            [JsonProperty(en ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")] public int style { get; set; }
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(en ? "Do you use GUI Announcements integration? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Banner color" : "Цвет баннера")] public string bannerColor { get; set; }
            [JsonProperty(en ? "Text color" : "Цвет текста")] public string textColor { get; set; }
            [JsonProperty(en ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition { get; set; }
        }

        public class NotifyPluginConfig
        {
            [JsonProperty(en ? "Do you use Notify integration? [true/false]" : "Использовать ли Notify? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Type" : "Тип")] public int type { get; set; }
        }

        public class DiscordMessagesConfig
        {
            [JsonProperty(en ? "Do you use DiscordMessages? [true/false]" : "Использовать ли DiscordMessages? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty("Webhook URL")] public string webhookUrl { get; set; }
            [JsonProperty(en ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int embedColor { get; set; }
            [JsonProperty(en ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> keys { get; set; }
        }

        public class EconomicsConfig
        {
            [JsonProperty(en ? "Enable economy" : "Включить экономику?")] public bool enable { get; set; }
            [JsonProperty(en ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> plugins { get; set; }
            [JsonProperty(en ? "The minimum value that a player must collect to earn the economy reward" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double minEconomyPiont { get; set; }
            [JsonProperty(en ? "The minimum value that a winner must collect to earn ist of commands reward" : "Минимальное значение, которое победитель должен заработать, чтобы сработали команды")] public double minCommandPoint { get; set; }
            [JsonProperty(en ? "Looting crates" : "Ограбление ящиков")] public Dictionary<string, double> crates { get; set; }
            [JsonProperty(en ? "Hacking a locked crate" : "Взлом заблокированного ящика")] public double lockedCratePoint { get; set; }
            [JsonProperty(en ? "Killing an NPC" : "Убийство NPC")] public double npcPoint { get; set; }
            [JsonProperty(en ? "Killing a Horseman" : "Убийство всадника")] public double horsemanPoint { get; set; }
            [JsonProperty(en ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> commands { get; set; }
        }

        public class GuiConfig
        {
            [JsonProperty(en ? "Use GUI? [true/false]" : "Использовать ли GUI? [true/false]")] public bool isEnable { get; set; }
            [JsonProperty(en ? "Vertical offset" : "Смещение по вертикали")] public int offsetMinY { get; set; }
        }

        public class SupportedPluginsConfig
        {
            [JsonProperty(en ? "PVE Mode Setting" : "Настройка PVE Mode")] public PveModeConfig pveMode { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(en ? "Use the PVEMode of the plugin? [true/false]" : "Использовать PVEMode? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "The owner of the event will be the one who stopped the event? [true/false]" : "Владельцем ивента будет становиться тот кто остановил ивент? [true/false]")] public bool ownerIsStopper { get; set; }
            [JsonProperty(en ? "If a player has a cooldown and the event has NO OWNERS, then he will not be able to interact with the event? [true/false]" : "Если у игрока кулдаун, а у ивента НЕТ ВЛАДЕЛЬЦЕВ, то он не сможет взаимодействовать с ивентом? [true/false]")] public bool noInterractIfCooldownAndNoOwners { get; set; }
            [JsonProperty(en ? "If a player has a cooldown, and the event HAS AN OWNER, then he will not be able to interact with the event, even if he is on a team with the owner? [true/false]" : "Если у игрока кулдаун, а у ивента ЕСТЬ ВЛАДЕЛЕЦ, то он не сможет взаимодействовать с ивентом, даже если находится в команде с владельцем? [true/false]")] public bool noDealDamageIfCooldownAndTeamOwner { get; set; }
            [JsonProperty(en ? "Allow only the owner or his teammates to loot crates? [true/false]" : "Разрешить лутать ящики только владельцу или его тиммейтам? [true/false]")] public bool canLootOnlyOwner { get; set; }
            [JsonProperty(en ? "Show the displayName of the event owner on a marker on the map? [true/false]" : "Отображать имя владелца ивента на маркере на карте? [true/false]")] public bool showEventOwnerNameOnMap { get; set; }
            [JsonProperty(en ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float damage { get; set; }
            [JsonProperty(en ? "Damage coefficients to calculate becomeing the Event Owner." : "Коэффициенты урона для подсчета, чтобы стать владельцем события.")] public Dictionary<string, float> scaleDamage { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool lootCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event hack locked crates? [true/false]" : "Может ли не владелец ивента взламывать заблокированные ящики? [true/false]")] public bool hackCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool lootNpc { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event deal damage to event NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool damageNpc { get; set; }
            [JsonProperty(en ? "Can an NPC attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool targetNpc { get; set; }
            [JsonProperty(en ? "Allow the non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")] public bool canEnter { get; set; }
            [JsonProperty(en ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")] public bool canEnterCooldownPlayer { get; set; }
            [JsonProperty(en ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")] public int timeExitOwner { get; set; }
            [JsonProperty(en ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")] public int alertTime { get; set; }
            [JsonProperty(en ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool restoreUponDeath { get; set; }
            [JsonProperty(en ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double cooldown { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")] public VersionNumber version { get; set; }
            [JsonProperty(en ? "General Settings" : "Общие настройки")] public MainConfig mainConfig { get; set; }
            [JsonProperty(en ? "Behavior Settings" : "Настройка поведения каравана")] public BehaviorConfig behaviorConfig { get; set; }
            [JsonProperty(en ? "Damage Settings" : "Настройки Урона")] public DamageConfig damageConfig { get; set; }
            [JsonProperty(en ? "Loot Settings" : "Настройки Лута")] public LootConfig lootConfig { get; set; }
            [JsonProperty(en ? "Caravan route Settings" : "Настройки маршрутов каравана")] public PathConfig pathConfig { get; set; }
            [JsonProperty(en ? "Event Presets" : "Пресеты ивентов")] public HashSet<EventConfig> eventConfigs { get; set; }
            [JsonProperty(en ? "Carriage Presets" : "Пресеты повозок")] public HashSet<CarriageConfig> carriageConfigs { get; set; }
            [JsonProperty(en ? "Horsemen Presets" : "Пресеты всадиков")] public HashSet<HorsemanConfig> horsemanConfigs { get; set; }
            [JsonProperty(en ? "Horse Presets" : "Пресеты лошадей")] public HashSet<HorseConfig> horseConfigs { get; set; }
            [JsonProperty(en ? "Hot air balloon Presets" : "Пресеты воздушных шаров")] public HashSet<AirBalloonConfig> airBalloonConfigs { get; set; }
            [JsonProperty(en ? "NPC Presets" : "Пресеты NPC")] public HashSet<NpcConfig> npcConfigs { get; set; }
            [JsonProperty(en ? "Crate Presets" : "Пресеты Крейтов")] public HashSet<CrateConfig> crateConfigs { get; set; }
            [JsonProperty(en ? "Marker Setting" : "Настройки маркера")] public MarkerConfig markerConfig { get; set; }
            [JsonProperty(en ? "Event zone setting" : "Настройка зоны ивента")] public ZoneConfig zoneConfig { get; set; }
            [JsonProperty(en ? "GUI setting" : "Настройки GUI")] public GuiConfig guiConfig { get; set; }
            [JsonProperty(en ? "Notification Settings" : "Настройки уведомлений")] public NotifyConfig notifyConfig { get; set; }
            [JsonProperty(en ? "Economy Settings (Determining the winner of the event and awarding using other plugins)" : "Настройки Экономики (Определение победителя ивента и выдача наград при помощи других плагинов)")] public EconomicsConfig economicsConfig { get; set; }
            [JsonProperty(en ? "Supported Plugins" : "Поддерживаемые плагины")] public SupportedPluginsConfig supportedPluginsConfig { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = new VersionNumber(1, 1, 7),
                    mainConfig = new MainConfig
                    {
                        isAutoEvent = true,
                        minTimeBetweenEvents = 3600,
                        maxTimeBetweenEvents = 7200,
                        preStartTime = 0,
                        endAfterDeathTime = 300,
                        enableStartStopLogs = false
                    },
                    behaviorConfig = new BehaviorConfig
                    {
                        agressiveTime = 80,
                        stopTime = 80,
                    },
                    damageConfig = new DamageConfig
                    {
                        isPlayerSamSiteCanAttackEvent = true
                    },
                    lootConfig = new LootConfig
                    {
                        dropLoot = true,
                        lootLossPercent = 0.5f,
                        blockLootingByNpcs = true,
                        blockLootingByMove = true,
                    },
                    pathConfig = new PathConfig
                    {
                        pathType = 1,
                        minRoadLength = 100,
                        blockRoads = new HashSet<int>(),
                        regularPathConfig = new RegularPathConfig
                        {
                            isRingRoad = true,
                        },
                        complexPathConfig = new ComplexPathConfig
                        {
                            chooseLongestRoute = true,
                            minRoadCount = 3
                        },
                        customPathConfig = new CustomPathConfig
                        {
                            customRoutesPresets = new List<string>()
                        }
                    },
                    eventConfigs = new HashSet<EventConfig>
                    {
                        new EventConfig
                        {
                            presetName = "farm",
                            displayName = en ? "Farmer's caravan" : "Фермерский караван",
                            isAutoStart = true,
                            chance = 50,
                            minTimeAfterWipe = 0,
                            maxTimeAfterWipe = 172800,
                            eventTime = 7200,
                            baseSpeed = 3.5f,
                            zoneRadius = 35,
                            maxGroundDamageDistance = 50f,
                            vehicleOrder = new List<CaravanVehicleConfig>
                            {
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_farm_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "farmer_nailgun",
                                        "farmer_nailgun",
                                        "farmer_nailgun",
                                        "farmer_nailgun"
                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_horse_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "ranger_shotgun_double",
                                        "ranger_shotgun_double"
                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_boxes_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "hunter_revolver",
                                        "hunter_revolver",
                                        "hunter_revolver",
                                        "hunter_revolver"
                                    }
                                }
                            }
                        },
                        new EventConfig
                        {
                            presetName = "trade",
                            displayName = en ? "Trader caravan" : "Караван торговцев",
                            isAutoStart = true,
                            chance = 35,
                            minTimeAfterWipe = 10800,
                            maxTimeAfterWipe = -1,
                            eventTime = 7200,
                            baseSpeed = 3.5f,
                            zoneRadius = 45,
                            maxGroundDamageDistance = 50f,
                            vehicleOrder = new List<CaravanVehicleConfig>
                            {
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_barrels_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "farmer_nailgun",
                                        "farmer_nailgun",
                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_kayak_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "horseman_сowboy",
                                        "horseman_сowboy"
                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_boat_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "horseman_sheriff",
                                        "horseman_sheriff"
                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_safe_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "bandit_thompson",
                                        "bandit_thompson",
                                        "bandit_thompson",
                                        "bandit_thompson"
                                    }
                                }
                            }
                        },
                        new EventConfig
                        {
                            presetName = "prison",
                            displayName = en ? "Prison caravan" : "Тюремный караван",
                            isAutoStart = true,
                            chance = 15,
                            eventTime = 7200,
                            minTimeAfterWipe = 36000,
                            maxTimeAfterWipe = -1,
                            baseSpeed = 3.5f,
                            zoneRadius = 60,
                            maxGroundDamageDistance = 100f,
                            vehicleOrder = new List<CaravanVehicleConfig>
                            {
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_medical_1",
                                    balloonPresetName = "balloon_regular",
                                    guardEntityPresets = new List<string>
                                    {
                                        "horseman_juggernaut",
                                        "horseman_juggernaut",
                                        "skull_1",
                                        "skull_1",
                                        "skull_1",
                                        "skull_1"
                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_prison_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "horseman_juggernaut",
                                        "horseman_juggernaut",
                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_boxes_2",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {

                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_crate_1",
                                    balloonPresetName = "",
                                    guardEntityPresets = new List<string>
                                    {
                                        "horseman_juggernaut",
                                        "horseman_juggernaut",
                                    }
                                },
                                new CaravanVehicleConfig
                                {
                                    presetName = "carriage_boxes_3",
                                    balloonPresetName = "balloon_regular",
                                    guardEntityPresets = new List<string>
                                    {
                                        "skull_1",
                                        "skull_1",
                                        "skull_1",
                                        "skull_1",
                                        "horseman_juggernaut",
                                        "horseman_juggernaut"
                                    }
                                },
                            }
                        }
                    },
                    carriageConfigs = new HashSet<CarriageConfig>
                    {
                        new CarriageConfig
                        {
                            presetName = "carriage_farm_1",
                            customizationPresetName = "carriage_farm",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "",
                                "",
                                "",
                                ""
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {
                                new NpcLocationConfig
                                {
                                    presetName = "drover",
                                    pose = 0,
                                    position = "(0.25, 0.6, 2.3)",
                                    rotation = "(0, 0, 0)"
                                },
                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "storage_barrel_c_food",
                                    position = "(-0.0466, 1.354, -0.826)",
                                    rotation = "(0, 0, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "crate_tool",
                                    position = "(-0.453, 0.722, 1.9)",
                                    rotation = "(0, 270, 0)"
                                }
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_horse_1",
                            customizationPresetName = "carriage_horse",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "",
                                "",
                                ""
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {

                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "crate_normal_weapon_1",
                                    position = "(0.031, 2.895, 1.342)",
                                    rotation = "(0, 351, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "crate_normal_weapon_1",
                                    position = "(0.031, 2.895, -1.313)",
                                    rotation = "(0, 19, 0)"
                                },
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_boxes_1",
                            customizationPresetName = "carriage_boxes",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "",
                                "",
                                ""
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {

                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes",
                                    position = "(0.012, 2.115, -2.048071)",
                                    rotation = "(270, 180, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes",
                                    position = "(0.01226763, 1.181267, -2.048071)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes",
                                    position = "(0.0123, 2.115, -0.03)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes",
                                    position = "(0.0123, 1.181, -0.03)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_boxes_2",
                            customizationPresetName = "carriage_boxes",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "",
                                "",
                                ""
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {

                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "frige_supply",
                                    position = "(0.012, 2.115, -2.048071)",
                                    rotation = "(270, 180, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes",
                                    position = "(0.01226763, 1.181267, -2.048071)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_supply",
                                    position = "(0.0123, 2.115, -0.03)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes",
                                    position = "(0.0123, 1.181, -0.03)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_boxes_3",
                            customizationPresetName = "carriage_boxes",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "",
                                "",
                                ""
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {

                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes_2",
                                    position = "(0.012, 2.115, -2.048071)",
                                    rotation = "(270, 180, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes_2",
                                    position = "(0.01226763, 1.181267, -2.048071)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes_2",
                                    position = "(0.0123, 2.115, -0.03)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "frige_boxes_2",
                                    position = "(0.0123, 1.181, -0.03)",
                                    rotation = "(270.00, 180.00, 0.00)"
                                },
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_barrels_1",
                            customizationPresetName = "carriage_barrels",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "vehicle.2mod.flatbed"
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {
                                new NpcLocationConfig
                                {
                                    presetName = "drover",
                                    pose = 0,
                                    position = "(0, 0.7, 2.45)",
                                    rotation = "(0, 0, 0)"
                                },
                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "storage_barrel_c_medical",
                                    position = "(0.2888113, 2.335415, 0.745923)",
                                    rotation = "(0, 90, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "storage_barrel_c_food",
                                    position = "(-0.1893209, 1.608467, 0.745923)",
                                    rotation = "(0, 90, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "storage_barrel_c_food",
                                    position = "(-0.5375684, 2.335415, 0.745923)",
                                    rotation = "(0, 90, 0)"
                                },
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_kayak_1",
                            customizationPresetName = "carriage_kayak",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "",
                                "vehicle.2mod.flatbed"
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {
                                new NpcLocationConfig
                                {
                                    presetName = "drover",
                                    pose = 0,
                                    position = "(0, 0.6, 2.45)",
                                    rotation = "(0, 0, 0)"
                                },
                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "crate_normal_weapon_2",
                                    position = "(0.113, 0.755, -0.389)",
                                    rotation = "(0, 90, 0)"
                                }
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_boat_1",
                            customizationPresetName = "carriage_boat",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "vehicle.2mod.flatbed"
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {
                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "crate_normal_weapon_3",
                                    position = "(-0.614, 2.099, -1.010)",
                                    rotation = "(64.127, 295.382, 207.803)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "crate_normal_weapon_3",
                                    position = "(-0.648, 2.032, 0.379)",
                                    rotation = "(62.790, 239.566, 146.552)"
                                }
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_safe_1",
                            customizationPresetName = "carriage_sofas",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "vehicle.2mod.flatbed"
                            },
                            horsePresetName = "horse_metal_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {
                                new NpcLocationConfig
                                {
                                    presetName = "drover",
                                    pose = 0,
                                    position = "(0, 0.7, 2.45)",
                                    rotation = "(0, 0, 0)"
                                },
                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "frige_safe",
                                    position = "(-0.08, 0.718, -0.952)",
                                    rotation = "(0, 180.00, 0.00)"
                                },
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_medical_1",
                            customizationPresetName = "carriage_medical",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "",
                                "",
                                ""
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {
                                new NpcLocationConfig
                                {
                                    presetName = "drover",
                                    pose = 0,
                                    position = "(-0.1, 0.75, 2.15)",
                                    rotation = "(0, 0, 0)"
                                },
                                new NpcLocationConfig
                                {
                                    presetName = "doctor_1",
                                    pose = 0,
                                    position = "(0.2, 0.65, -2)",
                                    rotation = "(0.00, 180, 0.00)"
                                },
                                new NpcLocationConfig
                                {
                                    presetName = "wounded_1",
                                    pose = 1,
                                    position = "(0.0, 1.35, -0.8)",
                                    rotation = "(270, 90, 90)"
                                }
                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "crate_normal_2_underwater",
                                    position = "(-0.224, 0.730, -1.102)",
                                    rotation = "(0, 0, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "crate_medical",
                                    position = "(-0.346, 0.728, -1.776)",
                                    rotation = "(0, 90, 0)"
                                },
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_crate_1",
                            customizationPresetName = "carriage_crate",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "vehicle.2mod.flatbed"
                            },
                            horsePresetName = "horse_wood_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {
                                new NpcLocationConfig
                                {
                                    presetName = "drover",
                                    pose = 0,
                                    position = "(0, 0.7, 2.45)",
                                    rotation = "(0, 0, 0)"
                                },
                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "storage_barrel_c_weapon",
                                    position = "(0.2888113, 2.335415, 0.745923)",
                                    rotation = "(0, 90, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "storage_barrel_c_weapon",
                                    position = "(-0.1893209, 1.608467, 0.745923)",
                                    rotation = "(0, 90, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "storage_barrel_c_weapon",
                                    position = "(-0.5375684, 2.335415, 0.745923)",
                                    rotation = "(0, 90, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "chinooklockedcrate",
                                    position = "(0, 0.8, -0.9)",
                                    rotation = "(0, 180, 0)"
                                },
                            }
                        },
                        new CarriageConfig
                        {
                            presetName = "carriage_prison_1",
                            customizationPresetName = "carriage_prison",
                            addVisualJoint = true,
                            modules = new List<string>
                            {
                                "",
                                "",
                                ""
                            },
                            horsePresetName = "horse_metal_armor",
                            npcs = new HashSet<NpcLocationConfig>
                            {
                                new NpcLocationConfig
                                {
                                    presetName = "prisoner_1",
                                    pose = 1,
                                    position = "(0.02115173, 0.7050699, -1.615)",
                                    rotation = "(0, 0, 0)"
                                },
                                new NpcLocationConfig
                                {
                                    presetName = "prisoner_1",
                                    pose = 1,
                                    position = "(0.02115173, 0.7050699, -0.650768)",
                                    rotation = "(0, 180, 0)"
                                },
                                new NpcLocationConfig
                                {
                                    presetName = "prisoner_1",
                                    pose = 1,
                                    position = "(0.02115173, 0.7050699, 0.4555934)",
                                    rotation = "(0, 180, 0)"
                                },
                                new NpcLocationConfig
                                {
                                    presetName = "prisoner_1",
                                    pose = 1,
                                    position = "(0.02115173, 0.7050699, 1.424)",
                                    rotation = "(0, 0, 0)"
                                }
                            },
                            crateLocations = new HashSet<PresetLocationConfig>
                            {
                            }
                        },
                    },
                    horsemanConfigs = new HashSet<HorsemanConfig>
                    {
                        new HorsemanConfig
                        {
                            presetName = "horseman_ranger",
                            horsePresetName = "horse_wood_armor",
                            frontNpcPresetName = "ranger_shotgun_double",
                            roamRadius = 20,
                            chaseRange = 110,
                            targetDistance = 10,
                        },
                        new HorsemanConfig
                        {
                            presetName = "horseman_sheriff",
                            horsePresetName = "horse_no_armor",
                            frontNpcPresetName = "sheriff_python",
                            roamRadius = 20,
                            chaseRange = 110,
                            targetDistance = 15
                        },
                        new HorsemanConfig
                        {
                            presetName = "horseman_сowboy",
                            horsePresetName = "horse_wood_armor",
                            frontNpcPresetName = "сowboy_pump",
                            roamRadius = 20,
                            chaseRange = 110,
                            targetDistance = 5
                        },
                        new HorsemanConfig
                        {
                            presetName = "horseman_juggernaut",
                            horsePresetName = "horse_metal_armor",
                            frontNpcPresetName = "juggernaut_1",
                            roamRadius = 20,
                            chaseRange = 110,
                            targetDistance = 5
                        }
                    },
                    horseConfigs = new HashSet<HorseConfig>
                    {
                        new HorseConfig
                        {
                            presetName = "horse_no_armor",
                            armorType = 0,
                            saddleType = 0,
                            breed = 0,
                            health = 1000
                        },
                        new HorseConfig
                        {
                            presetName = "horse_wood_armor",
                            armorType = 1,
                            saddleType = 0,
                            breed = 0,
                            health = 1000
                        },
                        new HorseConfig
                        {
                            presetName = "horse_metal_armor",
                            armorType = 2,
                            saddleType = 1,
                            breed = 0,
                            health = 1000
                        }
                    },
                    airBalloonConfigs = new HashSet<AirBalloonConfig>
                    {
                        new AirBalloonConfig
                        {
                            presetName = "balloon_regular",
                            health = 1000,
                            armorType = 0,
                            roamRadius = 50,
                            npcLocations = new HashSet<PresetLocationConfig>
                            {
                                new PresetLocationConfig
                                {
                                    presetName = "juggernaut_sniper",
                                    position = "(0, 0.398, 1.192)",
                                    rotation = "(0, 0, 0)"
                                },
                                new PresetLocationConfig
                                {
                                    presetName = "juggernaut_sniper",
                                    position = "(0, 0.398, -1.045)",
                                    rotation = "(0, 180, 0)"
                                },
                            }
                        }
                    },
                    npcConfigs = new HashSet<NpcConfig>
                    {
                        new NpcConfig
                        {
                            presetName = "farmer_nailgun",
                            displayName = en ? "Farmer" : "Фермер",
                            health = 75,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.shirt",
                                    skinID = 2039984110
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.trousers",
                                    skinID = 2039988322
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "hat.boonie",
                                    skinID = 2037650796
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "pistol.nailgun",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                },
                                new NpcBeltItemConfig
                                {
                                    shortName = "pitchfork",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 0.2f,
                            aimConeScale = 1.5f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "ranger_shotgun_double",
                            displayName = en ? "Ranger" : "Рейнджер",
                            health = 125,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.shirt",
                                    skinID = 724221934
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "pants",
                                    skinID = 733460470
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "shoes.boots",
                                    skinID = 630161832
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "hat.boonie",
                                    skinID = 661318553
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "wood.armor.pants",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "wood.armor.jacket",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "mask.bandana",
                                    skinID = 0
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "shotgun.double",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 1.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 0.5f,
                            aimConeScale = 1f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 3,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "ammo.pistol.fire",
                                            skin = 0,
                                            name = "",
                                            minAmount = 40,
                                            maxAmount = 60,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "ammo.pistol",
                                            skin = 0,
                                            name = "",
                                            minAmount = 40,
                                            maxAmount = 60,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "ammo.shotgun.fire",
                                            skin = 0,
                                            name = "",
                                            minAmount = 10,
                                            maxAmount = 15,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "ammo.shotgun",
                                            skin = 0,
                                            name = "",
                                            minAmount = 10,
                                            maxAmount = 15,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "ammo.shotgun.slug",
                                            skin = 0,
                                            name = "",
                                            minAmount = 10,
                                            maxAmount = 15,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "speargun.spear",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "arrow.fire",
                                            skin = 0,
                                            name = "",
                                            minAmount = 10,
                                            maxAmount = 20,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "hunter_revolver",
                            displayName = en ? "Hunter" : "Охотник",
                            health = 100,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "hoodie",
                                    skinID = 1360060594
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "pants",
                                    skinID = 1360069682
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "shoes.boots",
                                    skinID = 630161832
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "hat.cap",
                                    skinID = 1360067053
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "pistol.revolver",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 0.2f,
                            aimConeScale = 1.2f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "drover",
                            displayName = en ? "The Drover" : "Погонщик",
                            health = 10,
                            speed = 5f,

                            roamRange = 0,
                            chaseRange = 0,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.shirt",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "wood.armor.jacket",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "wood.armor.pants",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "hat.boonie",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.trousers",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.gloves.new",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "attire.hide.boots",
                                    skinID = 0
                                }
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "fishingrod.handmade",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 0.7f,
                            aimConeScale = 1.2f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "horse.shoes.basic",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "horse.shoes.advanced",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "horse.saddle.single",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "horse.saddlebag",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "horse.armor.roadsign",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "horse.armor.wood",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "sheriff_python",
                            displayName = en ? "Sheriff" : "Шериф",
                            health = 175,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "hoodie",
                                    skinID = 1766644324
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "pants",
                                    skinID = 1766646393
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "shoes.boots",
                                    skinID = 547978997
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "hat.boonie",
                                    skinID = 1760060421
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.gloves.new",
                                    skinID = 1338273501
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "pistol.python",
                                    amount = 1,
                                    skinID = 1455062983,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 0.1f,
                            aimConeScale = 1.3f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 3,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 5,
                                            maxLootScale = 6,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "bandit_thompson",
                            displayName = en ? "Bandit" : "Бандит",
                            health = 150,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "hoodie",
                                    skinID = 3074334545
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "pants",
                                    skinID = 3074335634
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "shoes.boots",
                                    skinID = 3088797079
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "mask.bandana",
                                    skinID = 0
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "smg.thompson",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 0.25f,
                            aimConeScale = 1.3f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 2,
                                            maxLootScale = 2,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "сowboy_pump",
                            displayName = en ? "Cowboy" : "Ковбой",
                            health = 175,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.shirt",
                                    skinID = 1755124648
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.trousers",
                                    skinID = 1755140135
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "shoes.boots",
                                    skinID = 547978997
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "hat.boonie",
                                    skinID = 1754286779
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.gloves.new",
                                    skinID = 1338273501
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "shotgun.pump",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 1.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 0.5f,
                            aimConeScale = 1f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 3,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 5,
                                            maxLootScale = 6,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "doctor_1",
                            displayName = en ? "Doctor" : "Врач",
                            health = 100,
                            speed = 5f,

                            roamRange = 0,
                            chaseRange = 0,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "deer.skull.mask",
                                    skinID = 882204381
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "draculacape",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "pants",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "shoes.boots",
                                    skinID = 0
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 3,
                                    maxItemsAmount = 3,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "radiationresisttea.pure",
                                            skin = 0,
                                            name = "",
                                            minAmount = 3,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "healingtea.pure",
                                            skin = 0,
                                            name = "",
                                            minAmount = 3,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "maxhealthtea.pure",
                                            skin = 0,
                                            name = "",
                                            minAmount = 3,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "oretea.pure",
                                            skin = 0,
                                            name = "",
                                            minAmount = 3,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "scraptea.pure",
                                            skin = 0,
                                            name = "",
                                            minAmount = 3,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "woodtea.pure",
                                            skin = 0,
                                            name = "",
                                            minAmount = 3,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "wounded_1",
                            displayName = en ? "Wounded man" : "Раненый",
                            health = 10,
                            speed = 5f,

                            roamRange = 0,
                            chaseRange = 0,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "frankensteins.monster.01.torso",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "frankensteins.monster.01.head",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "frankensteins.monster.01.legs",
                                    skinID = 0
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },

                        new NpcConfig
                        {
                            presetName = "prisoner_1",
                            displayName = en ? "Prisoner" : "Заключенный",
                            health = 10,
                            speed = 5f,

                            roamRange = 0,
                            chaseRange = 0,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.shirt",
                                    skinID = 2655843517
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.trousers",
                                    skinID = 2655838948
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.headwrap",
                                    skinID = 2655848185
                                }
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 60,
                            memoryDuration = 10f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 3,
                                            maxLootScale = 3,
                                            prefabName = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "juggernaut_1",
                            displayName = en ? "Juggernaut Rider" : "Джаггернаут - Всадник",
                            health = 100,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "heavy.plate.helmet",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "heavy.plate.jacket",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "heavy.plate.pants",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "shoes.boots",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "roadsign.gloves",
                                    skinID = 0
                                }
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "shotgun.spas12",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2f,
                            senseRange = 110,
                            memoryDuration = 10f,
                            damageScale = 1f,
                            aimConeScale = 1.3f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            presetName = "juggernaut_sniper",
                            displayName = en ? "Juggernaut Sniper" : "Джаггернаут - Снайпер",
                            health = 100,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "heavy.plate.helmet",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "heavy.plate.jacket",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "heavy.plate.pants",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "shoes.boots",
                                    skinID = 0
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "roadsign.gloves",
                                    skinID = 0
                                }
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "rifle.bolt",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2f,
                            senseRange = 110,
                            memoryDuration = 10f,
                            damageScale = 0.25f,
                            aimConeScale = 1.3f,
                            checkVisionCone = false,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {

                                    }
                                }
                            }
                        },

                        new NpcConfig
                        {
                            presetName = "skull_1",
                            displayName = en ? "Skull" : "Череп",
                            health = 200,
                            speed = 5f,
                            roamRange = 10,
                            chaseRange = 110,
                            wearItems = new List<NpcWearItemConfig>
                            {
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.shirt",
                                    skinID = 1170613745
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.trousers",
                                    skinID = 1170617392
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "metal.facemask",
                                    skinID = 1137533438
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "burlap.shoes",
                                    skinID = 1170611034
                                },
                                new NpcWearItemConfig
                                {
                                    shortName = "metal.plate.torso",
                                    skinID = 823132085
                                },
                            },
                            beltItems = new List<NpcBeltItemConfig>
                            {
                                new NpcBeltItemConfig
                                {
                                    shortName = "rifle.semiauto",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new List<string> { },
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = true,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 110,
                            memoryDuration = 10f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = true,
                            visionCone = 135f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    crateConfigs = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            presetName = "crate_tool",
                            prefabName = "assets/bundled/prefabs/radtown/crate_tools.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    },
                                    minItemsAmount = 0,
                                    maxItemsAmount = 0
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "storage_barrel_c_food",
                            prefabName = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "clone.hemp",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "clone.yellow.berry",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "clone.blue.berry",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "clone.red.berry",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "clone.pumpkin",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 2,
                                            maxLootScale = 2,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "storage_barrel_c_medical",
                            prefabName = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 15,
                                            maxLootScale = 20,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "storage_barrel_c_weapon",
                            prefabName = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 2,
                                            prefabName = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "storage_barrel_c_weapon_2",
                            prefabName = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "rifle.ak",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "rifle.lr300",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "homingmissile.launcher",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "rocket.launcher",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "hmlmg",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "lmg.m249",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "smg.mp5",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 15,
                                            maxLootScale = 20,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "crate_normal_weapon_1",
                            prefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "pistol.semiauto",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "crossbow",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 10,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "pistol.nailgun",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 10,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "shotgun.double",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 10,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "bow.compound",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 10,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "speargun",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 10,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "pistol.revolver",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 10,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "shotgun.waterpipe",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 10,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },

                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "crate_normal_weapon_2",
                            prefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 3,
                                    maxItemsAmount = 3,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "grenade.beancan",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "explosive.satchel",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 3,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "grenade.molotov",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 3,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "grenade.f1",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 5,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "weapon.mod.holosight",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "weapon.mod.silencer",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "weapon.mod.extendedmags",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "crate_normal_weapon_3",
                            prefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 1,
                                    maxItemsAmount = 1,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "smg.thompson",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "rifle.semiauto",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "shotgun.pump",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "pistol.python",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "pistol.m92",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "frige_boxes",
                            prefabName = "assets/prefabs/deployable/fridge/fridge.deployed.prefab",
                            skin = 2730178903,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    },
                                    minItemsAmount = 0,
                                    maxItemsAmount = 0
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "frige_boxes_2",
                            prefabName = "assets/prefabs/deployable/fridge/fridge.deployed.prefab",
                            skin = 2730178903,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    },
                                    minItemsAmount = 0,
                                    maxItemsAmount = 0
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 5,
                                            maxLootScale = 5,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_2.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "chinooklockedcrate",
                            prefabName = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab",
                            skin = 0,
                            hackTime = 0,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    },
                                    minItemsAmount = 0,
                                    maxItemsAmount = 0
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "crate_normal_2_underwater",
                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = true,
                                    useRandomItemsNumber = false,
                                    minItemsAmount = 3,
                                    maxItemsAmount = 3,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "clone.hemp",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "clone.yellow.berry",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "clone.blue.berry",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "clone.red.berry",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        },
                                        new LootItemConfig
                                        {
                                            shortname = "clone.pumpkin",
                                            skin = 0,
                                            name = "",
                                            minAmount = 1,
                                            maxAmount = 1,
                                            chance = 5,
                                            isBlueprint = false,
                                            genomes = new List<string>
                                            {
                                                "YYHGHG",
                                                "HHHYYY",
                                                "GHYGHY",
                                                "GGHHYY",
                                                "YYYYYY",
                                                "GGGGGG",
                                                "GGGYYY"
                                            }
                                        }
                                    }
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "crate_medical",
                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab",
                            skin = 0,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = true,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    },
                                    minItemsAmount = 0,
                                    maxItemsAmount = 0
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 10,
                                            maxLootScale = 10,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "frige_safe",
                            prefabName = "assets/prefabs/deployable/fridge/fridge.deployed.prefab",
                            skin = 3005880420,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    },
                                    minItemsAmount = 0,
                                    maxItemsAmount = 0
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 5,
                                            maxLootScale = 5,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_2.prefab"
                                        }
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "frige_supply",
                            prefabName = "assets/prefabs/deployable/fridge/fridge.deployed.prefab",
                            skin = 3102843771,
                            hackTime = -1,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                isCustomLoot = false,
                                randomItemsConfig = new RandomItemsConfig
                                {
                                    isEnable = false,
                                    items = new List<LootItemConfig>
                                    {
                                        new LootItemConfig
                                        {
                                            shortname = "scrap",
                                            skin = 0,
                                            name = "",
                                            minAmount = 50,
                                            maxAmount = 100,
                                            chance = 50,
                                            isBlueprint = false,
                                            genomes = new List<string>()
                                        }
                                    },
                                    minItemsAmount = 0,
                                    maxItemsAmount = 0
                                },
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = true,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/prefabs/misc/supply drop/supply_drop.prefab"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    markerConfig = new MarkerConfig
                    {
                        isShopMarker = true,
                        isRingMarker = true,
                        radius = 0.2f,
                        alpha = 0.6f,
                        color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                        color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                    },
                    zoneConfig = new ZoneConfig
                    {
                        isPVPZone = false,
                        darkening = 5,
                        isColoredBorder = false,
                        brightness = 5,
                        borderColor = 2
                    },
                    guiConfig = new GuiConfig
                    {
                        isEnable = true,
                        offsetMinY = -56
                    },
                    notifyConfig = new NotifyConfig
                    {
                        prefix = "[Caravan]",
                        chatConfig = new ChatConfig
                        {
                            isEnabled = true,
                        },
                        gameTipConfig = new GameTipConfig
                        {
                            isEnabled = false,
                            style = 2,
                        },
                        guiAnnouncementsConfig = new GUIAnnouncementsConfig
                        {
                            isEnabled = false,
                            bannerColor = "Grey",
                            textColor = "White",
                            apiAdjustVPosition = 0.03f
                        },
                        notifyPluginConfig = new NotifyPluginConfig
                        {
                            isEnabled = false,
                            type = 0
                        },
                        discordMessagesConfig = new DiscordMessagesConfig
                        {
                            isEnabled = false,
                            webhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                            embedColor = 13516583,
                            keys = new HashSet<string>
                            {
                                "PreStart",
                                "EventStart",
                                "Finish"
                            }
                        },
                        redefinedMessages = new HashSet<RedefinedMessageConfig>
                        {
                            new RedefinedMessageConfig
                            {
                                langKey = "EventStart",
                                isEnable = true,
                                chatConfig = new ChatConfig
                                {
                                    isEnabled = false,
                                },
                                gameTipConfig = new GameTipConfig
                                {
                                    isEnabled = true,
                                    style = 2,
                                },
                                guiAnnouncementsConfig = new GUIAnnouncementsConfig
                                {
                                    isEnabled = false,
                                    bannerColor = "Grey",
                                    textColor = "White",
                                    apiAdjustVPosition = 0.03f
                                },
                                notifyPluginConfig = new NotifyPluginConfig
                                {
                                    isEnabled = false,
                                    type = 0
                                },
                            }
                        }
                    },
                    economicsConfig = new EconomicsConfig
                    {
                        enable = false,
                        plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                        minCommandPoint = 0,
                        minEconomyPiont = 0,
                        lockedCratePoint = 20,
                        crates = new Dictionary<string, double>
                        {
                            ["crate_tool"] = 1,
                            ["storage_barrel_c_food"] = 2,
                            ["storage_barrel_c_medical"] = 5,
                            ["storage_barrel_c_weapon"] = 7,
                            ["storage_barrel_c_weapon_2"] = 10,
                            ["crate_normal_weapon_1"] = 3,
                            ["crate_normal_weapon_2"] = 5,
                            ["crate_normal_weapon_3"] = 10,
                            ["frige_boxes"] = 1,
                            ["frige_boxes_2"] = 3,
                            ["frige_safe"] = 2,
                            ["frige_supply"] = 10,
                            ["crate_normal_2_underwater"] = 7,
                            ["crate_medical"] = 3,
                        },
                        npcPoint = 2,
                        horsemanPoint = 5,
                        commands = new HashSet<string>()
                    },
                    supportedPluginsConfig = new SupportedPluginsConfig
                    {
                        pveMode = new PveModeConfig
                        {
                            enable = false,
                            ownerIsStopper = true,
                            noInterractIfCooldownAndNoOwners = true,
                            noDealDamageIfCooldownAndTeamOwner = false,
                            canLootOnlyOwner = true,
                            damage = 100f,
                            scaleDamage = new Dictionary<string, float>
                            {
                                ["Npc"] = 1f,
                            },
                            lootCrate = false,
                            hackCrate = false,
                            lootNpc = false,
                            damageNpc = false,
                            targetNpc = false,
                            canEnter = false,
                            canEnterCooldownPlayer = true,
                            timeExitOwner = 60,
                            alertTime = 60,
                            restoreUponDeath = true,
                            cooldown = 86400
                        }
                    }
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.CaravanExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();

            using (var enumerator = source.GetEnumerator())
                while (enumerator.MoveNext())
                    if (predicate(enumerator.Current))
                        result.Add(enumerator.Current);
            return result;
        }

        public static List<TSource> WhereList<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static HashSet<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(predicate(enumerator.Current));
            return result;
        }

        public static List<TResult> Select<TSource, TResult>(this IList<TSource> source, Func<TSource, TResult> predicate)
        {
            List<TResult> result = new List<TResult>();
            for (int i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                result.Add(predicate(element));
            }
            return result;
        }

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static bool IsRealPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static List<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            List<TSource> result = source.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                for (int j = 0; j < result.Count - 1; j++)
                {
                    if (predicate(result[j]) > predicate(result[j + 1]))
                    {
                        TSource z = result[j];
                        result[j] = result[j + 1];
                        result[j + 1] = z;
                    }
                }
            }
            return result;
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static List<TSource> Shuffle<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = source.ToList();

            for (int i = 0; i < result.Count; i++)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var temp = result[j];
                result[j] = result[i];
                result[i] = temp;
            }

            if (result == null)
                return new List<TSource>();

            return result;
        }

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue < resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }

        public static TSource First<TSource>(this IList<TSource> source) => source[0];

        public static TSource Last<TSource>(this IList<TSource> source) => source[source.Count - 1];

        public static bool IsEqualVector3(this Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.1f;

        public static List<TSource> OrderByQuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate)
        {
            return source.QuickSort(predicate, 0, source.Count - 1);
        }

        private static List<TSource> QuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate, int minIndex, int maxIndex)
        {
            if (minIndex >= maxIndex) return source;

            int pivotIndex = minIndex - 1;
            for (int i = minIndex; i < maxIndex; i++)
            {
                if (predicate(source[i]) < predicate(source[maxIndex]))
                {
                    pivotIndex++;
                    source.Replace(pivotIndex, i);
                }
            }
            pivotIndex++;
            source.Replace(pivotIndex, maxIndex);

            QuickSort(source, predicate, minIndex, pivotIndex - 1);
            QuickSort(source, predicate, pivotIndex + 1, maxIndex);

            return source;
        }

        private static void Replace<TSource>(this IList<TSource> source, int x, int y)
        {
            TSource t = source[x];
            source[x] = source[y];
            source[y] = t;
        }

        public static object GetPrivateFieldValue(this object obj, string fieldName)
        {
            FieldInfo fi = GetPrivateFieldInfo(obj.GetType(), fieldName);
            if (fi != null) return fi.GetValue(obj);
            else return null;
        }

        public static void SetPrivateFieldValue(this object obj, string fieldName, object value)
        {
            FieldInfo info = GetPrivateFieldInfo(obj.GetType(), fieldName);
            if (info != null) info.SetValue(obj, value);
        }

        public static FieldInfo GetPrivateFieldInfo(Type type, string fieldName)
        {
            foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) if (fi.Name == fieldName) return fi;
            return null;
        }

        public static Action GetPrivateAction(this object obj, string methodName)
        {
            MethodInfo mi = obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null) return (Action)Delegate.CreateDelegate(typeof(Action), obj, mi);
            else return null;
        }

        public static object CallPrivateMethod(this object obj, string methodName, params object[] args)
        {
            MethodInfo mi = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null) return mi.Invoke(obj, args);
            else return null;
        }
    }
}