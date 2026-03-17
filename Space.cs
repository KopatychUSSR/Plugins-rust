using System.Collections.Generic;
using Newtonsoft.Json;
using CompanionServer.Handlers;
using Oxide.Plugins.SpaceExtensionMethods;
using UnityEngine;
using System;
using Oxide.Core.Plugins;
using Rust;
using System.Collections;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Reflection;
using System.IO;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Space", "Adem", "1.3.8")]
    class Space : RustPlugin
    {
        [PluginReference] Plugin NpcSpawn, PveMode, GUIAnnouncements, DiscordMessages, Economics, ServerRewards, IQEconomic, NightVision, Friends, Clans, Notify;

        #region Variables
        const bool en = false;
        static Space ins;
        HashSet<string> allHooks = new HashSet<string>
        {
            "OnPlayerDeath",
            "OnPlayerSleep",
            "CanMoveItem",
            "OnItemRemovedFromContainer",
            "OnLootSpawn",
            "OnItemDropped",
            "OnEntityDismounted",
            "OnPlayerWantsMount",
            "OnEntityMounted",
            "OnStructureRepair",
            "OnHotAirBalloonToggled",
            "CanBuild",
            "CanAffordUpgrade",
            "OnStructureRotate",
            "OnEntityBuilt",
            "OnCardSwipe",
            "OnButtonPress",
            "OnTurretTarget",
            "CanBeTargeted",
            "OnDoorOpened",
            "OnEntitySpawned",
            "OnEntityTakeDamage",
            "OnEntityDeath",
            "OnEntityKill",
            "OnPlayerViolation",
            "OnLootEntity",

            "CanPopulateLoot",
            "OnCustomLootContainer",
            "CanEntityTakeDamage",
            "CanBradleySpawnNpc",
            "OnPlayerViolation",
            "OnCorpsePopulate",
            "CanEntityBeTargeted",
            "OnPlayerCorpseSpawned"
        };
        HashSet<string> permanentHooks = new HashSet<string>
        {
            "OnLootSpawn",
            "OnEntityMounted",
            "OnEntityDismounted",
            "OnEntityBuilt",
            "OnSamSiteTarget"
        };
        #endregion Variables

        #region API
        bool IsPositionInSpace(Vector3 position)
        {
            if (position == null) return false;
            return SpaceClass.IsInSpace(position, false);
        }

        float GetMinSpaceAltitude()
        {
            return _config.mainConfig.spaceHeight - _config.mainConfig.spaceRadius;
        }

        bool IsEventActive()
        {
            return EventManager.isEventActive;
        }
        #endregion API

        #region Hooks
        void Init() => Unsubscribes(true);

        void OnServerInitialized()
        {
            ins = this;
            Subscribes(false);
            GuiManager.LoadImages();
            UpdateConfig();
            LoadDefaultMessages();
            PostLoadCheck();

            SpaceShuttle.CheckAllDuoSubmarine();
            SpaceShuttle.RespawnShuttles();
            Aerostat.CheckAllBallons();

            if (_config.mainConfig.isAutoEvent)
                EventManager.TryStartEvent(immediately: false);
        }

        void Unload()
        {
            SpaceShuttle.StopRespawnShuttles();
            EventManager.StopEvent(unload: true);
            SpaceShuttle.DestroyAllShuttles();
            Aerostat.DestroyAllAerostats();
            ins = null;
        }

        void OnPlayerDeath(BasePlayer player)
        {
            if (player == null) return;
            Astronaut.TryRemoveComponentFromPlayer(player.userID);
            GuiManager.DestroyGui(player);
        }

        void OnPlayerSleep(BasePlayer player)
        {
            if (player == null) return;
            Astronaut.TryRemoveComponentFromPlayer(player.userID);
            GuiManager.DestroyGui(player);
        }

        void CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            if (item == null) return;
            else if (item.info.isWearable)
            {
                Astronaut Astronaut = Astronaut.GetComponentFromUserId(playerLoot.containerWear.playerOwner.userID);
                if (Astronaut != null) NextTick(() => Astronaut.OnWearContainerChanged());
            }
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (item == null || !item.info.isWearable || container == null || container.playerOwner == null) return;
            else if (container.uid == container.playerOwner.inventory.containerWear.uid)
            {
                Astronaut Astronaut = Astronaut.GetComponentFromUserId(container.playerOwner.userID);
                if (Astronaut != null) NextTick(() => Astronaut.OnWearContainerChanged());
            }
        }

        void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return;
            float itemChance;
            if (_config.shuttleConfig.itemConfig.inLootSpawn && _config.shuttleConfig.itemConfig.crateChanses.TryGetValue(container.PrefabName, out itemChance))
            {
                LootManager.TrySpawnItemInDefaultCrate(container, _config.shuttleConfig.itemConfig, itemChance, 0);
            }
            if (_config.spaceCardConfig.inLootSpawn && _config.spaceCardConfig.crateChanses.TryGetValue(container.PrefabName, out itemChance))
            {
                LootManager.TrySpawnItemInDefaultCrate(container, _config.spaceCardConfig, itemChance, 1);
            }
            if (_config.aerostatConfig.itemConfig.inLootSpawn && _config.aerostatConfig.itemConfig.crateChanses.TryGetValue(container.PrefabName, out itemChance))
            {
                LootManager.TrySpawnItemInDefaultCrate(container, _config.aerostatConfig.itemConfig, itemChance, 2);
            }
        }

        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (entity == null || !EventManager.isEventActive) return;
            if (SpaceClass.IsInSpace(entity.transform.position)) SpaceClass.OnItemDropInSpace(entity);
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (!player.IsRealPlayer() || !entity.IsExists()) return;
            BaseVehicle vehicle = entity.VehicleParent();
            if (vehicle != null)
            {
                SubmarineDuo submarineDuo = vehicle as SubmarineDuo;
                if (submarineDuo != null)
                {
                    SpaceShuttle spaceShuttle = SpaceShuttle.GetSpaceShuttleByMainSubmarineNetId(submarineDuo.net.ID.Value);
                    if (spaceShuttle != null)
                    {
                        spaceShuttle.DisactivateShuttle();
                        DisableThirdViewMod(player);
                    }
                }
                Astronaut.TryAddComponentToPlayer(player);
            }
        }

        object OnPlayerWantsMount(BasePlayer player, SubmarineDuo submarineDuo)
        {
            if (submarineDuo == null || !player.IsRealPlayer() || submarineDuo.net == null) return null;
            SpaceShuttle controlledSpaceShuttle = SpaceShuttle.GetSpaceShuttleByMainSubmarineNetId(submarineDuo.net.ID.Value);
            if (controlledSpaceShuttle != null)
            {
                if (!SpaceClass.IsInSpace(submarineDuo.transform.position) && Vector3.Angle(Vector3.up, submarineDuo.transform.up) > 90) return null;
                Astronaut.TryRemoveComponentFromPlayer(player.userID);
                submarineDuo.AttemptMount(player, false);
                return true;
            }
            return null;
        }

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            BaseVehicle vehicle = entity.VehicleParent();
            if (vehicle == null) return;
            SubmarineDuo submarineDuo = vehicle as SubmarineDuo;
            if (submarineDuo != null && submarineDuo.net != null)
            {
                SpaceShuttle spaceShuttle = SpaceShuttle.GetSpaceShuttleByMainSubmarineNetId(submarineDuo.net.ID.Value);
                if (spaceShuttle != null)
                {
                    spaceShuttle.ActivateShuttle(player);
                }
            }
        }

        object OnStructureRepair(DecorSubmarineDuo baseCombatEntity, BasePlayer player)
        {
            if (baseCombatEntity == null || baseCombatEntity.net == null) return null;
            if (baseCombatEntity.ShortPrefabName == "submarinesolo.entity")
            {
                SpaceShuttle controlledSpaceShuttle = SpaceShuttle.GetSpaceShuttleByDecorSubmarineNetId(baseCombatEntity.net.ID.Value);
                if (controlledSpaceShuttle != null) controlledSpaceShuttle.OnDecorSubmarineRepair(player);
                return true;
            }
            return null;
        }

        void OnHotAirBalloonToggled(HotAirBalloon balloon, BasePlayer player)
        {
            if (balloon == null || balloon.net == null) return;
            Aerostat aerostat = Aerostat.GetAetostatByBallonNetId(balloon.net.ID.Value);
            if (aerostat != null) aerostat.OnAerostatToggle();
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (!EventManager.isEventActive || planner == null) return null;
            BasePlayer player = planner.GetOwnerPlayer();
            if (player != null && SpaceClass.IsInSpace(player.transform.position)) return false;
            return null;
        }

        object CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (!EventManager.isEventActive || player == null) return null;
            if (player != null && SpaceClass.IsInSpace(player.transform.position)) return false;
            else return null;
        }

        object OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            if (!EventManager.isEventActive || player == null) return null;
            if (player != null && SpaceClass.IsInSpace(player.transform.position)) return true;
            else return null;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null) return;
            Item item = plan.GetItem();
            if (item == null) return;
            if (item.info.shortname == ins._config.shuttleConfig.itemConfig.shortname && item.skin == ins._config.shuttleConfig.itemConfig.skin)
            {
                BaseEntity entity = go.ToBaseEntity();
                if (entity == null) return;
                SpaceShuttle.SpawnSpaceShuttle(entity.transform.position, entity.transform.rotation);
                ins.NextTick(() => entity.Kill());
            }
            else if (item.info.shortname == ins._config.aerostatConfig.itemConfig.shortname && item.skin == ins._config.aerostatConfig.itemConfig.skin)
            {
                BaseEntity entity = go.ToBaseEntity();
                if (entity == null) return;
                Aerostat.SpawnAerostat(entity.transform.position, entity.transform.rotation);
                ins.NextTick(() => entity.Kill());
            }
        }

        object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (cardReader == null || card == null || player == null) return null;
            if (SpaceClass.IsInSpace(cardReader.transform.position))
                return SpaceStation.OnStationCardReaderSwipe(cardReader, card, player);

            return null;
        }

        void OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button == null) return;
            if (SpaceClass.IsInSpace(button.transform.position))
                SpaceStation.OnStationButtonPress(button);
        }

        object OnTurretTarget(AutoTurret turret, BasePlayer player)
        {
            if (!EventManager.isEventActive || turret == null) return null;
            if (SpaceClass.IsInSpace(turret.transform.position) && !player.IsRealPlayer()) return true;
            return null;
        }

        object CanBeTargeted(BasePlayer player, FlameTurret flameTurret)
        {
            if (!EventManager.isEventActive || !player.IsRealPlayer() || flameTurret == null) return null;
            if (SpaceClass.IsInSpace(flameTurret.transform.position)) return true;
            return null;
        }

        void OnDoorOpened(Door door, BasePlayer player)
        {
            if (!EventManager.isEventActive || door == null) return;
            if (SpaceClass.IsInSpace(door.transform.position)) SpaceStation.OnDoorOpened(door);
        }

        void OnCorpsePopulate(BasePlayer entity, NPCPlayerCorpse corpse)
        {
            if (!EventManager.isEventActive || entity == null || corpse == null) return;
            if (entity is ScientistNPC)
            {
                if (SpaceClass.IsInSpace(corpse.transform.position))
                {
                    Rigidbody rigidBody = corpse.GetComponent<Rigidbody>();
                    if (rigidBody != null && rigidBody.useGravity)
                    {
                        rigidBody.useGravity = false;
                        rigidBody.isKinematic = true;
                    }
                }

                NpcConfig npcConfig = _config.npcConfigs.FirstOrDefault(x => x.name == entity.displayName);
                if (npcConfig == null) return;
                NextTick(() =>
                {
                    if (corpse == null) return;
                    ItemContainer container = corpse.containers[0];
                    LootManager.UpdateLootContainerLootTable(container, npcConfig.lootTable, npcConfig.typeLootTable);
                    if (npcConfig.deleteCorpse && !corpse.IsDestroyed) corpse.Kill();
                });
            }
        }

        void OnPlayerCorpseSpawned(BasePlayer player, PlayerCorpse corpse)
        {
            if (corpse == null) return;
            if (SpaceClass.IsInSpace(corpse.transform.position))
            {
                Rigidbody rigidBody = corpse.GetComponent<Rigidbody>();
                if (rigidBody != null && rigidBody.useGravity)
                {
                    rigidBody.useGravity = false;
                    rigidBody.isKinematic = true;
                }
            }
        }

        void OnEntitySpawned(DroppedItemContainer entity)
        {
            if (entity == null) return;
            if (SpaceClass.IsInSpace(entity.transform.position))
            {
                Rigidbody rigidBody = entity.GetComponent<Rigidbody>();
                if (rigidBody == null || !rigidBody.useGravity) return;
                rigidBody.useGravity = false;
            }
        }

        void OnEntitySpawned(HelicopterDebris entity)
        {
            if (!EventManager.isEventActive || entity == null) return;
            if (SpaceClass.IsInSpace(entity.transform.position) && entity.IsExists()) entity.Kill();
        }

        void OnEntitySpawned(HotAirBalloon entity)
        {
            if (_config.aerostatConfig.allBallons && !SpaceClass.IsInSpace(entity.transform.position) && entity.skinID == 0)
                Aerostat.AttachAerostatClass(entity);
        }

        void OnEntitySpawned(FireBall entity)
        {
            if (entity == null)
                return;

            if (SpaceClass.IsInSpace(entity.transform.position, false))
                entity.Kill();
        }

        void OnEntityTakeDamage(SubmarineDuo submarine, HitInfo info)
        {
            if (!submarine.IsExists() || submarine.net == null) return;
            SpaceShuttle controlledSpaceShuttle = SpaceShuttle.GetSpaceShuttleByMainSubmarineNetId(submarine.net.ID.Value);
            if (controlledSpaceShuttle != null) controlledSpaceShuttle.OnMainSubmarineGetDamage();
            return;
        }

        object OnEntityTakeDamage(DecorSubmarineDuo baseCombatEntity, HitInfo info)
        {
            if (!baseCombatEntity.IsExists() || baseCombatEntity.net == null) return null;
            if (baseCombatEntity.ShortPrefabName == "submarinesolo.entity")
            {
                SpaceShuttle controlledSpaceShuttle = SpaceShuttle.GetSpaceShuttleByDecorSubmarineNetId(baseCombatEntity.net.ID.Value);
                if (controlledSpaceShuttle != null) controlledSpaceShuttle.OnDecorSubmarineGetDamage(info);
                return true;
            }
            return null;
        }

        object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (!EventManager.isEventActive || player == null || info == null || info.Initiator == null) return null;
            HotAirBalloon attackerBalloon = info.Initiator as HotAirBalloon;
            if (attackerBalloon != null && SpaceClass.IsInSpace(attackerBalloon.transform.position)) return true;
            return null;
        }

        object OnEntityTakeDamage(ScientistNPC scientistNPC, HitInfo info)
        {
            if (scientistNPC == null || info == null)
                return null;

            if (SpaceClass.IsInSpace(scientistNPC.transform.position))
            {
                if (info.InitiatorPlayer.IsRealPlayer() && !SpaceClass.IsInSpace(info.InitiatorPlayer.transform.position))
                {
                    return true;
                }
            }

            return null;
        }

        object OnEntityTakeDamage(BradleyAPC bradleyAPC, HitInfo info)
        {
            if (bradleyAPC == null || info == null)
                return null;

            if (SpaceClass.IsInSpace(bradleyAPC.transform.position))
            {
                if (info.InitiatorPlayer.IsRealPlayer() && !SpaceClass.IsInSpace(info.InitiatorPlayer.transform.position))
                {
                    return true;
                }
            }

            return null;
        }

        void OnEntityDeath(ScientistNPC scientistNPC, HitInfo info)
        {
            if (!EventManager.isEventActive || scientistNPC == null || info == null) return;
            if (info.InitiatorPlayer.IsRealPlayer() && SpaceClass.IsInSpace(scientistNPC.transform.position)) EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Npc");
        }

        void OnEntityDeath(BradleyAPC entity, HitInfo info)
        {
            if (!EventManager.isEventActive || entity == null || info == null) return;
            if (info.InitiatorPlayer.IsRealPlayer() && SpaceClass.IsInSpace(entity.transform.position)) EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Bradley");
        }

        void OnEntityDeath(AutoTurret entity, HitInfo info)
        {
            if (!EventManager.isEventActive || entity == null || info == null) return;
            if (info.InitiatorPlayer.IsRealPlayer() && SpaceClass.IsInSpace(entity.transform.position)) EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Turret");
        }

        void OnEntityDeath(FlameTurret entity, HitInfo info)
        {
            if (!EventManager.isEventActive || entity == null || info == null) return;
            if (info.InitiatorPlayer.IsRealPlayer() && SpaceClass.IsInSpace(entity.transform.position)) EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "FlameTurret");
        }

        void OnEntityDeath(Door entity, HitInfo info)
        {
            if (!EventManager.isEventActive || entity == null || info == null) return;
            if (info.InitiatorPlayer.IsRealPlayer() && SpaceClass.IsInSpace(entity.transform.position))
            {
                EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Door");
            }
        }

        void OnEntityKill(Door entity)
        {
            if (!EventManager.isEventActive || entity == null) return;
            if (SpaceClass.IsInSpace(entity.transform.position))
            {
                SpaceStation.OnDoorOpened(entity);
            }
        }

        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (EventManager.isEventActive && type == AntiHackType.FlyHack && SpaceClass.IsInSpace(player.transform.position))
            {
                return true;
            }
            return null;
        }

        void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || !container.IsExists() || container.net == null || !SpaceClass.IsInSpace(container.transform.position, false)) return;
            if (LootManager.IsEventCrate(container.net.ID.Value))
            {
                LootManager.OnEventCrateLooted(container, player);
            }
        }
        #region SupportedPLugins
        object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (!EventManager.isEventActive || entity == null) return null;

            NpcConfig roamNpcConfig = ins._config.npcConfigs.FirstOrDefault(x => x.name == entity.displayName);
            if (roamNpcConfig != null)
            {
                if (roamNpcConfig.typeLootTable != 2) return true;
                return null;
            }
            return null;
        }

        object CanPopulateLoot(LootContainer container)
        {
            if (!EventManager.isEventActive || container == null) return null;
            if (SpaceClass.IsInSpace(container.transform.position))
            {
                CrateConfig crateConfig = _config.crates.FirstOrDefault(x => x.prefab == container.PrefabName);
                if (crateConfig != null && crateConfig.typeLootTable != 2) return true;
            }
            return null;
        }

        object OnCustomLootContainer(ulong netID)
        {
            string cratePrefabName = null;
            cratePrefabName = SpaceStation.GetContainerPrefabNameBynetId(netID);
            if (cratePrefabName == null) SpaceClass.GetContainerPrefabNameBynetId(netID);
            if (cratePrefabName != null)
            {
                CrateConfig crateConfig = _config.crates.FirstOrDefault(x => x.prefab == cratePrefabName);
                if (crateConfig != null && crateConfig.typeLootTable != 3) return true;
            }
            return null;
        }

        object CanEntityTakeDamage(AutoTurret autoTurret, HitInfo hitinfo)
        {
            if (!EventManager.isEventActive || autoTurret == null || hitinfo == null) return null;
            if (SpaceClass.IsInSpace(autoTurret.transform.position))
            {
                if (!hitinfo.InitiatorPlayer.IsRealPlayer())
                    return false;

                if (hitinfo.InitiatorPlayer != null && !PveModeController.IsPveModeBlockAction(hitinfo.InitiatorPlayer))
                    return true;
                else
                    return null;
            }
            return null;
        }

        object CanEntityTakeDamage(FlameTurret flameTurret, HitInfo hitinfo)
        {
            if (!EventManager.isEventActive || flameTurret == null || hitinfo == null) return null;
            if (SpaceClass.IsInSpace(flameTurret.transform.position))
            {
                if (!hitinfo.InitiatorPlayer.IsRealPlayer())
                    return false;

                if (hitinfo.InitiatorPlayer != null && !PveModeController.IsPveModeBlockAction(hitinfo.InitiatorPlayer))
                    return true;
                else
                    return null;
            }
            return null;
        }

        object CanEntityTakeDamage(Door door, HitInfo hitinfo)
        {
            if (!EventManager.isEventActive || door == null || hitinfo == null) return null;
            if (SpaceClass.IsInSpace(door.transform.position))
            {
                if (!hitinfo.InitiatorPlayer.IsRealPlayer())
                    return false;

                if (hitinfo.InitiatorPlayer != null && !PveModeController.IsPveModeBlockAction(hitinfo.InitiatorPlayer))
                    return true;
                else
                    return null;
            }
            return null;
        }

        object CanEntityTakeDamage(ScientistNPC scientistNPC, HitInfo hitinfo)
        {
            if (!EventManager.isEventActive || scientistNPC == null || hitinfo == null) return null;
            if (SpaceClass.IsInSpace(scientistNPC.transform.position))
            {
                AutoTurret autoTurret = hitinfo.Initiator as AutoTurret;
                if (autoTurret != null) return false;
            }
            return null;
        }

        object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (!EventManager.isEventActive || victim == null || hitinfo == null || !victim.IsRealPlayer()) return null;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (attacker.IsRealPlayer())
            {
                if (_config.zoneConfig.isCreateZonePVP && EventZone.playersInSpace.Contains(victim) && EventZone.playersInSpace.Contains(attacker)) return true;
            }
            else if (hitinfo.Initiator != null && SpaceClass.IsInSpace(hitinfo.Initiator.transform.position))
            {
                AutoTurret autoTurret = hitinfo.Initiator as AutoTurret;
                if (autoTurret != null)
                {
                    if (victim != null && !PveModeController.IsPveModeBlockAction(victim))
                        return true;
                    else
                        return null;
                }
                FlameTurret flameTurret = hitinfo.Initiator as FlameTurret;
                if (flameTurret != null)
                {
                    if (victim != null && !PveModeController.IsPveModeBlockAction(victim))
                        return true;
                    else
                        return null;
                }
                HotAirBalloon attackerBalloon = hitinfo.Initiator as HotAirBalloon;
                if (attackerBalloon != null)
                {
                    if (hitinfo.InitiatorPlayer != null && !PveModeController.IsPveModeBlockAction(hitinfo.InitiatorPlayer))
                        return true;
                    else
                        return null;
                }
            }
            return null;
        }

        object CanEntityBeTargeted(BasePlayer target, AutoTurret autoTurret)
        {
            if (!EventManager.isEventActive || autoTurret == null || !target.IsRealPlayer()) return null;
            if (SpaceClass.IsInSpace(autoTurret.transform.position))
            {
                if (target != null && !PveModeController.IsPveModeBlockAction(target))
                    return true;
                else
                    return null;
            }
            return null;
        }

        object CanEntityBeTargeted(BasePlayer target, FlameTurret flameTurret)
        {
            if (!EventManager.isEventActive || flameTurret == null || !target.IsRealPlayer()) return null;
            if (SpaceClass.IsInSpace(flameTurret.transform.position))
            {
                if (target != null && !PveModeController.IsPveModeBlockAction(target))
                    return true;
                else
                    return null;
            }
            return null;
        }

        void OnJetpackRemoved(BasePlayer player)
        {
            if (player.IsRealPlayer()) Astronaut.TryAddComponentToPlayer(player);
        }

        void OnJetpackWear(BasePlayer player)
        {
            if (player.IsRealPlayer()) Astronaut.TryRemoveComponentFromPlayer(player.userID);
        }

        object CanBradleySpawnNpc(BradleyAPC bradley)
        {
            if (bradley != null && SpaceClass.IsInSpace(bradley.transform.position)) return true;
            return null;
        }

        void SetOwnerPveMode(string eventName, BasePlayer owner)
        {
            if (eventName == Name)
                PveModeController.SetPveModeOwner(owner);
        }

        void ClearOwnerPveMode(string eventName)
        {
            if (eventName == Name)
                PveModeController.ClearPveModeOwner();
        }
        #endregion SupportedPLugins
        #endregion Hooks

        #region Commands
        [ChatCommand("spacestart")]
        void ChatStartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            if (arg.Length > 0) EventManager.TryStartEvent(true, arg[0], player);
            else EventManager.TryStartEvent(immediately: true, initiator: player);
        }

        [ConsoleCommand("spacestart")]
        void ConsoleStartCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            if (arg.Args != null && arg.Args.Length > 0) EventManager.TryStartEvent(true, arg.Args[0]);
            else EventManager.TryStartEvent(immediately: true);
        }

        [ChatCommand("spacestop")]
        void ChatStopCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            EventManager.StopEvent();
        }

        [ConsoleCommand("spacestop")]
        void ConsoleStopCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) EventManager.StopEvent();
        }

        [ChatCommand("zerogravity")]
        void ChatZeroGravityCommand(BasePlayer player, string command, string[] arg)
        {
            if (Astronaut.GetComponentFromUserId(player.userID) != null) Astronaut.TryRemoveComponentFromPlayer(player.userID);
            else Astronaut.TryAddComponentToPlayer(player);
        }

        [ChatCommand("spawnshuttle")]
        void ChatShuttleCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            SpaceShuttle spaceShuttle = SpaceShuttle.SpawnSpaceShuttle(player.transform.position, Quaternion.identity);
            spaceShuttle.AddFuel(100);
        }

        [ChatCommand("spawnaerostat")]
        void ChatAerostatCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            Aerostat.SpawnAerostat(player.transform.position, Quaternion.identity);
        }

        [ChatCommand("giveshuttle")]
        void ChatGiveShuttleCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            Item shuttleItem = LootManager.CreateItem(_config.shuttleConfig.itemConfig, 1);
            LootManager.GiveItemToPLayer(player, shuttleItem);
        }

        [ChatCommand("giveaerostat")]
        void ChatGiveAetostatCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            Item aerostatItem = LootManager.CreateItem(_config.aerostatConfig.itemConfig, 1);
            LootManager.GiveItemToPLayer(player, aerostatItem);
        }

        [ConsoleCommand("givepurplecard")]
        void GivePurpleCardCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) 
                return;

            BasePlayer target = null;
            int amount = 1;

            if (arg.Args.Length >= 1) target = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0]));
            if (arg.Args.Length >= 2) amount = Convert.ToInt32(arg.Args[1]);

            if (target == null)
            {
                NotifyManager.PrintError(null, "Player not found");
                return;
            }

            Item spaceCardItem = LootManager.CreateItem(_config.spaceCardConfig, amount);
            LootManager.GiveItemToPLayer(target, spaceCardItem);
            PrintToChat(target, GetMessage("GetSpaceCard", target.UserIDString, _config.prefix));
            Puts($"A space card was given to {target.displayName}");
        }

        [ConsoleCommand("giveshuttle")]
        void GiveShuttleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            BasePlayer target = null;
            int amount = 1;

            if (arg.Args.Length >= 1) target = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0]));
            if (arg.Args.Length >= 2) amount = Convert.ToInt32(arg.Args[1]);

            if (target == null)
            {
                NotifyManager.PrintError(null, "Player not found");
                return;
            }

            Item spaceCardItem = LootManager.CreateItem(_config.shuttleConfig.itemConfig, amount);
            LootManager.GiveItemToPLayer(target, spaceCardItem);
            PrintToChat(target, GetMessage("GetSpaceShuttle", target.UserIDString, _config.prefix));
            Puts($"A shuttle was given to {target.displayName}");
        }

        [ConsoleCommand("giveaerostat")]
        void GiveAerostatCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            BasePlayer target = null;
            int amount = 1;

            if (arg.Args.Length >= 1) target = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0]));
            if (arg.Args.Length >= 2) amount = Convert.ToInt32(arg.Args[1]);

            if (target == null)
            {
                NotifyManager.PrintError(null, "Player not found");
                return;
            }
            Item spaceCardItem = LootManager.CreateItem(_config.aerostatConfig.itemConfig, amount);
            LootManager.GiveItemToPLayer(target, spaceCardItem);
            PrintToChat(target, GetMessage("GetAerostat", target.UserIDString, _config.prefix));
            Puts($"An aerostat was given to {target.displayName}");
        }

        [ConsoleCommand("givespacesuit")]
        void GiveSuitCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            BasePlayer target = null;

            if (arg.Args.Length < 2) return;
            SpaceSuitConfig spaceSuitConfig = _config.spaceSuits.FirstOrDefault(x => x.presetName == arg.Args[0]);
            int amount = 1;
            if (arg.Args.Length >= 2) target = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[1]));
            if (arg.Args.Length >= 3) amount = Convert.ToInt32(arg.Args[2]);

            if (target == null)
            {
                NotifyManager.PrintError(null, "Player not found");
                return;
            }

            Item spaceCardItem = LootManager.CreateItem(spaceSuitConfig.itemConfig, amount);
            LootManager.GiveItemToPLayer(target, spaceCardItem);
            PrintToChat(target, GetMessage("GetSpaceSuit", target.UserIDString, _config.prefix));
            Puts($"An Space Suit was given to {target.displayName}");
        }

        [ChatCommand("spacepoint")]
        void SpawnPointCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            _config.mainConfig.customSpawnPoints.Add(player.transform.position.ToString());
            PrintToChat(player, _config.prefix + " New spawn point <color=#738d43>successfully</color> added to the config");
            SaveConfig();
        }

        [ChatCommand("spacetest")]
        void SpaceTestCommand(BasePlayer player, string command, string[] arg)
        {
            SpaceShuttle.SpawnSpaceShuttle(player.transform.position, Quaternion.identity);
        }
        #endregion Commands

        #region Methods
        void UpdateConfig()
        {
            if (_config.version == null || _config.version == new VersionNumber(0, 0, 0))
            {
                ins.PrintError("The configuration file is corrupted!");
                return;
            }
            if (_config.version.Minor == 0)
            {
                if (_config.version.Patch == 0)
                {
                    _config.shuttleConfig.control = new ShuttleControl
                    {
                        invertX = false,
                        invertY = false,
                    };

                    foreach (StorageCrateConfig storageCrateConfig in ins._config.storageCrates)
                    {
                        foreach (LootItemConfig lootItemConfig in storageCrateConfig.lootTable.items)
                        {
                            if (lootItemConfig.shortname == "chocholate")
                            {
                                lootItemConfig.shortname = "potato";
                            }
                        }
                    }
                }
                if (_config.version.Patch == 1)
                {
                    _config.astronautConfig.thirdPerson = false;
                }
                if (_config.version.Patch < 4)
                {
                    _config.mainConfig.spaceHeight = 700;
                    _config.shuttleConfig.samSiteConfig = new SamSiteConfig
                    {
                        enable = true,
                        bigRangeAndSpeed = false
                    };
                    _config.shuttleConfig.respawnConfig.eventBegignigSpawnConfig = new ShuttleEventBegignigSpawnConfig
                    {
                        deleteShuttles = true,
                        roadShuttlesNumber = 0
                    };
                }
                if (_config.version.Patch < 5)
                {
                    _config.mainConfig.spaceRadius = 200;
                }
                if (_config.version.Patch <= 9)
                {
                    _config.shuttleConfig.shuttleMarkerConfig = new ShuttleMarkerConfig
                    {
                        enable = false,
                        name = en ? "Space Shuttle" : "Космический корабль"
                    };
                }
                _config.version = new VersionNumber(1, 1, 0);
            }
            if (_config.version.Minor == 1)
            {
                if (_config.version.Patch <= 1)
                {
                    _config.shuttleConfig.enableCampfires = true;
                }
                if (_config.version.Patch <= 8)
                {
                    SpaceSuitConfig spaceSuitConfig = _config.spaceSuits.FirstOrDefault(x => x.itemConfig.shortname == "hazmatsuit" && x.itemConfig.skin == 10180);
                    if (spaceSuitConfig != null)
                    {
                        spaceSuitConfig.itemConfig.shortname = "hazmatsuit.spacesuit";
                        spaceSuitConfig.itemConfig.skin = 0;
                    }
                }
                _config.version = new VersionNumber(1, 2, 0);
            }
            if (_config.version.Minor == 2)
            {
                if (_config.version.Patch == 0)
                {
                    _config.mainConfig.customSpawnPoints = new List<string>();
                }
                if (_config.version.Patch <= 3)
                {
                    if (_config.mainConfig.customSpawnPoints == null)
                        _config.mainConfig.customSpawnPoints = new List<string>();
                }
                if (_config.version.Patch <= 6)
                {
                    _config.spaceSuits = new HashSet<SpaceSuitConfig>
                    {
                        new SpaceSuitConfig
                        {
                            presetName = "spacesuit_1",
                            itemConfig = new ItemConfig
                            {
                                shortname = "hazmatsuit",
                                skin = 0,
                                name = ""
                            },
                            additionalAstronautParameters = new AstronautParametersConfig(canBreathe: true, soudOfBreathe: true)
                        },
                        new SpaceSuitConfig
                        {
                            presetName = "spacesuit_2",
                            itemConfig = new ItemConfig
                            {
                                shortname = "hazmatsuit",
                                skin = 10180,
                                name = ""
                            },
                            additionalAstronautParameters = new AstronautParametersConfig(canBreathe: true, soudOfBreathe: true)
                        },
                        new SpaceSuitConfig
                        {
                            presetName = "spacesuit_3",
                            itemConfig = new ItemConfig
                            {
                                shortname = "diving.tank",
                                skin = 0,
                                name = ""
                            },
                            additionalAstronautParameters = new AstronautParametersConfig(canBreathe: true)
                        }
                    };
                }
                if (_config.version.Patch <= 7)
                {
                    foreach (EventConfig eventConfig in _config.eventConfigs)
                    {
                        eventConfig.mainDoorConfig = new MainDoorConfig
                        {
                            isOpenAutomatically = true,
                            openButtonLocations = new HashSet<LocationConfig>
                            {
                                new LocationConfig
                                {
                                    position = "(-4.302, -1.706, -56.597)",
                                    rotation = "(0, 0, 0)"
                                },
                                new LocationConfig
                                {
                                    position = "(4.666, -1.807, -56.595)",
                                    rotation = "(0, 0, 0)"
                                },
                                new LocationConfig
                                {
                                    position = "(-4.75, -0.634, -56.72)",
                                    rotation = "(0, 180, 0)"
                                },
                                new LocationConfig
                                {
                                    position = "(4.75, -0.737, -56.72)",
                                    rotation = "(0, 180, 0)"
                                },

                                new LocationConfig
                                {
                                    position = "(-0.9, -1.251, -47.5)",
                                    rotation = "(0, 0, 0)"
                                },
                                new LocationConfig
                                {
                                    position = "(1.5, -1.349, -47.811)",
                                    rotation = "(0, 180, 0)"
                                }
                            }
                        };
                    }
                }
                _config.version = new VersionNumber(1, 3, 0);
            }
            if (_config.version.Minor == 3)
            {
                if (_config.version.Patch == 0)
                {
                    _config.shuttleConfig.maxTorpedoDistance = 150;
                    _config.notifyConfig.guiConfig.offsetMinY = -56;
                }

                if (_config.version.Patch <= 1)
                {
                    _config.supportedPluginsConfig.pveMode.scaleDamage = new Dictionary<string, float>
                    {
                        ["Npc"] = 1f,
                        ["Bradley"] = 2f,
                        ["Turret"] = 1f
                    };
                }

                if (_config.version.Patch <= 2)
                {
                    _config.markerConfig.isRingMarker = true;
                    _config.markerConfig.isShopMarker = true;
                }

                if (_config.version.Patch <= 7)
                {
                    _config.notifyConfig.gameTipConfig = new GameTipConfig
                    {
                        isEnabled = false,
                        style = 2,
                    };
                }
            }
            foreach (CrateConfig crateConfig in _config.crates)
            {
                crateConfig.lootTable.items = crateConfig.lootTable.items.OrderBy(x => x.chance);
            }

            foreach (StorageCrateConfig storageCrateConfig in _config.storageCrates)
            {
                storageCrateConfig.lootTable.items = storageCrateConfig.lootTable.items.OrderBy(x => x.chance);
            }
            _config.version = Version;
            SaveConfig();
        }

        void PostLoadCheck()
        {
            NpcManager.CheckNPCSpawn();
        }

        void Unsubscribes(bool all)
        {
            foreach (string hook in allHooks) if (all || !permanentHooks.Contains(hook)) Unsubscribe(hook);
        }

        void Subscribes(bool all)
        {
            foreach (string hook in allHooks)
            {
                if (all || permanentHooks.Contains(hook))
                {
                    Subscribe(hook);
                }
            }
        }

        void EnableThirdViewMod(BasePlayer player)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
        }

        void DisableThirdViewMod(BasePlayer player)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
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
        static class EventManager
        {
            internal static EventConfig eventConfig { get; private set; }
            internal static bool isEventActive { get; private set; }
            static Coroutine eventCorountine;
            internal static int eventTime = 0;

            internal static void ChangeEventTime(int newEventTimeValue)
            {
                if (eventTime < newEventTimeValue) return;
                eventTime = newEventTimeValue;
            }

            internal static void TryStartEvent(bool immediately, string presetName = "", BasePlayer initiator = null)
            {
                if (isEventActive)
                {
                    NotifyManager.PrintError(initiator, "EventActive_Exeption");
                    return;
                }

                eventConfig = DefineEventConfig(presetName);
                if (eventConfig == null)
                {
                    NotifyManager.PrintError(initiator, "СonfigurationNotFound_Exeption");
                    return;
                }

                if (eventCorountine != null)
                    ServerMgr.Instance.StopCoroutine(eventCorountine);

                if (immediately)
                    StartPeriodicEvent(true);
                else
                    DelayedStartPeriodicEvent();

                if (immediately && initiator == null)
                    ins.Puts("The event is activated!");
            }

            static EventConfig DefineEventConfig(string eventPresetName)
            {
                if (eventPresetName != "")
                {
                    return ins._config.eventConfigs.FirstOrDefault(x => x.presetName == eventPresetName);
                }

                else
                {
                    if (!ins._config.eventConfigs.Any(x => x.chance != 0)) return null;

                    float sumChance = 0;
                    foreach (EventConfig eventConfig in ins._config.eventConfigs)
                    {
                        sumChance += eventConfig.chance;
                    }
                    float random = UnityEngine.Random.Range(0, sumChance);
                    foreach (EventConfig eventConfig in ins._config.eventConfigs)
                    {
                        sumChance -= eventConfig.chance;
                        if (sumChance <= 0) return eventConfig;
                    }

                    return null;
                }
            }

            static void DelayedStartPeriodicEvent()
            {
                if (eventCorountine != null) ServerMgr.Instance.StopCoroutine(eventCorountine);
                eventCorountine = ServerMgr.Instance.StartCoroutine(DelayStartPeriodicEventCoroutine());
            }

            static IEnumerator DelayStartPeriodicEventCoroutine()
            {
                yield return CoroutineEx.waitForSeconds(UnityEngine.Random.Range(ins._config.mainConfig.minTimeBetweenEvent, ins._config.mainConfig.maxTimeBetweenEvent));
                eventCorountine = null;
                StartPeriodicEvent();
            }

            static void StartPeriodicEvent(bool over = false)
            {
                eventTime = eventConfig.eventTime;
                if (!over && eventCorountine != null) return;
                if (eventCorountine != null)
                    ServerMgr.Instance.StopCoroutine(eventCorountine);
                eventCorountine =
                    ServerMgr.Instance.StartCoroutine(StartPeriodicEventCoroutine());
            }

            static IEnumerator StartPeriodicEventCoroutine()
            {
                if (ins._config.mainConfig.preStartTime > 0)
                    NotifyManager.SendMessageToAll("PreStartEvent", ins._config.prefix, eventConfig.displayName, ins._config.mainConfig.preStartTime);

                yield return CoroutineEx.waitForSeconds(ins._config.mainConfig.preStartTime);
                StartEvent();
            }

            static void StartEvent()
            {
                SpaceClass.CreateSpaceFirstStage();
                isEventActive = true;
                ins.Subscribes(true);
                if (eventCorountine != null)
                    ServerMgr.Instance.StopCoroutine(eventCorountine);

                eventCorountine = ServerMgr.Instance.StartCoroutine(PeriodicEventCorountine());
            }

            internal static void PostStartEvent()
            {
                NotifyManager.SendMessageToAll("StartEvent", ins._config.prefix, eventConfig.displayName, LocationDefiner.PositionToGridCoord(SpaceClass.GetSpaceTransform().position));
                SpaceShuttle.OnEventStart();
                Interface.CallHook("OnSpaceEventStart");
            }

            static IEnumerator PeriodicEventCorountine()
            {
                while (eventTime > 0 || (ins._config.mainConfig.dontStopIfPlayerInSpace && EventZone.playersInSpace.Any(x => x != null && x.IsConnected && !x.IsSleeping() && x.transform != null && SpaceClass.IsInSpace(x.transform.position, false))))
                {
                    if (ins._config.mainConfig.remainTimeNotifications.Contains(eventTime))
                        NotifyManager.SendMessageToAll("RemainTime", ins._config.prefix, eventConfig.displayName, eventTime);

                    if (ins._config.mainConfig.descriptionNotifications.Contains(eventTime))
                        NotifyManager.SendMessageToAll("EventDescription", ins._config.prefix, eventConfig.displayName, LocationDefiner.PositionToGridCoord(SpaceClass.GetSpaceTransform().position));

                    if (ins._config.notifyConfig.guiConfig.isCountdownGUI)
                    {
                        int countOfCrates = LootManager.GetCountUnlootedCrates();
                        foreach (BasePlayer player in EventZone.playersInSpace)
                            if (player != null)
                                GuiManager.CreateGui(player, NotifyManager.GetTimeMessage(player.UserIDString, eventTime), countOfCrates.ToString());
                    }

                    eventTime--;
                    yield return CoroutineEx.waitForSeconds(1);
                }
                StopEvent();
            }

            internal static void StopEvent(bool unload = false)
            {
                ins.Unsubscribes(unload);
                if (isEventActive)
                {
                    isEventActive = false;
                    SpaceClass.DestroySpace();
                    LootManager.DestroyEventCrates();
                    if (eventCorountine != null) ServerMgr.Instance.StopCoroutine(eventCorountine);
                    EconomyManager.SendBalance();
                    eventCorountine = null;
                    eventConfig = null;
                    eventTime = 0;
                    if (ins._config.supportedPluginsConfig.pveMode.enable && ins.plugins.Exists("PveMode")) ins.PveMode.Call("EventRemovePveMode", ins.Name, false);
                    NotifyManager.SendMessageToAll("EndEvent", ins._config.prefix);
                    Astronaut.RemoveAllComponents();
                    SpaceShuttle.OnEventStop();
                    Interface.CallHook("OnSpaceEventStop");
                }
                if (!unload && ins._config.mainConfig.isAutoEvent) TryStartEvent(immediately: false);
                else if (eventCorountine != null && unload) ServerMgr.Instance.StopCoroutine(eventCorountine);
                GuiManager.DestroyAllGui();
            }
        }

        sealed class SpaceClass : FacepunchBehaviour
        {
            internal const float maxSpaceHeight = 980;
            static SpaceClass spaceClass;
            EventZone eventZone;
            EventMapMarker eventMapMarker;
            HashSet<BaseEntity> crates = new HashSet<BaseEntity>();
            HashSet<BaseEntity> meteors = new HashSet<BaseEntity>();
            HashSet<ScientistNPC> spaceRoamNpcs = new HashSet<ScientistNPC>();
            List<SpaceShuttle> spaceShuttles = new List<SpaceShuttle>();
            List<Aerostat> aerostats = new List<Aerostat>();
            HashSet<BaseEntity> droppedItems = new HashSet<BaseEntity>();

            internal static HashSet<BaseEntity> GetSpaceCrates()
            {
                if (spaceClass == null) return null;
                return spaceClass.crates;
            }

            internal static HashSet<ScientistNPC> GetSpaceNPCs()
            {
                if (spaceClass == null) return null;
                return spaceClass.spaceRoamNpcs;
            }

            internal static int GetSpaceStationNPCsCount()
            {
                if (spaceClass == null)
                    return 0;

                return spaceClass.spaceRoamNpcs.Where(x => x.IsExists()).Count;
            }

            internal static void CreateSpaceFirstStage()
            {
                if (spaceClass != null) return;
                GameObject gameObject = new GameObject();

                Vector3 spacePosition = LocationDefiner.GetEventPosition();
                gameObject.transform.position = spacePosition;
                gameObject.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0.0f, 360.0f), 0);
                gameObject.layer = (int)Layer.Reserved1;

                spaceClass = gameObject.AddComponent<SpaceClass>();
                if (EventManager.eventConfig.spaceStationConfig.enable)
                    SpaceStation.SpawnSpaceStation(EventManager.eventConfig.spaceStationConfig.presetName);
                else
                    CreateSpaceSecondStage();
            }

            internal static void CreateSpaceSecondStage()
            {
                if (spaceClass == null) return;
                RespawnSpaceEntities(true);
            }

            internal static void CreateSpaceThirdStage()
            {
                if (spaceClass == null) return;
                if (spaceClass.eventZone == null) spaceClass.eventZone = EventZone.AddZoneComponrntToGameObject(spaceClass);
                if (ins._config.markerConfig.enable)
                    spaceClass.eventMapMarker = EventMapMarker.CreateMarker();
                EventManager.PostStartEvent();
                SpaceWeather.StartSpaceWeatherChange();
            }

            internal static void RespawnSpaceEntities(bool first = false)
            {
                if (spaceClass == null) return;
                SpaceObjects.RespawnObjects(first);
            }

            internal static void OnItemDropInSpace(BaseEntity entity)
            {
                if (spaceClass == null) return;
                Rigidbody rigidbody = entity.GetComponent<Rigidbody>();
                if (rigidbody == null) return;
                rigidbody.useGravity = false;
                rigidbody.drag = 0.5f;
                spaceClass.droppedItems.Add(entity);
            }

            internal static void DestroySpace()
            {
                SpaceObjects.StopRespawn();
                SpaceStation.DestroySpaceStation();

                SpaceWeather.StopSpaceWeatherChange();

                if (spaceClass != null)
                {
                    spaceClass.DestroySpaceObjects();
                    spaceClass.CheckRemainSpaceObjects();

                    EventMapMarker.DeleteMapMarker();

                    UnityEngine.Object.Destroy(spaceClass.gameObject);
                }
            }

            internal static Transform GetSpaceTransform()
            {
                if (spaceClass == null) return null;
                return spaceClass.transform;
            }

            internal static Vector3 GetSpacePosition()
            {
                return spaceClass.transform.position;
            }

            internal static bool IsInSpace(Vector3 position, bool checkMaxHeight = true)
            {
                return EventManager.isEventActive && position.y >= ins._config.mainConfig.spaceHeight - ins._config.mainConfig.spaceRadius && (!checkMaxHeight || position.y <= maxSpaceHeight);
            }

            internal static string GetContainerPrefabNameBynetId(ulong netId)
            {
                if (spaceClass == null) return null;
                BaseEntity crate = spaceClass.crates.FirstOrDefault(x => x != null && x.net.ID.Value == netId);
                if (crate != null) return crate.PrefabName;
                return null;
            }

            void DestroySpaceObjects()
            {
                foreach (BaseEntity entity in crates) if (entity.IsExists()) entity.Kill();
                foreach (BaseEntity entity in meteors) if (entity.IsExists()) entity.Kill();
                foreach (ScientistNPC scientistNPC in spaceRoamNpcs) if (scientistNPC.IsExists()) scientistNPC.Kill();
                foreach (SpaceShuttle spaceShuttle in spaceShuttles) if (spaceShuttle != null && !spaceShuttle.IsAnyMounted()) spaceShuttle.KillSubmarine();
                foreach (Aerostat aerostat in aerostats) if (aerostat != null && !aerostat.IsAnyMounted()) aerostat.KillAerostat();
                foreach (BaseEntity entity in droppedItems) if (entity.IsExists()) entity.Kill();
            }

            void CheckRemainSpaceObjects()
            {
                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(transform.position, 300))
                {
                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity == null) continue;
                    if (entity.IsExists() && (entity is BaseCorpse || entity is DroppedItemContainer)) entity.Kill();
                }
            }

            static class SpaceObjects
            {
                static Coroutine respawnCorountine;

                internal static void RespawnObjects(bool first)
                {
                    spaceClass.crates.RemoveWhere(x => !x.IsExists());
                    spaceClass.meteors.RemoveWhere(x => !x.IsExists());
                    spaceClass.spaceRoamNpcs.RemoveWhere(x => x == null);

                    if (respawnCorountine != null) ServerMgr.Instance.StopCoroutine(respawnCorountine);
                    respawnCorountine = ServerMgr.Instance.StartCoroutine(RespawnCorountine(first));
                }

                internal static void StopRespawn()
                {
                    if (respawnCorountine != null) ServerMgr.Instance.StopCoroutine(respawnCorountine);
                    respawnCorountine = null;
                }

                static IEnumerator RespawnCorountine(bool first)
                {
                    if (EventManager.eventConfig.spaceLootCrateConfig.enable && EventManager.eventConfig.spaceLootCrateConfig.probability.Any(x => x.Value > 1))
                    {
                        while (spaceClass.crates.Count < EventManager.eventConfig.spaceLootCrateConfig.crateCount)
                        {
                            SpaceCrate.SpawnRandomCrate();
                            yield return null;
                        }
                    }

                    if (EventManager.eventConfig.meteorsConfig.enable)
                    {
                        while (spaceClass.meteors.Count < EventManager.eventConfig.meteorsConfig.meteorCount)
                        {
                            MeteorClass.SpawnRandomMeteor();
                            yield return null;
                        }
                    }

                    if (EventManager.eventConfig.roamNpcSpawnConfig.enable)
                    {
                        while (spaceClass.spaceRoamNpcs.Count < EventManager.eventConfig.roamNpcSpawnConfig.npcsCount)
                        {
                            if (!RoamSpaceNpc.SpawnRandomRoamNpc()) break;
                            yield return null;
                        }
                    }

                    if (EventManager.eventConfig.spaceShuttleInSpaceSpawnConfig.enable)
                    {
                        while (spaceClass.spaceShuttles.Count < EventManager.eventConfig.spaceShuttleInSpaceSpawnConfig.count)
                        {
                            Vector3 position = LocationDefiner.GetRandomSpacePoint();
                            if (position == Vector3.zero) break;
                            SpaceShuttle spaceShuttle = SpaceShuttle.SpawnSpaceShuttle(position, LocationDefiner.GetRandomRotation());
                            if (spaceShuttle != null) spaceShuttle.AddFuel(EventManager.eventConfig.spaceShuttleInSpaceSpawnConfig.fuelAmountInShuttle);
                            spaceClass.spaceShuttles.Add(spaceShuttle);
                            yield return null;
                        }
                    }

                    if (EventManager.eventConfig.aerostatInSpaceSpawnConfig.enable)
                    {
                        while (spaceClass.aerostats.Count < EventManager.eventConfig.aerostatInSpaceSpawnConfig.count)
                        {
                            Vector3 position = LocationDefiner.GetRandomSpacePoint();
                            if (position == Vector3.zero) break;
                            Aerostat aerostat = Aerostat.SpawnAerostat(position, LocationDefiner.GetRandomRotation());
                            if (aerostat != null) aerostat.AddFuel(EventManager.eventConfig.aerostatInSpaceSpawnConfig.fuelAmount);
                            spaceClass.aerostats.Add(aerostat);
                            yield return null;
                        }
                    }

                    if (first) CreateSpaceThirdStage();
                }

                static class MeteorClass
                {
                    enum MeteorSize
                    {
                        Small,
                        Large
                    }

                    enum MeteorType
                    {
                        Stone,
                        Iron,
                        Sulfur
                    }

                    internal static void SpawnRandomMeteor()
                    {
                        Vector3 position = LocationDefiner.GetRandomSpacePoint();
                        if (position == Vector3.zero) return;
                        BaseEntity meteorEntity = BuildManager.CreateStaticEntity(DefineMeteoriteRandomPrefabName(), position, LocationDefiner.GetRandomRotation());
                        spaceClass.meteors.Add(meteorEntity);
                    }

                    static string DefineMeteoriteRandomPrefabName()
                    {
                        MeteorType randomMeteorType = GetRandomMeteorType();
                        if (GetRandomMeteorSize() == MeteorSize.Small)
                        {
                            if (randomMeteorType == MeteorType.Stone) return "assets/bundled/prefabs/autospawn/collectable/stone/stone-collectable.prefab";
                            else if (randomMeteorType == MeteorType.Iron) return "assets/bundled/prefabs/autospawn/collectable/stone/metal-collectable.prefab";
                            else if (randomMeteorType == MeteorType.Sulfur) return "assets/bundled/prefabs/autospawn/collectable/stone/sulfur-collectable.prefab";
                        }

                        else
                        {
                            if (randomMeteorType == MeteorType.Stone) return "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab";
                            else if (randomMeteorType == MeteorType.Iron) return "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab";
                            else if (randomMeteorType == MeteorType.Sulfur) return "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab";
                        }
                        return "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab";
                    }

                    static MeteorSize GetRandomMeteorSize()
                    {
                        return UnityEngine.Random.Range(0, 100) < EventManager.eventConfig.meteorsConfig.largeMeteorChance ? MeteorSize.Large : MeteorSize.Small;
                    }

                    static MeteorType GetRandomMeteorType()
                    {
                        float sumChance = EventManager.eventConfig.meteorsConfig.stoneMeteorChance + EventManager.eventConfig.meteorsConfig.ironMeteorChance + EventManager.eventConfig.meteorsConfig.sulphurMeteorChance;
                        float random = UnityEngine.Random.Range(0, sumChance);

                        if (random < EventManager.eventConfig.meteorsConfig.stoneMeteorChance) return MeteorType.Stone;
                        sumChance -= EventManager.eventConfig.meteorsConfig.stoneMeteorChance;
                        if (random < EventManager.eventConfig.meteorsConfig.ironMeteorChance) return MeteorType.Iron;
                        else return MeteorType.Sulfur;
                    }
                }

                static class SpaceCrate
                {
                    internal static void SpawnRandomCrate()
                    {
                        Vector3 position = LocationDefiner.GetRandomSpacePoint();
                        if (position == Vector3.zero) return;
                        BaseEntity crateEntity = LootManager.CreateCrate(DefineCratePrefabName(), position, LocationDefiner.GetRandomRotation());
                        spaceClass.crates.Add(crateEntity);
                    }

                    static string DefineCratePrefabName()
                    {
                        while (true)
                        {
                            int randomIndex = UnityEngine.Random.Range(0, EventManager.eventConfig.spaceLootCrateConfig.probability.Count);
                            var pair = EventManager.eventConfig.spaceLootCrateConfig.probability.ElementAt(randomIndex);
                            if (UnityEngine.Random.Range(0, 100) <= pair.Value) return pair.Key;
                        }
                    }
                }

                class RoamSpaceNpc : FacepunchBehaviour
                {
                    ScientistNPC scientistNPC;
                    AutoPilotZeroGravity autoPilotZeroGravity;
                    Coroutine targetFindCorountine;
                    BasePlayer currentTarget;
                    float speed = 0;

                    internal static bool SpawnRandomRoamNpc()
                    {
                        NpcConfig roamNpcConfig = GetRandomRoamNpcConfig();
                        if (roamNpcConfig == null)
                        {
                            ins.PrintError("Couldn't find the random Npc preset");
                            return false;
                        }
                        Vector3 position = LocationDefiner.GetRandomSpacePoint();
                        if (position == Vector3.zero) return false;
                        CreateNewSpaceNpc(position, roamNpcConfig);
                        return true;
                    }

                    static NpcConfig GetRandomRoamNpcConfig()
                    {
                        float sumChance = 0;
                        string presetName = "";
                        foreach (var chancePair in EventManager.eventConfig.roamNpcSpawnConfig.probability)
                        {
                            sumChance += chancePair.Value;
                        }
                        float random = UnityEngine.Random.Range(0, sumChance);
                        foreach (var chancePair in EventManager.eventConfig.roamNpcSpawnConfig.probability)
                        {
                            sumChance -= chancePair.Value;
                            if (sumChance <= 0)
                            {
                                presetName = chancePair.Key;
                                break;
                            }
                        }
                        return ins._config.npcConfigs.FirstOrDefault(x => x.presetName == presetName);
                    }

                    static void CreateNewSpaceNpc(Vector3 position, NpcConfig roamNpcConfig)
                    {
                        ScientistNPC scientistNPC = NpcManager.CreateStationScientistNpc(roamNpcConfig, position);
                        RoamSpaceNpc spaceNpcComponent = scientistNPC.gameObject.AddComponent<RoamSpaceNpc>();
                        spaceNpcComponent.InitComponent(scientistNPC, roamNpcConfig);
                        spaceClass.spaceRoamNpcs.Add(scientistNPC);
                    }

                    void InitComponent(ScientistNPC scientistNPC, NpcConfig roamNpcConfig)
                    {
                        this.scientistNPC = scientistNPC;
                        autoPilotZeroGravity = AutoPilotZeroGravity.CreateAutoPilotZeroGravity(scientistNPC, this);
                        targetFindCorountine = ServerMgr.Instance.StartCoroutine(TargetFindCorountine());
                        speed = roamNpcConfig.speed;
                    }

                    IEnumerator TargetFindCorountine()
                    {
                        while (true)
                        {
                            currentTarget = null;
                            BaseEntity entity = scientistNPC.GetBestTarget();
                            if (entity != null && SpaceClass.IsInSpace(entity.transform.position))
                            {
                                currentTarget = entity as BasePlayer;
                            }
                            yield return CoroutineEx.waitForSeconds(5f);
                        }
                    }

                    void OnDestroy()
                    {
                        if (autoPilotZeroGravity != null) autoPilotZeroGravity.DestroyZeroGravity();
                        if (targetFindCorountine != null) ServerMgr.Instance.StopCoroutine(targetFindCorountine);
                    }

                    class AutoPilotZeroGravity : ZeroGravity
                    {
                        RoamSpaceNpc spaceNpcComponent;
                        Vector3 homePosition;

                        internal static AutoPilotZeroGravity CreateAutoPilotZeroGravity(BasePlayer player, RoamSpaceNpc spaceNpcComponent)
                        {
                            AutoPilotZeroGravity zeroGravity = (new GameObject()).AddComponent<AutoPilotZeroGravity>();
                            zeroGravity.Init(player, spaceNpcComponent);
                            return zeroGravity;
                        }

                        void Init(BasePlayer player, RoamSpaceNpc spaceNpcComponent)
                        {
                            base.Init(player);
                            this.spaceNpcComponent = spaceNpcComponent;
                            homePosition = player.transform.position;
                            rigidbody.drag = 0.5f;
                        }

                        internal void FixedUpdate()
                        {
                            if (spaceNpcComponent.currentTarget == null) return;
                            UpdateRotation();
                            UpdatePosition();
                        }

                        void UpdateRotation()
                        {
                            movableDroppedItem.transform.rotation = Quaternion.Lerp(movableDroppedItem.transform.rotation, spaceNpcComponent.scientistNPC.eyes.GetAimRotation(), 0.05f * 1);
                        }

                        void UpdatePosition()
                        {
                            Vector3 targetPosition = homePosition;
                            if (spaceNpcComponent.currentTarget != null && spaceNpcComponent.scientistNPC.CanSeeTarget(spaceNpcComponent.currentTarget))
                            {
                                targetPosition = spaceNpcComponent.currentTarget.transform.position;
                            }
                            if (Vector3.Distance(targetPosition, movableDroppedItem.transform.position) < 10) return;
                            Vector3 targetDirecrion = (targetPosition - movableDroppedItem.transform.position).normalized * 1;
                            if (rigidbody.velocity.magnitude < spaceNpcComponent.speed) AddForce(targetDirecrion, spaceNpcComponent.speed);
                        }

                        internal override void DestroyZeroGravity()
                        {
                            base.DestroyZeroGravity();
                        }
                    }
                }
            }

            static class SpaceWeather
            {
                static Coroutine updateWeatherCorountine;
                static HashSet<BasePlayer> players = new HashSet<BasePlayer>();

                internal static void StartSpaceWeatherChange()
                {
                    if (!ins._config.supportedPluginsConfig.nightVisionConfig.enable) return;
                    if (!ins.plugins.Exists("NightVision"))
                    {
                        ins.PrintError("NightVision plugin doesn`t exist!");
                        return;
                    }
                    if (updateWeatherCorountine != null) return;
                    updateWeatherCorountine = ServerMgr.Instance.StartCoroutine(UpdateWeatherCorountine());
                }

                internal static void StopSpaceWeatherChange()
                {
                    if (updateWeatherCorountine != null) ServerMgr.Instance.StopCoroutine(updateWeatherCorountine);
                    foreach (BasePlayer player in BasePlayer.activePlayerList) if (player != null && SpaceClass.IsInSpace(player.transform.position)) StopUpdateWeather(player);
                }

                static IEnumerator UpdateWeatherCorountine()
                {
                    while (true)
                    {
                        UpdateWeather();
                        yield return CoroutineEx.waitForSeconds(5f);
                    }
                }

                static void UpdateWeather()
                {
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        if (player == null) continue;
                        if (SpaceClass.IsInSpace(player.transform.position))
                        {
                            if (!players.Contains(player)) StartUpdateWeather(player);
                        }
                        else if (players.Contains(player)) StopUpdateWeather(player);
                    }
                }

                static void StartUpdateWeather(BasePlayer player)
                {
                    if ((bool)ins.NightVision?.CallHook("IsPlayerTimeLocked", player)) return;
                    ins.NightVision?.CallHook("LockPlayerTime", player, ins._config.supportedPluginsConfig.nightVisionConfig.time);
                    players.Add(player);
                }

                static void StopUpdateWeather(BasePlayer player)
                {
                    players.Remove(player);
                    ins.NightVision?.CallHook("UnlockPlayerTime", player);
                }
            }
        }

        sealed class EventZone : FacepunchBehaviour
        {
            SphereCollider sphereCollider;
            HashSet<BaseEntity> spheres = new HashSet<BaseEntity>();
            TriggerRadiation triggerRadiation;
            TriggerTemperature triggerTemperature;
            internal static List<BasePlayer> playersInSpace = new List<BasePlayer>();

            internal static EventZone AddZoneComponrntToGameObject(SpaceClass spaceClass)
            {
                EventZone spaceZone = spaceClass.gameObject.AddComponent<EventZone>();
                spaceZone.Init(spaceClass);
                return spaceZone;
            }

            void Init(SpaceClass spaceClass)
            {
                sphereCollider = BuildManager.CreateSphereCollider(this.gameObject, ins._config.mainConfig.spaceRadius, SpaceClass.GetSpaceTransform().position);

                CreateRadiationZone();
                CreateTemperatuteTrigger();

                if (ins._config.supportedPluginsConfig.pveMode.enable && ins.plugins.Exists("PveMode")) CreatePveModeZone();
                else CreateBlackSpheres();
            }

            void CreatePveModeZone()
            {
                Dictionary<string, object> config = new Dictionary<string, object>
                {
                    ["Damage"] = ins._config.supportedPluginsConfig.pveMode.damage,
                    ["ScaleDamage"] = ins._config.supportedPluginsConfig.pveMode.scaleDamage,
                    ["LootCrate"] = ins._config.supportedPluginsConfig.pveMode.lootCrate,
                    ["HackCrate"] = ins._config.supportedPluginsConfig.pveMode.hackCrate,
                    ["LootNpc"] = ins._config.supportedPluginsConfig.pveMode.lootNpc,
                    ["DamageNpc"] = ins._config.supportedPluginsConfig.pveMode.damageNpc,
                    ["DamageTank"] = ins._config.supportedPluginsConfig.pveMode.damageTank,
                    ["DamageTurret"] = ins._config.supportedPluginsConfig.pveMode.damageTurret,
                    ["DamageHelicopter"] = false,
                    ["TargetNpc"] = ins._config.supportedPluginsConfig.pveMode.targetNpc,
                    ["TargetTank"] = ins._config.supportedPluginsConfig.pveMode.targetTank,
                    ["TargetTurret"] = ins._config.supportedPluginsConfig.pveMode.targetTurret,
                    ["TargetHelicopter"] = false,
                    ["CanEnter"] = true,
                    ["CanEnterCooldownPlayer"] = ins._config.supportedPluginsConfig.pveMode.canEnterCooldownPlayer,
                    ["TimeExitOwner"] = ins._config.supportedPluginsConfig.pveMode.timeExitOwner,
                    ["AlertTime"] = ins._config.supportedPluginsConfig.pveMode.alertTime,
                    ["RestoreUponDeath"] = ins._config.supportedPluginsConfig.pveMode.restoreUponDeath,
                    ["CooldownOwner"] = ins._config.supportedPluginsConfig.pveMode.cooldownOwner,
                    ["Darkening"] = ins._config.supportedPluginsConfig.pveMode.darkening
                };

                HashSet<ulong> npcs = new HashSet<ulong>();
                HashSet<ulong> bradleys = new HashSet<ulong>();
                HashSet<ulong> crates = new HashSet<ulong>();
                HashSet<ulong> turrets = SpaceStation.GetSpaceStationTurretsNetIDs();

                foreach (ScientistNPC scientistNPC in SpaceStation.GetSpaceStationNPCs()) if (scientistNPC.IsExists()) npcs.Add(scientistNPC.net.ID.Value);
                foreach (ScientistNPC scientistNPC in SpaceClass.GetSpaceNPCs()) if (scientistNPC.IsExists()) npcs.Add(scientistNPC.net.ID.Value);

                foreach (BaseEntity lootContainer in SpaceStation.GetSpaceStationCrates()) if (lootContainer.IsExists()) crates.Add(lootContainer.net.ID.Value);
                foreach (BaseEntity lootContainer in SpaceClass.GetSpaceCrates()) if (lootContainer.IsExists()) crates.Add(lootContainer.net.ID.Value);

                foreach (BradleyAPC bradleyAPC in SpaceStation.GetSpaceStationBradleys()) if (bradleyAPC.IsExists()) bradleys.Add(bradleyAPC.net.ID.Value);

                ins.PveMode.Call("EventAddPveMode", ins.Name, config, SpaceClass.GetSpaceTransform().position, ins._config.mainConfig.spaceRadius, crates, npcs, bradleys, new HashSet<ulong>(), turrets, new HashSet<ulong>(), null);
            }

            void CreateTemperatuteTrigger()
            {
                triggerTemperature = sphereCollider.gameObject.gameObject.AddComponent<TriggerTemperature>();
                triggerTemperature.Temperature = EventManager.eventConfig.temperature;
                triggerTemperature.triggerSize = ins._config.mainConfig.spaceRadius;
                triggerTemperature.interestLayers = 1 << 17;
            }

            void CreateRadiationZone()
            {
                if (EventManager.eventConfig.radiation <= 0) return;
                triggerRadiation = sphereCollider.gameObject.AddComponent<TriggerRadiation>();
                triggerRadiation.RadiationAmountOverride = EventManager.eventConfig.radiation;
                triggerRadiation.interestLayers = 131072;
                triggerRadiation.enabled = true;
            }

            void CreateBlackSpheres()
            {
                if (!ins._config.zoneConfig.isDome) return;
                for (int i = 0; i < ins._config.zoneConfig.darkening; i++)
                {
                    BaseEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", SpaceClass.GetSpaceTransform().position);
                    SphereEntity entity = sphere as SphereEntity;
                    entity.currentRadius = ins._config.mainConfig.spaceRadius * 2;
                    entity.lerpSpeed = 0f;
                    sphere.enableSaving = false;
                    sphere.Spawn();
                    spheres.Add(sphere);
                }
            }

            void OnTriggerEnter(Collider other)
            {
                if (other.ToBaseEntity() == null) return;
                BasePlayer player = other.ToBaseEntity() as BasePlayer;
                if (player.IsRealPlayer())
                {
                    playersInSpace.Add(player);
                    if (ins._config.zoneConfig.isCreateZonePVP) NotifyManager.SendMessageToPlayer(player, "EnterPVP", ins._config.prefix);
                    if (triggerRadiation != null) player.EnterTrigger(triggerRadiation);
                    player.EnterTrigger(triggerTemperature);
                }
            }

            void OnTriggerExit(Collider other)
            {
                if (other.ToBaseEntity() == null) return;
                BasePlayer player = other.ToBaseEntity() as BasePlayer;
                if (player.IsRealPlayer())
                {
                    playersInSpace.Remove(player);
                    GuiManager.DestroyGui(player);
                    if (ins._config.zoneConfig.isCreateZonePVP) NotifyManager.SendMessageToPlayer(player, "ExitPVP", ins._config.prefix);
                    if (triggerRadiation != null) player.LeaveTrigger(triggerRadiation);
                }
            }

            void OnDestroy()
            {
                foreach (BaseEntity sphere in spheres)
                    if (sphere.IsExists())
                        sphere.Kill();
                foreach (BasePlayer player in BasePlayer.activePlayerList) if (player != null) CuiHelper.DestroyUi(player, "SputnikGui");
                playersInSpace.Clear();
            }
        }

        class EventMapMarker : FacepunchBehaviour
        {
            static EventMapMarker eventMapMarker;

            MapMarkerGenericRadius radiusMarker;
            VendingMachineMapMarker vendingMarker;
            Coroutine updateCounter;

            internal static EventMapMarker CreateMarker()
            {
                if (!ins._config.markerConfig.enable)
                    return null;

                GameObject gameObject = new GameObject();
                gameObject.layer = (int)Rust.Layer.Reserved1;
                eventMapMarker = gameObject.AddComponent<EventMapMarker>();
                eventMapMarker.Init();
                return eventMapMarker;
            }

            void Init()
            {
                Vector3 eventPosition = SpaceClass.GetSpacePosition();
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
                vendingMarker.markerShopName = $"{EventManager.eventConfig.displayName} ({NotifyManager.GetTimeMessage(null, EventManager.eventTime)})";
            }

            IEnumerator MarkerUpdateCounter()
            {
                while (EventManager.isEventActive)
                {
                    Vector3 position = SpaceClass.GetSpacePosition();
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
                BasePlayer pveModeEventOwner = PveModeController.GetEventOwner();
                string displayEventOwnerName = ins._config.supportedPluginsConfig.pveMode.showEventOwnerNameOnMap && pveModeEventOwner != null ? GetMessage("Marker_EventOwner", null, pveModeEventOwner.displayName) : "";
                vendingMarker.markerShopName = $"{EventManager.eventConfig.displayName} ({NotifyManager.GetTimeMessage(null, EventManager.eventTime)}) {displayEventOwnerName}";
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

        sealed class SpaceStation
        {
            static SpaceStation spaceStation;

            SpaceStationConfig spaceStationConfig;
            Coroutine spawnConstructionsCorountine;
            Coroutine spawnEntitiesCorountine;
            MainDoor mainDoor;
            HashSet<BaseEntity> constructionEntities = new HashSet<BaseEntity>();
            List<RegularDoor> regularDoors = new List<RegularDoor>();
            HashSet<BaseEntity> crates = new HashSet<BaseEntity>();
            HashSet<AutoTurret> turrets = new HashSet<AutoTurret>();
            HashSet<FlameTurret> flameTurrets = new HashSet<FlameTurret>();
            HashSet<BradleyAPC> bradleys = new HashSet<BradleyAPC>();
            HashSet<CardDoor> cardDoors = new HashSet<CardDoor>();
            HashSet<ScientistNPC> stationNpcs = new HashSet<ScientistNPC>();

            internal static HashSet<BaseEntity> GetSpaceStationCrates()
            {
                if (spaceStation == null) return null;
                return spaceStation.crates;
            }

            internal static HashSet<BradleyAPC> GetSpaceStationBradleys()
            {
                if (spaceStation == null) return null;
                return spaceStation.bradleys;
            }

            internal static HashSet<ScientistNPC> GetSpaceStationNPCs()
            {
                if (spaceStation == null) return null;
                return spaceStation.stationNpcs;
            }

            internal static int GetSpaceStationNPCsCount()
            {
                if (spaceStation == null)
                    return 0;

                return spaceStation.stationNpcs.Where(x => x.IsExists()).Count;
            }

            internal static HashSet<ulong> GetSpaceStationTurretsNetIDs()
            {
                HashSet<ulong> turretList = new HashSet<ulong>();

                if (spaceStation == null)
                    return turretList;

                foreach (AutoTurret turret in spaceStation.turrets)
                    if (turret.IsExists())
                        turretList.Add(turret.net.ID.Value);

                foreach (FlameTurret turret in spaceStation.flameTurrets)
                    if (turret.IsExists())
                        turretList.Add(turret.net.ID.Value);

                return turretList;
            }

            internal static void SpawnSpaceStation(string spaceStationPreset)
            {
                if (spaceStation != null) return;
                spaceStation = new SpaceStation(EventManager.eventConfig.spaceStationConfig.presetName);
            }

            internal static void RespawnSpaceStationEntities()
            {
                if (spaceStation == null) return;
                spaceStation.DespawnStationEntities();
                spaceStation.SpawnStatonsEntities();
            }

            internal static object OnStationCardReaderSwipe(CardReader cardReader, Keycard card, BasePlayer player)
            {
                if (spaceStation == null)
                    return null;

                CardDoor cardDoor = spaceStation.cardDoors.FirstOrDefault(x => x.IsOwnCardReader(cardReader.net.ID.Value));

                if (cardDoor != null)
                {
                    cardDoor.OnCardSwipe(card, player);
                    return true;
                }

                return null;
            }

            internal static void OnStationButtonPress(PressButton pressButton)
            {
                if (spaceStation == null) return;

                CardDoor cardDoor = spaceStation.cardDoors.FirstOrDefault(x => x.IsOwnButton(pressButton.net.ID.Value));
                if (cardDoor != null)
                {
                    cardDoor.OnButtonPress();
                    return;
                }

                if (spaceStation.mainDoor != null && spaceStation.mainDoor.IsMainDoorButton(pressButton.net.ID.Value))
                    spaceStation.mainDoor.SwitchMainDoor();
            }

            internal static void OnDoorOpened(Door door)
            {
                if (spaceStation == null) return;
                RegularDoor regularDoor = spaceStation.cardDoors.FirstOrDefault(x => x != null && !x.isDoorOpen && x.IsOwnDoor(door.net.ID.Value));
                if (regularDoor == null)
                {
                    regularDoor = spaceStation.regularDoors.FirstOrDefault(x => x != null && !x.isDoorOpen && x.IsOwnDoor(door.net.ID.Value));
                }

                if (regularDoor != null)
                {
                    regularDoor.isDoorOpen = true;
                    spaceStation.SpawnDoorNpcs(regularDoor);
                }
            }

            internal static string GetContainerPrefabNameBynetId(ulong netId)
            {
                if (spaceStation == null) return null;
                BaseEntity crate = spaceStation.crates.FirstOrDefault(x => x != null && x.net.ID.Value == netId);
                if (crate != null) return crate.PrefabName;
                return null;
            }

            internal static bool IsPositionInSpaceStation(Vector3 position)
            {
                RaycastHit raycastHit;
                if (Physics.Raycast(position, Vector3.up, out raycastHit, 10, 1 << 21) &&
                    Physics.Raycast(position, -Vector3.up, out raycastHit, 10, 1 << 21) &&
                    !raycastHit.collider.name.Contains("floor.triangle.twig") && !raycastHit.collider.name.Contains("floor.frame")) return true;
                return false;
            }

            void SpawnDoorNpcs(RegularDoor regularDoor)
            {
                if (NpcManager.CheckNPCSpawn())
                {
                    foreach (var pair in regularDoor.npcPositions)
                    {
                        NpcConfig npcConfig = ins._config.npcConfigs.FirstOrDefault(x => x.presetName == pair.Key);
                        if (npcConfig == null)
                        {
                            ins.PrintError("The NPC preset does not exist - " + pair.Key);
                            continue;
                        }
                        Vector3 lastPosition = Vector3.zero;
                        foreach (LocationConfig locationConfig in pair.Value)
                        {
                            Vector3 spawnPosition = BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), locationConfig.position.ToVector3());
                            if (Vector3.Distance(lastPosition, spawnPosition) < 0.25f)
                                continue;

                            ScientistNPC scientistNPC = NpcManager.CreateStationScientistNpc(npcConfig, spawnPosition);
                            lastPosition = spawnPosition;
                            stationNpcs.Add(scientistNPC);
                        }
                    }

                    if (ins._config.supportedPluginsConfig.pveMode.enable && ins.plugins.Exists("PveMode")) ins.PveMode.Call("EventAddScientists", ins.Name, stationNpcs.Where(x => x.IsExists()).Select(x => x.net.ID));
                }
            }

            SpaceStation(string stationPrefabName)
            {
                HashSet<DataFileEntity> prefabs = LoadStationDataFile(stationPrefabName);
                spaceStationConfig = LoadStationConfigFile(stationPrefabName);

                if (prefabs == null || prefabs.Count == 0 || spaceStationConfig == null)
                {
                    DestroySpaceStation();
                    ins.PrintError($"File {stationPrefabName} or {stationPrefabName}_config is corrupted and cannot be loaded!!");
                    SpaceClass.CreateSpaceSecondStage();
                    return;
                }

                spawnConstructionsCorountine = ServerMgr.Instance.StartCoroutine(SpaceStationSpawnCorountine(prefabs));
                mainDoor = MainDoor.CreateMainDoor(EventManager.eventConfig.mainDoorConfig);
            }

            static HashSet<DataFileEntity> LoadStationDataFile(string fileName)
            {
                return Interface.Oxide.DataFileSystem.ReadObject<HashSet<DataFileEntity>>($"Space/Stations/{fileName}");
            }

            static SpaceStationConfig LoadStationConfigFile(string fileName)
            {
                SpaceStationConfig spaceStationConfig = Interface.Oxide.DataFileSystem.ReadObject<SpaceStationConfig>($"Space/Stations/{fileName}_config");
                return spaceStationConfig;
            }

            IEnumerator SpaceStationSpawnCorountine(HashSet<DataFileEntity> dataFileEntities)
            {
                foreach (DataFileEntity dataFileEntity in dataFileEntities)
                {
                    BuildEntity(dataFileEntity);
                    yield return null;
                }
                SpawnStatonsEntities();
                SpaceClass.CreateSpaceSecondStage();
                PostSpawnUpdate();
            }

            void SpawnStatonsEntities()
            {
                if (spawnEntitiesCorountine != null) return;
                spawnEntitiesCorountine = ServerMgr.Instance.StartCoroutine(SpaceStationEntitiesCorountine());
            }

            IEnumerator SpaceStationEntitiesCorountine()
            {
                foreach (CardDoorConfig cardDoorConfig in spaceStationConfig.cardReaderConfigs)
                {
                    cardDoors.Add(new CardDoor(cardDoorConfig));
                    yield return CoroutineEx.waitForSeconds(0.01f);
                }

                foreach (DoorConfig doorConfig in spaceStationConfig.doors)
                {
                    RegularDoor regularDoor = RegularDoor.CreateDoor(doorConfig.npcLocations);

                    foreach (LocationConfig locationConfig in doorConfig.locations)
                    {
                        Door door = BuildManager.CreateStaticEntity(doorConfig.prefab, BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), locationConfig.position.ToVector3()), BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), locationConfig.rotation.ToVector3())) as Door;
                        door.canTakeCloser = false;
                        door.canTakeKnocker = false;
                        door.canTakeLock = false;
                        if (doorConfig.blockDoor) door.canHandOpen = false;
                        regularDoor.AddDoor(door);
                        yield return CoroutineEx.waitForSeconds(0.01f);
                    }
                    regularDoors.Add(regularDoor);
                }
                if (NpcManager.CheckNPCSpawn())
                {
                    foreach (var pair in spaceStationConfig.outsideNpcLocations)
                    {
                        NpcConfig npcConfig = ins._config.npcConfigs.FirstOrDefault(x => x.presetName == pair.Key);
                        if (npcConfig == null)
                        {
                            ins.PrintError("The NPC preset does not exist - " + pair.Key);
                            continue;
                        }
                        foreach (LocationConfig locationConfig in pair.Value)
                        {
                            ScientistNPC scientistNPC = NpcManager.CreateStationScientistNpc(npcConfig, BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), locationConfig.position.ToVector3()));
                            stationNpcs.Add(scientistNPC);
                            yield return CoroutineEx.waitForSeconds(0.01f);
                        }
                    }
                }
                foreach (BradleyConfig bradleyConfig in spaceStationConfig.bradleyConfigs)
                {
                    foreach (LocationConfig locationConfig in bradleyConfig.locations)
                    {
                        BradleyAPC bradley = BuildManager.CreateRegularEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), locationConfig.position.ToVector3()), BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), locationConfig.rotation.ToVector3())) as BradleyAPC;
                        bradley.myRigidBody.isKinematic = true;
                        bradley.skinID = 755447;
                        bradley.DoAI = true;
                        bradley._maxHealth = bradleyConfig.hp;
                        bradley.health = bradleyConfig.hp;
                        bradley.maxCratesToSpawn = 0;
                        bradley.viewDistance = bradleyConfig.viewDistance;
                        bradley.searchRange = bradleyConfig.searchDistance;
                        bradley.coaxAimCone *= bradleyConfig.coaxAimCone;
                        bradley.coaxFireRate *= bradleyConfig.coaxFireRate;
                        bradley.coaxBurstLength = bradleyConfig.coaxBurstLength;
                        bradley.nextFireTime = bradleyConfig.nextFireTime;
                        bradley.topTurretFireRate = bradleyConfig.topTurretFireRate;
                        bradley.recoilScale = 0;
                        bradley.ScientistSpawnCount = 0;

                        bradleys.Add(bradley);
                        yield return CoroutineEx.waitForSeconds(0.01f);
                    }
                }
                foreach (TurretConfig stationTurretConfig in spaceStationConfig.turrets)
                {
                    foreach (LocationConfig locationConfig in stationTurretConfig.locations)
                    {
                        AutoTurret autoTurret = BuildManager.CreateStaticEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), locationConfig.position.ToVector3()), BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), locationConfig.rotation.ToVector3())) as AutoTurret;
                        ContainerIOEntity containerIO = autoTurret.GetComponent<ContainerIOEntity>();
                        containerIO.inventory.Insert(ItemManager.CreateByName(stationTurretConfig.shortNameWeapon));
                        containerIO.inventory.Insert(ItemManager.CreateByName(stationTurretConfig.shortNameAmmo, stationTurretConfig.countAmmo));
                        containerIO.SendNetworkUpdate();
                        autoTurret.InitializeHealth(stationTurretConfig.hp, stationTurretConfig.hp);
                        autoTurret.UpdateFromInput(10, 0);
                        autoTurret.isLootable = false;
                        autoTurret.dropFloats = false;
                        autoTurret.dropsLoot = false;
                        turrets.Add(autoTurret);
                        yield return CoroutineEx.waitForSeconds(0.01f);
                    }
                }
                foreach (FlameTurretConfig stationFlameTurretConfig in spaceStationConfig.flameTurrets)
                {
                    foreach (LocationConfig locationConfig in stationFlameTurretConfig.locations)
                    {
                        FlameTurret flameTurret = BuildManager.CreateStaticEntity("assets/prefabs/npc/flame turret/flameturret.deployed.prefab", BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), locationConfig.position.ToVector3()), BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), locationConfig.rotation.ToVector3())) as FlameTurret;
                        flameTurret.inventory.Insert(ItemManager.CreateByName("lowgradefuel", stationFlameTurretConfig.fuel));
                        flameTurret.SendNetworkUpdate();
                        flameTurret.InitializeHealth(stationFlameTurretConfig.hp, stationFlameTurretConfig.hp);
                        flameTurret.isLootable = false;
                        flameTurret.dropFloats = false;
                        flameTurret.dropsLoot = false;
                        flameTurrets.Add(flameTurret);
                        yield return CoroutineEx.waitForSeconds(0.01f);
                    }
                }

                foreach (StationCratesConfig startionCrateConfig in spaceStationConfig.crates)
                {
                    foreach (LocationConfig locationConfig in startionCrateConfig.locations)
                    {
                        BaseEntity entity = LootManager.CreateCrate(startionCrateConfig.prefab, BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), locationConfig.position.ToVector3()), BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), locationConfig.rotation.ToVector3()));
                        crates.Add(entity);
                        yield return CoroutineEx.waitForSeconds(0.01f);
                    }
                }
                spawnEntitiesCorountine = null;
            }

            void BuildEntity(DataFileEntity dataFileEntity)
            {
                BaseEntity entity;

                if (dataFileEntity.prefab.Contains("vendingmachine")) entity = BuildManager.CreateDecorEntityInChildTransform(dataFileEntity, SpaceClass.GetSpaceTransform(), dataFileEntity.skin);
                else entity = BuildManager.CreateRegularEntityInChildTransform(dataFileEntity, SpaceClass.GetSpaceTransform(), dataFileEntity.skin);

                if (entity == null) return;
                constructionEntities.Add(entity);
                PostSpawnEntityUpdate(entity);
            }

            void PostSpawnEntityUpdate(BaseEntity entity)
            {
                if (entity is BuildingBlock)
                {
                    BuildingBlock buildingBlock = entity as BuildingBlock;
                    buildingBlock.SetGrade(BuildingGrade.Enum.TopTier);
                    buildingBlock.SetHealthToMax();
                    UpdateDecorEntity(buildingBlock);
                    return;
                }

                if (entity is ShopFront)
                {
                    ShopFront shopFront = entity as ShopFront;
                    UpdateDecorEntity(shopFront);
                    return;
                }

                if (entity is Door)
                {
                    Door door = entity as Door;
                    if (door.PrefabName.Contains("garage") && door.skinID == 2404906710) UpdateDecorEntity(door);
                    return;
                }

                if (entity is SlidingProgressDoor)
                {
                    SlidingProgressDoor slidingProgressDoor = entity as SlidingProgressDoor;
                    mainDoor.AddSlidingDoor(slidingProgressDoor);
                }

                if (entity is Recycler)
                {
                    return;
                }

                if (entity is ResearchTable)
                {
                    ResearchTable researchTable = entity as ResearchTable;
                    if (!ins._config.spaceStaionConfig.allowResearchTables) UpdateInactiveEntity(entity);
                    return;
                }

                if (entity is RepairBench)
                {
                    RepairBench repairBench = entity as RepairBench;
                    if (!ins._config.spaceStaionConfig.allowRepairbenches) UpdateInactiveEntity(entity);
                    return;
                }

                if (entity is MixingTable)
                {
                    MixingTable mixingTable = entity as MixingTable;
                    if (!ins._config.spaceStaionConfig.allowRepairbenches) UpdateInactiveEntity(entity);
                    return;
                }

                if (entity is Workbench)
                {
                    Workbench workbench = entity as Workbench;
                    if (!ins._config.spaceStaionConfig.allowWorkbenches) UpdateInactiveEntity(entity);
                    return;
                }

                if (entity is ComputerStation)
                {
                    ComputerStation computerStation = entity as ComputerStation;
                    if (!ins._config.spaceStaionConfig.allowComputerStations) UpdateInactiveEntity(entity);
                    return;
                }

                if (entity is StaticInstrument)
                {
                    StaticInstrument staticInstrument = entity as StaticInstrument;
                    if (!ins._config.spaceStaionConfig.allowMusicalInstruments) UpdateInactiveEntity(entity);
                    return;
                }

                if (entity is Barricade)
                {
                    Barricade barricade = entity as Barricade;
                    if (!ins._config.spaceStaionConfig.allowBarricadeDamage) UpdateDecorEntity(barricade);
                    return;
                }

                if (entity is BoxStorage)
                {
                    BoxStorage boxStorage = entity as BoxStorage;
                }

                else
                {
                    entity.SetFlag(BaseEntity.Flags.Busy, true);
                    entity.SetFlag(BaseEntity.Flags.Locked, true);
                }
            }

            void UpdateDecorEntity(BaseCombatEntity baseCombatEntity)
            {
                UpdateInactiveEntity(baseCombatEntity);
                baseCombatEntity.lifestate = BaseCombatEntity.LifeState.Dead;
            }

            void UpdateInactiveEntity(BaseEntity entity)
            {
                entity.SetFlag(BaseEntity.Flags.Busy, true);
                entity.SetFlag(BaseEntity.Flags.Locked, true);
            }

            void PostSpawnUpdate()
            {

            }

            internal static void DestroySpaceStation()
            {
                if (spaceStation == null) return;
                if (spaceStation.spawnEntitiesCorountine != null) ServerMgr.Instance.StopCoroutine(spaceStation.spawnEntitiesCorountine);
                if (spaceStation.spawnConstructionsCorountine != null) ServerMgr.Instance.StopCoroutine(spaceStation.spawnConstructionsCorountine);

                foreach (BaseEntity entity in spaceStation.constructionEntities) if (entity.IsExists()) entity.Kill();
                spaceStation.DespawnStationEntities();
                spaceStation.mainDoor.Destroy();

                spaceStation = null;
            }

            void DespawnStationEntities()
            {
                foreach (CardDoor cardDoor in cardDoors) if (cardDoor != null) cardDoor.DestroyCardDoor();
                foreach (RegularDoor regularDoor in regularDoors) if (regularDoor != null) regularDoor.DestroyDoor();
                foreach (BaseEntity entity in crates) if (entity.IsExists()) entity.Kill();
                foreach (AutoTurret entity in turrets)
                {
                    if (entity.IsExists())
                    {
                        AutoTurret.interferenceUpdateList.Remove(entity);
                        entity.Kill();
                    }
                }
                foreach (BradleyAPC entity in bradleys) if (entity.IsExists()) entity.Kill();
                foreach (FlameTurret entity in flameTurrets) if (entity.IsExists()) entity.Kill();
                foreach (ScientistNPC entity in stationNpcs) if (entity.IsExists()) entity.Kill();

                cardDoors.Clear();
                regularDoors.Clear();
                crates.Clear();
                turrets.Clear();
                bradleys.Clear();
                flameTurrets.Clear();
                stationNpcs.Clear();
            }

            sealed class MainDoor : FacepunchBehaviour
            {
                HashSet<SlidingProgressDoor> firsDoors = new HashSet<SlidingProgressDoor>();
                SlidingProgressDoor secondDoor;
                Coroutine doorOpenCorountine;
                SphereCollider sphereCollider;
                Vector3 doorPosition;
                float opentTime = 0;
                MainDoorConfig mainDoorConfig;
                HashSet<PressButton> pressButtons = new HashSet<PressButton>();

                internal static MainDoor CreateMainDoor(MainDoorConfig mainDoorConfig)
                {
                    GameObject gameObject = new GameObject();
                    gameObject.layer = (int)Layer.Reserved1;
                    gameObject.transform.position = SpaceClass.GetSpaceTransform().position;
                    MainDoor mainDoor = gameObject.AddComponent<MainDoor>();
                    mainDoor.mainDoorConfig = mainDoorConfig;

                    return mainDoor;
                }

                internal void AddSlidingDoor(SlidingProgressDoor slidingProgressDoor)
                {
                    firsDoors.Add(slidingProgressDoor);
                    if (firsDoors.Count == 5)
                        Init();
                }

                internal bool IsMainDoorButton(ulong netId)
                {
                    return pressButtons.Any(x => x.IsExists() && x.net.ID.Value == netId);
                }

                void Init()
                {
                    DefineSecondDoor();
                    DefineMainDoorPositionAndCreateTrigger();
                    SpawnDoorOpenButtons();
                    StartDoorCorountine();
                }

                void DefineSecondDoor()
                {
                    secondDoor = firsDoors.Min(x => Vector3.Distance(SpaceClass.GetSpaceTransform().position, x.transform.position));
                    firsDoors.Remove(secondDoor);
                }

                void DefineMainDoorPositionAndCreateTrigger()
                {
                    if (!mainDoorConfig.isOpenAutomatically)
                        return;

                    Vector3 triggerPosition = Vector3.zero;

                    foreach (SlidingProgressDoor slidingProgressDoor in firsDoors)
                        triggerPosition += slidingProgressDoor.transform.position;

                    triggerPosition /= firsDoors.Count;

                    doorPosition = triggerPosition;
                    triggerPosition -= SpaceClass.GetSpaceTransform().forward * 5.5f;

                    sphereCollider = BuildManager.CreateSphereCollider(this.gameObject, 7.5f, triggerPosition);
                }

                void SpawnDoorOpenButtons()
                {
                    foreach (LocationConfig locationConfig in mainDoorConfig.openButtonLocations)
                    {
                        Vector3 localPosition = locationConfig.position.ToVector3();
                        Vector3 localRotation = locationConfig.rotation.ToVector3();

                        Vector3 globalPosition = BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), localPosition);
                        Quaternion globalRotation = BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), localRotation);

                        PressButton pressButton = BuildManager.CreateStaticEntity("assets/prefabs/deployable/playerioents/button/button.prefab", globalPosition, globalRotation) as PressButton;
                        pressButtons.Add(pressButton);
                    }
                }

                void StartDoorCorountine()
                {
                    if (doorOpenCorountine != null)
                        ServerMgr.Instance.StopCoroutine(doorOpenCorountine);

                    doorOpenCorountine = ServerMgr.Instance.StartCoroutine(DoorOpenCorountine());
                }

                IEnumerator DoorOpenCorountine()
                {
                    while (true)
                    {
                        while (opentTime <= 0)
                        {
                            secondDoor.IOInput(null, IOEntity.IOType.Electric, 10 * (secondDoor.openProgress + 0.025f));
                            secondDoor.UpdateProgress();
                            yield return CoroutineEx.waitForSeconds(0.1f);
                        }
                        while (opentTime > 0)
                        {
                            foreach (SlidingProgressDoor slidingProgressDoor in firsDoors)
                            {
                                if (slidingProgressDoor == null) continue;
                                slidingProgressDoor.IOInput(null, IOEntity.IOType.Electric, 10 * (secondDoor.openProgress + 0.025f));
                                slidingProgressDoor.UpdateProgress();
                            }
                            opentTime -= 0.1f;
                            if (opentTime <= 0)
                            {
                                yield return CoroutineEx.waitForSeconds(3);
                                Astronaut.CheckAllAsrtoanuts();
                                SpaceShuttle.UpdateAllSpaceShuttles();
                            }
                            yield return CoroutineEx.waitForSeconds(0.1f);
                        }
                    }
                }

                void OnTriggerEnter(Collider other)
                {
                    BaseEntity entity = other.ToBaseEntity();
                    if (entity == null) return;

                    BasePlayer player = entity as BasePlayer;
                    if (player != null)
                    {
                        if (ins._config.astronautConfig.astronautSpaceStationMode) return;
                        if (Vector3.Distance(doorPosition, SpaceClass.GetSpaceTransform().position) > Vector3.Distance(player.transform.position, SpaceClass.GetSpaceTransform().position))
                        {
                            ins.NextTick(() => Astronaut.TryAddComponentToPlayer(player, false));
                        }

                        OpenMainDoor();
                    }
                    else if (entity is BaseSubmarine)
                        OpenMainDoor();
                }

                internal void SwitchMainDoor()
                {
                    if (opentTime > 0)
                    {
                        opentTime = 0;
                        Astronaut.CheckAllAsrtoanuts();
                        SpaceShuttle.UpdateAllSpaceShuttles();
                    }
                    else
                        opentTime = 20;
                }

                internal void OpenMainDoor()
                {
                    opentTime = 20;
                }

                internal void Destroy()
                {
                    if (doorOpenCorountine != null)
                        ServerMgr.Instance.StopCoroutine(doorOpenCorountine);

                    UnityEngine.GameObject.DestroyImmediate(this.gameObject);
                }

                void OnDestroy()
                {
                    foreach (PressButton pressButton in pressButtons)
                        if (pressButton.IsExists())
                            pressButton.Kill();
                }
            }

            sealed class CardDoor : RegularDoor
            {
                CardReader cardReader;
                PressButton pressButton;

                internal CardDoor(CardDoorConfig cardDoorConfig)
                {
                    Door door = BuildManager.CreateStaticEntity(cardDoorConfig.doorPrefab, BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), cardDoorConfig.doorLocation.position.ToVector3()), BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), cardDoorConfig.doorLocation.rotation.ToVector3())) as Door;
                    cardReader = BuildManager.CreateStaticEntity("assets/prefabs/io/electric/switches/cardreader.prefab", BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), cardDoorConfig.cardReaderLocation.position.ToVector3()), BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), cardDoorConfig.cardReaderLocation.rotation.ToVector3())) as CardReader;

                    cardReader.accessLevel = 1;
                    cardReader.SetFlag(cardReader.AccessLevel1, false);
                    cardReader.SetFlag(cardReader.AccessLevel2, false);
                    cardReader.SetFlag(cardReader.AccessLevel3, false);
                    if (cardDoorConfig.cardType == 0)
                    {

                        cardReader.SetFlag(cardReader.AccessLevel1, true);
                        cardReader.accessLevel = 1;
                    }
                    else if (cardDoorConfig.cardType == 1)
                    {
                        cardReader.SetFlag(cardReader.AccessLevel2, true);
                        cardReader.accessLevel = 2;
                    }
                    else if (cardDoorConfig.cardType == 2)
                    {
                        cardReader.SetFlag(cardReader.AccessLevel3, true);
                        cardReader.accessLevel = 3;
                    }
                    else if (cardDoorConfig.cardType == 3)
                    {
                        cardReader.SetFlag(cardReader.AccessLevel3, true);
                        cardReader.accessLevel = 4;
                    }
                    cardReader.UpdateFromInput(10, 0);
                    cardReader.SendNetworkUpdate();
                    pressButton = BuildManager.CreateStaticEntity("assets/prefabs/io/electric/switches/pressbutton/pressbutton.prefab", BuildManager.GetGlobalPosition(SpaceClass.GetSpaceTransform(), cardDoorConfig.buttonLocation.position.ToVector3()), BuildManager.GetGlobalRotation(SpaceClass.GetSpaceTransform(), cardDoorConfig.buttonLocation.rotation.ToVector3())) as PressButton;
                    Init(door, cardDoorConfig.npcLocations);
                }

                internal bool IsOwnCardReader(ulong cardReaderNetId)
                {
                    return cardReader != null && cardReaderNetId == cardReader.net.ID.Value;
                }

                internal bool IsOwnButton(ulong buttonNetId)
                {
                    return pressButton != null && pressButton.net.ID.Value == buttonNetId;
                }

                internal void OnCardSwipe(Keycard card, BasePlayer player)
                {
                    Effect.server.Run(cardReader.swipeEffect.resourcePath, cardReader.audioPosition.position, Vector3.up, player.net.connection, false);
                    string cardName = "";
                    if (cardReader.accessLevel == 1)
                    {
                        cardName = "GreenCard";
                        if (card.accessLevel != 1)
                        {
                            DeniedAcces();
                            return;
                        }
                    }
                    else if (cardReader.accessLevel == 2)
                    {
                        cardName = "BlueCard";
                        if (card.accessLevel != 2)
                        {
                            DeniedAcces();
                            return;
                        }
                    }
                    else if (cardReader.accessLevel == 3)
                    {
                        cardName = "RedCard";
                        if (card.accessLevel != 3)
                        {
                            DeniedAcces();
                            return;
                        }
                    }
                    else if (cardReader.accessLevel == 4)
                    {
                        cardName = "SpaceCard";
                        Item cardItem = card.GetItem();
                        if (cardItem.info.shortname != ins._config.spaceCardConfig.shortname || cardItem.skin != ins._config.spaceCardConfig.skin)
                        {
                            NotifyManager.SendMessageToPlayer(player, "NeedUseSpaceCard", ins._config.prefix);
                            DeniedAcces();
                            return;
                        }
                    }

                    if (ins.plugins.Exists("PveMode") && ins.PveMode.Call("CanActionEvent", ins.Name, player) != null)
                    {
                        DeniedAcces();
                        return;
                    }

                    EconomyManager.ActionEconomy(player.userID, cardName);
                    card.GetItem().LoseCondition(ins._config.spaceCardConfig.helthLossScale);
                    OpenDoor();
                    Effect.server.Run(cardReader.accessGrantedEffect.resourcePath, cardReader.audioPosition.position, Vector3.up);
                }

                void DeniedAcces()
                {
                    Effect.server.Run(cardReader.accessDeniedEffect.resourcePath, cardReader.audioPosition.position, Vector3.up);
                    cardReader.CancelInvoke(cardReader.GrantCard);
                }

                internal void OnButtonPress()
                {
                    OpenDoor();
                }

                void OpenDoor()
                {
                    foreach (Door door in doors)
                    {
                        if (door != null)
                            door.SetOpen(true);
                    }
                }

                internal void DestroyCardDoor()
                {
                    DestroyDoor();
                    if (cardReader.IsExists()) cardReader.Kill();
                    if (pressButton.IsExists()) pressButton.Kill();
                }
            }

            class RegularDoor
            {
                internal HashSet<Door> doors = new HashSet<Door>();
                internal Dictionary<string, HashSet<LocationConfig>> npcPositions;
                internal bool isDoorOpen = false;

                internal static RegularDoor CreateDoor(Dictionary<string, HashSet<LocationConfig>> npcPositions)
                {
                    RegularDoor regularDoor = new RegularDoor();
                    regularDoor.npcPositions = npcPositions;
                    return regularDoor;
                }

                internal void AddDoor(Door door)
                {
                    if (!doors.Contains(door)) doors.Add(door);
                }

                internal bool IsOwnDoor(ulong doorNetId)
                {
                    return doors.Any(x => x != null && x.net.ID.Value == doorNetId);
                }

                protected void Init(Door door, Dictionary<string, HashSet<LocationConfig>> npcPositions)
                {
                    if (door != null && !doors.Contains(door)) doors.Add(door);
                    this.npcPositions = npcPositions;
                }

                internal void DestroyDoor()
                {
                    foreach (Door door in doors) if (door.IsExists()) door.Kill();
                }
            }
        }

        sealed class Astronaut : FacepunchBehaviour
        {
            static HashSet<Astronaut> astronauts = new HashSet<Astronaut>();
            internal AstronautParametersConfig astronautParameters = ins._config.astronautConfig.parametersConfig;
            BasePlayer player;
            PlayerZeroGravity playerZeroGravity;
            Coroutine checkPLayerHeightCorountine;
            Coroutine breatheCorountine;

            internal static void TryAddComponentToPlayer(BasePlayer player, bool checkSpaceStation = true)
            {
                if (!ins._config.mainConfig.enableZeroGravity || player == null || !EventManager.isEventActive || !SpaceClass.IsInSpace(player.transform.position, checkMaxHeight: false) || (player.IsAdmin && player.IsFlying) || player.transform.position.y > 1500f) return;
                astronauts.RemoveWhere(x => x == null);
                BaseEntity playerParentEntity = player.GetParentEntity();
                if (GetComponentFromUserId(player.userID) != null || player.isMounted || (checkSpaceStation && IsPlayerInSpaceStation(player)) || (playerParentEntity != null && playerParentEntity is HotAirBalloon)) return;
                AddComponentToPlayer(player);
            }

            internal static Astronaut GetComponentFromUserId(ulong userId)
            {
                return astronauts.FirstOrDefault(x => x != null && x.player != null && x.player.userID == userId);
            }

            static void AddComponentToPlayer(BasePlayer player)
            {
                Astronaut Astronaut = player.gameObject.AddComponent<Astronaut>();
                Astronaut.InitComponent(player);
                astronauts.Add(Astronaut);
            }

            internal static void RemoveAllComponents()
            {
                foreach (Astronaut astronaut in astronauts)
                {
                    RemoveComponent(astronaut);
                }
            }

            internal static void TryRemoveComponentFromPlayer(ulong userID)
            {
                Astronaut astronaut = GetComponentFromUserId(userID);
                if (astronaut != null)
                {
                    RemoveComponent(astronaut);
                }
            }

            internal static void RemoveComponent(Astronaut astronaut)
            {
                if (astronaut != null)
                {
                    ins.DisableThirdViewMod(astronaut.player);
                    UnityEngine.Object.Destroy(astronaut);
                }
            }

            internal void OnWearContainerChanged()
            {
                astronautParameters = ins._config.astronautConfig.parametersConfig;
                foreach (Item item in player.inventory.containerWear.itemList)
                {
                    AstronautParametersConfig additionalAstronautParameter = GetAdditionalParametersFromItem(item);
                    if (additionalAstronautParameter != null) astronautParameters += additionalAstronautParameter;
                }
            }

            internal static bool IsPlayerInSpaceStation(BasePlayer player)
            {
                if (!ins._config.spaceStaionConfig.enableGravity) return false;
                RaycastHit raycastHit;
                Physics.Raycast(player.eyes.transform.position, Vector3.up, out raycastHit, 10, 1 << 21);
                if (Physics.Raycast(player.eyes.transform.position, Vector3.up, out raycastHit, 10, 1 << 21) &&
                    Physics.Raycast(player.eyes.transform.position, -Vector3.up, out raycastHit, 10, 1 << 21) &&
                    !raycastHit.collider.name.Contains("floor.triangle.twig") && !raycastHit.collider.name.Contains("floor.frame")) return true;
                return false;
            }

            AstronautParametersConfig GetAdditionalParametersFromItem(Item item)
            {
                if (item == null) return null;
                SpaceSuitConfig spaceSuitConfig = ins._config.spaceSuits.FirstOrDefault(x => x.itemConfig.shortname == item.info.shortname && (x.itemConfig.skin == 0 || x.itemConfig.skin == item.skin));
                if (spaceSuitConfig == null) return null;
                return spaceSuitConfig.additionalAstronautParameters;
            }

            void InitComponent(BasePlayer player)
            {
                this.player = player;
                if (ins._config.astronautConfig.thirdPerson) ins.EnableThirdViewMod(player);
                playerZeroGravity = PlayerZeroGravity.CreatePlayerZeroGravity(player, this);
                breatheCorountine = ServerMgr.Instance.StartCoroutine(BreatheCorountine());
                checkPLayerHeightCorountine = ServerMgr.Instance.StartCoroutine(CheckPLayerHeightCorountine());
                OnWearContainerChanged();
            }

            IEnumerator CheckPLayerHeightCorountine()
            {
                int time = -1;
                while (true)
                {
                    if (!SpaceClass.IsInSpace(player.transform.position, false))
                    {
                        if (time == -1)
                        {
                            NotifyManager.SendMessageToPlayer(player, "GravityTakeEffect", ins._config.prefix, time);
                            time = 10;
                        }

                        time--;
                        if (time == -1)
                            RemoveComponent(this);
                    }
                    else if (time != -1)
                    {
                        time = -1;
                    }
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            IEnumerator BreatheCorountine()
            {
                while (true)
                {
                    if (astronautParameters.soudOfBreathe) Effect.server.Run(astronautParameters.canBreathe ? "assets/prefabs/clothes/diving.tank/effects/scuba_inhale.prefab" : "assets/bundled/prefabs/fx/player/drown.prefab", player, StringPool.Get("jaw"), Vector3.zero, Vector3.forward, null, false);
                    yield return CoroutineEx.waitForSeconds(4f);
                }
            }

            void FixedUpdate()
            {
                if (player == null) return;
                CheckNearShuttle();
            }

            void CheckNearShuttle()
            {
                if (!player.serverInput.WasJustPressed(BUTTON.USE)) return;
                RaycastHit raycastHit;
                if (!Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 2.5f)) return;
                BaseEntity entity = raycastHit.GetEntity();
                if (entity == null) return;
                else if (entity.ShortPrefabName == "submarineduo.entity")
                {
                    SpaceShuttle controlledSpaceShuttle = SpaceShuttle.GetSpaceShuttleByMainSubmarineNetId(entity.net.ID.Value);
                    if (controlledSpaceShuttle != null && controlledSpaceShuttle.IsPlayerCanMount(player))
                    {
                        controlledSpaceShuttle.TryMountPlayer(player);
                    }
                }
            }

            void OnDestroy()
            {
                playerZeroGravity.DestroyZeroGravity();
                if (checkPLayerHeightCorountine != null) ServerMgr.Instance.StopCoroutine(checkPLayerHeightCorountine);
                if (breatheCorountine != null) ServerMgr.Instance.StopCoroutine(breatheCorountine);
            }

            internal static void CheckAllAsrtoanuts()
            {
                astronauts.RemoveWhere(x => x == null);
                foreach (Astronaut astronaut in astronauts)
                {
                    if (astronaut == null) continue;
                    if (SpaceStation.IsPositionInSpaceStation(astronaut.player.eyes.transform.position))
                    {
                        RaycastHit raycastHit;
                        Physics.Raycast(astronaut.player.eyes.transform.position, Vector3.up, out raycastHit, 10, 1 << 21);
                        float upDistance = raycastHit.distance;
                        if (upDistance < 1.5f)
                        {
                            astronaut.playerZeroGravity.Move(Vector3.down, 100);
                            astronaut.Invoke(() =>
                            {
                                if (astronaut != null) RemoveComponent(astronaut);
                            }, 0.5f);
                        }
                        else RemoveComponent(astronaut);
                    }
                }
            }

            class PlayerZeroGravity : ZeroGravity
            {
                Astronaut astronaut;
                InputState inputState;

                internal static PlayerZeroGravity CreatePlayerZeroGravity(BasePlayer player, Astronaut astronaut)
                {
                    PlayerZeroGravity playerZeroGravity = (new GameObject()).AddComponent<PlayerZeroGravity>();
                    playerZeroGravity.Init(player, astronaut);
                    return playerZeroGravity;
                }

                internal void Init(BasePlayer player, Astronaut astronaut)
                {
                    base.Init(player);
                    this.astronaut = astronaut;
                    inputState = player.serverInput;
                    if (movableBaseMountable != null) movableBaseMountable.InitAstronaut(astronaut);
                    rigidbody.angularDrag = 1;
                    rigidbody.drag = ins._config.astronautConfig.controlConfig.drag;
                }

                internal void DestroyPLayerZeroGraviety()
                {
                    base.DestroyZeroGravity();
                }

                internal void Move(Vector3 direction, float force)
                {
                    AddForce(direction, force);
                }

                void FixedUpdate()
                {
                    if (!movableBaseMountable._mounted.IsExists())
                    {
                        Astronaut.RemoveComponent(astronaut);
                    }
                    if (movableDroppedItem.transform.position.y > SpaceClass.maxSpaceHeight)
                    {
                        AddForce(Vector3.down * 1.5f, 1);
                    }
                    else
                    {
                        UpdateButtonControl();
                    }
                    UpdateMouseControl();
                    if (ins._config.astronautConfig.controlConfig.controlType != 1) AutoHorizont();
                }

                void UpdateButtonControl()
                {
                    if (inputState.IsDown(BUTTON.SPRINT)) AddForce(movableDroppedItem.transform.up * 1.5f, astronaut.astronautParameters.speedScale);
                    else if (inputState.IsDown(BUTTON.DUCK)) AddForce(-movableDroppedItem.transform.up * 1.5f, astronaut.astronautParameters.speedScale);

                    if (inputState.IsDown(BUTTON.FORWARD)) AddForce(movableDroppedItem.transform.forward * 1.5f, astronaut.astronautParameters.speedScale);
                    else if (inputState.IsDown(BUTTON.BACKWARD)) AddForce(-movableDroppedItem.transform.forward * 1.5f, astronaut.astronautParameters.speedScale);

                    if (ins._config.astronautConfig.controlConfig.controlType == 1)
                    {
                        if (inputState.IsDown(BUTTON.LEFT)) AddTorgue(movableDroppedItem.transform.forward, ins._config.astronautConfig.controlConfig.rotationButtonSpeedScale * 2.5f);
                        else if (inputState.IsDown(BUTTON.RIGHT)) AddTorgue(-movableDroppedItem.transform.forward, ins._config.astronautConfig.controlConfig.rotationButtonSpeedScale * 2.5f);
                    }
                    else
                    {
                        if (inputState.IsDown(BUTTON.LEFT)) AddTorgue(-movableDroppedItem.transform.up * 3.5f, ins._config.astronautConfig.controlConfig.rotationButtonSpeedScale);
                        else if (inputState.IsDown(BUTTON.RIGHT)) AddTorgue(movableDroppedItem.transform.up * 3.5f, ins._config.astronautConfig.controlConfig.rotationButtonSpeedScale);
                    }
                }

                void UpdateMouseControl()
                {
                    if (ins._config.astronautConfig.controlConfig.controlType != 1 || inputState.IsDown(BUTTON.FIRE_SECONDARY)) return;
                    Quaternion targetrotation = player.eyes.GetLookRotation();
                    movableDroppedItem.transform.rotation = Quaternion.Lerp(movableDroppedItem.transform.rotation, targetrotation, 0.05f * ins._config.astronautConfig.controlConfig.rotationMouseSpeedScale);
                }

                void AutoHorizont()
                {
                    float angel = Vector3.Angle(Vector3.up, movableDroppedItem.transform.up);
                    rigidbody.AddForceAtPosition(Vector3.up * 0.005f * angel * rigidbody.mass, movableDroppedItem.transform.position + movableDroppedItem.transform.up);
                    rigidbody.AddForceAtPosition(-Vector3.up * 0.005f * angel * rigidbody.mass, movableDroppedItem.transform.position - movableDroppedItem.transform.up);
                }
            }
        }

        sealed class Aerostat : FacepunchBehaviour
        {
            const ulong aerostatSkin = 15446541673;
            static HashSet<Aerostat> aerostats = new HashSet<Aerostat>();
            HotAirBalloon hotAirBalloon;
            AerostatState aerostatState = AerostatState.Default;

            enum AerostatState
            {
                Default,
                FlyingToSpace,
                InSpace,
                FallingfromSpace
            }

            internal static Aerostat GetAetostatByBallonNetId(ulong netId)
            {
                return aerostats.FirstOrDefault(x => x != null && x.hotAirBalloon != null && x.hotAirBalloon.net.ID.Value == netId);
            }

            internal static Aerostat SpawnAerostat(Vector3 position, Quaternion rotation)
            {
                HotAirBalloon hotAirBalloon = BuildManager.CreateRegularEntity("assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", position, rotation, enableSaving: true) as HotAirBalloon;
                return AttachAerostatClass(hotAirBalloon);
            }

            internal static void DestroyAllAerostats()
            {
                foreach (Aerostat aerostat in aerostats)
                {
                    Destroy(aerostat);
                }
                aerostats.Clear();
            }

            internal static void CheckAllBallons()
            {
                foreach (HotAirBalloon hotAirBalloon in BaseNetworkable.serverEntities.OfType<HotAirBalloon>())
                {
                    if (!hotAirBalloon.IsExists()) return;
                    if (ins._config.aerostatConfig.allBallons || hotAirBalloon.skinID == aerostatSkin)
                        AttachAerostatClass(hotAirBalloon);
                }
            }

            internal static Aerostat AttachAerostatClass(HotAirBalloon hotAirBalloon)
            {
                if (aerostats.Any(x => x != null && x.hotAirBalloon != null && x.hotAirBalloon.net.ID == hotAirBalloon.net.ID)) return null;
                hotAirBalloon.skinID = aerostatSkin;
                Aerostat aerostat = hotAirBalloon.gameObject.AddComponent<Aerostat>();
                aerostat.Init(hotAirBalloon);
                aerostats.Add(aerostat);
                return aerostat;
            }

            internal void AddFuel(int fuelAmount)
            {
                if (fuelAmount <= 0) return;
                StorageContainer fuelContainer = hotAirBalloon.fuelSystem.GetFuelContainer();
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);
            }

            void Init(HotAirBalloon hotAirBalloon)
            {
                this.hotAirBalloon = hotAirBalloon;
                if (EventManager.isEventActive)
                    FlyingToSpaceControl();
            }

            internal void OnAerostatToggle()
            {
                if (aerostatState == AerostatState.InSpace)
                {
                    hotAirBalloon.myRigidbody.useGravity = true;
                }
                else if (hotAirBalloon.inflationLevel == 0 && !SpaceClass.IsInSpace(hotAirBalloon.transform.position) && EventManager.isEventActive)
                {
                    foreach (BasePlayer player in hotAirBalloon.GetComponentsInChildren<BasePlayer>())
                    {
                        if (player.IsRealPlayer()) NotifyManager.SendMessageToPlayer(player, "AerostatDescription", ins._config.prefix);
                    }
                }
            }

            internal bool IsAnyMounted()
            {
                return hotAirBalloon.GetComponentsInChildren<BasePlayer>().Length > 0;
            }

            void FixedUpdate()
            {
                if (aerostatState == AerostatState.Default) AtmosphereControll();
                if (aerostatState == AerostatState.FlyingToSpace) FlyingToSpaceControl();
                if (aerostatState == AerostatState.InSpace) InSpaceControl();
            }

            void AtmosphereControll()
            {
                float avgTerrainHeight = Mathf.Lerp(hotAirBalloon.avgTerrainHeight, TerrainMeta.HeightMap.GetHeight(hotAirBalloon.transform.position), UnityEngine.Time.deltaTime);
                float single2 = 1f - Mathf.InverseLerp(avgTerrainHeight + HotAirBalloon.serviceCeiling - 20f, avgTerrainHeight + HotAirBalloon.serviceCeiling, hotAirBalloon.buoyancyPoint.position.y);
                if (single2 < 1)
                {
                    ChangeState(AerostatState.FlyingToSpace);
                }
            }

            void FlyingToSpaceControl()
            {
                if (!EventManager.isEventActive)
                {
                    ChangeState(AerostatState.Default);
                    return;
                }
                float upForceMultiplicator = GetUpForceMultiplicator();
                Vector3 spacePosition = SpaceClass.GetSpaceTransform().position;
                if (hotAirBalloon.IsFullyInflated)
                {
                    if (spacePosition.y > hotAirBalloon.transform.position.y) hotAirBalloon.myRigidbody.AddForceAtPosition(((Vector3.up * hotAirBalloon.liftAmount) * hotAirBalloon.currentBuoyancy) * (1 - upForceMultiplicator), hotAirBalloon.buoyancyPoint.position, ForceMode.Force);
                    Vector3 direction = (SpaceClass.GetSpaceTransform().position - hotAirBalloon.transform.position).normalized;
                    hotAirBalloon.myRigidbody.AddForceAtPosition(direction * 200, hotAirBalloon.buoyancyPoint.position, ForceMode.Force);
                }

                if (Vector3.Distance(spacePosition, hotAirBalloon.transform.position) < ins._config.mainConfig.spaceRadius && SpaceClass.IsInSpace(hotAirBalloon.transform.position))
                {
                    ChangeState(AerostatState.InSpace);
                    return;
                }
            }

            void InSpaceControl()
            {
                if (!EventManager.isEventActive || GetUpForceMultiplicator() == 1)
                {
                    ChangeState(AerostatState.Default);
                    return;
                }

                hotAirBalloon.myRigidbody.AddForceAtPosition(Vector3.up * 500, hotAirBalloon.centerOfMass.position + hotAirBalloon.transform.up);
                hotAirBalloon.myRigidbody.AddForceAtPosition(-Vector3.up * 500, hotAirBalloon.centerOfMass.position - hotAirBalloon.transform.up);
            }

            void ChangeState(AerostatState aerostatState)
            {
                this.aerostatState = aerostatState;
                if (aerostatState == AerostatState.Default)
                {
                    hotAirBalloon.myRigidbody.useGravity = true;
                    hotAirBalloon.windForce = 600f;
                }

                if (aerostatState == AerostatState.FlyingToSpace)
                {
                    hotAirBalloon.myRigidbody.useGravity = true;
                }

                if (aerostatState == AerostatState.InSpace)
                {
                    hotAirBalloon.ScheduleOff();
                    hotAirBalloon.myRigidbody.useGravity = false;
                    hotAirBalloon.windForce = 0.0001f;
                    hotAirBalloon.inflationLevel = 0;

                    foreach (BasePlayer player in hotAirBalloon.GetComponentsInChildren<BasePlayer>())
                    {
                        if (player.IsRealPlayer())
                        {
                            NotifyManager.SendMessageToPlayer(player, "AerostatInSpace", ins._config.prefix);
                            Astronaut.TryAddComponentToPlayer(player);
                        }
                    }
                }
            }

            float GetUpForceMultiplicator()
            {
                float avgTerrainHeight = Mathf.Lerp(hotAirBalloon.avgTerrainHeight, TerrainMeta.HeightMap.GetHeight(hotAirBalloon.transform.position), UnityEngine.Time.deltaTime);
                return 1f - Mathf.InverseLerp(avgTerrainHeight + HotAirBalloon.serviceCeiling - 20f, avgTerrainHeight + HotAirBalloon.serviceCeiling, hotAirBalloon.buoyancyPoint.position.y);
            }

            void OnTriggerEnter(Collider other)
            {
                if (!SpaceClass.IsInSpace(hotAirBalloon.transform.position)) return;
                BaseEntity entity = other.ToBaseEntity();
                if (entity == null) return;
                BasePlayer player = entity as BasePlayer;
                if (player != null) Invoke(() => Astronaut.TryRemoveComponentFromPlayer(player.userID), 0.25f);
            }

            void OnTriggerExit(Collider other)
            {
                if (!SpaceClass.IsInSpace(hotAirBalloon.transform.position)) return;
                BaseEntity entity = other.ToBaseEntity();
                if (entity == null) return;
                BasePlayer player = entity as BasePlayer;
                if (player != null) Invoke(() => Astronaut.TryAddComponentToPlayer(player), 0.5f);
            }

            internal void KillAerostat()
            {
                hotAirBalloon.Kill();
            }

            void OnDestroy()
            {
                hotAirBalloon.myRigidbody.useGravity = true;
                hotAirBalloon.windForce = 600f;
            }
        }

        sealed class SpaceShuttle : FacepunchBehaviour
        {
            const ulong shuttleSkin = 15446541672;
            const ulong activateShuttleSkin = 15446541673;
            static List<SpaceShuttle> spaceShuttles = new List<SpaceShuttle>();
            static List<SpaceShuttle> eventBeginSpaceShuttles = new List<SpaceShuttle>();
            static Coroutine shuttleRespawnCorountine;
            SubmarineDuo submarineDuo;
            BasePlayer driver;
            ShuttleParameters spaceShuttleParameters;
            Rigidbody rigidbody;
            Coroutine updateCorountine;
            HashSet<BaseCombatEntity> decorSubmarines = new HashSet<BaseCombatEntity>();
            HashSet<BaseEntity> decorEntities = new HashSet<BaseEntity>();
            HashSet<BaseEntity> decorCampfires = new HashSet<BaseEntity>();
            VendingMachineMapMarker vendingMarker;

            bool shoudBeDestroyed;

            internal static void RespawnShuttles()
            {
                if (!ins._config.shuttleConfig.respawnConfig.enableSpawn) return;
                if (shuttleRespawnCorountine != null) return;
                shuttleRespawnCorountine = ServerMgr.Instance.StartCoroutine(RespawnShuttlesCorountine());
            }

            internal static void UpdateAllSpaceShuttles()
            {
                for (int i = 0; i < spaceShuttles.Count; i++)
                {
                    SpaceShuttle spaceShuttle = spaceShuttles[i];
                    if (spaceShuttle == null) spaceShuttles.RemoveAt(i);
                    else if (SpaceStation.IsPositionInSpaceStation(spaceShuttle.submarineDuo.transform.position)) spaceShuttle.submarineDuo.rigidBody.useGravity = true;
                }
            }

            internal static void StopRespawnShuttles()
            {
                if (shuttleRespawnCorountine != null) ServerMgr.Instance.StopCoroutine(shuttleRespawnCorountine);
            }

            static IEnumerator RespawnShuttlesCorountine()
            {
                yield return CoroutineEx.waitForSeconds(ins._config.shuttleConfig.respawnConfig.shuttleRespawnPeriod);
                RespawnRoadShuttles();
                shuttleRespawnCorountine = null;
                RespawnShuttles();
            }

            static void RespawnRoadShuttles()
            {
                if (ins._config.shuttleConfig.respawnConfig.roadShuttlesNumber == 0) return;
                int spawnCount = 0;
                HashSet<JunkPile> junkPilePopulation = BaseNetworkable.serverEntities.OfType<JunkPile>();
                foreach (JunkPile junkPile in junkPilePopulation)
                {
                    if (spawnCount >= ins._config.shuttleConfig.respawnConfig.roadShuttlesNumber) break;
                    if (junkPile == null || junkPile.transform.position.y < 0.2f || junkPile.name.Contains("water")) continue;
                    if (BaseNetworkable.HasCloseConnections(junkPile.transform.position, 15)) continue;
                    Vector3 position = junkPile.transform.position;
                    Quaternion rotation = junkPile.transform.rotation;
                    junkPile.SinkAndDestroy();
                    junkPile.Kill();
                    SpawnSpaceShuttle(position, rotation);
                    spawnCount++;
                }
            }

            internal static void OnEventStart()
            {
                if (ins._config.shuttleConfig.respawnConfig.eventBegignigSpawnConfig.roadShuttlesNumber == 0) return;

                eventBeginSpaceShuttles.Clear();
                int spawnCount = 0;
                HashSet<JunkPile> junkPilePopulation = BaseNetworkable.serverEntities.OfType<JunkPile>();
                foreach (JunkPile junkPile in junkPilePopulation)
                {
                    if (spawnCount >= ins._config.shuttleConfig.respawnConfig.eventBegignigSpawnConfig.roadShuttlesNumber) break;
                    if (junkPile == null || junkPile.transform.position.y < 1 || junkPile.name.Contains("water")) continue;
                    if (BaseNetworkable.HasCloseConnections(junkPile.transform.position, 15)) continue;
                    Vector3 position = junkPile.transform.position;
                    Quaternion rotation = junkPile.transform.rotation;
                    junkPile.SinkAndDestroy();
                    junkPile.Kill();
                    SpaceShuttle spaceShuttle = SpawnSpaceShuttle(position, rotation);
                    eventBeginSpaceShuttles.Add(spaceShuttle);
                    spawnCount++;
                }
            }

            internal static void OnEventStop()
            {
                if (ins._config.shuttleConfig.respawnConfig.eventBegignigSpawnConfig.deleteShuttles)
                {
                    foreach (SpaceShuttle spaceShuttle in eventBeginSpaceShuttles)
                    {
                        if (spaceShuttle != null)
                        {
                            if (!spaceShuttle.IsAnyMounted())
                                spaceShuttle.KillSubmarine();
                            else
                                spaceShuttle.shoudBeDestroyed = true;
                        }
                    }
                }

                if (ins._config.shuttleConfig.respawnConfig.destroyAllShutlesAfterEvent)
                {
                    foreach (SpaceShuttle spaceShuttle in spaceShuttles)
                    {
                        if (spaceShuttle != null)
                        {
                            if (!spaceShuttle.IsAnyMounted())
                                spaceShuttle.KillSubmarine();
                            else
                                spaceShuttle.shoudBeDestroyed = true;
                        }
                    }
                }
            }

            internal static SpaceShuttle SpawnSpaceShuttle(Vector3 position, Quaternion rotation)
            {
                SubmarineDuo submarineDuo = BuildManager.CreateRegularEntity("assets/content/vehicles/submarine/submarineduo.entity.prefab", position + Vector3.up, rotation, enableSaving: false) as SubmarineDuo;
                return AttachSuttleClass(submarineDuo);
            }

            internal static void CheckAllDuoSubmarine()
            {
                foreach (SubmarineDuo submarineDuo in BaseNetworkable.serverEntities.OfType<SubmarineDuo>())
                {
                    if (!submarineDuo.IsExists()) continue;
                    if (submarineDuo.skinID == shuttleSkin || submarineDuo.skinID == activateShuttleSkin) AttachClassToDuoSubmarine(submarineDuo);
                }
            }

            internal static void AttachClassToDuoSubmarine(SubmarineDuo submarineDuo)
            {
                if (spaceShuttles.Any(x => x != null && x.submarineDuo.net.ID == submarineDuo.net.ID)) return;
                submarineDuo.transform.position += Vector3.up;
                AttachSuttleClass(submarineDuo);
            }

            static SpaceShuttle AttachSuttleClass(SubmarineDuo submarineDuo)
            {
                SpaceShuttle controlledSpaceShuttle = submarineDuo.gameObject.AddComponent<SpaceShuttle>();
                controlledSpaceShuttle.Init(submarineDuo);
                spaceShuttles.Add(controlledSpaceShuttle);
                return controlledSpaceShuttle;
            }

            internal static void DestroyAllShuttles()
            {
                foreach (SpaceShuttle controlledSpaceShuttle in spaceShuttles)
                {
                    if (controlledSpaceShuttle != null)
                        controlledSpaceShuttle.DestroyShuttle();
                }
            }

            internal static SpaceShuttle GetSpaceShuttleByMainSubmarineNetId(ulong netId)
            {
                return spaceShuttles.FirstOrDefault(x => x != null && x.submarineDuo != null && x.submarineDuo.net != null && x.submarineDuo.net.ID.Value == netId);
            }

            internal static SpaceShuttle GetSpaceShuttleByDecorSubmarineNetId(ulong netId)
            {
                return spaceShuttles.FirstOrDefault(x => x != null && x.decorSubmarines.Any(y => y.IsExists() && y.net != null && y.net.ID.Value == netId));
            }

            void Init(SubmarineDuo submarineDuo)
            {
                this.submarineDuo = submarineDuo;
                if (submarineDuo.skinID != shuttleSkin && submarineDuo.skinID != activateShuttleSkin) submarineDuo.skinID = shuttleSkin;
                rigidbody = submarineDuo.rigidBody;
                rigidbody.centerOfMass = new Vector3(0, -0.5f, -1.8f);
                CreateDecorSubmarine(new Vector3(-1.535f, 0.141f, -1.796f), new Vector3(0, 0, 270));
                CreateDecorSubmarine(new Vector3(1.535f, 0.141f, -1.796f), new Vector3(0, 0, 90));
                CreateDecorEngine();
                UpdatePilotSeat();
                ins.NextTick(DelayedUpdate);
            }

            internal void AddFuel(int fuelAmount)
            {
                if (fuelAmount <= 0) return;
                EntityFuelSystem entityFuelSystem = submarineDuo.GetFuelSystem() as EntityFuelSystem;
                StorageContainer fuelContainer = entityFuelSystem.GetFuelContainer();
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);
            }

            internal bool IsAnyMounted()
            {
                return submarineDuo.AnyMounted();
            }

            void CreateDecorSubmarine(Vector3 position, Vector3 rotation)
            {
                BaseCombatEntity baseSubmarine = BuildManager.CreateDecorChildCombatEntity<BaseCombatEntity>(submarineDuo, "assets/content/vehicles/submarine/submarinesolo.entity.prefab", position, rotation) as BaseCombatEntity;
                baseSubmarine.SetFlag(BaseEntity.Flags.Busy, true);
                baseSubmarine.skinID = shuttleSkin;
                baseSubmarine.SetMaxHealth(submarineDuo.MaxHealth());
                baseSubmarine.InitializeHealth(submarineDuo.Health(), submarineDuo.MaxHealth());
                decorSubmarines.Add(baseSubmarine);
            }

            void UpdatePilotSeat()
            {
                BaseVehicle.MountPointInfo mountPointInfo = submarineDuo.mountPoints[0];
                BaseMountable newPilotSeat = GameManager.server.CreateEntity("assets/prefabs/vehicle/seats/miniheliseat.prefab", mountPointInfo.mountable.transform.position, mountPointInfo.mountable.transform.rotation) as BaseMountable;
                BuildManager.SetParent(submarineDuo, newPilotSeat, new Vector3(0, 0.075f, 0.95f), Vector3.zero);
                newPilotSeat.maxMountDistance = 3;
                newPilotSeat.Spawn();
                mountPointInfo.mountable.Kill();
                mountPointInfo.mountable = newPilotSeat;
            }

            void CreateDecorEngine()
            {
                BaseEntity entity = BuildManager.CreateChildEntity(submarineDuo, "assets/content/vehicles/modularcar/module_entities/1module_engine.prefab", new Vector3(0, 0.55f, -2.607f), new Vector3(0, 180, 0));
                entity.gameObject.layer = 12;
                decorEntities.Add(entity);
            }

            void DelayedUpdate()
            {
                if (ins._config.shuttleConfig.shuttleMarkerConfig.enable && submarineDuo.skinID == shuttleSkin) CreateMapMarker();
                CheckSpace();
            }

            void CreateMapMarker()
            {
                if (SpaceClass.IsInSpace(submarineDuo.transform.position, true)) return;
                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", transform.position) as VendingMachineMapMarker;
                vendingMarker.enableSaving = false;
                vendingMarker.markerShopName = $"{ins._config.shuttleConfig.shuttleMarkerConfig.name}";
                vendingMarker.Spawn();
            }

            internal void ActivateShuttle(BasePlayer player)
            {
                if (!player.IsRealPlayer()) return;
                if (updateCorountine == null)
                {
                    if (vendingMarker.IsExists()) vendingMarker.Kill();
                    if (submarineDuo.skinID != activateShuttleSkin) submarineDuo.skinID = activateShuttleSkin;
                    submarineDuo.OwnerID = player.OwnerID;
                    if (!submarineDuo.enableSaving)
                    {
                        submarineDuo.enableSaving = true;
                        if (!BaseEntity.saveList.Contains(submarineDuo))
                            BaseEntity.saveList.Add(submarineDuo);
                    }
                    updateCorountine = ServerMgr.Instance.StartCoroutine(UpdateCorountine());
                    driver = submarineDuo.GetDriver();
                    if (ins._config.shuttleConfig.enableCampfires)
                    {
                        CreateDecorCampfire(new Vector3(0.964f, 0.134f, -3.780f), new Vector3(270, 0, 0));
                        CreateDecorCampfire(new Vector3(-0.964f, 0.134f, -3.780f), new Vector3(270, 0, 0));
                    }
                }
            }

            void CreateDecorCampfire(Vector3 position, Vector3 rotation)
            {
                BaseOven baseOven = BuildManager.CreateChildEntity(submarineDuo, "assets/prefabs/misc/halloween/skull_fire_pit/skull_fire_pit.prefab", position, rotation) as BaseOven;
                baseOven.lifestate = BaseCombatEntity.LifeState.Dead;
                baseOven.gameObject.layer = 12;
                baseOven.SetFlag(BaseEntity.Flags.Busy, true);
                baseOven.SetFlag(BaseEntity.Flags.Locked, true);
                baseOven.SetFlag(BaseEntity.Flags.On, true);
                decorCampfires.Add(baseOven);
            }

            internal void DisactivateShuttle()
            {
                if (updateCorountine != null) ServerMgr.Instance.StopCoroutine(updateCorountine);
                updateCorountine = null;
                driver = submarineDuo.GetDriver();
                DestroyDecorCampfires();

                if (SpaceStation.IsPositionInSpaceStation(submarineDuo.transform.position))
                    EnterInAtmosphere();

                if (shoudBeDestroyed)
                    KillSubmarine();
            }

            void DestroyDecorCampfires()
            {
                foreach (BaseEntity entity in decorCampfires) if (entity.IsExists()) entity.Kill();
                decorCampfires.Clear();
            }

            internal void OnDecorSubmarineGetDamage(HitInfo info)
            {
                if (submarineDuo == null) return;
                submarineDuo.Hurt(info);
                AlignHealthes();
            }

            internal void OnMainSubmarineGetDamage()
            {
                AlignHealthes();
            }

            internal void OnDecorSubmarineRepair(BasePlayer player)
            {
                submarineDuo.DoRepair(player);
                AlignHealthes();
            }

            internal bool IsPlayerCanMount(BasePlayer player)
            {
                return submarineDuo.mountPoints.Any(x => x.mountable._mounted == null);
            }

            internal void TryMountPlayer(BasePlayer player)
            {
                if (player.GetMounted() != null && player.GetMounted() is MovableBaseMountable == false) return;
                submarineDuo.AttemptMount(player, false);
                if (player.GetMountedVehicle() != submarineDuo)
                {
                    Astronaut.TryAddComponentToPlayer(player);
                    return;
                }
                Astronaut.TryRemoveComponentFromPlayer(player.userID);
            }

            void AlignHealthes()
            {
                foreach (BaseCombatEntity baseCombatEntity in decorSubmarines)
                {
                    baseCombatEntity.SetHealth(submarineDuo.health);
                }
            }

            IEnumerator UpdateCorountine()
            {
                while (true)
                {
                    CheckSpace();
                    if (submarineDuo.engineController.IsOn)
                    {
                        Control();
                    }
                    yield return CoroutineEx.waitForSeconds(0.2f);
                }
            }

            void CheckSpace()
            {
                if (spaceShuttleParameters == null) EnterInAtmosphere();
                if (rigidbody.useGravity)
                {
                    if (SpaceClass.IsInSpace(submarineDuo.transform.position, false)) EnterInSpace();
                }
                else
                {
                    if (!SpaceClass.IsInSpace(submarineDuo.transform.position, false)) EnterInAtmosphere();
                }
            }

            void EnterInSpace()
            {
                rigidbody.useGravity = false;
                rigidbody.angularDrag = 0.5f;
                spaceShuttleParameters = ins._config.shuttleConfig.spaceParameters;
                UpdateShutteParameters();

                submarineDuo.dismountPositions[0].gameObject.transform.localPosition = new Vector3(0, -1, 2);
                submarineDuo.dismountPositions[1].gameObject.transform.localPosition = new Vector3(0, 1, -2);
                submarineDuo.dismountPositions[3].gameObject.transform.localPosition = new Vector3(-1.5f, 1, 0);
                submarineDuo.dismountPositions[4].gameObject.transform.localPosition = new Vector3(1.5f, -1, 0);

                foreach (var mountPoint in submarineDuo.allMountPoints)
                {
                    if (mountPoint.mountable == null) continue;
                    if (mountPoint.mountable._mounted != null) NotifyManager.SendMessageToPlayer(mountPoint.mountable._mounted, "ShuttleInSpace", ins._config.prefix);
                }
            }

            void EnterInAtmosphere()
            {
                rigidbody.useGravity = true;
                rigidbody.angularDrag = 0.5f;
                spaceShuttleParameters = ins._config.shuttleConfig.atmosphereParameters;
                UpdateShutteParameters();

                submarineDuo.dismountPositions[0].gameObject.transform.localPosition = new Vector3(0, 0, 2);
                submarineDuo.dismountPositions[1].gameObject.transform.localPosition = new Vector3(0, 1.6f, -1.2f);
                submarineDuo.dismountPositions[3].gameObject.transform.localPosition = new Vector3(0, 1.4f, 1.3f);
                submarineDuo.dismountPositions[4].gameObject.transform.localPosition = new Vector3(0, 1.4f, -1.7f);

                foreach (var mountPoint in submarineDuo.allMountPoints)
                {
                    if (mountPoint.mountable == null) continue;
                    if (mountPoint.mountable._mounted != null) NotifyManager.SendMessageToPlayer(mountPoint.mountable._mounted, "ShuttleOutSpace", ins._config.prefix);
                }
            }

            void UpdateShutteParameters()
            {
                submarineDuo.idleFuelPerSec = 0.03f * spaceShuttleParameters.fuelConsumptionScale;
                submarineDuo.maxFuelPerSec = 0.15f * spaceShuttleParameters.fuelConsumptionScale;
                rigidbody.drag = spaceShuttleParameters.drag;
                rigidbody.angularDrag = spaceShuttleParameters.angulardrag;
            }

            void Control()
            {
                if (driver == null) return;
                if (!driver.isMounted) driver = submarineDuo.GetDriver();
                ButtonControl();
                MouseControl();
                AutoHorizont();
            }

            void ButtonControl()
            {
                if (submarineDuo.transform.position.y > SpaceClass.maxSpaceHeight)
                {
                    rigidbody.AddForce(Vector3.down * 120000);
                    return;
                }
                if (driver.serverInput.IsDown(BUTTON.SPRINT)) rigidbody.AddForce(submarineDuo.transform.up.normalized * 120000 * spaceShuttleParameters.shiftForceScale);
                else if (driver.serverInput.IsDown(BUTTON.DUCK)) rigidbody.AddForce(-submarineDuo.transform.up.normalized * 120000 * spaceShuttleParameters.ctrlForceScale);
                if (driver.serverInput.IsDown(BUTTON.FORWARD)) rigidbody.AddForce(submarineDuo.transform.forward.normalized * 200000 * spaceShuttleParameters.wForceScale);
                else if (driver.serverInput.IsDown(BUTTON.BACKWARD)) rigidbody.AddForce(-submarineDuo.transform.forward.normalized * 200000 * spaceShuttleParameters.sForceScale);


                if (ins._config.shuttleConfig.control.mibicoperControl)
                {
                    if (driver.serverInput.IsDown(BUTTON.LEFT)) rigidbody.AddTorque(-submarineDuo.transform.up * 10000 * spaceShuttleParameters.turningADSpeed);
                    if (driver.serverInput.IsDown(BUTTON.RIGHT)) rigidbody.AddTorque(submarineDuo.transform.up * 10000 * spaceShuttleParameters.turningADSpeed);
                }
                else
                {
                    if (driver.serverInput.IsDown(BUTTON.LEFT)) rigidbody.AddTorque(submarineDuo.transform.forward * 10000 * spaceShuttleParameters.turningADSpeed);
                    if (driver.serverInput.IsDown(BUTTON.RIGHT)) rigidbody.AddTorque(-submarineDuo.transform.forward * 10000 * spaceShuttleParameters.turningADSpeed);
                }
            }

            void MouseControl()
            {
                Vector2 driverMouseDelta = GetPlayerDriverDelta();
                rigidbody.AddTorque(ins._config.shuttleConfig.control.invertY ? submarineDuo.transform.right * driverMouseDelta.y * 10000 * spaceShuttleParameters.turningMouseSpeed : submarineDuo.transform.right * driverMouseDelta.y * -10000 * spaceShuttleParameters.turningMouseSpeed);

                if (ins._config.shuttleConfig.control.mibicoperControl)
                {
                    rigidbody.AddTorque(ins._config.shuttleConfig.control.invertX ? submarineDuo.transform.forward * driverMouseDelta.x * 10000 * spaceShuttleParameters.turningMouseSpeed : submarineDuo.transform.forward * driverMouseDelta.x * -10000 * spaceShuttleParameters.turningMouseSpeed);
                }
                else
                {
                    rigidbody.AddTorque(ins._config.shuttleConfig.control.invertX ? submarineDuo.transform.up * driverMouseDelta.x * -10000 * spaceShuttleParameters.turningMouseSpeed : submarineDuo.transform.up * driverMouseDelta.x * 10000 * spaceShuttleParameters.turningMouseSpeed);
                }
            }

            Vector2 GetPlayerDriverDelta()
            {
                float upDown = driver.serverInput.MouseDelta().y;
                if (upDown > 10) upDown = 10;
                if (upDown < -10) upDown = -10;

                float leftRight = driver.serverInput.MouseDelta().x;
                if (leftRight > 10) leftRight = 10;
                if (leftRight < -10) leftRight = -10;

                return new Vector2(leftRight, -upDown);
            }

            void FixedUpdate()
            {
                if (!submarineDuo.engineController.IsOn || driver == null) return;
                rigidbody.angularDrag = 1;
                if (driver.serverInput.IsDown(BUTTON.FIRE_PRIMARY) || driver.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY)) FireTorpedo(driver);
            }

            void FireTorpedo(BasePlayer driver)
            {
                ServerProjectile serverProjectile;
                if (!spaceShuttleParameters.allowTorpedo) return;
                if (submarineDuo.timeSinceTorpedoFired >= submarineDuo.reloadTime && submarineDuo.TryFireProjectile(submarineDuo.GetTorpedoContainer(), AmmoTypes.TORPEDO, submarineDuo.torpedoFiringPoint.position + submarineDuo.torpedoFiringPoint.up + submarineDuo.torpedoFiringPoint.forward * 2, submarineDuo.torpedoFiringPoint.forward * 3, driver, 1f, 0, out serverProjectile))
                {
                    serverProjectile.drag = 0;
                    serverProjectile.swimScale = Vector3.zero;
                    serverProjectile.gravityModifier = -1;
                    SpaceTorpedo.AddComponent(serverProjectile);
                    submarineDuo.timeSinceTorpedoFired = 0f;
                    driver.MarkHostileFor(60f);
                    submarineDuo.ClientRPC(null, "TorpedoFired");
                }
            }

            void DestroyShuttle()
            {
                Destroy(this);
            }

            internal void KillSubmarine()
            {
                if (submarineDuo.IsExists())
                    submarineDuo.Kill();
            }

            void AutoHorizont()
            {
                if (!rigidbody.useGravity) return;
                float angel = Vector3.Angle(Vector3.up, submarineDuo.transform.up);
                rigidbody.AddForceAtPosition(Vector3.up * 0.1f * angel * rigidbody.mass, submarineDuo.transform.position + submarineDuo.transform.up);
                rigidbody.AddForceAtPosition(-Vector3.up * 0.1f * angel * rigidbody.mass, submarineDuo.transform.position - submarineDuo.transform.up);
            }

            void OnDestroy()
            {
                if (updateCorountine != null) ServerMgr.Instance.StopCoroutine(updateCorountine);
                foreach (BaseCombatEntity baseCombatEntity in decorSubmarines) if (baseCombatEntity.IsExists()) baseCombatEntity.Kill();
                foreach (BaseEntity baseEntity in decorEntities) if (baseEntity.IsExists()) baseEntity.Kill();
                DestroyDecorCampfires();
                if (vendingMarker.IsExists()) vendingMarker.Kill();
            }

            sealed class SpaceTorpedo : FacepunchBehaviour
            {
                static HashSet<SpaceTorpedo> spaceTorpedos = new HashSet<SpaceTorpedo>();
                ServerProjectile torpedo;
                float initialYDirection;
                Vector3 startPosition;

                internal static void AddComponent(ServerProjectile torpedo)
                {
                    SpaceTorpedo spaceTorpedo = torpedo.gameObject.AddComponent<SpaceTorpedo>();
                    spaceTorpedo.torpedo = torpedo;
                    spaceTorpedo.initialYDirection = torpedo.CurrentVelocity.y;
                    spaceTorpedo.startPosition = torpedo.transform.position;
                    spaceTorpedos.Add(spaceTorpedo);
                }

                void FixedUpdate()
                {
                    if (!CheckMaxDistance())
                    {
                        torpedo.baseEntity.Kill();
                        return;
                    }

                    torpedo.CurrentVelocity = new Vector3(torpedo.CurrentVelocity.x, initialYDirection, torpedo.CurrentVelocity.z);
                }

                bool CheckMaxDistance()
                {
                    return Vector3.Distance(startPosition, torpedo.transform.position) < ins._config.shuttleConfig.maxTorpedoDistance;
                }
            }
        }

        class ZeroGravity : FacepunchBehaviour
        {
            protected MovableBaseMountable movableBaseMountable;
            protected MovableDroppedItem movableDroppedItem;
            protected CapsuleCollider capsuleCollider;
            protected Rigidbody rigidbody;
            protected BasePlayer player;

            protected void Init(BasePlayer player)
            {
                this.player = player;
                BuildEntities();
                GetAndUpdateRigidBody();
                MountPlayer();
            }

            protected void BuildEntities()
            {
                Vector3 rotation = player.eyes.GetLookRotation().eulerAngles;
                movableDroppedItem = MovableDroppedItem.CreateMovableDroppedItem(player.transform.position + new Vector3(0, 1.3f, 0), Quaternion.Euler(rotation.x, rotation.y, rotation.z));
                movableBaseMountable = MovableBaseMountable.CreateMovableBaseMountable(movableDroppedItem);
                CreateSphere();
            }

            protected void CreateSphere()
            {
                capsuleCollider = movableDroppedItem.gameObject.AddComponent<CapsuleCollider>();
                capsuleCollider.gameObject.layer = 12;
                capsuleCollider.radius = 0.55f;
                capsuleCollider.center = new Vector3(0, -0.1f, 0);
                capsuleCollider.height = 1.8f;
            }

            protected void GetAndUpdateRigidBody()
            {
                rigidbody = movableDroppedItem.GetComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.mass = 100;
            }

            protected void MountPlayer()
            {
                movableBaseMountable.MountPlayer(player);
            }

            protected void AddForce(Vector3 direction, float amount)
            {
                rigidbody.AddForce(direction * amount * rigidbody.mass, ForceMode.Force);
            }

            protected void AddTorgue(Vector3 axis, float amount)
            {
                rigidbody.AddTorque(axis * amount / 50, ForceMode.VelocityChange);
            }

            internal virtual void DestroyZeroGravity()
            {
                if (movableBaseMountable.IsExists()) movableBaseMountable.Kill();
                if (movableDroppedItem.IsExists()) movableDroppedItem.Kill();
                UnityEngine.GameObject.Destroy(this.gameObject);
            }
        }

        static class PveModeController
        {
            static BasePlayer eventOwner;

            internal static void SetPveModeOwner(BasePlayer newEventOwner)
            {
                eventOwner = newEventOwner;
                NotifyManager.SendMessageToAll("PveMode_NewOwner", ins._config.prefix, newEventOwner.displayName);
            }

            internal static void ClearPveModeOwner()
            {
                eventOwner = null;
            }

            internal static bool IsPveModeBlockAction(BasePlayer player)
            {
                if (!ins._config.supportedPluginsConfig.pveMode.enable)
                    return false;

                if (eventOwner == null)
                    return false;

                if (player.userID == eventOwner.userID)
                    return false;

                if (IsTeam(player, eventOwner.userID))
                    return false;

                return true;
            }

            internal static BasePlayer GetEventOwner()
            {
                return eventOwner;
            }

            static bool IsTeam(BasePlayer player, ulong targetId)
            {
                if (player == null || targetId == 0)
                    return false;

                if (player.currentTeam != 0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (playerTeam != null && playerTeam.members.Contains(targetId))
                        return true;
                }

                if (ins.plugins.Exists("Friends") && (bool)ins.Friends.Call("AreFriends", (ulong)player.userID, targetId))
                    return true;

                if (ins.plugins.Exists("Clans") && ins.Clans.Author == "k1lly0u" && (bool)ins.Clans.Call("IsMemberOrAlly", player.UserIDString, targetId.ToString()))
                    return true;

                return false;
            }
        }

        sealed class DecorSubmarineDuo : BaseCombatEntity, SamSite.ISamSiteTarget
        {
            public override float MaxVelocity()
            {
                return 100;
            }

            public SamSite.SamTargetType SAMTargetType => ins._config.shuttleConfig.samSiteConfig.bigRangeAndSpeed ? SamSite.targetTypeMissile : SamSite.targetTypeVehicle;

            public bool IsValidSAMTarget(bool staticRespawn) => ins._config.shuttleConfig.samSiteConfig.enable;
        }

        sealed class MovableBaseMountable : BaseMountable
        {
            Astronaut Astronaut;

            internal static MovableBaseMountable CreateMovableBaseMountable(BaseEntity parentEntity, string seatPrefab = "assets/prefabs/vehicle/seats/testseat.prefab")
            {
                BaseMountable baseMountable = GameManager.server.CreateEntity(seatPrefab, parentEntity.transform.position) as BaseMountable;
                baseMountable.enableSaving = false;
                baseMountable.skinID = 45124514;
                MovableBaseMountable movableBaseMountable = baseMountable.gameObject.AddComponent<MovableBaseMountable>();
                BuildManager.CopySerializableFields(baseMountable, movableBaseMountable);

                baseMountable.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(baseMountable, true);
                BuildManager.SetParent(parentEntity, movableBaseMountable, new Vector3(0, -1f, 0), Vector3.zero);
                movableBaseMountable.Spawn();
                return movableBaseMountable;
            }

            internal void InitAstronaut(Astronaut Astronaut)
            {
                this.Astronaut = Astronaut;
            }

            public override bool BlocksWaterFor(BasePlayer player)
            {
                return Astronaut != null && !Astronaut.astronautParameters.canBreathe;
            }

            public override float AirFactor()
            {
                return 0;
            }

            public override void DismountAllPlayers()
            {

            }

            public override bool GetDismountPosition(BasePlayer player, out Vector3 res, bool checkPlayerLos = true)
            {
                res = player.transform.position;
                return true;
            }
        }

        sealed class MovableDroppedItem : DroppedItem
        {
            internal static MovableDroppedItem CreateMovableDroppedItem(Vector3 position, Quaternion rotation, bool hide = true)
            {
                DroppedItem droppedItem = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", position, rotation) as DroppedItem;
                droppedItem.enableSaving = false;
                droppedItem.allowPickup = false;
                droppedItem.item = ItemManager.CreateByName("weapon.mod.muzzleboost");

                if (hide) droppedItem.SetFlag(BaseEntity.Flags.Disabled, true);

                MovableDroppedItem movableDroppedItem = droppedItem.gameObject.AddComponent<MovableDroppedItem>();
                BuildManager.CopySerializableFields(droppedItem, movableDroppedItem);
                droppedItem.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(droppedItem, true);
                movableDroppedItem.Spawn();
                return movableDroppedItem;
            }

            public override float MaxVelocity()
            {
                return 100;
            }

            public override float GetDespawnDuration()
            {
                return float.MaxValue;
            }
        }

        static class BuildManager
        {
            internal static BaseEntity CreateRegularEntity(string prefabName, Vector3 position, Quaternion rotation, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, enableSaving);
                entity.Spawn();
                return entity;
            }

            internal static BaseEntity CreateStaticEntity(string prefabName, Vector3 position, Quaternion rotation, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, enableSaving);
                DestroyUnnessesaryComponents(entity);
                entity.Spawn();
                StabilityEntity stabilityEntity = entity as StabilityEntity;
                if (stabilityEntity != null) stabilityEntity.grounded = true;
                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                if (baseCombatEntity != null) baseCombatEntity.pickup.enabled = false;
                return entity;
            }

            internal static BaseEntity CreateChildEntity(BaseEntity parrentEntity, string prefabName, Vector3 localPosition, Vector3 localRotation)
            {
                BaseEntity entity = CreateEntity(prefabName, parrentEntity.transform.position, Quaternion.identity);
                SetParent(parrentEntity, entity, localPosition, localRotation);
                DestroyUnnessesaryComponents(entity);
                entity.Spawn();
                return entity;
            }

            internal static void SetParent(BaseEntity parrentEntity, BaseEntity childEntity, Vector3 localPosition, Vector3 localRotation)
            {
                childEntity.SetParent(parrentEntity, true, false);
                childEntity.transform.localPosition = localPosition;
                childEntity.transform.localEulerAngles = localRotation;
            }

            internal static BaseEntity CreateRegularEntityInChildTransform(DataFileEntity entData, Transform parentTransform, ulong skinID = 0)
            {
                BaseEntity entity = CreateEntity(entData.prefab, GetGlobalPosition(parentTransform, entData.pos.ToVector3()), GetGlobalRotation(parentTransform, entData.rot.ToVector3()));
                if (entity == null) return null;
                entity.skinID = skinID;

                DestroyUnnessesaryComponents(entity);
                entity.Spawn();

                StabilityEntity stabilityEntity = entity as StabilityEntity;
                if (stabilityEntity != null) stabilityEntity.grounded = true;
                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                if (baseCombatEntity != null) baseCombatEntity.pickup.enabled = false;

                return entity;
            }

            internal static BaseEntity CreateDecorEntityInChildTransform(DataFileEntity entData, Transform parentTransform, ulong skinID = 0)
            {
                BaseEntity entity = CreateEntity(entData.prefab, GetGlobalPosition(parentTransform, entData.pos.ToVector3()), GetGlobalRotation(parentTransform, entData.rot.ToVector3()));
                if (entity == null) return null;
                entity.skinID = skinID;

                BaseEntity newEntity = entity.gameObject.AddComponent<BaseEntity>();
                CopySerializableFields(entity, newEntity);
                UnityEngine.GameObject.DestroyImmediate(entity, true);
                entity = newEntity;

                DestroyUnnessesaryComponents(entity);
                entity.Spawn();
                return entity;
            }

            internal static BaseCombatEntity CreateDecorChildCombatEntity<T>(BaseEntity parrentEntity, string prefabName, Vector3 localPosition, Vector3 localRotation)
            {
                BaseCombatEntity dynamicEntity = CreateEntity(prefabName, parrentEntity.transform.position, Quaternion.identity) as BaseCombatEntity;

                DecorSubmarineDuo decorEntity = dynamicEntity.gameObject.AddComponent<DecorSubmarineDuo>();
                CopySerializableFields(dynamicEntity, decorEntity);
                dynamicEntity.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(dynamicEntity, true);
                SetParent(parrentEntity, decorEntity, localPosition, localRotation);

                DestroyUnnessesaryComponents(decorEntity);
                decorEntity.Spawn();
                return decorEntity;
            }

            internal static SphereCollider CreateSphereCollider(GameObject gameObject, float radius, Vector3 position)
            {
                SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = radius;
                sphereCollider.transform.position = position;
                return sphereCollider;
            }

            static BaseEntity CreateEntity(string prefabName, Vector3 position, Quaternion rotation, bool enableSaving = false)
            {
                BaseEntity entity = GameManager.server.CreateEntity(prefabName, position, rotation);
                entity.enableSaving = enableSaving;

                BradleyAPC bradleyAPC = entity as BradleyAPC;

                if (bradleyAPC != null)
                    bradleyAPC.ScientistSpawnCount = 0;

                return entity;
            }

            internal static Vector3 GetGlobalPosition(Transform parentTransform, Vector3 position)
            {
                return parentTransform.transform.TransformPoint(position);
            }

            internal static Quaternion GetGlobalRotation(Transform parentTransform, Vector3 rotation)
            {
                return parentTransform.rotation * Quaternion.Euler(rotation);
            }

            internal static void DestroyEntityConponent<T>(BaseEntity entity)
            {
                T component = entity.GetComponent<T>();
                if (component != null) UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
            }

            static void DestroyUnnessesaryComponents(BaseEntity entity)
            {
                DestroyEntityConponent<Rigidbody>(entity);
                DestroyEntityConponent<GroundWatch>(entity);
                DestroyEntityConponent<DestroyOnGroundMissing>(entity);
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

        static class LocationDefiner
        {
            internal static string PositionToGridCoord(Vector3 position)
            {
                // Convert world position to grid coordinates
                // The grid is based on the world size and typically uses letters for X and numbers for Z
                float worldSize = TerrainMeta.Size.x;
                float gridSize = worldSize / 26f; // 26 letters in alphabet
                
                int x = Mathf.FloorToInt((position.x + worldSize/2f) / gridSize);
                int z = Mathf.FloorToInt((position.z + worldSize/2f) / gridSize);
                
                char letter = (char)('A' + x);
                return $"{letter}{z}";
            }

            internal static Vector3 GetEventPosition()
            {
                Vector3 position = Vector3.zero;
                if (ins._config.mainConfig.useCustomCoords)
                {
                    position = ins._config.mainConfig.customSpawnPoints.GetRandom().ToVector3();
                }

                if (position == Vector3.zero)
                {
                    if (ins._config.mainConfig.onlyOceanSpawn)
                    {
                        float randomx = UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2);
                        float randomz = UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2);

                        if (Math.Abs(randomx) > Math.Abs(randomz))
                        {
                            if (randomx > 0) randomx = TerrainMeta.Size.x / 2;
                            else randomx = -TerrainMeta.Size.x / 2;
                        }
                        else
                        {
                            if (randomz > 0) randomz = TerrainMeta.Size.x / 2;
                            else randomz = -TerrainMeta.Size.x / 2;
                        }
                        position = new Vector3(randomx, ins._config.mainConfig.spaceHeight, randomz);
                    }
                    else
                        position = new Vector3(UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2), ins._config.mainConfig.spaceHeight, UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2));
                }
                return position;
            }

            internal static Vector3 GetRandomSpacePoint(float radius = 5)
            {
                Vector3 position = Vector3.zero;
                int counter = 0;
                Vector3 spacePosition = SpaceClass.GetSpaceTransform().position;
                while (position == Vector3.zero && counter < 100)
                {
                    float randomRadius = UnityEngine.Random.Range(0, ins._config.mainConfig.spaceRadius);
                    float randomRadian = (2f * Mathf.PI / 360) * UnityEngine.Random.Range(0, 360);
                    Vector3 checkPosition = spacePosition + new Vector3(randomRadius * Mathf.Cos(randomRadian), UnityEngine.Random.Range(-150, 150), randomRadius * Mathf.Sin(randomRadian));
                    if (CheckSpacePosition(checkPosition, radius))
                    {
                        position = checkPosition;
                        break;
                    }
                    counter++;
                }

                return position;
            }

            static bool CheckSpacePosition(Vector3 position, float checkingRadius)
            {
                if (UnityEngine.Physics.OverlapSphere(position, checkingRadius).Any(x => x.name != "New Game Object") || Vector3.Distance(SpaceClass.GetSpaceTransform().position, position) > ins._config.mainConfig.spaceRadius) return false;
                return true;
            }

            internal static Quaternion GetRandomRotation()
            {
                return Quaternion.Euler(UnityEngine.Random.Range(0.0f, 360.0f), UnityEngine.Random.Range(0.0f, 360.0f), UnityEngine.Random.Range(0.0f, 360.0f));
            }
        }

        static class NpcManager
        {
            internal static bool CheckNPCSpawn()
            {
                if (!ins.plugins.Exists("NpcSpawn"))
                {
                    ins.PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt. NPCs will not spawn!");
                    return false;
                }
                else return true;
            }

            internal static ScientistNPC CreateStationScientistNpc(NpcConfig baseNpcConfig, Vector3 position)
            {
                JObject baseNpcConfigObj = GetBaseNpcConfig(baseNpcConfig);
                ScientistNPC scientistNPC = (ScientistNPC)ins.NpcSpawn.Call("SpawnNpc", position, baseNpcConfigObj);
                scientistNPC.transform.Rotate(Vector3.up, 180);
                return scientistNPC;
            }

            static JObject GetBaseNpcConfig(NpcConfig config)
            {
                HashSet<string> states = new HashSet<string> { "IdleState", "CombatStationaryState" };
                if (config.beltItems.Any(x => x.shortName == "rocket.launcher" || x.shortName == "explosive.timed")) states.Add("RaidState");
                return new JObject
                {
                    ["Name"] = config.name,
                    ["WearItems"] = new JArray
                    {
                        config.wearItems.Select(x => new JObject
                        {
                            ["ShortName"] = x.shortName,
                            ["SkinID"] = x.skinID
                        })
                    },
                    ["BeltItems"] = new JArray { config.beltItems.Select(x => new JObject { ["ShortName"] = x.shortName, ["Amount"] = x.amount, ["SkinID"] = x.skinID, ["Mods"] = new JArray { x.Mods.ToHashSet() }, ["Ammo"] = x.ammo }) },
                    ["Kit"] = config.kit,
                    ["Health"] = config.health,
                    ["RoamRange"] = 0,
                    ["ChaseRange"] = config.chaseRange,
                    ["SenseRange"] = config.senseRange,
                    ["ListenRange"] = config.senseRange / 2f,
                    ["AttackRangeMultiplier"] = config.attackRangeMultiplier,
                    ["VisionCone"] = config.visionCone,
                    ["DamageScale"] = config.damageScale,
                    ["TurretDamageScale"] = 1f,
                    ["AimConeScale"] = config.aimConeScale,
                    ["DisableRadio"] = config.disableRadio,
                    ["CanRunAwayWater"] = true,
                    ["CanSleep"] = false,
                    ["Speed"] = config.speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = config.memoryDuration,
                    ["States"] = new JArray { states }
                };
            }
        }

        static class LootManager
        {
            static HashSet<LootContainer> crates = new HashSet<LootContainer>();
            internal static HashSet<ulong> openedCrates = new HashSet<ulong>();

            internal static OwnLootTableConfig GetUpdatedLootTable(OwnLootTableConfig ownLootTable)
            {
                ownLootTable.items = ownLootTable.items.OrderBy(x => x.chance);
                if (ownLootTable.maxItemsAmount > ownLootTable.items.Where(x => x.chance > 1).Count) ownLootTable.maxItemsAmount = ownLootTable.items.Count;
                if (ownLootTable.minItemsAmount > ownLootTable.maxItemsAmount) ownLootTable.minItemsAmount = ownLootTable.maxItemsAmount;
                return ownLootTable;
            }

            internal static BaseEntity CreateCrate(string prefab, Vector3 position, Quaternion rotation)
            {
                BaseEntity entity = BuildManager.CreateStaticEntity(prefab, position, rotation);

                LootContainer lootContainer = entity as LootContainer;
                if (lootContainer != null)
                {
                    crates.Add(lootContainer);
                    HackableLockedCrate hackableLockedCrate = lootContainer as HackableLockedCrate;
                    if (hackableLockedCrate != null)
                    {
                        CrateConfig crateConfig = ins._config.crates.FirstOrDefault(x => x.prefab == lootContainer.PrefabName);
                        hackableLockedCrate.DestroyShared();
                        hackableLockedCrate.shouldDecay = false;
                        hackableLockedCrate.decayTimer = float.MaxValue;
                        hackableLockedCrate.SendNetworkUpdate();
                        hackableLockedCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - crateConfig.crateUnlockTime;
                    }
                    ins.NextTick(() => UpdateLootContainer(lootContainer));
                    return entity;
                }

                Locker locker = entity as Locker;
                if (locker != null)
                {
                    ins.NextTick(() => UpdateLockerContainer(locker));
                    return entity;
                }

                BoxStorage boxStorage = entity as BoxStorage;
                if (boxStorage != null)
                {
                    ins.NextTick(() => UpdateBoxStorage(boxStorage));
                    return entity;
                }

                return entity;
            }

            internal static void TrySpawnItemInDefaultCrate(LootContainer lootContatiner, ItemConfig itemConfig, float chance, int removeItemIndex = 0)
            {
                ins.NextTick(() =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= chance)
                    {
                        Item item = LootManager.CreateItem(itemConfig, 1);
                        if (lootContatiner.inventory.itemList.Count > removeItemIndex)
                        {
                            Item removeItem = lootContatiner.inventory.itemList[removeItemIndex];
                            if (removeItem != null) lootContatiner.inventory.Remove(removeItem);
                        }
                        if (!item.MoveToContainer(lootContatiner.inventory)) item.Remove();
                    }
                });
            }

            internal static int GetCountUnlootedCrates()
            {
                return crates.Where(x => x.IsExists() && x.net != null && !openedCrates.Contains(x.net.ID.Value)).Count;
            }

            static void UpdateLootContainer(LootContainer lootContainer)
            {
                if (lootContainer == null) return;
                CrateConfig crateConfig = ins._config.crates.FirstOrDefault(x => x.prefab == lootContainer.PrefabName);
                if (crateConfig != null) UpdateLootContainerLootTable(lootContainer.inventory, crateConfig.lootTable, crateConfig.typeLootTable);
            }

            static void UpdateLockerContainer(Locker locker)
            {
                if (locker == null) return;
                StorageCrateConfig storageCrateConfig = ins._config.storageCrates.FirstOrDefault(x => x.prefab == locker.PrefabName);
                if (storageCrateConfig == null) return;

                int countLootInContainerWear = 0;
                int countLootInContainerBelt = 0;
                int countLoot = UnityEngine.Random.Range(storageCrateConfig.lootTable.minItemsAmount, storageCrateConfig.lootTable.maxItemsAmount + 1);
                if (countLoot > storageCrateConfig.lootTable.items.Where(x => x.chance > 0).Count) countLoot = storageCrateConfig.lootTable.items.Count;

                while (countLootInContainerWear + countLootInContainerBelt < countLoot && countLootInContainerBelt < 18 && countLootInContainerWear < 21)
                {
                    foreach (LootItemConfig item in storageCrateConfig.lootTable.items)
                    {
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.chance)
                        {
                            int amount = UnityEngine.Random.Range(item.minAmount, item.maxAmount + 1);
                            Item newItem = item.isBlueprint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.shortname, amount, item.skin);
                            if (item.isBlueprint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.shortname).itemid;
                            if (item.name != "") newItem.name = item.name;
                            if (newItem.info.category == ItemCategory.Attire && countLootInContainerWear < 21)
                            {
                                int targetPos = countLootInContainerWear;
                                if (targetPos > 6 && targetPos < 13) targetPos = 13 + countLootInContainerWear - 7;
                                else if (targetPos > 19 && targetPos < 26) targetPos = 26 + countLootInContainerWear - 14;
                                if (newItem.MoveToContainer(locker.inventory, targetPos)) countLootInContainerWear++;
                                else newItem.Remove();
                            }
                            else if (countLootInContainerBelt < 18)
                            {
                                int targetPos = countLootInContainerBelt + 7;
                                if (targetPos > 12 && targetPos < 20) targetPos = 20 + countLootInContainerWear - 6;
                                else if (targetPos > 25 && targetPos < 33) targetPos = 33 + countLootInContainerWear - 12;
                                if (newItem.MoveToContainer(locker.inventory, targetPos)) countLootInContainerBelt++;
                                else newItem.Remove();
                            }
                            if (countLootInContainerWear + countLootInContainerBelt >= countLoot) return;
                        }
                    }
                }
            }

            static void UpdateBoxStorage(BoxStorage boxStorage)
            {
                if (boxStorage == null) return;
                StorageCrateConfig storageCrateConfig = ins._config.storageCrates.FirstOrDefault(x => x.prefab == boxStorage.PrefabName);
                if (storageCrateConfig != null) UpdateLootContainerLootTable(boxStorage.inventory, storageCrateConfig.lootTable, 1);
                boxStorage.dropsLoot = false;
            }

            internal static void UpdateLootContainerLootTable(ItemContainer itemContainer, OwnLootTableConfig lootTableConfig, int typeOfLootTable)
            {
                if (typeOfLootTable != 1 && typeOfLootTable != 4) return;
                if (typeOfLootTable == 1) itemContainer.ClearItemsContainer();
                int countLootInContainer = 0;
                int countLoot = UnityEngine.Random.Range(lootTableConfig.minItemsAmount, lootTableConfig.maxItemsAmount);
                if (countLoot > lootTableConfig.items.Where(x => x.chance > 0).Count) countLoot = lootTableConfig.items.Count;
                if (typeOfLootTable == 4) itemContainer.capacity += countLoot;
                else itemContainer.capacity = countLoot;
                while (countLootInContainer < countLoot)
                {
                    HashSet<LootItemConfig> suitableItems = lootTableConfig.items.Where(y => !itemContainer.itemList.Any(x => x.info.shortname == y.shortname));
                    if (suitableItems == null || suitableItems.Count == 0) return;

                    foreach (LootItemConfig item in suitableItems)
                    {
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.chance)
                        {
                            int amount = UnityEngine.Random.Range(item.minAmount, item.maxAmount);
                            Item newItem = item.isBlueprint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.shortname, amount, item.skin);
                            if (item.isBlueprint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.shortname).itemid;
                            if (item.name != "") newItem.name = item.name;
                            if (!newItem.MoveToContainer(itemContainer))
                            {
                                newItem.Remove();
                                return;
                            }
                            else
                            {
                                if (countLootInContainer >= countLoot) return;
                                countLootInContainer++;
                            }
                        }
                    }
                }
                itemContainer.capacity = itemContainer.itemList.Count;
            }

            internal static void DestroyEventCrates()
            {
                foreach (LootContainer lootContainer in crates)
                {
                    if (lootContainer.IsExists()) lootContainer.Kill();
                }
                crates.Clear();
                openedCrates.Clear();
            }

            internal static Item CreateItem(ItemConfig itemConfig, int amount)
            {
                Item item = ItemManager.CreateByName(itemConfig.shortname, amount, itemConfig.skin);
                if (itemConfig.name != "") item.name = itemConfig.name;
                return item;
            }

            internal static void GiveItemToPLayer(BasePlayer player, Item item)
            {
                int spaceCountItem = PLayerInventory.GetSpaceCountItem(player, item.info.shortname, item.MaxStackable(), item.skin);
                int inventoryItemCount;
                if (spaceCountItem > item.amount) inventoryItemCount = item.amount;
                else inventoryItemCount = spaceCountItem;

                if (inventoryItemCount > 0)
                {
                    Item itemInventory = ItemManager.CreateByName(item.info.shortname, inventoryItemCount, item.skin);
                    if (item.skin != 0) itemInventory.name = item.name;

                    item.amount -= inventoryItemCount;
                    PLayerInventory.MoveInventoryItem(player, itemInventory);
                }

                if (item.amount > 0) PLayerInventory.DropExtraItem(player, item);
            }

            internal static bool IsEventCrate(ulong netId)
            {
                return crates.Any(x => x.IsExists() && x.net != null && x.net.ID.Value == netId);
            }

            internal static void OnEventCrateLooted(LootContainer lootContainer, BasePlayer player)
            {
                if (openedCrates.Contains(lootContainer.net.ID.Value))
                    return;
                else
                {
                    openedCrates.Add(lootContainer.net.ID.Value);
                    EconomyManager.ActionEconomy(player.userID, "Crates", lootContainer.PrefabName);
                    if (ins._config.mainConfig.timeAfterLootingAllLockedCrates == 0)
                        return;
                    HackableLockedCrate hackableLockedCrate = lootContainer as HackableLockedCrate;
                    if (hackableLockedCrate != null)
                    {
                        if (!crates.Any(x => x.IsExists() && x is HackableLockedCrate && !openedCrates.Contains(x.net.ID.Value)))
                        {
                            EventManager.ChangeEventTime(ins._config.mainConfig.timeAfterLootingAllLockedCrates);
                        }
                    }
                }
            }

            static class PLayerInventory
            {
                internal static int GetSpaceCountItem(BasePlayer player, string shortname, int stack, ulong skinID)
                {
                    int slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
                    int taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
                    int result = (slots - taken) * stack;

                    List<Item> allItems = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(allItems);

                    foreach (Item item in allItems) 
                        if (item.info.shortname == shortname && item.skin == skinID && item.amount < stack) 
                            result += stack - item.amount;

                    Pool.FreeUnmanaged(ref allItems);

                    return result;
                }

                internal static void MoveInventoryItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable())
                    {
                        List<Item> allItems = Pool.Get<List<Item>>();
                        player.inventory.GetAllItems(allItems);

                        foreach (Item itemInv in allItems)
                        {
                            if (itemInv.info.shortname == item.info.shortname && itemInv.skin == item.skin && itemInv.amount < itemInv.MaxStackable())
                            {
                                if (itemInv.amount + item.amount <= itemInv.MaxStackable())
                                {
                                    itemInv.amount += item.amount;
                                    itemInv.MarkDirty();
                                    Pool.FreeUnmanaged(ref allItems);
                                    return;
                                }
                                else
                                {
                                    item.amount -= itemInv.MaxStackable() - itemInv.amount;
                                    itemInv.amount = itemInv.MaxStackable();
                                }
                            }
                        }

                        Pool.FreeUnmanaged(ref allItems);

                        if (item.amount > 0) 
                            player.inventory.GiveItem(item);
                    }
                    else
                    {
                        while (item.amount > item.MaxStackable())
                        {
                            Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                            if (item.skin != 0) thisItem.name = item.name;
                            player.inventory.GiveItem(thisItem);
                            item.amount -= item.MaxStackable();
                        }
                        if (item.amount > 0) player.inventory.GiveItem(item);
                    }
                }

                internal static void DropExtraItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable()) item.Drop(player.transform.position, Vector3.up);
                    else
                    {
                        while (item.amount > item.MaxStackable())
                        {
                            Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                            if (item.skin != 0) thisItem.name = item.name;
                            thisItem.Drop(player.transform.position, Vector3.up);
                            item.amount -= item.MaxStackable();
                        }
                        if (item.amount > 0) item.Drop(player.transform.position, Vector3.up);
                    }
                }
            }
        }

        static class EconomyManager
        {
            static readonly Dictionary<ulong, double> _playersBalance = new Dictionary<ulong, double>();

            internal static void ActionEconomy(ulong playerId, string type, string arg = "")
            {
                switch (type)
                {
                    case "Crates":
                        double economyCrateData;
                        if (ins._config.supportedPluginsConfig.economy.crates.TryGetValue(arg, out economyCrateData))
                            AddBalance(playerId, economyCrateData);
                        break;
                    case "Npc":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.npcPoint);
                        break;
                    case "Turret":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.turretPoint);
                        break;
                    case "FlameTurret":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.flameTurretPoint);
                        break;
                    case "Bradley":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.bradleyPoint);
                        break;
                    case "Door":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.doorPoint);
                        break;
                    case "RedCard":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.redCardPoint);
                        break;
                    case "BlueCard":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.blueCardPoint);
                        break;
                    case "GreenCard":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.greenCardPoint);
                        break;
                    case "SpaceCard":
                        AddBalance(playerId, ins._config.supportedPluginsConfig.economy.spaceCardPoint);
                        break;
                }
            }

            static void AddBalance(ulong playerId, double balance)
            {
                if (balance == 0) return;
                if (_playersBalance.ContainsKey(playerId)) _playersBalance[playerId] += balance;
                else _playersBalance.Add(playerId, balance);
            }

            internal static void SendBalance()
            {
                DefineEventWinner();

                if (!ins._config.supportedPluginsConfig.economy.enable || _playersBalance.Count == 0)
                {
                    _playersBalance.Clear();
                    return;
                }
                foreach (KeyValuePair<ulong, double> dic in _playersBalance)
                {
                    if (dic.Value < ins._config.supportedPluginsConfig.economy.minEconomyPiont) continue;
                    int intCount = Convert.ToInt32(dic.Value);
                    if (ins._config.supportedPluginsConfig.economy.plugins.Contains("Economics") && ins.plugins.Exists("Economics") && dic.Value > 0) ins.Economics.Call("Deposit", dic.Key.ToString(), dic.Value);
                    if (ins._config.supportedPluginsConfig.economy.plugins.Contains("Server Rewards") && ins.plugins.Exists("ServerRewards") && intCount > 0) ins.ServerRewards.Call("AddPoints", dic.Key, intCount);
                    if (ins._config.supportedPluginsConfig.economy.plugins.Contains("IQEconomic") && ins.plugins.Exists("IQEconomic") && intCount > 0) ins.IQEconomic.Call("API_SET_BALANCE", dic.Key, intCount);
                    BasePlayer player = BasePlayer.FindByID(dic.Key);
                    if (player != null) NotifyManager.SendMessageToPlayer(player, "SendEconomy", ins._config.prefix, dic.Value);
                }

                double max = 0;
                ulong winnerId = 0;
                foreach (var a in _playersBalance)
                {
                    if (a.Value > max)
                    {
                        max = a.Value;
                        winnerId = a.Key;
                    }
                }

                if (max >= ins._config.supportedPluginsConfig.economy.minCommandPoint) foreach (string command in ins._config.supportedPluginsConfig.economy.commands) ins.Server.Command(command.Replace("{steamid}", $"{winnerId}"));
                _playersBalance.Clear();
            }

            static void DefineEventWinner()
            {
                var winnerPair = _playersBalance.Max(x => (float)x.Value);

                if (winnerPair.Value > 0)
                    Interface.CallHook("OnSpaceEventWin", winnerPair.Key);
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

                string playerMessage = GetMessage(langKey, player.UserIDString, args);

                if (ins._config.notifyConfig.isChatEnable || langKey == "EventDescription")
                    ins.PrintToChat(player, playerMessage);

                if (ins._config.notifyConfig.gameTipConfig.isEnabled && langKey != "EventDescription")
                    player.SendConsoleCommand("gametip.showtoast", ins._config.notifyConfig.gameTipConfig.style, ClearColorAndSize(playerMessage), string.Empty);

                if (ins._config.supportedPluginsConfig.guiAnnouncementsConfig.isEnabled && ins.plugins.Exists("guiAnnouncementsConfig"))
                    ins.GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(playerMessage), ins._config.supportedPluginsConfig.guiAnnouncementsConfig.bannerColor, ins._config.supportedPluginsConfig.guiAnnouncementsConfig.textColor, player, ins._config.supportedPluginsConfig.guiAnnouncementsConfig.apiAdjustVPosition);

                if (ins._config.supportedPluginsConfig.notifyPluginConfig.isEnabled && ins.plugins.Exists("Notify"))
                    ins.Notify?.Call("SendNotify", player, ins._config.supportedPluginsConfig.notifyPluginConfig.type, ClearColorAndSize(playerMessage));
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
                    ins.DiscordMessages?.Call("API_SendFancyMessage", ins._config.supportedPluginsConfig.discordMessagesConfig.webhookUrl, "", ins._config.supportedPluginsConfig.discordMessagesConfig.embedColor, JsonConvert.SerializeObject(fields), null, ins);
                }
            }

            static bool CanSendDiscordMessage(string langKey)
            {
                return ins._config.supportedPluginsConfig.discordMessagesConfig.keys.Contains(langKey) && ins._config.supportedPluginsConfig.discordMessagesConfig.isEnabled && !string.IsNullOrEmpty(ins._config.supportedPluginsConfig.discordMessagesConfig.webhookUrl) && ins._config.supportedPluginsConfig.discordMessagesConfig.webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
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
                new ImageInfo("Crates_Adem")
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
                CuiHelper.DestroyUi(player, "Tabs_Adem");
                CuiElementContainer container = new CuiElementContainer();
                float halfWidth = tabWidth / 2 + tabWidth / 2 * (iconImageInfos.Count - 1);

                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{-halfWidth} {ins._config.notifyConfig.guiConfig.offsetMinY}", OffsetMax = $"{halfWidth} {ins._config.notifyConfig.guiConfig.offsetMinY + tabHeigth}" },
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

        public class DataFileEntity
        {
            public string prefab;
            public string pos;
            public string rot;
            public ulong skin;

            public DataFileEntity(string prefab, string position, string rotation, ulong skin = 0)
            {
                this.prefab = prefab;
                this.pos = position;
                this.rot = rotation;
                this.skin = skin;
            }
        }
        #endregion Classes

        #region Lang
        internal static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        internal static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "Ивент в данный момент активен, сначала завершите текущий ивент (<color=#ce3f27>/spacestop</color>)!",
                ["СonfigurationNotFound_Exeption"] = "<color=#ce3f27>Не удалось</color> найти конфигурацию ивента!",

                ["GravityTakeEffect"] = "{0} Гравитация начнёт действовать через <color=#8f43d8>{1}</color>",
                ["AerostatDescription"] = "{0} Аэростат <color=#738d43>автоматически</color> направится в космос, когда вы наберете необходимую <color=#738d43>высоту</color>",
                ["AerostatInSpace"] = "{0} Вы вышли в <color=#8f43d8>открытый космос!</color>\nНажмите <color=#738d43>кнопку</color> для возвращения на землю",

                ["ShuttleInSpace"] = "{0} Вы вышли в <color=#8f43d8>открытый космос!</color>",
                ["ShuttleOutSpace"] = "{0} Вы вошли в <color=#738d43>атмосферу!</color>",

                ["PreStartEvent"] = "{0} <color=#8f43d8>{1}</color> появится на орбите через <color=#8f43d8>{2}</color>",
                ["StartEvent"] = "{0} <color=#8f43d8>{1}</color> появилась в квадрате <color=#8f43d8>{2}</color> на высоте 750 м. В космосе низкая температура, отсутствует кислород и гравитация! Попасть в космос можно при помощи космического корабля или воздушного шара. Для того чтобы <color=#ce3f27>выжить</color> вам понадобится скафандр или хазмат или кислородный балон",
                ["EventDescription"] = "{0} <color=#8f43d8>{1}</color> находится в квадрате <color=#8f43d8>{2}</color> на высоте 750 м. В космосе низкая температура, отсутствует кислород и гравитация! Попасть в космос можно при помощи космического корабля или воздушного шара. Для того чтобы <color=#ce3f27>выжить</color> вам понадобится скафандр или хазмат или кислородный балон",
                ["RemainTime"] = "{0} {1} будет уничтожена через <color=#ce3f27>{2}</color>! Покиньте космическую станцию, или будете <color=#ce3f27>убиты</color>!",
                ["NeedUseSpaceCard"] = "{0} Используйте <color=#8f43d8>космическую карту</color> чтобы открыть дверь!",
                ["EndEvent"] = "{0} Ивент <color=#8f43d8>окончен</color>!",

                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",

                ["GetSpaceCard"] = "{0} Вы получили <color=#8f43d8>Космическую Карту</color>!",
                ["GetSpaceShuttle"] = "{0} Вы получили <color=#8f43d8>Космический корабль</color>!",
                ["GetAerostat"] = "{0} Вы получили <color=#8f43d8>Аэростат</color>!",
                ["GetSpaceSuit"] = "{0} Вы получили <color=#8f43d8>Скафандр</color>!",

                ["Hour"] = "ч.",
                ["Min"] = "м.",
                ["Sec"] = "с.",

                ["PveMode_NewOwner"] = "{0} <color=#55aaff>{1}</color> стал владельцем ивента!",

            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "This event is active now. Finish the current event! (<color=#ce3f27>/spacestop</color>)!",
                ["СonfigurationNotFound_Exeption"] = "The event configuration <color=#ce3f27>could not</color> be found!",

                ["GravityTakeEffect"] = "{0} Gravity will take effect in <color=#ce3f27>{1}</color>",
                ["AerostatDescription"] = "{0} The balloon will <color=#ce3f27>automatically</color> send you into space when you reach the required <color=#ce3f27>height</color>",
                ["AerostatInSpace"] = "{0} You have entered <color=#8f43d8>space!</color>\nPress <color=#738d43>button</color> to return to earth",

                ["ShuttleInSpace"] = "{0} You have gone into <color=#8f43d8>space!</color>",
                ["ShuttleOutSpace"] = "{0} You have entered the <color=#738d43>atmosphere!</color>",

                ["PreStartEvent"] = "{0} <color=#8f43d8>{1}</color> will appear in orbit in <color=#8f43d8>{2}</color>!",
                ["StartEvent"] = "{0} <color=#8f43d8>{1}</color> is spawned at grid <color=#8f43d8>{2}</color> at an altitude of 750 m. The temperature in space is very low, there is no oxygen and gravity! You can get into space with the help of a spaceship or any balloon. In order to <color=#ce3f27>survive</color>, you will need a spacesuit or a hazmat or an oxygen tank.",
                ["EventDescription"] = "{0} <color=#8f43d8>{1}</color> is located  at grid <color=#8f43d8>{2}</color> at an altitude of 750 m. The temperature in space is very low, there is no oxygen and gravity! You can get into space with the help of a spaceship or any balloon. In order to <color=#ce3f27>survive</color>, you will need a spacesuit or a hazmat or an oxygen tank.",
                ["RemainTime"] = "{0} {1} will be destroyed in <color=#ce3f27>{2}</color>! Leave the space station, or you will be <color=#ce3f27>killed</color>!",
                ["NeedUseSpaceCard"] = "{0} Use the <color=#8f43d8>space card</color> to unlock the door!",
                ["EndEvent"] = "{0} The event is <color=#ce3f27>over</color>!",

                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have gone out</color> the PVP zone, now other players <color=#738d43>can't damage</color> you!",

                ["GetSpaceCard"] = "{0} You got a <color=#8f43d8>Space Sard</color>!",
                ["GetSpaceShuttle"] = "{0} You got a <color=#8f43d8>Space Shuttle</color>!",
                ["GetAerostat"] = "{0} You got an <color=#8f43d8>Aerostat</color>!",
                ["GetSpaceSuit"] = "{0} You got a <color=#8f43d8>Space Suit</color>!",

                ["Hour"] = "h.",
                ["Min"] = "m.",
                ["Sec"] = "s.",

                ["Marker_EventOwner"] = "Event Owner: {0}",
                ["PveMode_NewOwner"] = "{0} <color=#55aaff>{1}</color> became the owner of the event!",
            }, this);
        }
        #endregion Lang

        #region Config
        private PluginConfig _config;

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #region StationConfig
        public class SpaceStationConfig
        {
            [JsonProperty(en ? "Turret Settings" : "Настройка турелей")] public HashSet<TurretConfig> turrets { get; set; }
            [JsonProperty(en ? "Flame Turret Settings" : "Настройка огненнных турелей")] public HashSet<FlameTurretConfig> flameTurrets { get; set; }
            [JsonProperty(en ? "Bradley Settings" : "Настройка Бредли")] public HashSet<BradleyConfig> bradleyConfigs { get; set; }
            [JsonProperty(en ? "Crate locations" : "Расположение ящиков")] public HashSet<StationCratesConfig> crates { get; set; }
            [JsonProperty(en ? "Settings for Card Doors and NPCs" : "Настройка дверей с карточками и NPC")] public HashSet<CardDoorConfig> cardReaderConfigs { get; set; }
            [JsonProperty(en ? "Setting up NPCs and basic Doors" : "Настройка обычных дверей и NPC")] public HashSet<DoorConfig> doors { get; set; }
            [JsonProperty(en ? "Settings for Static NPC that appear when the station spawns (Preset name - positions)" : "Настройка статичных NPC, которые появляются при спавне станции (Названия пресета - расположения)")] public Dictionary<string, HashSet<LocationConfig>> outsideNpcLocations { get; set; }

            public static SpaceStationConfig GetDefaultConfig()
            {
                return new SpaceStationConfig
                {
                    turrets = new HashSet<TurretConfig>
                    {
                        new TurretConfig
                        {
                            shortNameWeapon = "smg.2",
                            hp = 100,
                            shortNameAmmo = "ammo.pistol",
                            countAmmo = 100,
                            locations = new HashSet<LocationConfig>()
                        }
                    },
                    flameTurrets = new HashSet<FlameTurretConfig>
                    {
                        new FlameTurretConfig
                        {
                            hp = 100,
                            fuel = 75,
                            locations = new HashSet<LocationConfig>()
                        }
                    },
                    bradleyConfigs = new HashSet<BradleyConfig>
                    {
                        new BradleyConfig
                        {
                            hp = 900f,
                            scaleDamage = 0.3f,
                            viewDistance = 100.0f,
                            searchDistance = 100.0f,
                            coaxAimCone = 1.1f,
                            coaxFireRate = 1.0f,
                            coaxBurstLength = 10,
                            nextFireTime = 10f,
                            topTurretFireRate = 0.25f,
                            locations = new HashSet<LocationConfig>
                            {

                            }
                        }
                    },
                    cardReaderConfigs = new HashSet<CardDoorConfig>(),
                    doors = new HashSet<DoorConfig>(),
                    crates = new HashSet<StationCratesConfig>(),
                    outsideNpcLocations = new Dictionary<string, HashSet<LocationConfig>>()
                };
            }
        }

        public class TurretConfig
        {
            [JsonProperty(en ? "Weapon Short Name" : "ShortName оружия")] public string shortNameWeapon { get; set; }
            [JsonProperty(en ? "Hit Points" : "Кол-во ХП")] public float hp { get; set; }
            [JsonProperty(en ? "Ammo Short Name" : "ShortName патронов")] public string shortNameAmmo { get; set; }
            [JsonProperty(en ? "Ammo Ammount" : "Кол-во патронов")] public int countAmmo { get; set; }
            [JsonProperty(en ? "Locations" : "Расположения")] public HashSet<LocationConfig> locations { get; set; }
        }

        public class FlameTurretConfig
        {
            [JsonProperty(en ? "Hit Points" : "Кол-во ХП")] public float hp { get; set; }
            [JsonProperty(en ? "Fuel" : "Кол-во топлива")] public int fuel { get; set; }
            [JsonProperty(en ? "Locations" : "Расположения")] public HashSet<LocationConfig> locations { get; set; }
        }

        public class BradleyConfig
        {
            [JsonProperty(en ? "Hit Points" : "ХП")] public float hp { get; set; }
            [JsonProperty(en ? "Damage Scale" : "Множитель урона")] public float scaleDamage { get; set; }
            [JsonProperty(en ? "Viewable Distance" : "Дальность обзора")] public float viewDistance { get; set; }
            [JsonProperty(en ? "Search Radius" : "Радиус поиска")] public float searchDistance { get; set; }
            [JsonProperty(en ? "The multiplier of Machine-gun aim cone" : "Множитель разброса пулемёта")] public float coaxAimCone { get; set; }
            [JsonProperty(en ? "The multiplier of Machine-gun fire rate" : "Множитель скорострельности пулемёта")] public float coaxFireRate { get; set; }
            [JsonProperty(en ? "Amount of Machine-gun burst shots" : "Кол-во выстрелов очереди пулемёта")] public int coaxBurstLength { get; set; }
            [JsonProperty(en ? "The time between shots of the main gun [sec.]" : "Время между залпами основного орудия [sec.]")] public float nextFireTime { get; set; }
            [JsonProperty(en ? "The time between shots of the main gun in a fire rate [sec.]" : "Время между выстрелами основного орудия в залпе [sec.]")] public float topTurretFireRate { get; set; }
            [JsonProperty(en ? "Locations" : "Расположения", Order = 100)] public HashSet<LocationConfig> locations { get; set; }
        }

        public class StationCratesConfig
        {
            [JsonProperty(en ? "Prefab" : "Префаб")] public string prefab { get; set; }
            [JsonProperty(en ? "Locations" : "Расположения", Order = 100)] public HashSet<LocationConfig> locations { get; set; }
        }

        public class CardDoorConfig : NpcSpawnerDoor
        {
            [JsonProperty(en ? "Door prefab" : "Префаб двери")] public string doorPrefab { get; set; }
            [JsonProperty(en ? "Card type (0 - green, 1 - blue, 2 - red, 3 - space card)" : "Тип карточки (0 - зеленая, 1 - синяя, 2 - красная, 3 - космическая)")] public int cardType { get; set; }
            [JsonProperty(en ? "Door location" : "Расположение двери")] public LocationConfig doorLocation { get; set; }
            [JsonProperty(en ? "Card reader location" : "Расположение считывателя карт")] public LocationConfig cardReaderLocation { get; set; }
            [JsonProperty(en ? "Button location" : "Расположение кнопки")] public LocationConfig buttonLocation { get; set; }
        }

        public class DoorConfig : NpcSpawnerDoor
        {
            [JsonProperty(en ? "Prefab" : "Префаб")] public string prefab { get; set; }
            [JsonProperty(en ? "Lock the Door ? (Force Door Raiding)" : "Заблокировать дверь? (Нужно будет рейдить)")] public bool blockDoor { get; set; }
            [JsonProperty(en ? "Door locations " : "Расположения дверей")] public HashSet<LocationConfig> locations { get; set; }

        }

        public class NpcSpawnerDoor
        {
            [JsonProperty(en ? "Settings for Static NPCs that appear when a door is opened (Preset name - positions)" : "Настройка статичных NPC, которые появляются при открытии одной из дверей (Названия пресета - расположения)", Order = 100)] public Dictionary<string, HashSet<LocationConfig>> npcLocations { get; set; }
        }

        #endregion StationConfig

        public class MainConfig
        {
            [JsonProperty(en ? "Enable automatic event [true/false]" : "Включить автоматическое проведение ивента [true/false]")] public bool isAutoEvent { get; set; }
            [JsonProperty(en ? "Minimum time between events [sec]" : "Минимальное вермя между ивентами [sec]")] public int minTimeBetweenEvent { get; set; }
            [JsonProperty(en ? "Maximum time between events [sec]" : "Максимальное вермя между ивентами [sec]")] public int maxTimeBetweenEvent { get; set; }
            [JsonProperty(en ? "Countdown from Event Start Notification until Event Starts [sec]" : "Время от оповещения до начала ивента [sec]")] public int preStartTime { get; set; }
            [JsonProperty(en ? "Event End Countdown Times for Chat to announce the Event End Timer [sec]" : "Время до конца ивента, когда выводится сообщение о сокром окончании ивента [sec]")] public HashSet<int> remainTimeNotifications { get; set; }
            [JsonProperty(en ? "Event End Countdown Times for Chat to announce the Event Description [sec]" : "Время до конца ивента, когда выводится описание ивента [sec]")] public HashSet<int> descriptionNotifications { get; set; }
            [JsonProperty(en ? "Allow Event Spawns on Ocean Topology Only [true/false]" : "Разрешить появляение ивента только над океаном [true/false]")] public bool onlyOceanSpawn { get; set; }
            [JsonProperty(en ? "Enable weightlessness in space? [true/false]" : "Включить невесомость в космосе? [true/false]")] public bool enableZeroGravity { get; set; }
            [JsonProperty(en ? "Space altitude (max - 830)" : "Высота космоса (max - 830)")] public float spaceHeight { get; set; }
            [JsonProperty(en ? "Space radius" : "Радиус космоса")] public float spaceRadius { get; set; }
            [JsonProperty(en ? "Do not stop the event if there is someone in the event area" : "Не останавливать ивент, если в зоне ивента кто-то есть")] public bool dontStopIfPlayerInSpace { get; set; }
            [JsonProperty(en ? "Use custom spawn points [true/false]" : "Использовать кастомные координаты спавна [true/false]")] public bool useCustomCoords { get; set; }
            [JsonProperty(en ? "Custom spawn points (/spacepoint)" : "Кастомные координаты для спавна поезда (/spacepoint)")] public List<string> customSpawnPoints { get; set; }
            [JsonProperty(en ? "The time until the end of the event after hacking all the locked crates at the station (0 - do not change the time) [sec]" : "Время до завершения ивента после взлома всех заблокированных ящиков на станции (0 - не изменять время) [sec]")] public int timeAfterLootingAllLockedCrates { get; set; }
        }

        public class EventConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Display name" : "Отображаемое имя")] public string displayName { get; set; }
            [JsonProperty(en ? "Periodic Event Length [sec]" : "Продолжительность периодического ивента [sec]")] public int eventTime { get; set; }
            [JsonProperty(en ? "Probability" : "Вероятность")] public float chance { get; set; }
            [JsonProperty(en ? "Radiation" : "Радиация")] public float radiation { get; set; }
            [JsonProperty(en ? "Temperature" : "Температура")] public float temperature { get; set; }
            [JsonProperty(en ? "Space Station Settings" : "Настройка космической станции")] public SpaceStationSpawnConfig spaceStationConfig { get; set; }
            [JsonProperty(en ? "Crate Settings" : "Настройка ящиков")] public SpaceLootCrateConfig spaceLootCrateConfig { get; set; }
            [JsonProperty(en ? "Meteorite Settings" : "Настройка метеоритов")] public MetheorsConfig meteorsConfig { get; set; }
            [JsonProperty(en ? "Mobile NPC Settings" : "Настройка подвижных NPC")] public SpawnRoamNpcConfig roamNpcSpawnConfig { get; set; }
            [JsonProperty(en ? "Spawn of Space Shuttles Settings" : "Настройка спавна шатлов")] public SpaceShuttleInSpaceSpawnConfig spaceShuttleInSpaceSpawnConfig { get; set; }
            [JsonProperty(en ? "Spawn of aerostats Settings" : "Настройка спавна аэростатов")] public AerostatInSpaceSpawnConfig aerostatInSpaceSpawnConfig { get; set; }
            [JsonProperty(en ? "Main Door Settings" : "Настройки главной двери ")] public MainDoorConfig mainDoorConfig { get; set; }
        }

        public class MainDoorConfig
        {
            [JsonProperty(en ? "Automatically open the door" : "Автоматически открывать дверь")] public bool isOpenAutomatically { get; set; }
            [JsonProperty(en ? "Button locations for opening the door" : "Расположения кнопок для открытия двери")] public HashSet<LocationConfig> openButtonLocations { get; set; }
        }

        public class SpaceStationSpawnConfig
        {
            [JsonProperty(en ? "Allow space station spawn? [true/false]" : "Разрешить спавн космической станции? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Space Station Preset (data/Space/Stations)" : "Пресет космической станции (data/Space/Stations)")] public string presetName { get; set; }
        }

        public class MetheorsConfig
        {
            [JsonProperty(en ? "Allow meteorite spawn [true/false]" : "Разрешить спавн метеоритов [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Number of meteorites" : "Количество метеоритов")] public int meteorCount { get; set; }
            [JsonProperty(en ? "Probability of a large meteorite [0 - 100]" : "Вероятность большого метеорита [0 - 100]")] public float largeMeteorChance { get; set; }
            [JsonProperty(en ? "Probability of a stone meteorite [0 - 100]" : "Вероятность метеорита из камня [0 - 100]")] public float stoneMeteorChance { get; set; }
            [JsonProperty(en ? "Probability of an iron meteorite [0 - 100]" : "Вероятность метеорита из железа [0 - 100]")] public float ironMeteorChance { get; set; }
            [JsonProperty(en ? "Probability of a sulfur meteorite [0 - 100]" : "Вероятность метеорита из серы [0 - 100]")] public float sulphurMeteorChance { get; set; }
        }

        public class SpaceLootCrateConfig
        {
            [JsonProperty(en ? "Allow spawn crates in space [true/false]" : "Разрешить ящиков в космосе [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Number of crates in space" : "Количество ящиков в космосе")] public int crateCount { get; set; }
            [JsonProperty(en ? "Prefab - probability" : "Префаб - вероятность")] public Dictionary<string, float> probability { get; set; }
        }

        public class SpawnRoamNpcConfig
        {
            [JsonProperty(en ? "Allow spawn of mobile NPCs? [true/false]" : "Разрешить спавн подвижных NPC? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Number of NPCs" : "Количество NPC")] public int npcsCount { get; set; }
            [JsonProperty(en ? "The name of the NPC preset - probability [0-100]" : "Название пресета NPC - вероятность [0-100]")] public Dictionary<string, float> probability { get; set; }
        }

        public class SpaceStationBaseConfig
        {
            [JsonProperty(en ? "Turn on gravity at the station? [true/false]" : "Включить гравитацию на станции? [true/false]")] public bool enableGravity { get; set; }
            [JsonProperty(en ? "Allow the use of research tables [true/false]" : "Разрешить использование столов для исследования [true/false]")] public bool allowResearchTables { get; set; }
            [JsonProperty(en ? "Allow the use of workbenches [true/false]" : "Разрешить использование верстаков [true/false]")] public bool allowWorkbenches { get; set; }
            [JsonProperty(en ? "Allow the use of repair benches [true/false]" : "Разрешить использование ремонтных верстаков [true/false]")] public bool allowRepairbenches { get; set; }
            [JsonProperty(en ? "Allow the use of mixing tables [true/false]" : "Разрешить использование стола для смешивания [true/false]")] public bool allowMixingBenches { get; set; }
            [JsonProperty(en ? "Allow the use of computer stations [true/false]" : "Разрешить использование компьютерных станций [true/false]")] public bool allowComputerStations { get; set; }
            [JsonProperty(en ? "Allow the use of musical instruments [true/false]" : "Разрешить использование музыкальных инструментов [true/false]")] public bool allowMusicalInstruments { get; set; }
            [JsonProperty(en ? "Allow damage to barricades [true/false]" : "Разрешить урон по баррикадам [true/false]")] public bool allowBarricadeDamage { get; set; }
        }

        public class SpaceShuttleInSpaceSpawnConfig
        {
            [JsonProperty(en ? "Allow space shuttle spawn? [true/false]" : "Разрешить спавн космических шатлов? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Number of capsules" : "Количество шатлов")] public int count { get; set; }
            [JsonProperty(en ? "The amount of fuel in the shuttle" : "Количество топлива в шатле")] public int fuelAmountInShuttle { get; set; }
        }

        public class AerostatInSpaceSpawnConfig
        {
            [JsonProperty(en ? "Allow aerostat spawn? [true/false]" : "Разрешить спавн аэростатов? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Number of aerostats" : "Количество аэростатов")] public int count { get; set; }
            [JsonProperty(en ? "The amount of fuel in the aerostat" : "Количество топлива в аэростате")] public int fuelAmount { get; set; }
        }

        public class AstronautConfig
        {
            [JsonProperty(en ? "Turn on astronaut mode on space stations? [true/false]" : "Включить режим астронавта на космических станциях? [true/false]")] public bool astronautSpaceStationMode { get; set; }
            [JsonProperty(en ? "Turn on third-person mode? [true/false]" : "Включить режим от третьего лица? [true/false]")] public bool thirdPerson { get; set; }
            [JsonProperty(en ? "Control config" : "Настройки управление в космосе")] public AstronautControlConfig controlConfig { get; set; }
            [JsonProperty(en ? "Basic parameters of an astronaut in space (without a spacesuit)" : "Базовые параметры астронавта в космосе (без скафандра)")] public AstronautParametersConfig parametersConfig { get; set; }
        }

        public class AstronautControlConfig
        {
            [JsonProperty(en ? "Drag" : "Сопротивление воздуха")] public float drag { get; set; }
            [JsonProperty(en ? "Type of control in zero gravity (0 - simplified, 1 - full)" : "Тип управления в невесомости (0 - упрощенное, 1 - полное)")] public int controlType { get; set; }
            [JsonProperty(en ? "Rotation speed multiplier (A/D)" : "Множитель скорости поворота (A/D)")] public float rotationButtonSpeedScale { get; set; }
            [JsonProperty(en ? "Rotation speed multiplier (MOUSE)" : "Множитель скорости поворота (МЫШКА)")] public float rotationMouseSpeedScale { get; set; }
        }

        public class AstronautParametersConfig
        {
            [JsonProperty(en ? "Allow breathing in space? [true/false]" : "Разрешить дышать в космосе? [true/false]")] public bool canBreathe { get; set; }
            [JsonProperty(en ? "Add the sound of breathing? [true/false]" : "Добавить звук дыхания? [true/false]")] public bool soudOfBreathe { get; set; }
            [JsonProperty(en ? "Speed multiplier" : "Множитель скорости движения")] public float speedScale { get; set; }

            public AstronautParametersConfig(bool canBreathe = false, bool soudOfBreathe = false, float speedScale = 1)
            {
                this.canBreathe = canBreathe;
                this.soudOfBreathe = soudOfBreathe;
                this.speedScale = speedScale;
            }
            public static AstronautParametersConfig operator +(AstronautParametersConfig parameter1, AstronautParametersConfig parameter2)
            {
                return new AstronautParametersConfig
                {
                    canBreathe = parameter1.canBreathe || parameter2.canBreathe,
                    soudOfBreathe = parameter1.soudOfBreathe || parameter2.soudOfBreathe,
                    speedScale = parameter1.speedScale + parameter2.speedScale,
                };
            }
        }

        public class SpaceSuitConfig
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Item" : "Предмет")] public ItemConfig itemConfig { get; set; }
            [JsonProperty(en ? "Additional effects" : "Дополнительные эффекты")] public AstronautParametersConfig additionalAstronautParameters { get; set; }
        }

        public class SpaceCardConfig : SpawnedItemConfig
        {
            [JsonProperty(en ? "Multiplier of card health loss when using" : "Множитель потери прочности карты при использовании", Order = 103)] public float helthLossScale { get; set; }
        }

        public class ShuttleConfig
        {
            [JsonProperty(en ? "Control" : "Управление")] public ShuttleControl control { get; set; }
            [JsonProperty(en ? "Parameters of the Shuttle in the Atmosphere" : "Параметры шатла в атмосфере")] public ShuttleParameters atmosphereParameters { get; set; }
            [JsonProperty(en ? "Parameters of the Shuttle in Space" : "Параметры шатла в космосе")] public ShuttleParameters spaceParameters { get; set; }
            [JsonProperty(en ? "Item for placement" : "Предмет для размещения")] public SpawnedItemConfig itemConfig { get; set; }
            [JsonProperty(en ? "Setting up the spawn of shuttles" : "Настройка спавна шатлов")] public ShuttleSpawnConfig respawnConfig { get; set; }
            [JsonProperty(en ? "Setting up Samsites" : "Настройка зенитных турелей")] public SamSiteConfig samSiteConfig { get; set; }
            [JsonProperty(en ? "Setting up a marker for free spaceships" : "Настройка маркера для свободных космических кораблей")] public ShuttleMarkerConfig shuttleMarkerConfig { get; set; }
            [JsonProperty(en ? "Spawn bonfires on the ship? [true/false]" : "Включить костры на корабле [true/false]")] public bool enableCampfires { get; set; }
            [JsonProperty(en ? "Maximum distance for torpedoes" : "Максимальная дистанция для торпед")] public float maxTorpedoDistance { get; set; }
        }

        public class ShuttleMarkerConfig
        {
            [JsonProperty(en ? "Enable Map Marker? [true/false]" : "Использовать ли маркер? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Name" : "Название")] public string name { get; set; }
        }

        public class SamSiteConfig
        {
            [JsonProperty(en ? "SamSites attack shuttles" : "Зенитные турели будут атаковать шатлы")] public bool enable { get; set; }
            [JsonProperty(en ? "Increase the range and speed of missiles" : "Увеличить дальность и скорость зенитных раке?")] public bool bigRangeAndSpeed { get; set; }
        }

        public class ShuttleParameters
        {
            [JsonProperty(en ? "Fuel consumption multiplier" : "Множитель расхода топлива")] public float fuelConsumptionScale { get; set; }
            [JsonProperty(en ? "Allow firing torpedoes? [true/false]" : "Разрешить стрельбу торпедами? [true/false]")] public bool allowTorpedo { get; set; }
            [JsonProperty(en ? "Drag" : "Сопротивление воздуха")] public float drag { get; set; }
            [JsonProperty(en ? "Angular drag" : "Сопротивление воздуха при вращении")] public float angulardrag { get; set; }
            [JsonProperty(en ? "Thrust multiplier [SHIFT]" : "Множитель тяги [SHIFT]")] public float shiftForceScale { get; set; }
            [JsonProperty(en ? "Thrust multiplier [CTRL]" : "Множитель тяги [CTRL]")] public float ctrlForceScale { get; set; }
            [JsonProperty(en ? "Thrust multiplier [W]" : "Множитель тяги [W]")] public float wForceScale { get; set; }
            [JsonProperty(en ? "Thrust multiplier [S]" : "Множитель тяги [S]")] public float sForceScale { get; set; }
            [JsonProperty(en ? "Turning speed multiplier [MOUSE]" : "Множитель скорости поворота [MOUSE]")] public float turningMouseSpeed { get; set; }
            [JsonProperty(en ? "Turning speed multiplier [A/D]" : "Множитель скорости поворота [A/D]")] public float turningADSpeed { get; set; }
        }

        public class ShuttleControl
        {
            [JsonProperty(en ? "Use the control like a minicopter? [true/false]" : "Использовать схему управления Мини-коптера [true/false]")] public bool mibicoperControl { get; set; }
            [JsonProperty(en ? "Invert the X-axis? [true/false]" : "Инвертировать ось X? [true/false]")] public bool invertX { get; set; }
            [JsonProperty(en ? "Invert the Y-axis? [true/false]" : "Инвертировать ось Y? [true/false]")] public bool invertY { get; set; }
        }

        public class ShuttleSpawnConfig
        {
            [JsonProperty(en ? "Setting up the respawn of shuttles at the beginning of the event" : "Настройка респавна шатлов перед ивентом")] public ShuttleEventBegignigSpawnConfig eventBegignigSpawnConfig { get; set; }
            [JsonProperty(en ? "Turn on shuttle respawn? [true/false]" : "Включить респавн шатлов? [true/false]")] public bool enableSpawn { get; set; }
            [JsonProperty(en ? "Destroy all shuttles at the end of the event [true/false]" : "Уничтожать все шатлы по окончанию ивента [true/false]")] public bool destroyAllShutlesAfterEvent { get; set; }
            [JsonProperty(en ? "Time between shuttle respawns [sec]" : "Период респавна шатлов [сек]")] public float shuttleRespawnPeriod { get; set; }
            [JsonProperty(en ? "Number of shuttles by the road" : "Количество шатлов у дороги")] public int roadShuttlesNumber { get; set; }
        }

        public class ShuttleEventBegignigSpawnConfig
        {
            [JsonProperty(en ? "Delete shuttles that have not been used after the end of the event [true/false]" : "Удалять шаттлы, которые не были использованы, после окончания ивента [true/false]")] public bool deleteShuttles { get; set; }
            [JsonProperty(en ? "Number of shuttles by the road" : "Количество шатлов у дороги")] public int roadShuttlesNumber { get; set; }
        }

        public class AerostatConfig
        {
            [JsonProperty(en ? "Use all balloons to fly into space? [true/false]" : "Использовать все воздушные шары для полета в космос? [true/false]")] public bool allBallons { get; set; }
            [JsonProperty(en ? "Item for placement" : "Предмет для размещения")] public SpawnedItemConfig itemConfig { get; set; }
        }

        public class SpawnedItemConfig : ItemConfig
        {
            [JsonProperty(en ? "Enable spawn in crates [true/false]" : "Включить спавн в ящиках [true/false]", Order = 100)] public bool inLootSpawn { get; set; }
            [JsonProperty(en ? "The crate prefab - chance" : "Префаб ящика - шанс", Order = 101)] public Dictionary<string, float> crateChanses { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty("Shortname")] public string shortname { get; set; }
            [JsonProperty("Skin")] public ulong skin { get; set; }
            [JsonProperty("Name")] public string name { get; set; }
        }

        public class ZoneConfig
        {
            [JsonProperty(en ? "Create a PVP zone? (only for those who use the TruePVE plugin) [true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool isCreateZonePVP { get; set; }
            [JsonProperty(en ? "Use the dome? [true/false]" : "Использовать ли купол? [true/false]")] public bool isDome { get; set; }
            [JsonProperty(en ? "Darkening the dome" : "Затемнение купола")] public int darkening { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(en ? "Enable Map Marker? [true/false]" : "Использовать ли маркер? [true/false]")] public bool enable { get; set; }
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

        public class NpcConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Name" : "Название")] public string name { get; set; }
            [JsonProperty(en ? "Health" : "Кол-во ХП")] public float health { get; set; }
            [JsonProperty(en ? "Wear items" : "Одежда")] public List<NpcWear> wearItems { get; set; }
            [JsonProperty(en ? "Belt items" : "Быстрые слоты")] public List<NpcBelt> beltItems { get; set; }
            [JsonProperty(en ? "Kit" : "Kit")] public string kit { get; set; }
            [JsonProperty(en ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float attackRangeMultiplier { get; set; }
            [JsonProperty(en ? "Sense Range" : "Радиус обнаружения цели")] public float senseRange { get; set; }
            [JsonProperty(en ? "Memory duration [sec.]" : "Длительность памяти цели [sec.]")] public float memoryDuration { get; set; }
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float damageScale { get; set; }
            [JsonProperty(en ? "Aim Cone Scale" : "Множитель разброса")] public float aimConeScale { get; set; }
            [JsonProperty(en ? "Detect the target only in the NPC's viewing vision cone?" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool checkVisionCone { get; set; }
            [JsonProperty(en ? "Vision Cone" : "Угол обзора")] public float visionCone { get; set; }
            [JsonProperty(en ? "Should remove the corpse?" : "Удалять труп?")] public bool deleteCorpse { get; set; }
            [JsonProperty(en ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool disableRadio { get; set; }
            [JsonProperty(en ? "Chase Range (only for roam NPCs)" : "Дальность погони за целью (только для подвижных NPC)")] public float chaseRange { get; set; }
            [JsonProperty(en ? "Speed (only for roam NPCs)" : "Скорость (только для подвижных NPC)")] public float speed { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - Add Items)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - Добавить предметы)")] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Loot table" : "Собственная лутовая таблицв")] public OwnLootTableConfig lootTable { get; set; }
        }

        public class LocationConfig
        {
            [JsonProperty(en ? "Position" : "Позиция")] public string position { get; set; }
            [JsonProperty(en ? "Rotation" : "Вращение")] public string rotation { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "skinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
        }

        public class NpcBelt
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "Amount" : "Кол-во")] public int amount { get; set; }
            [JsonProperty(en ? "skinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
            [JsonProperty(en ? "Mods" : "Модификации на оружие")] public List<string> Mods { get; set; }
            [JsonProperty(en ? "Ammo" : "Патроны")] public string ammo { get; set; }
        }

        public class OwnLootTableConfig
        {
            [JsonProperty(en ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int minItemsAmount { get; set; }
            [JsonProperty(en ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int maxItemsAmount { get; set; }
            [JsonProperty(en ? "List of items" : "Список предметов")] public List<LootItemConfig> items { get; set; }
        }

        public class LootItemConfig
        {
            [JsonProperty("ShortName")] public string shortname { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong skin { get; set; }
            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")] public string name { get; set; }
            [JsonProperty(en ? "Minimum" : "Минимальное кол-во")] public int minAmount { get; set; }
            [JsonProperty(en ? "Maximum" : "Максимальное кол-во")] public int maxAmount { get; set; }
            [JsonProperty(en ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float chance { get; set; }
            [JsonProperty(en ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool isBlueprint { get; set; }
        }

        public class CrateConfig
        {
            [JsonProperty("Prefab")] public string prefab { get; set; }
            [JsonProperty(en ? "Time to unlock the crates (LockedCrate) [sec.]" : "Время до открытия заблокированного ящика (LockedCrate) [sec.]")] public float crateUnlockTime { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - Add Items)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - Добавить предметы)")] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Loot table" : "Собственная лутовая таблицв")] public OwnLootTableConfig lootTable { get; set; }
        }

        public class StorageCrateConfig
        {
            [JsonProperty("Prefab")] public string prefab { get; set; }
            [JsonProperty(en ? "Loot table" : "Собственная лутовая таблицв")] public OwnLootTableConfig lootTable { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(en ? "Use Chat Notifications? [true/false]" : "Использовать ли чат? [true/false]")] public bool isChatEnable { get; set; }
            [JsonProperty(en ? "GUI Setting" : "Настройки GUI")] public GUIConfig guiConfig { get; set; }
            [JsonProperty(en ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip")] public GameTipConfig gameTipConfig { get; set; }
        }

        public class GameTipConfig
        {
            [JsonProperty(en ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")] public int style { get; set; }
        }

        public class GUIConfig
        {
            [JsonProperty(en ? "Use the Countdown GUI? [true/false]" : "Использовать ли GUI обратного отсчета? [true/false]")] public bool isCountdownGUI { get; set; }
            [JsonProperty(en ? "Vertical offset" : "Смещение по вертикали")] public int offsetMinY { get; set; }
        }

        public class SupportedPluginsConfig
        {
            [JsonProperty(en ? "GUIAnnouncements Settings" : "Настройка GUI Announcements")] public GUIAnnouncementsConfig guiAnnouncementsConfig { get; set; }
            [JsonProperty(en ? "Notify Settings" : "Настройка Notify")] public NotifyPluginConfig notifyPluginConfig { get; set; }
            [JsonProperty(en ? "DiscordMessages Settings" : "Настройка DiscordMessages")] public DiscordConfig discordMessagesConfig { get; set; }
            [JsonProperty(en ? "Economy Settings" : "Настройка экономики")] public EconomyConfig economy { get; set; }
            [JsonProperty(en ? "PVE Mode Setting" : "Настройка PVE Mode")] public PveModeConfig pveMode { get; set; }
            [JsonProperty(en ? "Night Vision Setting" : "Настройка Night Vision")] public NightVisionConfig nightVisionConfig { get; set; }
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(en ? "GUIAnnouncements plugin in use? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Banner color" : "Цвет баннера")] public string bannerColor { get; set; }
            [JsonProperty(en ? "Text color" : "Цвет текста")] public string textColor { get; set; }
            [JsonProperty(en ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition { get; set; }
        }

        public class NotifyPluginConfig
        {
            [JsonProperty(en ? "Notify plugin in use? [true/false]" : "Использовать ли Notify? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Type" : "Тип")] public string type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(en ? "DiscordMessages plugin in use? [true/false]" : "Использовать ли Discord? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty("Webhook URL")] public string webhookUrl { get; set; }
            [JsonProperty(en ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int embedColor { get; set; }
            [JsonProperty(en ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> keys { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(en ? "Enable economy" : "Включить экономику?")] public bool enable { get; set; }
            [JsonProperty(en ? "The time between determining the winners with a permanent event mode" : "Время между определением победителей и добавлением баланса при постоянном режиме ивента")] public int economyPermanentPeriod { get; set; }
            [JsonProperty(en ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> plugins { get; set; }
            [JsonProperty(en ? "The minimum value that a player must collect to get points for the economy" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double minEconomyPiont { get; set; }
            [JsonProperty(en ? "The minimum value that a winner must collect to make the commands work" : "Минимальное значение, которое победитель должен заработать, чтобы сработали команды")] public double minCommandPoint { get; set; }
            [JsonProperty(en ? "Looting of crates" : "Ограбление ящиков")] public Dictionary<string, double> crates { get; set; }
            [JsonProperty(en ? "Killing an NPC" : "Убийство NPC")] public double npcPoint { get; set; }
            [JsonProperty(en ? "Killing an Turret" : "Уничтожение Турели")] public double turretPoint { get; set; }
            [JsonProperty(en ? "Killing an Flame Turret" : "Уничтожение огненной турели")] public double flameTurretPoint { get; set; }
            [JsonProperty(en ? "Killing an Bradley" : "Уничтожение Bradley")] public double bradleyPoint { get; set; }
            [JsonProperty(en ? "Killing an Door" : "Уничтожение Двери")] public double doorPoint { get; set; }
            [JsonProperty(en ? "Using the Red card" : "Использование красной карты")] public double redCardPoint { get; set; }
            [JsonProperty(en ? "Using the Blue card" : "Использование синей карты")] public double blueCardPoint { get; set; }
            [JsonProperty(en ? "Using the Green card" : "Использование зеленой карты")] public double greenCardPoint { get; set; }
            [JsonProperty(en ? "Using the Space card" : "Использование космической карты")] public double spaceCardPoint { get; set; }
            [JsonProperty(en ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> commands { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(en ? "Use the PVE mode of the plugin? [true/false]" : "Использовать PVE режим работы плагина? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Show the displayName of the event owner on a marker on the map? [true/false]" : "Отображать имя владелца ивента на маркере на карте? [true/false]")] public bool showEventOwnerNameOnMap { get; set; }
            [JsonProperty(en ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float damage { get; set; }
            [JsonProperty(en ? "Damage coefficients for calculate to become the Event Owner." : "Коэффициенты урона для подсчета, чтобы стать владельцем события.")] public Dictionary<string, float> scaleDamage { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool lootCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event hack locked crates? [true/false]" : "Может ли не владелец ивента взламывать заблокированные ящики? [true/false]")] public bool hackCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool lootNpc { get; set; }

            [JsonProperty(en ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool targetNpc { get; set; }
            [JsonProperty(en ? "Can Bradley attack a non-owner of the event? [true/false]" : "Может ли Bradley атаковать не владельца ивента? [true/false]")] public bool targetTank { get; set; }
            [JsonProperty(en ? "Can Turret attack a non-owner of the event? [true/false]" : "Может ли Турель атаковать не владельца ивента? [true/false]")] public bool targetTurret { get; set; }

            [JsonProperty(en ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool damageNpc { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event do damage to Bradley? [true/false]" : "Может ли не владелец ивента наносить урон по Bradley? [true/false]")] public bool damageTank { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event do damage to Turret? [true/false]" : "Может ли не владелец ивента наносить урон по Турелям? [true/false]")] public bool damageTurret { get; set; }

            [JsonProperty(en ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")] public bool canEnterCooldownPlayer { get; set; }
            [JsonProperty(en ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")] public int timeExitOwner { get; set; }
            [JsonProperty(en ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")] public int alertTime { get; set; }
            [JsonProperty(en ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool restoreUponDeath { get; set; }
            [JsonProperty(en ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double cooldownOwner { get; set; }
            [JsonProperty(en ? "Darkening the dome (0 - disables the dome)" : "Затемнение купола (0 - отключает купол)")] public int darkening { get; set; }
        }

        public class NightVisionConfig
        {
            [JsonProperty(en ? "NightVision plugin in use? [true/false]" : "Использовать ли NightVision? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Time" : "Время")] public float time { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")] public VersionNumber version { get; set; }
            [JsonProperty(en ? "Chat Prefix" : "Префикс в чате")] public string prefix { get; set; }
            [JsonProperty(en ? "Main settings" : "Основные настройки")] public MainConfig mainConfig { get; set; }
            [JsonProperty(en ? "Event Settings" : "Конфигурации ивента")] public HashSet<EventConfig> eventConfigs { get; set; }
            [JsonProperty(en ? "Map marker settings" : "Настройка маркера на карте")] public MarkerConfig markerConfig { get; set; }
            [JsonProperty(en ? "Setting up the event area" : "Настройка области ивента")] public ZoneConfig zoneConfig { get; set; }
            [JsonProperty(en ? "Space Card Settings" : "Настройка космической карты")] public SpaceCardConfig spaceCardConfig { get; set; }
            [JsonProperty(en ? "Space Shuttle Settings" : "Настройка космических кораблей")] public ShuttleConfig shuttleConfig { get; set; }
            [JsonProperty(en ? "Aerostats Settings" : "Настройка аэростатов")] public AerostatConfig aerostatConfig { get; set; }
            [JsonProperty(en ? "Astronaut settings without a spacesuit" : "Настройки астронавта без скафанда")] public AstronautConfig astronautConfig { get; set; }
            [JsonProperty(en ? "Spacesuit Settings" : "Настройка скафандров")] public HashSet<SpaceSuitConfig> spaceSuits { get; set; }
            [JsonProperty(en ? "Space Station Settings" : "Настройка космических станций")] public SpaceStationBaseConfig spaceStaionConfig { get; set; }
            [JsonProperty(en ? "NPCs Settings" : "Настройка нпс")] public HashSet<NpcConfig> npcConfigs { get; set; }
            [JsonProperty(en ? "Crates configurations" : "Настройка ящиков")] public List<CrateConfig> crates { get; set; }
            [JsonProperty(en ? "Friges, small/large boxes, lockers configurations" : "Настройка холодильников, маленьких/больших ящиков, шкафов для переодевания")] public List<StorageCrateConfig> storageCrates { get; set; }
            [JsonProperty(en ? "Notification Settings" : "Настройкa уведомлений")] public NotifyConfig notifyConfig { get; set; }
            [JsonProperty(en ? "Supported Plugins" : "Поддерживаемые плагины")] public SupportedPluginsConfig supportedPluginsConfig { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = new VersionNumber(1, 3, 8),
                    prefix = "[Space]",
                    mainConfig = new MainConfig
                    {
                        isAutoEvent = true,
                        minTimeBetweenEvent = 7200,
                        maxTimeBetweenEvent = 7200,
                        preStartTime = 600,
                        remainTimeNotifications = new HashSet<int>
                        {
                            300,
                            30,
                            5

                        },
                        descriptionNotifications = new HashSet<int>
                        {
                            7100,
                            5400,
                            3600,
                            1800
                        },
                        onlyOceanSpawn = false,
                        enableZeroGravity = true,
                        spaceHeight = 800,
                        spaceRadius = 200,
                        customSpawnPoints = new List<string>(),
                        timeAfterLootingAllLockedCrates = 300
                    },
                    eventConfigs = new HashSet<EventConfig>
                    {
                        new EventConfig
                        {
                            presetName = "iss",
                            displayName = en ? "Space station" : "Космическая станция",
                            eventTime = 7200,
                            chance = 100,
                            radiation = 10,
                            temperature = -10,
                            spaceStationConfig = new SpaceStationSpawnConfig
                            {
                                enable = true,
                                presetName = "ISS"
                            },
                            spaceLootCrateConfig = new SpaceLootCrateConfig
                            {
                                enable = true,
                                crateCount = 20,
                                probability = new Dictionary<string, float>
                                {
                                    ["assets/prefabs/deployable/fridge/fridge.deployed.prefab"] = 10,
                                    ["assets/prefabs/deployable/locker/locker.deployed.prefab"] = 10,
                                    ["assets/bundled/prefabs/radtown/crate_normal.prefab"] = 20,
                                    ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 10,
                                }
                            },
                            meteorsConfig = new MetheorsConfig
                            {
                                enable = true,
                                meteorCount = 60,
                                largeMeteorChance = 75,
                                sulphurMeteorChance = 30,
                                stoneMeteorChance = 30,
                                ironMeteorChance = 30
                            },
                            roamNpcSpawnConfig = new SpawnRoamNpcConfig
                            {
                                enable = true,
                                npcsCount = 12,
                                probability = new Dictionary<string, float>
                                {
                                    ["astronaut_1"] = 100
                                }
                            },
                            spaceShuttleInSpaceSpawnConfig = new SpaceShuttleInSpaceSpawnConfig
                            {
                                enable = true,
                                count = 4,
                                fuelAmountInShuttle = 100,
                            },
                            aerostatInSpaceSpawnConfig = new AerostatInSpaceSpawnConfig
                            {
                                enable = true,
                                count = 3,
                                fuelAmount = 100
                            },
                            mainDoorConfig = new MainDoorConfig
                            {
                                isOpenAutomatically = true,
                                openButtonLocations = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(-4.302, -1.706, -56.597)",
                                        rotation = "(0, 0, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(4.666, -1.807, -56.595)",
                                        rotation = "(0, 0, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-4.75, -0.634, -56.72)",
                                        rotation = "(0, 180, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(4.75, -0.737, -56.72)",
                                        rotation = "(0, 180, 0)"
                                    },

                                    new LocationConfig
                                    {
                                        position = "(-0.9, -1.251, -47.5)",
                                        rotation = "(0, 0, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(1.5, -1.349, -47.811)",
                                        rotation = "(0, 180, 0)"
                                    }
                                }
                            }
                        }
                    },
                    markerConfig = new MarkerConfig
                    {
                        enable = true,
                        isRingMarker = true,
                        isShopMarker = true,
                        radius = 0.45f,
                        alpha = 0.75f,
                        color1 = new ColorConfig { r = 0.44f, g = 0.0f, b = 0.80f },
                        color2 = new ColorConfig { r = 0.44f, g = 0.0f, b = 0.80f },
                    },
                    zoneConfig = new ZoneConfig
                    {
                        isCreateZonePVP = false,
                        isDome = false,
                        darkening = 5
                    },
                    spaceCardConfig = new SpaceCardConfig
                    {
                        shortname = "keycard_green",
                        name = en ? "SPACE CARD" : "КОСМИЧЕСКАЯ КАРТА",
                        skin = 2841475252,
                        helthLossScale = 1,
                        inLootSpawn = false,
                        crateChanses = new Dictionary<string, float>
                        {
                            ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 5
                        }
                    },
                    astronautConfig = new AstronautConfig
                    {
                        astronautSpaceStationMode = false,
                        thirdPerson = false,
                        controlConfig = new AstronautControlConfig
                        {
                            controlType = 0,
                            drag = 0.5f,
                            rotationButtonSpeedScale = 1,
                            rotationMouseSpeedScale = 1
                        },
                        parametersConfig = new AstronautParametersConfig(canBreathe: true)
                    },
                    spaceSuits = new HashSet<SpaceSuitConfig>
                    {
                        new SpaceSuitConfig
                        {
                            presetName = "spacesuit_1",
                            itemConfig = new ItemConfig
                            {
                                shortname = "hazmatsuit",
                                skin = 0,
                                name = ""
                            },
                            additionalAstronautParameters = new AstronautParametersConfig(canBreathe: true, soudOfBreathe: true)
                        },
                        new SpaceSuitConfig
                        {
                            presetName = "spacesuit_2",
                            itemConfig = new ItemConfig
                            {
                                shortname = "hazmatsuit",
                                skin = 10180,
                                name = ""
                            },
                            additionalAstronautParameters = new AstronautParametersConfig(canBreathe: true, soudOfBreathe: true)
                        },
                        new SpaceSuitConfig
                        {
                            presetName = "spacesuit_3",
                            itemConfig = new ItemConfig
                            {
                                shortname = "diving.tank",
                                skin = 0,
                                name = ""
                            },
                            additionalAstronautParameters = new AstronautParametersConfig(canBreathe: true)
                        }
                    },
                    shuttleConfig = new ShuttleConfig
                    {
                        enableCampfires = true,
                        control = new ShuttleControl
                        {
                            mibicoperControl = false,
                            invertX = false,
                            invertY = false
                        },
                        atmosphereParameters = new ShuttleParameters
                        {
                            fuelConsumptionScale = 10,
                            allowTorpedo = false,
                            drag = 1,
                            angulardrag = 0.25f,
                            shiftForceScale = 1.2f,
                            ctrlForceScale = 1,
                            wForceScale = 1,
                            sForceScale = 1,
                            turningMouseSpeed = 1,
                            turningADSpeed = 1,
                        },
                        spaceParameters = new ShuttleParameters
                        {
                            fuelConsumptionScale = 1,
                            allowTorpedo = true,
                            drag = 1f,
                            angulardrag = 0.25f,
                            shiftForceScale = 1f,
                            ctrlForceScale = 1,
                            wForceScale = 1,
                            sForceScale = 1,
                            turningMouseSpeed = 1,
                            turningADSpeed = 1,

                        },
                        itemConfig = new SpawnedItemConfig
                        {
                            shortname = "box.wooden.large",
                            skin = 2867628402,
                            name = en ? "SPACE SHUTTLE" : "КОСМИЧЕСКИЙ ШАТЛ",
                            inLootSpawn = false,
                            crateChanses = new Dictionary<string, float>
                            {
                                ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 5
                            }
                        },
                        respawnConfig = new ShuttleSpawnConfig
                        {
                            eventBegignigSpawnConfig = new ShuttleEventBegignigSpawnConfig
                            {
                                deleteShuttles = true,
                                roadShuttlesNumber = 8
                            },
                            enableSpawn = false,
                            shuttleRespawnPeriod = 3600,
                            roadShuttlesNumber = 4
                        },
                        samSiteConfig = new SamSiteConfig
                        {
                            enable = true,
                            bigRangeAndSpeed = false
                        },
                        shuttleMarkerConfig = new ShuttleMarkerConfig
                        {
                            enable = true,
                            name = en ? "Space Shuttle" : "Космический корабль"
                        },
                        maxTorpedoDistance = 150
                    },
                    aerostatConfig = new AerostatConfig
                    {
                        allBallons = true,
                        itemConfig = new SpawnedItemConfig
                        {
                            shortname = "box.wooden.large",
                            skin = 2871122577,
                            name = en ? "BALLOON" : "АЭРОСТАТ",
                            inLootSpawn = true,
                            crateChanses = new Dictionary<string, float>
                            {
                                ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 5
                            }
                        }
                    },
                    spaceStaionConfig = new SpaceStationBaseConfig
                    {
                        enableGravity = true,
                        allowResearchTables = true,
                        allowWorkbenches = true,
                        allowRepairbenches = true,
                        allowMixingBenches = true,
                        allowComputerStations = true,
                        allowMusicalInstruments = true,
                        allowBarricadeDamage = true,
                    },
                    npcConfigs = new HashSet<NpcConfig>
                    {
                        new NpcConfig
                        {
                            presetName = "astronaut_1",
                            name = en ? "Astronaut" : "Космонавт",
                            health = 25,
                            wearItems = new List<NpcWear>
                            {
                                new NpcWear
                                {
                                    shortName = "hazmatsuit",
                                    skinID = 0
                                }
                            },
                            beltItems = new List<NpcBelt>
                            {
                                new NpcBelt
                                {
                                    shortName = "rifle.lr300",
                                    amount = 1,
                                    skinID = 0,
                                    Mods = new List<string> { "weapon.mod.flashlight", "weapon.mod.holosight" },
                                    ammo = ""
                                },
                                new NpcBelt
                                {
                                    shortName = "syringe.medical",
                                    amount = 10,
                                    skinID = 0,
                                    Mods = new List<string> (),
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = false,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 100,
                            memoryDuration = 60f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = false,
                            visionCone = 135f,
                            typeLootTable = 0,
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = ""
                                    }
                                }
                            },
                            chaseRange = 100,
                            speed = 1
                        },
                        new NpcConfig
                        {
                            presetName = "guard_1",
                            name = en ? "Space station guard" : "Охранник космической станции",
                            health = 200f,
                            wearItems = new List<NpcWear>
                            {
                                new NpcWear
                                {
                                    shortName = "hazmatsuit",
                                    skinID = 0
                                }
                            },
                            beltItems = new List<NpcBelt>
                            {
                                new NpcBelt
                                {
                                    shortName = "rifle.lr300",
                                    amount = 1,
                                    skinID = 0,
                                    Mods = new List<string> { "weapon.mod.flashlight", "weapon.mod.holosight" },
                                    ammo = ""
                                },
                                new NpcBelt
                                {
                                    shortName = "syringe.medical",
                                    amount = 10,
                                    skinID = 0,
                                    Mods = new List<string> (),
                                    ammo = ""
                                },
                                new NpcBelt
                                {
                                    shortName = "grenade.f1",
                                    amount = 10,
                                    skinID = 0,
                                    Mods = new List<string> (),
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = false,
                            attackRangeMultiplier = 2.5f,
                            senseRange = 100,
                            memoryDuration = 60f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = false,
                            visionCone = 135f,
                            typeLootTable = 0,
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = ""
                                    }
                                }
                            },
                            chaseRange = 0,
                            speed = 0
                        }
                    },
                    crates = new List<CrateConfig>
                    {
                        new CrateConfig
                        {
                            prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                            typeLootTable = 0,
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = ""
                                    }
                                }
                            },
                        },
                        new CrateConfig
                        {
                            prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                            typeLootTable = 0,
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = ""
                                    }
                                }
                            },
                        },
                        new CrateConfig
                        {
                            prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            typeLootTable = 0,
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = ""
                                    }
                                }
                            },
                        },
                        new CrateConfig
                        {
                            prefab = "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab",
                            typeLootTable = 0,
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = ""
                                    }
                                }
                            },
                        },
                        new CrateConfig
                        {
                            prefab = "assets/bundled/prefabs/radtown/foodbox.prefab",
                            typeLootTable = 0,
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = ""
                                    }
                                }
                            },
                        },
                        new CrateConfig
                        {
                            prefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab",
                            typeLootTable = 0,
                            crateUnlockTime = 900,
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = ""
                                    }
                                }
                            },
                        }
                    },
                    storageCrates = new List<StorageCrateConfig>
                    {
                        new StorageCrateConfig
                        {
                            prefab = "assets/prefabs/deployable/fridge/fridge.deployed.prefab",
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 2,
                                maxItemsAmount = 6,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "smallwaterbottle",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "apple",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 3,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "apple",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 3,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "can.tuna",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 3,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "can.beans",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 3,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "granolabar",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 3,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "potato",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 3,
                                        name = ""
                                    }
                                }
                            }

                        },
                        new StorageCrateConfig
                        {
                            prefab = "assets/prefabs/deployable/locker/locker.deployed.prefab",
                            lootTable = new OwnLootTableConfig
                            {
                                minItemsAmount = 5,
                                maxItemsAmount = 10,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "hoodie",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "hazmatsuit",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "hazmatsuit",
                                        skin = 10180,
                                        chance = 25,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "shoes.boots",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "metal.plate.torso",
                                        skin = 0,
                                        chance = 15,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "metal.facemask",
                                        skin = 0,
                                        chance = 15,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "pants",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "roadsign.kilt",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "roadsign.jacket",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "coffeecan.helmet",
                                        skin = 0,
                                        chance = 50,
                                        isBlueprint = false,
                                        minAmount = 1,
                                        maxAmount = 1,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    notifyConfig = new NotifyConfig
                    {
                        isChatEnable = true,
                        gameTipConfig = new GameTipConfig
                        {
                            isEnabled = false,
                            style = 2,
                        },
                        guiConfig = new GUIConfig
                        {
                            isCountdownGUI = true,
                            offsetMinY = -56
                        }
                    },
                    supportedPluginsConfig = new SupportedPluginsConfig
                    {
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
                            type = "0"
                        },
                        discordMessagesConfig = new DiscordConfig
                        {
                            isEnabled = false,
                            webhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                            embedColor = 13516583,
                            keys = new HashSet<string>
                            {
                                "PreStartEvent",
                                "StartEvent",
                                "Crash"
                            }
                        },
                        economy = new EconomyConfig
                        {
                            enable = false,
                            economyPermanentPeriod = 3600,
                            plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                            minCommandPoint = 0,
                            minEconomyPiont = 0,
                            crates = new Dictionary<string, double>
                            {
                                ["assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab"] = 0.4
                            },
                            npcPoint = 2,
                            turretPoint = 2,
                            flameTurretPoint = 2,
                            bradleyPoint = 5,
                            doorPoint = 1,
                            redCardPoint = 5,
                            blueCardPoint = 4,
                            greenCardPoint = 3,
                            spaceCardPoint = 10,
                            commands = new HashSet<string>()
                        },
                        pveMode = new PveModeConfig
                        {
                            enable = false,
                            showEventOwnerNameOnMap = true,
                            damage = 500f,
                            scaleDamage = new Dictionary<string, float>
                            {
                                ["Npc"] = 1f,
                                ["Bradley"] = 2f,
                                ["Turret"] = 1f
                            },
                            lootCrate = false,
                            hackCrate = false,
                            lootNpc = false,
                            damageNpc = false,
                            targetNpc = false,
                            targetTank = false,
                            damageTank = false,
                            canEnterCooldownPlayer = true,
                            timeExitOwner = 300,
                            alertTime = 60,
                            restoreUponDeath = true,
                            cooldownOwner = 86400,
                            darkening = 5
                        },
                        nightVisionConfig = new NightVisionConfig
                        {
                            enable = false,
                            time = 19.5f
                        }
                    }
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.SpaceExtensionMethods
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

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static void ClearItemsContainer(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }

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

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
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
    }
}