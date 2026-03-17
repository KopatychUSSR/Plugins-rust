using System.Collections.Generic;
using Newtonsoft.Json;
using CompanionServer.Handlers;
using UnityEngine;
using Oxide.Core.Plugins;
using Network;
using Rust;
using Rust.Modular;
using System.Collections;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using System;
using Oxide.Plugins.ConvoyExtensionMethods;
using Time = UnityEngine.Time;
using System.Reflection;
using static BaseVehicle;

namespace Oxide.Plugins
{
    [Info("Convoy", "Adem", "2.4.1")]
    class Convoy : RustPlugin
    {
        [PluginReference] Plugin NpcSpawn, GUIAnnouncements, DiscordMessages, PveMode, Economics, ServerRewards, IQEconomic, DynamicPVP;

        #region Variables
        const bool en = false;
        static Convoy ins;
        HashSet<string> subscribeMetods = new HashSet<string>
        {
            "OnCustomNpcTarget",
            "OnEntitySpawned",
            "OnEntityTakeDamage",
            "OnEntityKill",
            "OnPlayerDeath",
            "OnEntityDeath",
            "CanHackCrate",
            "OnLootEntity",
            "CanMountEntity",
            "OnHelicopterRetire",
            "CanHelicopterTarget",
            "CanBradleyApcTarget",
            "OnTurretTarget",
            "OnExplosiveThrown",
            "OnCorpsePopulate",

            "OnCustomNpcTarget",
            "CanPopulateLoot",
            "OnCustomLootContainer",
            "CanEntityTakeDamage",
            "OnCreateDynamicPVP",
            "OnBotReSpawnCrateDropped",
            "CanBradleySpawnNpc",
            "CanHelicopterSpawnNpc",
        };
        ConvoyController convoyController;
        HashSet<ulong> bradleyOrHeliContainers = new HashSet<ulong>();
        #endregion Variables

        #region API
        private bool IsConvoyVehicle(BaseEntity entity)
        {
            if (entity == null || convoyController == null) return false;
            return convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value) != null;
        }

        private bool IsConvoyCrate(HackableLockedCrate crate)
        {
            if (crate == null || convoyController == null) return false;
            return convoyController.GetTruckByCrateNetID(crate.net.ID.Value) != null;
        }

        private bool IsConvoyHeli(PatrolHelicopter patrolHelicopter)
        {
            if (patrolHelicopter == null || patrolHelicopter.net == null) return false;
            return convoyController != null && convoyController.IsConvoyHeli(patrolHelicopter.net.ID.Value);
        }
        #endregion API

        #region Hooks
        void Init()
        {
            ins = this;
            UpdateConfig();
            Unsubscribes();
            LoadData();
        }

        void OnServerInitialized()
        {
            if (!NpcManager.CheckNPCSpawn())
            {
                PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt");
                NextTick(() => Server.Command($"o.unload {Name}"));
            }

            LoadDefaultMessages();
            CheckZeroCoord();
            ConvoyLauncher.AutoStartEvent();
        }

        void Unload()
        {
            ConvoyLauncher.StopEvent(true);
            RootCar.RootStop();
        }

        void OnEntitySpawned(LockedByEntCrate crate)
        {
            if (crate == null) return;

            if (crate.ShortPrefabName == "bradley_crate")
            {
                ConvoyBradley convoyBradley = convoyController.GetConvoyBradleyAtPosition(crate.transform.position);
                if (convoyBradley == null) return;

                NextTick(() => LootManager.UpdateConvoyLockedByEntCrate(crate, convoyBradley.bradleyConfig.offDelay, convoyBradley.bradleyConfig.typeLootTable, convoyBradley.bradleyConfig.lootTable));
                bradleyOrHeliContainers.Add(crate.net.ID.Value);
            }
            else if (crate.ShortPrefabName == "heli_crate")
            {
                ConvoyHeli convoyHeli = convoyController.GetCConvoyHeliAtPosition(crate.transform.position);
                if (convoyHeli == null) return;

                NextTick(() => LootManager.UpdateConvoyLockedByEntCrate(crate, convoyHeli.heliConfig.offDelay, convoyHeli.heliConfig.typeLootTable, convoyHeli.heliConfig.lootTable));
                bradleyOrHeliContainers.Add(crate.net.ID.Value);
            }
        }

        void OnEntitySpawned(HelicopterDebris entity)
        {
            if (!entity.IsExists()) return;

            if (convoyController.GetConvoyBradleyAtPosition(entity.transform.position) != null || convoyController.GetCConvoyHeliAtPosition(entity.transform.position) != null)
            {
                Rigidbody rigidbody = entity.gameObject.GetComponent<Rigidbody>();
                rigidbody.mass = 1;
            }
        }

        object OnTurretTarget(AutoTurret turret, ScientistNPC scientistNPC)
        {
            if (turret == null || scientistNPC == null)
                return null;

            if (NpcManager.IsConvoyNpc(scientistNPC.displayName))
            {
                if (!_config.enablePlayerTurret)
                    return true;
                else if(scientistNPC.isMounted)
                    return true;
                else if (!convoyController.IsPlayerTurretCanAttackConvoy(turret))
                    return true;
            }

            return null;
        }

        object OnEntityTakeDamage(BaseVehicleModule baseVehicleModule, HitInfo info)
        {
            if (baseVehicleModule == null || info == null) return null;

            BaseModularVehicle modularVehicle = baseVehicleModule.Vehicle;
            if (modularVehicle == null || modularVehicle.net == null) return null;

            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(modularVehicle.net.ID.Value);
            if (convoyVehicle == null) return null;

            if (convoyController.CanConvoyEntityTakeDamage(info, baseVehicleModule))
            {
                ConvoyModular convoyModular = convoyVehicle as ConvoyModular;
                modularVehicle.health -= convoyModular.supportModularConfig.damageMultiplier * info.damageTypes.Total() / 5;

                if (!modularVehicle.IsDestroyed && modularVehicle.health <= 0)
                    modularVehicle.Kill(BaseNetworkable.DestroyMode.Gib);
                else
                {
                    for (int i = 0; i <= modularVehicle.moduleSockets.Count; i++)
                    {
                        BaseVehicleModule module;
                        if (modularVehicle.TryGetModuleAt(i, out module))
                            module.SetHealth(module._maxHealth * modularVehicle.health / modularVehicle._maxHealth);
                    }
                }

                if (info.InitiatorPlayer != null)
                    convoyController.OnConvoyAttacked(info.InitiatorPlayer);

                return null;
            }
            else return true;
        }

        object OnEntityTakeDamage(ModularCar entity, HitInfo info)
        {
            if (entity == null || info == null || entity.net == null) return null;

            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value);
            if (convoyVehicle == null) return null;

            if (convoyController.CanConvoyEntityTakeDamage(info, entity))
            {
                ConvoyModular convoyModular = convoyVehicle as ConvoyModular;
                info.damageTypes.ScaleAll(convoyModular.supportModularConfig.damageMultiplier);
                convoyController.OnConvoyAttacked(info.InitiatorPlayer);
                return null;
            }
            else return true;
        }

        object OnEntityTakeDamage(BasicCar entity, HitInfo info)
        {
            if (entity == null || info == null || entity.net == null) return null;

            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value);
            if (convoyVehicle == null) return null;

            if (convoyController.CanConvoyEntityTakeDamage(info, entity))
            {
                convoyController.OnConvoyAttacked(info.InitiatorPlayer);
                return null;
            }
            else return true;
        }

        object OnEntityTakeDamage(BradleyAPC entity, HitInfo info)
        {
            if (entity == null || info == null || entity.net == null) return null;

            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value);
            if (convoyVehicle == null) return null;

            if (convoyController.CanConvoyEntityTakeDamage(info, entity))
            {
                if (info.InitiatorPlayer.IsRealPlayer()) convoyController.OnConvoyAttacked(info.InitiatorPlayer);
                return null;
            }
            else return true;
        }

        object OnEntityTakeDamage(ScientistNPC entity, HitInfo info)
        {
            if (info == null || entity == null || !info.InitiatorPlayer.IsRealPlayer())
                return null;

            if (NpcManager.IsConvoyNpc(entity.displayName) && convoyController.CanConvoyNpcTakeDamage(info))
            {
                BaseVehicle baseVehicle = entity.GetMountedVehicle();

                if (baseVehicle != null && baseVehicle.IsDriver(entity))
                {
                    ins.NextTick(() =>
                    {
                        if (convoyController != null) 
                            convoyController.OnConvoyAttacked(info.InitiatorPlayer);
                    });
                    return true;
                }

                ins.NextTick(() =>
                {
                    if (convoyController != null)
                        convoyController.OnConvoyAttacked(info.InitiatorPlayer);
                });
                return null;
            }

            return null;
        }

        object OnEntityTakeDamage(PatrolHelicopter entity, HitInfo info)
        {
            if (entity == null || info == null)
                return null;

            if (IsConvoyHeli(entity) && info.InitiatorPlayer != null)
            {
                if (!convoyController.CanConvoyEntityTakeDamage(info, entity))
                {
                    info.damageTypes.ScaleAll(0);
                    return true;
                }
                else
                {
                    convoyController.OnConvoyAttacked(info.InitiatorPlayer);
                }
            }

            return null;
        }

        void OnEntityTakeDamage(BuildingBlock buildingBlock, HitInfo info)
        {
            if (buildingBlock == null || info.PointStart == null) return;
            if (info.WeaponPrefab != null && info.WeaponPrefab.name == "MainCannonShell")
            {
                if (!convoyController.convoyVehicles.Any(x => x != null && Vector3.Distance(info.PointStart, x.baseEntity.transform.position) < _config.maxDamageDistance * 2)) return;
                info.damageTypes.ScaleAll(100 * _config.bradleyBuildingDamageScale);
                return;
            }

            if (info.Initiator == null || info.Initiator.net == null)
                return;
            BradleyAPC bradley = info.Initiator as BradleyAPC;
            if (bradley != null)
            {
                ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(bradley.net.ID.Value);
                if (convoyVehicle != null)
                    info.damageTypes.ScaleAll(_config.bradleyBuildingDamageScale);
            }
        }

        void OnEntityKill(ModularCar entity)
        {
            if (entity == null || entity.net == null) return;
            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value);
            if (convoyVehicle == null) return;

            convoyController.OnConvoyVehicleDie(convoyVehicle);

            ConvoyTruck convoyTruck = convoyVehicle as ConvoyTruck;
            if (convoyTruck != null)
            {
                if (_config.dropCrateAfterKillTruck)
                    convoyTruck.DropCrate();
                else
                    NotifyManager.SendMessageToAll("Failed", ins._config.prefix);

                ins.NextTick(convoyController.CheckConvoyHasNoLootedTruck);
            }
        }

        void OnEntityKill(BradleyAPC entity)
        {
            if (entity == null || entity.net == null) return;
            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value);
            if (convoyVehicle == null) return;
            convoyController.OnConvoyVehicleDie(convoyVehicle);

            ConvoyBradley convoyBradley = convoyVehicle as ConvoyBradley;
            if (ins._config.pveMode.pve && ins.plugins.Exists("PveMode"))
            {
                timer.In(1f, () =>
                {
                    ins.PveMode.Call("EventAddCrates", ins.Name, ins.bradleyOrHeliContainers);
                    ins.bradleyOrHeliContainers.Clear();
                });
            }
        }

        void OnEntityKill(BasicCar entity)
        {
            if (entity == null || entity.net == null) return;
            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value);
            if (convoyVehicle == null) return;
            convoyController.OnConvoyVehicleDie(convoyVehicle);
        }

        void OnEntityKill(PatrolHelicopter entity)
        {
            if (entity == null) return;

            if (!IsConvoyHeli(entity)) return;
            if (_config.pveMode.pve && plugins.Exists("PveMode"))
            {
                timer.In(1f, () =>
                {
                    PveMode.Call("EventAddCrates", Name, bradleyOrHeliContainers);
                    ins.bradleyOrHeliContainers.Clear();
                });
            }
        }

        void OnEntityKill(ScientistNPC scientistNPC)
        {
            if (scientistNPC == null || scientistNPC.net == null || !NpcManager.IsConvoyNpc(scientistNPC.displayName) || scientistNPC.isMounted) return;

            if (ins._config.blockSpawnDieNpc)
            {
                ConvoyVehicle convoyVehicle = convoyController.convoyVehicles.FirstOrDefault(x => x != null && x.roamNpcsNetIDs.Any(y => y == scientistNPC.net.ID.Value));
                if (convoyVehicle != null) convoyVehicle.countDieNpc++;
            }

            if (convoyController.IsConvoyStop() && _config.needKillNpc)
            {
                convoyController.roamNpc.Remove(scientistNPC);
                if (convoyController.roamNpc.Count == 0)
                {
                    NotifyManager.SendMessageToAll("SecurityNpcKill", _config.prefix);
                }
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;

            if (ZoneController.IsPlayerInZone(player.userID))
                ZoneController.OnPlayerLeaveZone(player);
        }

        void OnEntityDeath(ModularCar entity, HitInfo info)
        {
            if (entity == null || entity.net == null || info == null || !info.InitiatorPlayer.IsRealPlayer()) return;
            EconomyManager.CheckIfPlayerKillConvoyVehicle(entity.net.ID.Value, "Modular", info.InitiatorPlayer);
        }

        void OnEntityDeath(BradleyAPC entity, HitInfo info)
        {
            if (entity == null || entity.net == null || info == null || !info.InitiatorPlayer.IsRealPlayer()) return;
            EconomyManager.CheckIfPlayerKillConvoyVehicle(entity.net.ID.Value, "Bradley", info.InitiatorPlayer);
        }

        void OnEntityDeath(BasicCar entity, HitInfo info)
        {
            if (entity == null || entity.net == null || info == null || !info.InitiatorPlayer.IsRealPlayer()) return;
            EconomyManager.CheckIfPlayerKillConvoyVehicle(entity.net.ID.Value, "Sedan", info.InitiatorPlayer);
        }

        void OnEntityDeath(PatrolHelicopter entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null) return;
            if (IsConvoyHeli(entity)) EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Heli");
        }

        void OnEntityDeath(ScientistNPC scientistNPC, HitInfo info)
        {
            if (scientistNPC == null || scientistNPC.net == null || !NpcManager.IsConvoyNpc(scientistNPC.displayName)) return;

            if (info != null && info.InitiatorPlayer.IsRealPlayer()) EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Npc");
        }

        object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate == null || player == null) return null;
            ConvoyTruck convoyTruck = convoyController.GetTruckByCrateNetID(crate.net.ID.Value);
            if (convoyTruck == null) return null;

            if (player.InSafeZone()) return true;

            else if ((!_config.needStopConvoy || convoyController.IsConvoyStop()) && (!_config.needKillCars || !convoyController.ConvoyHaveAnySecurityVehivcle()) && (!_config.needKillNpc || !convoyController.roamNpc.Any(x => x.IsExists())))
            {
                if (_config.pveMode.pve && plugins.Exists("PveMode") && !_config.pveMode.hackCrate && PveMode.Call("CanActionEvent", Name, player) != null) return true;
                NotifyManager.SendMessageToAll("StartHackCrate", _config.prefix, player.displayName);
                convoyController.OnConvoyAttacked(player);
                convoyTruck.UpdateCrateHackTimeWithDelay();
                return null;
            }
            else
            {
                NotifyManager.SendMessageToPlayer(player, "CantHackCrate", _config.prefix);
                return true;
            }
        }

        void OnLootEntity(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null || crate.net == null) return;

            ConvoyTruck convoyTruck = convoyController.GetTruckByCrateNetID(crate.net.ID.Value);
            if (convoyTruck != null) convoyController.OnCrateLooted(crate.net.ID.Value);
        }

        object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (!player.IsRealPlayer() || entity == null) return null;

            BaseEntity vehicle = entity.VehicleParent();
            if (vehicle != null & IsConvoyVehicle(vehicle)) return true;
            return null;
        }

        object OnHelicopterRetire(PatrolHelicopterAI ai)
        {
            if (convoyController.convoyHeli != null && convoyController.convoyHeli.patrolHelicopterAI == ai) return true;
            return null;
        }

        object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (heli == null || heli.helicopterBase == null) return null;
            if (IsConvoyHeli(heli.helicopterBase))
            {
                if (!player.IsRealPlayer() || player.IsSleeping()) return false;
                else if (!_config.isAggressive && !convoyController.IsConvoyStop() && !convoyController.IsAnyCrateHacking()) return false;
            }
            return null;
        }

        object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (apc == null || entity == null || apc.net == null) return null;

            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(apc.net.ID.Value);
            if (convoyVehicle == null) return null;

            if (!_config.isAggressive && !convoyController.IsConvoyStop() && !convoyController.IsAnyCrateHacking()) return false;

            BasePlayer player = entity as BasePlayer;
            if (!player.IsRealPlayer() || player.IsSleeping() || player.InSafeZone()) return false;

            return null;
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            if (!player.IsRealPlayer()) return;
            if (convoyController.convoyVehicles.Any(x => x != null && x.baseEntity != null && Vector3.Distance(player.transform.position, x.baseEntity.transform.position) < 25f))
                convoyController.OnConvoyAttacked(player);
        }

        void OnCorpsePopulate(ScientistNPC scientistNPC, NPCPlayerCorpse corpse)
        {
            if (scientistNPC == null || corpse == null) 
                return;

            NpcConfig npcConfig = NpcManager.GetNpcConfigByDisplayName(scientistNPC.displayName);
            if (npcConfig == null) 
                return;

            NextTick(() =>
            {
                if (corpse != null) 
                    LootManager.UpdateNpcLootContainer(corpse, npcConfig);
            });
        }
        #region OtherPLugins
        object OnCustomNpcTarget(ScientistNPC npc, BasePlayer player)
        {
            if (convoyController == null || npc == null) return null;
            if (!convoyController.IsConvoyStop() && !_config.isAggressive && NpcManager.IsConvoyNpc(npc.displayName)) return false;
            return null;
        }

        object CanPopulateLoot(LootContainer container)
        {
            if (convoyController == null || container == null) return null;

            if (container is HackableLockedCrate)
            {
                ConvoyTruck convoyTruck = convoyController.GetTruckByCrateNetID(container.net.ID.Value);
                if (convoyTruck == null) return null;
                if (convoyTruck.modularConfig.typeLootTable == 2) return null;
                else return true;
            }

            else if (container.ShortPrefabName == "bradley_crate")
            {
                ConvoyBradley convoyBradley = convoyController.GetConvoyBradleyAtPosition(container.transform.position);
                if (convoyBradley == null) return null;
                if (convoyBradley.bradleyConfig.typeLootTable == 2) return null;
                else return true;
            }

            else if (container.ShortPrefabName == "heli_crate")
            {
                ConvoyHeli convoyHeli = convoyController.GetCConvoyHeliAtPosition(container.transform.position);
                if (convoyHeli == null) return null;
                if (convoyHeli.heliConfig.typeLootTable == 2) return null;
                else return true;
            }

            else return null;
        }

        object CanPopulateLoot(ScientistNPC scientistNPC, NPCPlayerCorpse corpse)
        {
            if (convoyController == null || scientistNPC == null || corpse == null || convoyController.convoyVehicles.Count == 0) return null;
            if (convoyController.convoyVehicles.Any(x => x != null && x.scientists.Contains(scientistNPC)))
            {
                NpcConfig npcConfig = _config.NPC.FirstOrDefault(x => x.name == scientistNPC.name);
                if (npcConfig == null) return null;
                if (npcConfig.typeLootTable == 2) return null;
                else return true;
            }
            return null;
        }

        object OnCustomLootContainer(NetworkableId netID)
        {
            if (convoyController == null || netID == null) return null;
            ConvoyTruck convoyTruck = convoyController.GetTruckByCrateNetID(netID.Value);
            if (convoyTruck == null) return null;
            if (convoyTruck.modularConfig.typeLootTable != 3) return true;
            return null;
        }

        object CanEntityBeTargeted(ScientistNPC scientistNPC, AutoTurret turret)
        {
            if (turret == null || scientistNPC == null)
                return null;

            if (NpcManager.IsConvoyNpc(scientistNPC.displayName))
            {
                if (!_config.enablePlayerTurret)
                    return false;
                if (scientistNPC.isMounted)
                    return false;
                else if (!convoyController.IsPlayerTurretCanAttackConvoy(turret))
                    return false;
            }

            return null;
        }

        object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (convoyController == null || !_config.eventZone.isCreateZonePVP || hitinfo == null || !victim.IsRealPlayer()) return null;

            if (ZoneController.IsPlayerInZone(victim.userID) && hitinfo.InitiatorPlayer.IsRealPlayer() && ZoneController.IsPlayerInZone(hitinfo.InitiatorPlayer.userID)) return true;
            else return null;
        }

        object CanEntityTakeDamage(ScientistNPC entity, HitInfo info)
        {
            if (info == null || entity == null || !info.InitiatorPlayer.IsRealPlayer())
                return null;

            if (NpcManager.IsConvoyNpc(entity.displayName) && convoyController.CanConvoyNpcTakeDamage(info))
            {
                BaseVehicle baseVehicle = entity.GetMountedVehicle();

                if (baseVehicle != null && baseVehicle.IsDriver(entity))
                {
                    convoyController.OnConvoyAttacked(info.InitiatorPlayer);
                    return false;
                }

                convoyController.OnConvoyAttacked(info.InitiatorPlayer);
                return null;
            }

            return null;
        }

        object CanEntityTakeDamage(BasicCar victim, HitInfo hitinfo)
        {
            if (convoyController == null || victim == null || hitinfo == null) return null;

            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(victim.net.ID.Value);
            if (convoyVehicle == null) return null;

            if (convoyController.CanConvoyEntityTakeDamage(hitinfo, victim)) return true;
            else return false;
        }

        object CanEntityTakeDamage(ModularCar victim, HitInfo hitinfo)
        {
            if (convoyController == null || victim == null || victim.net == null || hitinfo == null || !hitinfo.InitiatorPlayer.IsRealPlayer()) return null;
            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(victim.net.ID.Value);
            if (convoyVehicle == null) return null;
            if (!PveModeAllowAction(hitinfo.InitiatorPlayer)) return false;
            return true;
        }

        object CanEntityTakeDamage(CustomBradley victim, HitInfo hitinfo)
        {
            if (convoyController == null || victim == null || victim.net == null || hitinfo == null || !hitinfo.InitiatorPlayer.IsRealPlayer()) return null;
            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(victim.net.ID.Value);
            if (convoyVehicle != null) return true;
            return null;
        }

        object CanEntityTakeDamage(BaseVehicleModule baseVehicleModule, HitInfo hitinfo)
        {
            if (convoyController == null || baseVehicleModule == null || baseVehicleModule.Vehicle == null || baseVehicleModule.Vehicle.net == null || hitinfo == null || !hitinfo.InitiatorPlayer.IsRealPlayer()) return null;
            ConvoyVehicle convoyVehicle = convoyController.GetConvoyVehicleByEntityNetId(baseVehicleModule.Vehicle.net.ID.Value);
            if (convoyVehicle == null) return null;
            if (!PveModeAllowAction(hitinfo.InitiatorPlayer)) return false;
            return true;
        }

        object CanEntityTakeDamage(PatrolHelicopter entity, HitInfo hitinfo)
        {
            if (entity == null || hitinfo == null) return null;
            if (IsConvoyHeli(entity) && hitinfo.InitiatorPlayer != null)
            {
                if (!convoyController.CanConvoyEntityTakeDamage(hitinfo, entity))
                    return false;
            }

            return null;
        }

        object OnCreateDynamicPVP(string eventName, BradleyAPC entity)
        {
            if (convoyController == null || entity == null) return null;
            if (IsConvoyVehicle(entity)) return true;
            return null;
        }

        object OnCreateDynamicPVP(string eventName, PatrolHelicopter entity)
        {
            if (convoyController == null || entity == null) return null;
            if (IsConvoyHeli(entity)) return true;
            return null;
        }

        object OnBotReSpawnCrateDropped(HackableLockedCrate crate)
        {
            if (convoyController == null || crate == null || crate.net == null) return null;
            if (IsConvoyCrate(crate)) return true;
            return null;
        }

        object CanBradleySpawnNpc(BradleyAPC bradley)
        {
            if (convoyController == null || _config.betterNpcConfig.bradleyNpc || bradley == null || bradley.net == null) return null;
            if (convoyController.GetConvoyVehicleByEntityNetId(bradley.net.ID.Value) != null) return true;
            return null;
        }

        object CanHelicopterSpawnNpc(PatrolHelicopter helicopter)
        {
            if (convoyController == null || _config.betterNpcConfig.heliNpc || helicopter == null) return null;
            if (IsConvoyHeli(helicopter)) return true;
            return null;
        }

        private object OnRestoreUponDeath(BasePlayer player)
        {
            if (player == null)
                return null;

            if (_config.eventZone.blockRestoreUponDeath && ZoneController.IsPlayerInZone(player.userID))
                return false;

            return null;
        }
        #endregion OtherPLugins
        #endregion Hooks

        #region Commands
        [ChatCommand("convoystart")]
        void StartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            if (arg != null && arg.Length >= 1) ConvoyLauncher.StartEvent(player, arg[0]);
            else ConvoyLauncher.StartEvent(player);
        }

        [ChatCommand("convoystop")]
        void StopCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            ConvoyLauncher.StopEvent();
        }

        [ConsoleCommand("convoystart")]
        void ConsoleStartCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            if (arg.Args != null && arg.Args.Length > 0) ConvoyLauncher.StartEvent(null, arg.Args[0]);
            ConvoyLauncher.StartEvent();
            Puts("Event activated");
        }

        [ConsoleCommand("convoystop")]
        void ConsoleStopCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                ConvoyLauncher.StopEvent();
        }

        [ChatCommand("convoyrootstart")]
        void RootStartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || player.isInAir) return;
            RootCar.CreateRootCar(player);
        }

        [ChatCommand("convoyrootstop")]
        void RootStopCommand(BasePlayer player, string command, string[] arg)
        {
            if (player.IsAdmin) RootCar.RootStop();
        }

        [ChatCommand("convoyrootsave")]
        void RootSaveCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;

            if (arg == null || arg.Length == 0)
            {
                NotifyManager.SendMessageToPlayer(player, $"{_config.prefix} To save the route, use the command: <color=#738d43>convoyrootsave [rootpresetname]</color>");
                return;
            }

            RootCar.SaveRoot(player, arg[0]);
        }

        [ChatCommand("convoyroadblock")]
        void RoadBlockCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || player.isInAir) return;

            PathList blockRoad = TerrainMeta.Path.Roads.FirstOrDefault(x => x.Path.Points.Any(y => Vector3.Distance(player.transform.position, y) < 10));
            if (blockRoad == null) NotifyManager.SendMessageToPlayer(player, $"{_config.prefix} Road not found <color=#ce3f27>not found</color>");
            int index = TerrainMeta.Path.Roads.IndexOf(blockRoad);
            if (_config.blockRoads.Contains(index)) NotifyManager.SendMessageToPlayer(player, $"{_config.prefix} The road is already <color=#ce3f27>blocked</color>");
            else if (blockRoad != null)
            {
                _config.blockRoads.Add(index);
                SaveConfig();
                NotifyManager.SendMessageToPlayer(player, $"{_config.prefix} The road with the index <color=#738d43>{index}</color> is <color=#ce3f27>blocked</color>");
            }
        }
        #endregion Commands

        #region Method
        void UpdateConfig()
        {
            if (_config.version != Version.ToString())
            {
                VersionNumber versionNumber;
                var versionArray = _config.version.Split('.');
                versionNumber.Major = Convert.ToInt32(versionArray[0]);
                versionNumber.Minor = Convert.ToInt32(versionArray[1]);
                versionNumber.Patch = Convert.ToInt32(versionArray[2]);
                if (versionNumber.Major == 2)
                {
                    if (versionNumber.Minor == 0)
                    {
                        if (versionNumber.Patch == 0)
                        {
                            foreach (ConvoySetting convoySetting in _config.convoys) convoySetting.displayName = "Convoy";
                        }

                        if (versionNumber.Patch <= 7)
                        {
                            foreach (NpcConfig npcConfig in _config.NPC) npcConfig.kit = "";
                        }

                        if (versionNumber.Patch <= 9)
                        {
                            _config.autoEvent = true;
                        }
                        versionNumber.Minor = 1;
                        versionNumber.Patch = 0;
                    }
                    if (versionNumber.Minor == 1)
                    {
                        if (versionNumber.Patch <= 5)
                        {
                            if (!_config.barriers.Contains("xmasportalentry"))
                            {
                                _config.barriers.Add("xmasportalentry");
                            }
                        }

                        if (versionNumber.Patch < 7)
                        {
                            if (!_config.barriers.Contains("stone-ore")) _config.barriers.Add("stone-ore");
                            if (!_config.barriers.Contains("metal-ore")) _config.barriers.Add("metal-ore");
                            if (!_config.barriers.Contains("sulfur-ore")) _config.barriers.Add("sulfur-ore");
                        }

                        if (versionNumber.Patch <= 9)
                        {
                            _config.pveMode.scaleDamage.Add(new ScaleDamageConfig { Type = "Helicopter", Scale = 1 });
                        }

                        versionNumber.Minor = 2;
                        versionNumber.Patch = 0;
                    }
                    if (versionNumber.Minor == 2)
                    {
                        if (versionNumber.Patch == 0)
                        {
                            PrintWarning("Some parameters in the config have been changed due to the addition of new functions!\nRead the changelog!");

                            _config.roadLength = 300;
                            _config.killConvoyAfterLoot = true;
                            _config.bradleyBuildingDamageScale = 1f;
                            _config.killTimeConvoyAfterLoot = 300;
                            _config.isAggressive = true;
                            _config.timeNotifications = new HashSet<int>
                            {
                                300,
                                60,
                                30,
                                5
                            };
                            for (int i = 0; i < _config.convoys.Count; i++)
                            {
                                ConvoySetting convoySetting = _config.convoys[i];
                                convoySetting.speed = 5f;
                                if (convoySetting.name.Contains("hard"))
                                {
                                    convoySetting.vehiclesOrder = new List<string>
                                    {
                                        "bradley_1",
                                        "modular_1",
                                        "sedan_1",
                                        "truck_1",
                                        "sedan_1",
                                        "modular_1",
                                        "bradley_1"
                                    };
                                }
                                else
                                {
                                    convoySetting.vehiclesOrder = new List<string>
                                    {
                                        "bradley_1",
                                        "sedan_1",
                                        "truck_1",
                                        "sedan_1",
                                        "bradley_1"
                                    };
                                }
                            }

                            for (int i = 0; i < _config.modularConfiguration.Count; i++)
                            {
                                ModularConfig modularConfig = _config.modularConfiguration[i];
                                modularConfig.changeUnlockTime = true;
                            }

                            for (int i = 0; i < _config.NPC.Count; i++)
                            {
                                NpcConfig npcConfig = _config.NPC[i];
                                npcConfig.turretDamageScale = 1;
                                for (int a = 0; a < npcConfig.beltItems.Count; a++)
                                {
                                    NpcBelt npcBelt = npcConfig.beltItems[a];
                                    npcBelt.ammo = "";
                                }
                            }
                        }
                        if (versionNumber.Patch < 9)
                        {
                            _config.marker.useRingMarker = true;
                            _config.marker.useShopMarker = true;
                        }
                        versionNumber.Minor = 3;
                        versionNumber.Patch = 0;
                    }
                    if (versionNumber.Minor == 3)
                    {
                        if (versionNumber.Patch == 0)
                        {
                            foreach (SupportModularConfig supportModularConfig in _config.supportModularConfiguration)
                                supportModularConfig.frontVehicleDistance = 10;
                            foreach (SedanConfig sedanConfig in _config.sedanConfiguration)
                                sedanConfig.frontVehicleDistance = 10;
                            foreach (ModularConfig modularConfig in _config.modularConfiguration)
                                modularConfig.frontVehicleDistance = 10;
                            foreach (BradleyConfig bradleyConfig in _config.bradleyConfiguration)
                                bradleyConfig.frontVehicleDistance = 10;

                            _config.pveMode.showEventOwnerNameOnMap = true;
                        }

                        if (versionNumber.Patch <= 4)
                        {
                            foreach (SupportModularConfig supportModularConfig in _config.supportModularConfiguration)
                                supportModularConfig.numberOfNpc = 4;
                            foreach (SedanConfig sedanConfig in _config.sedanConfiguration)
                                sedanConfig.numberOfNpc = 4;
                            foreach (ModularConfig modularConfig in _config.modularConfiguration)
                                modularConfig.numberOfNpc = 4;
                            foreach (BradleyConfig bradleyConfig in _config.bradleyConfiguration)
                                bradleyConfig.numberOfNpc = 6;
                        }

                        if (versionNumber.Patch <= 7)
                        {
                            foreach (HeliConfig heliConfig in _config.heliesConfiguration)
                            {
                                heliConfig.outsideTime = 30;
                            }
                        }

                        versionNumber.Minor = 4;
                        versionNumber.Patch = 0;
                    }
                    if (versionNumber.Minor == 4)
                    {
                        if (versionNumber.Patch == 0)
                        {
                            _config.enablePlayerTurret = true;
                        }
                    }
                }
                else
                {
                    PrintError("Delete the configuration file!");
                    NextTick(() => Server.Command($"o.unload {Name}"));
                    return;
                }
                _config.version = Version.ToString();
                SaveConfig();
            }
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

        void CheckZeroCoord()
        {
            foreach (Collider collider in UnityEngine.Physics.OverlapSphere(Vector3.zero, 3f))
            {
                BaseEntity entity = collider.ToBaseEntity();
                if (entity == null) continue;
                if (entity.PrefabName.Contains("modular") || entity.PrefabName.Contains("locked")) entity.Kill();
            }
        }

        bool PveModeAllowAction(BasePlayer player)
        {
            if (!ins.plugins.Exists("PveMode") || !plugins.Exists("PveMode")) return true;
            if (ins.PveMode.Call("CanActionEvent", ins.Name, player) != null) return false;
            return true;
        }

        void CopySerializableFields<T>(T src, T dst)
        {
            FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in srcFields)
            {
                object value = field.GetValue(src);
                field.SetValue(dst, value);
            }
        }
        #endregion Method

        #region Classes 
        static class ConvoyLauncher
        {
            static Coroutine autoEventCoroutine;

            internal static void AutoStartEvent()
            {
                if (!ins._config.autoEvent) return;
                if (autoEventCoroutine != null) return;
                autoEventCoroutine = ServerMgr.Instance.StartCoroutine(AutoEventCorountine());
            }

            static IEnumerator AutoEventCorountine()
            {
                yield return CoroutineEx.waitForSeconds(UnityEngine.Random.Range(ins._config.minTimeBetweenEvent, ins._config.maxTimeBetweenEvent));
                StartEvent();
            }

            internal static void StartEvent(BasePlayer initiator = null, string presetName = "")
            {
                if (ins.convoyController != null)
                {
                    NotifyManager.PrintError(initiator, "EventActive_Exeption");
                    return;
                }

                ConvoySetting convoysetting = DefineEventConfig(presetName);
                if (convoysetting == null)
                {
                    NotifyManager.PrintError(initiator, "СonfigurationNotFound_Exeption");
                    StopEvent();
                    return;
                }

                ConvoyPath convoyPath = ConvoyPath.DefineConvoyPath(convoysetting.vehiclesOrder.Count);
                if (convoyPath == null)
                {
                    NotifyManager.PrintError(initiator, "RootNotFound_Exeption", convoysetting.name);
                    StopEvent();
                    return;
                }

                if (autoEventCoroutine != null)
                {
                    ServerMgr.Instance.StopCoroutine(autoEventCoroutine);
                    autoEventCoroutine = null;
                }

                GameObject gameObject = new GameObject();
                ins.convoyController = gameObject.AddComponent<ConvoyController>();
                ins.convoyController.Init(convoysetting, convoyPath);
            }

            static ConvoySetting DefineEventConfig(string eventPresetName)
            {
                if (eventPresetName != "")
                {
                    return ins._config.convoys.FirstOrDefault(x => x.name == eventPresetName);
                }

                else
                {
                    if (!ins._config.convoys.Any(x => x.on && x.chance != 0)) return null;

                    float sumChance = 0;
                    foreach (ConvoySetting eventConfig in ins._config.convoys)
                    {
                        if (eventConfig.on) sumChance += eventConfig.chance;
                    }
                    float random = UnityEngine.Random.Range(0, sumChance);
                    foreach (ConvoySetting eventConfig in ins._config.convoys)
                    {
                        if (eventConfig.on)
                        {
                            random -= eventConfig.chance;
                            if (random <= 0) return eventConfig;
                        }
                    }

                    return null;
                }
            }

            internal static void StopEvent(bool isPluginUnloading = false)
            {
                if (ins.convoyController != null)
                {
                    ins.Unsubscribes();
                    GameObject.Destroy(ins.convoyController.gameObject);
                    ZoneController.ClearZoneData();
                    EconomyManager.SendBalance();
                    NotifyManager.SendMessageToAll("Finish", ins._config.prefix);
                    Interface.CallHook("OnConvoyStop");
                    if (ins._config.enableStartStopLogs)
                        NotifyManager.PrintLogMessage("EventStop_Log");
                }

                if (!isPluginUnloading)
                    AutoStartEvent();
                else if (autoEventCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(autoEventCoroutine);
            }
        }

        sealed class ConvoyController : FacepunchBehaviour
        {
            internal ConvoySetting convoySetting;

            internal List<ConvoyVehicle> convoyVehicles = new List<ConvoyVehicle>();
            internal HashSet<ConvoyTruck> trucks = new HashSet<ConvoyTruck>();
            internal List<ScientistNPC> roamNpc = new List<ScientistNPC>();
            HashSet<ulong> lootedCrates = new HashSet<ulong>();
            internal ConvoyHeli convoyHeli;

            HashSet<ulong> convoyAttackersIds = new HashSet<ulong>();

            ConvoyPath convoyPath;
            ConvoyMarker convoyMarker;
            Coroutine stopCoroutine;
            Coroutine eventCoroutine;

            int eventTime;
            int stopTime;

            #region API
            internal bool CanConvoyEntityTakeDamage(HitInfo info, BaseEntity entity)
            {
                if (info.Initiator != null && info.Initiator is AutoTurret)
                    return true;

                if (!info.InitiatorPlayer.IsRealPlayer()) return false;
                if (!ins.PveModeAllowAction(info.InitiatorPlayer)) return false;
                if (entity is PatrolHelicopter)
                {
                    if (Vector3.Distance(info.InitiatorPlayer.transform.position, entity.transform.position) >= ins._config.maxDamageDistance * 2)
                    {
                        NotifyManager.SendMessageToPlayer(info.InitiatorPlayer, "Distance", info.InitiatorPlayer.UserIDString, ins._config.prefix);
                        return false;
                    }
                }
                else if (Vector3.Distance(info.InitiatorPlayer.transform.position, entity.transform.position) >= ins._config.maxDamageDistance)
                {
                    NotifyManager.SendMessageToPlayer(info.InitiatorPlayer, "Distance", info.InitiatorPlayer.UserIDString, ins._config.prefix);
                    return false;
                }
                return true;
            }

            internal bool CanConvoyNpcTakeDamage(HitInfo info)
            {
                if (info.ProjectileDistance >= ins._config.maxDamageDistance)
                {
                    NotifyManager.SendMessageToPlayer(info.InitiatorPlayer, "Distance", info.InitiatorPlayer.UserIDString, ins._config.prefix);
                    return false;
                }
                return true;
            }

            internal void OnConvoyVehicleDie(ConvoyVehicle convoyVehicle)
            {
                DefineFollow(convoyVehicle);

                if (!ConvoyHaveAnySecurityVehivcle())
                {
                    OnConvoyAttacked();
                    NotifyManager.SendMessageToAll("VehiclesKill", ins._config.prefix);
                }
                convoyVehicles.Remove(convoyVehicle);
            }

            internal ConvoyVehicle GetConvoyVehicleByEntityNetId(ulong netID)
            {
                return convoyVehicles.FirstOrDefault(x => x != null && x.baseEntity != null && x.baseEntity.net.ID.Value == netID);
            }

            internal ConvoyTruck GetTruckByCrateNetID(ulong crateNetID)
            {
                return trucks.FirstOrDefault(x => x != null && x.IsOwnCrate(crateNetID));
            }

            internal Vector3 GetConvoyPosition()
            {
                if (convoyVehicles == null || convoyVehicles.Count == 0)
                    return Vector3.zero;

                ConvoyVehicle frontVehicle = convoyVehicles.FirstOrDefault(x => x != null && x.baseEntity != null);
                ConvoyVehicle lastVehicle = convoyVehicles.Last();

                if (frontVehicle != null && lastVehicle != null && lastVehicle.baseEntity != null)
                    return (frontVehicle.baseEntity.transform.position + lastVehicle.baseEntity.transform.position) / 2;

                else if (frontVehicle != null)
                    return frontVehicle.baseEntity.transform.position;

                return Vector3.zero;
            }

            internal bool ConvoyHaveAnySecurityVehivcle()
            {
                return convoyVehicles.Any(x => x != null && x.baseEntity != null && x is ConvoyTruck == false);
            }

            internal bool IsAnyCrateHacking()
            {
                return trucks.Any(x => x != null && x.IsCrateHacking());
            }

            internal bool IsConvoyHeli(ulong heliNetId)
            {
                return convoyHeli != null && convoyHeli.patrolHelicopter != null && convoyHeli.patrolHelicopter.net != null && convoyHeli.patrolHelicopter.net.ID.Value == heliNetId;
            }

            internal bool IsConvoyStop()
            {
                return stopTime > 0;
            }

            internal bool IsPlayerTurretCanAttackConvoy(AutoTurret autoTurret)
            {
                return convoyAttackersIds.Any(x => autoTurret.IsAuthed(x));
            }

            internal int GetEventTime()
            {
                return eventTime;
            }

            internal ConvoyBradley GetConvoyBradleyAtPosition(Vector3 position)
            {
                ConvoyVehicle bradleyVehicle = convoyVehicles.FirstOrDefault(x => x != null && x is ConvoyBradley && Vector3.Distance(position, x.baseEntity.transform.position) < 7.5f);

                if (bradleyVehicle != null) return bradleyVehicle as ConvoyBradley;
                return null;
            }

            internal ConvoyHeli GetCConvoyHeliAtPosition(Vector3 position)
            {
                if (convoyHeli != null && Vector3.Distance(position, convoyHeli.patrolHelicopter.transform.position) < 10f) return convoyHeli;
                return null;
            }

            internal void OnCrateLooted(ulong crateID)
            {
                lootedCrates.Add(crateID);
                CheckConvoyHasNoLootedTruck();
            }

            internal void CheckConvoyHasNoLootedTruck()
            {
                if (!ins._config.killConvoyAfterLoot) return;
                if (!ConvoyHaveUnlootedCrate())
                {
                    if (eventTime > ins._config.killTimeConvoyAfterLoot)
                        eventTime = ins._config.killTimeConvoyAfterLoot + 1;
                }
            }

            bool ConvoyHaveUnlootedCrate()
            {
                if (trucks.Any(x => x != null && x.crate != null && x.crate.net != null && !lootedCrates.Contains(x.crate.net.ID.Value))) return true;
                return false;
            }
            #endregion API

            internal void Init(ConvoySetting convoySetting, ConvoyPath convoyPath)
            {
                this.convoySetting = convoySetting;
                this.convoyPath = convoyPath;
                eventTime = ins._config.eventTime;

                eventCoroutine = ServerMgr.Instance.StartCoroutine(EventCounter());
            }

            IEnumerator EventCounter()
            {
                NotifyManager.SendMessageToAll("PreStart", ins._config.prefix, ins._config.preStartTime);
                yield return CoroutineEx.waitForSeconds(ins._config.preStartTime);

                if (ins._config.enableStartStopLogs)
                    NotifyManager.PrintLogMessage("EventStart_Log", convoySetting.name);
                CreateConvoy();
                if (ins._config.marker.IsMarker) convoyMarker = ConvoyMarker.CreateConvoyMarker();
                ins.Subscribes();

                Interface.CallHook("OnConvoyStart");
                NotifyManager.SendMessageToAll("EventStart", ins._config.prefix, convoySetting.displayName);

                while (eventTime > 0 || (ins._config.dontStopEventIfPlayerInZone && ZoneController.AnyPlayerInZone()))
                {
                    if (eventTime > 0)
                        eventTime--;

                    SendRemainTimeMessage();

                    yield return CoroutineEx.waitForSeconds(1f);
                }
                ConvoyLauncher.StopEvent();
            }

            void SendRemainTimeMessage()
            {
                if (ins._config.timeNotifications.Contains(eventTime))
                {
                    NotifyManager.SendMessageToAll("PreFinish", ins._config.prefix, eventTime);
                }
            }

            void CreateConvoy()
            {
                int firstpoint = convoyPath.spawnPointIndex;

                for (int i = convoySetting.vehiclesOrder.Count - 1; i >= 0; i--)
                {
                    string vehiclePresetName = convoySetting.vehiclesOrder[i];
                    int secondpoint = 0;
                    DefineNextPathPoint(ref firstpoint, ref secondpoint);
                    SpawnConvoyVehicle(vehiclePresetName, firstpoint, secondpoint);
                    firstpoint += convoyPath.countPointsBetweenCars;
                }

                convoyVehicles.Reverse();

                if (convoySetting.heliOn)
                {
                    HeliConfig heliConfig = ins._config.heliesConfiguration.FirstOrDefault(x => x.presetName == convoySetting.heliConfigurationName);

                    if (heliConfig != null)
                        convoyHeli = ConvoyHeli.CreateHelicopter(heliConfig, convoyPath.currentPath[firstpoint]);
                }
            }

            void DefineNextPathPoint(ref int firstPoint, ref int endPoint)
            {
                if (firstPoint > convoyPath.currentPath.Count - 1)
                {
                    if (convoyPath.round)
                        firstPoint = firstPoint - convoyPath.currentPath.Count + 1;
                    else
                    {
                        ins.PrintError("Insufficient route length!");
                        ConvoyLauncher.StopEvent();
                    }
                }

                endPoint = firstPoint + 1;
                if (endPoint > convoyPath.currentPath.Count - 1)
                {
                    if (convoyPath.round)
                        endPoint = endPoint - convoyPath.currentPath.Count + 1;
                    else
                    {
                        ins.PrintError("Insufficient route length!");
                        ConvoyLauncher.StopEvent();
                    }
                }
            }

            void SpawnConvoyVehicle(string vehiclePresetName, int startPoint, int targetPoint)
            {
                BradleyConfig bradleyConfig = ins._config.bradleyConfiguration.FirstOrDefault(x => x.presetName == vehiclePresetName);
                ConvoyVehicle convoyVehicle = null;

                if (bradleyConfig != null)
                {
                    convoyVehicle = ConvoyBradley.CreateBradley(startPoint, targetPoint, bradleyConfig, convoyPath);
                    goto StartSpawn;
                }

                SedanConfig sedanConfig = ins._config.sedanConfiguration.FirstOrDefault(x => x.presetName == vehiclePresetName);
                if (sedanConfig != null)
                {
                    convoyVehicle = ConvoySedan.CreateSedan(startPoint, targetPoint, sedanConfig, convoyPath);
                    goto StartSpawn;
                }

                SupportModularConfig supportModularConfig = ins._config.supportModularConfiguration.FirstOrDefault(x => x.presetName == vehiclePresetName);
                if (supportModularConfig != null)
                {
                    convoyVehicle = ConvoyModular.CreateModular(supportModularConfig, startPoint, targetPoint, convoyPath);
                    goto StartSpawn;
                }

                ModularConfig modularConfig = ins._config.modularConfiguration.FirstOrDefault(x => x.presetName == vehiclePresetName);
                if (modularConfig != null)
                {
                    convoyVehicle = ConvoyTruck.CreateTruck(modularConfig, startPoint, targetPoint, convoyPath);
                    if (convoyVehicle != null) trucks.Add(convoyVehicle as ConvoyTruck);
                    goto StartSpawn;
                }

            StartSpawn:
                if (convoyVehicle != null)
                    convoyVehicles.Add(convoyVehicle);
            }

            void StartConvoy()
            {
                if (IsAnyCrateHacking() || !ConvoyHaveUnlootedCrate())
                    return;

                convoyAttackersIds.Clear();

                if (stopCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(stopCoroutine);

                stopCoroutine = null;
                stopTime = 0;
                ZoneController.DeleteZone(false);

                foreach (ConvoyVehicle convoyVehicle in convoyVehicles)
                    convoyVehicle.StartMoving();

                foreach (ScientistNPC scientist in roamNpc)
                    if (scientist.IsExists())
                        scientist.Kill();

                if (convoyHeli != null)
                    convoyHeli.OnConvoyStopOrStart();

                roamNpc.Clear();
                Interface.CallHook("OnConvoyStartMoving", GetConvoyPosition());
            }

            internal void OnConvoyAttacked(BasePlayer initiator = null)
            {
                if (convoyVehicles.Any(x => x != null && x.IsConvoyVehicleInSafeZone()))
                    return;

                stopTime = ins._config.damamageStopTime;

                if (!convoyAttackersIds.Contains(initiator.userID))
                    convoyAttackersIds.Add(initiator.userID);

                if (stopCoroutine != null)
                    return;

                stopCoroutine = ServerMgr.Instance.StartCoroutine(StopCounter());

                foreach (ConvoyVehicle convoyVehicle in convoyVehicles)
                {
                    if (convoyVehicle != null)
                        convoyVehicle.StopMoving(true, true);
                }

                if (convoyHeli != null)
                    convoyHeli.OnConvoyStopOrStart();


                if (initiator != null)
                    NotifyManager.SendMessageToAll("ConvoyAttacked", ins._config.prefix, initiator.displayName);

                Invoke(() =>
                {
                    if (ins.convoyController != null)
                    {
                        ZoneController.CreateZone(GetConvoyPosition());
                        Interface.CallHook("OnConvoyStopMoving", GetConvoyPosition());
                    }

                }, 2f);
            }

            IEnumerator StopCounter()
            {
                while (stopTime > 0)
                {
                    stopTime--;
                    yield return CoroutineEx.waitForSeconds(1f);
                }
                stopTime = 0;
                StartConvoy();
            }

            void DefineFollow(ConvoyVehicle convoyVehicle)
            {
                if (convoyVehicle == null) return;
                int index = convoyVehicles.IndexOf(convoyVehicle);
                index++;
                if (index >= convoyVehicles.Count) return;
                ConvoyVehicle nextVehicle = convoyVehicles[index];
                if (nextVehicle == null)
                {
                    convoyVehicles.Remove(nextVehicle);
                    DefineFollow(convoyVehicle);
                    return;
                }
                BaseEntity baseEntity = nextVehicle.baseEntity;
                if (baseEntity == null || baseEntity.IsDestroyed) return;
                convoyVehicles.Remove(convoyVehicle);
                ins.NextTick(() =>
                {
                    if (nextVehicle != null && nextVehicle.baseEntity.IsExists())
                        nextVehicle.DefineFollowEntity();
                });
            }

            internal void ReverseConvoy()
            {
                convoyPath.currentPath.Reverse();
                convoyVehicles.Reverse();
                foreach (ConvoyVehicle convoyVehicle in convoyVehicles)
                {
                    if (convoyVehicle != null) convoyVehicle.Rotate();
                }
            }

            void OnDestroy()
            {
                KillConvoyVehicles();
                KillRoamScientists();

                if (convoyHeli != null)
                    convoyHeli.KillHeli();

                if (convoyMarker != null)
                    convoyMarker.DeleteMarker();

                if (eventCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(eventCoroutine);

                if (stopCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(stopCoroutine);
            }

            void KillConvoyVehicles()
            {
                foreach (ConvoyVehicle convoyVehicle in convoyVehicles)
                {
                    if (convoyVehicle != null && convoyVehicle.baseEntity.IsExists())
                    {
                        convoyVehicle.baseEntity.Kill();
                    }
                }
            }

            void KillRoamScientists()
            {
                foreach (ScientistNPC scientist in roamNpc)
                {
                    if (scientist.IsExists())
                        scientist.Kill();
                }
            }
        }

        sealed class ConvoySedan : ConvoyVehicle
        {
            internal BasicCar basicCar;
            internal SedanConfig sedanConfig;

            FlasherLight flasherLight;
            internal int currentPoint = 0;
            float lastDistance = 0;

            internal static ConvoySedan CreateSedan(int firstPoint, int secondPoint, SedanConfig sedanConfig, ConvoyPath convoyPath)
            {
                Vector3 vector3 = convoyPath.currentPath[firstPoint];
                DeleteEntitiesInSpawnPosition(vector3);
                BasicCar car = GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", vector3, Quaternion.LookRotation(convoyPath.currentPath[secondPoint] - convoyPath.currentPath[firstPoint])) as BasicCar;
                car.enableSaving = false;
                car.skinID = 755446;
                car.Spawn();
                ConvoySedan convoySedan = car.gameObject.AddComponent<ConvoySedan>();
                convoySedan.currentPoint = firstPoint;
                convoySedan.InitSedan(sedanConfig, convoyPath);
                return convoySedan;
            }

            internal void InitSedan(SedanConfig sedanConfig, ConvoyPath convoyPath)
            {
                this.sedanConfig = sedanConfig;
                base.InitVehicle(sedanConfig.npcName, sedanConfig.numberOfNpc, convoyPath);

                basicCar = GetComponent<BasicCar>();
                basicCar.motorForceConstant = 1000;
                basicCar._maxHealth = sedanConfig.hp;
                basicCar.health = sedanConfig.hp;
                foreach (BasicCar.VehicleWheel vehicleWheel in basicCar.wheels) vehicleWheel.powerWheel = true;

                flasherLight = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab") as FlasherLight;
                flasherLight.enableSaving = false;
                flasherLight.SetParent(basicCar);
                flasherLight.transform.localPosition = new Vector3(0.45f, 1.64f, 0.4f);
                flasherLight.Spawn();
                flasherLight.UpdateFromInput(1, 0);
                InvokeRepeating(UpdateFlasher, 10, 10);
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (currentPoint >= convoyPath.currentPath.Count - 2) ins.convoyController.ReverseConvoy();
                if (UnityEngine.Physics.RaycastAll(transform.position, basicCar.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.convoyController.ReverseConvoy();
            }

            internal override int GetCurrentPointIndex()
            {
                return currentPoint;
            }

            internal override void Rotate()
            {
                base.Rotate();
                currentPoint = convoyPath.currentPath.Count - currentPoint;
            }

            void FixedUpdate()
            {
                if (allConvoyStop) return;

                if (convoyPath.round)
                {
                    if (currentPoint >= convoyPath.currentPath.Count - 2) currentPoint = 0;
                }
                else if (currentPoint >= convoyPath.currentPath.Count - 2)
                {
                    ins.convoyController.ReverseConvoy();
                    return;
                }

                Vector3 nextPoint = convoyPath.currentPath[currentPoint + 1];
                float destanationDistance = Vector3.Distance(new Vector3(basicCar.transform.position.x, 0, basicCar.transform.position.z), new Vector3(nextPoint.x, 0, nextPoint.z));
                if (destanationDistance < 6f)
                {
                    lastDistance = 0;
                    currentPoint++;
                }

                if (rigidbody.velocity.magnitude < 0.5f)
                {
                    if (lastDistance > 0 && lastDistance - destanationDistance < -0.0f)
                    {
                        rigidbody.isKinematic = false;
                        rigidbody.AddForce(new Vector3(basicCar.transform.forward.x, 0, basicCar.transform.forward.z) * (rigidbody.velocity.magnitude + 0.1f), ForceMode.VelocityChange);
                        lastDistance = 0;
                    }
                }
                lastDistance = destanationDistance;
                basicCar.SetFlag(BaseEntity.Flags.Reserved2, true);
                ControlTurn();
                ControlTrottle();
            }

            void UpdateFlasher()
            {
                flasherLight.limitNetworking = true;
                flasherLight.limitNetworking = false;
            }

            void ControlTrottle()
            {
                if (previusVehicle != null && previusVehicle.baseEntity != null && !previusVehicle.baseEntity.IsDestroyed && Vector3.Distance(previusVehicle.baseEntity.transform.position, baseEntity.transform.position) > 35)
                    SetSpeed(-1);
                else
                {
                    if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed)
                    {
                        SetSpeed(80);
                    }
                    else
                    {
                        float distance = Vector3.Distance(basicCar.transform.position, followVehicle.baseEntity.transform.position);
                        float speed = GetSpeed(distance, 100, 10f, sedanConfig.frontVehicleDistance);
                        SetSpeed(speed);
                    }
                }
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop) return;
                float maxSpeed = followVehicle == null ? 4 : 6;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    basicCar.brakePedal = 100;
                    return;
                }

                else if (gasP > 0 && stop)
                {
                    StartMoving();
                    basicCar.brakePedal = 0;
                }

                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    if (rigidbody.velocity.magnitude > ++maxSpeed)
                        basicCar.brakePedal = 50;
                    basicCar.gasPedal = 0;
                }

                else
                {
                    basicCar.gasPedal = gasP;
                    basicCar.brakePedal = 0;
                }

                basicCar.motorForceConstant = gasP;
                rigidbody.isKinematic = false;
            }

            void ControlTurn()
            {
                float turning = 0;

                Vector3 targetDirection = BradleyAPC.Direction2D(convoyPath.currentPath[currentPoint + 1], basicCar.transform.position);
                float num2 = Vector3.Dot(targetDirection, basicCar.transform.right);
                float num3 = Vector3.Dot(targetDirection, basicCar.transform.right);
                float num4 = Vector3.Dot(targetDirection, -basicCar.transform.right);

                if (Vector3.Dot(targetDirection, -basicCar.transform.forward) > num2)
                {
                    if (num3 >= num4) turning = 1f;
                    else turning = -1f;
                }
                else turning = Mathf.Clamp(num2 * 3f, -1f, 1f);
                if (rigidbody.velocity.magnitude < 0.6f) turning = 0;

                basicCar.steering = turning * 70;
                basicCar.DoSteering();
            }
        }

        sealed class ConvoyTruck : ConvoyModular
        {
            internal ModularConfig modularConfig;
            internal HackableLockedCrate crate;
            Coroutine crateUpdateCoroutine;

            internal static ConvoyTruck CreateTruck(ModularConfig modularConfig, int firstPoint, int secondPoint, ConvoyPath convoyPath)
            {
                ModularCar car = CreateModularCar(firstPoint, secondPoint, modularConfig.prefabName, convoyPath);
                ConvoyTruck convoyTruck = car.gameObject.AddComponent<ConvoyTruck>();
                convoyTruck.InitTruck(car, modularConfig, firstPoint, convoyPath);
                return convoyTruck;
            }

            void InitTruck(ModularCar modularCar, ModularConfig modularConfig, int currentPoint, ConvoyPath convoyPath)
            {
                this.modularConfig = modularConfig;
                base.InitModular(modularCar, modularConfig, currentPoint, convoyPath);
                Invoke(CreateCrate, 0.5f);
            }

            internal bool IsOwnCrate(ulong crateNetID)
            {
                return crate != null && crate.net.ID.Value == crateNetID;
            }

            internal bool IsCrateHacking()
            {
                return crate != null && crate.IsBeingHacked();
            }

            internal void UpdateCrateHackTimeWithDelay()
            {
                Invoke(() =>
                {
                    if (crate != null) UpdateCrateHackTime();
                }, 0.5f);
            }

            void CreateCrate()
            {
                SpawnCrate();
                crateUpdateCoroutine = ServerMgr.Instance.StartCoroutine(CrateUpdateCoroutine());
            }

            internal void SpawnCrate()
            {
                crate = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", modularCar.transform.InverseTransformPoint(modularConfig.crateLocation.position.ToVector3())) as HackableLockedCrate;
                crate.SetParent(modularCar, false, true);
                crate.transform.localPosition = modularConfig.crateLocation.position.ToVector3();
                crate.transform.localEulerAngles = modularConfig.crateLocation.rotation.ToVector3();
                crate.Spawn();

                Rigidbody crateRigidbody = crate.GetComponent<Rigidbody>();
                Destroy(crateRigidbody);

                Invoke(() =>
                    LootManager.UpdateLootContainerLootTable(crate.inventory, modularConfig.lootTable, modularConfig.typeLootTable), 0.5f);

                crate.DestroyShared();
                crate.EnableGlobalBroadcast(true);
                crate.syncPosition = true;
                crate.SendNetworkUpdate();

                UpdateCrateHackTime();
            }

            void UpdateCrateHackTime()
            {
                if (!modularConfig.changeUnlockTime) return;
                crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - modularConfig.crateUnlockTime;
            }

            IEnumerator CrateUpdateCoroutine()
            {
                while (crate.IsExists())
                {
                    crate.SendNetworkUpdate();
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            internal void DropCrate()
            {
                if (!crate.IsExists()) return;
                crate.SetParent(null, true);
                Rigidbody rigidBody = crate.gameObject.AddComponent<Rigidbody>();
                rigidBody.mass = 1;
                crate.gameObject.layer = 9;
            }

            internal override void OnDestroy()
            {
                if (crateUpdateCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(crateUpdateCoroutine);
                base.OnDestroy();
            }
        }

        class ConvoyModular : ConvoyVehicle
        {
            internal SupportModularConfig supportModularConfig;
            internal ModularCar modularCar;
            int currentPoint = 0;
            float lastDistance = 0;
            BasePlayer driver;

            internal static ConvoyModular CreateModular(SupportModularConfig supportModularConfig, int firstPoint, int secondPoint, ConvoyPath convoyPath)
            {
                ModularCar car = CreateModularCar(firstPoint, secondPoint, supportModularConfig.prefabName, convoyPath);
                ConvoyModular modular = car.gameObject.AddComponent<ConvoyModular>();
                modular.baseEntity = car;
                modular.currentPoint = firstPoint;
                modular.InitModular(car, supportModularConfig, firstPoint, convoyPath);
                return modular;
            }

            internal static ModularCar CreateModularCar(int firstPoint, int secondPoint, string presetName, ConvoyPath convoyPath)
            {
                Vector3 vector3 = convoyPath.currentPath[firstPoint];
                DeleteEntitiesInSpawnPosition(vector3);
                ModularCar car = GameManager.server.CreateEntity(presetName, vector3, Quaternion.LookRotation(convoyPath.currentPath[secondPoint] - convoyPath.currentPath[firstPoint])) as ModularCar;
                car.enableSaving = false;
                car.spawnSettings.useSpawnSettings = false;
                car.skinID = 755446;
                car.Spawn();
                return car;
            }

            protected void InitModular(ModularCar modularCar, SupportModularConfig supportModularConfig, int currentPoint, ConvoyPath convoyPath)
            {
                base.InitVehicle(supportModularConfig.npcName, supportModularConfig.numberOfNpc, convoyPath);

                this.supportModularConfig = supportModularConfig;
                this.modularCar = modularCar;
                this.currentPoint = currentPoint;
                modularCar = GetComponent<ModularCar>();

                Invoke(Build, 0.25f);
            }

            internal override int GetCurrentPointIndex()
            {
                return currentPoint;
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (currentPoint >= convoyPath.currentPath.Count - 3) ins.convoyController.ReverseConvoy();

                if (UnityEngine.Physics.RaycastAll(transform.position, modularCar.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.convoyController.ReverseConvoy();
            }

            internal override void Rotate()
            {
                base.Rotate();
                currentPoint = convoyPath.currentPath.Count - currentPoint;
            }

            void Build()
            {
                AddCarModules();
                modularCar.GetFuelSystem().cachedHasFuel = true;
                modularCar.GetFuelSystem().nextFuelCheckTime = float.MaxValue;
            }

            void AddCarModules()
            {
                List<string> modules = supportModularConfig.modules;
                for (int socketIndex = 0; socketIndex < modularCar.TotalSockets && socketIndex < modules.Count; socketIndex++)
                {
                    string shortName = modules[socketIndex];
                    if (shortName == "") continue;
                    Item existingItem = modularCar.Inventory.ModuleContainer.GetSlot(socketIndex);
                    if (existingItem != null) continue;
                    Item moduleItem = ItemManager.CreateByName(shortName);
                    if (moduleItem == null) continue;
                    moduleItem.conditionNormalized = 100;

                    if (!modularCar.TryAddModule(moduleItem, socketIndex)) moduleItem.Remove();
                }

                Invoke(AddEngineParts, 1f);
            }

            void AddEngineParts()
            {
                foreach (BaseVehicleModule module in modularCar.AttachedModuleEntities)
                {
                    VehicleModuleEngine engineModule = module as VehicleModuleEngine;
                    if (engineModule == null) continue;
                    engineModule.engine.maxFuelPerSec = 0;
                    engineModule.engine.idleFuelPerSec = 0;
                    EngineStorage engineStorage = engineModule.GetContainer() as EngineStorage;
                    if (engineStorage == null) continue;
                    engineStorage.dropsLoot = false;
                    ItemContainer inventory = engineStorage.inventory;
                    for (int i = 0; i < inventory.capacity; i++)
                    {
                        ItemModEngineItem output;
                        if (!engineStorage.allEngineItems.TryGetItem(1, engineStorage.slotTypes[i], out output)) continue;
                        ItemDefinition component = output.GetComponent<ItemDefinition>();
                        Item item = ItemManager.Create(component);
                        if (item == null) continue;
                        item._maxCondition = int.MaxValue;
                        item.condition = int.MaxValue;
                        item.MoveToContainer(engineStorage.inventory, i, allowStack: false);
                    }
                    engineModule.RefreshPerformanceStats(engineStorage);
                    return;
                }
            }

            protected override void CreatePassengers()
            {
                base.CreatePassengers();
                driver = modularCar.GetDriver();
            }

            void FixedUpdate()
            {
                if (allConvoyStop) return;
                if (driver == null)
                {
                    driver = modularCar.GetDriver();
                    return;
                }

                if (modularCar.engineController.IsOff)
                {
                    AddEngineParts();
                    TryLauchEngine();
                }

                if (convoyPath.round)
                {
                    if (currentPoint >= convoyPath.currentPath.Count - 2) currentPoint = 0;
                }
                else if (currentPoint >= convoyPath.currentPath.Count - 2)
                {
                    ins.convoyController.ReverseConvoy();
                    return;
                }

                Vector3 nextPint = convoyPath.currentPath[currentPoint + 1];
                float destanationDistance = Vector3.Distance(new Vector3(modularCar.transform.position.x, 0, modularCar.transform.position.z), new Vector3(nextPint.x, 0, nextPint.z));

                if (destanationDistance < 7f)
                {
                    currentPoint++;
                    lastDistance = 0;
                }

                if (rigidbody.velocity.magnitude < 1f)
                {
                    if (lastDistance > 0 && lastDistance - destanationDistance < -0.0f)
                    {
                        rigidbody.isKinematic = false;
                        rigidbody.AddForce(new Vector3(modularCar.transform.forward.x, 0, modularCar.transform.forward.z) * (rigidbody.velocity.magnitude + 0.5f), ForceMode.VelocityChange);
                        lastDistance = 0;
                    }
                    lastDistance = destanationDistance;
                }

                ControlTrottle();
                ControlTurn();
            }

            void TryLauchEngine()
            {
                if (!modularCar.engineController.CanRunEngine())
                {
                    AddCarModules();
                    modularCar.GetFuelSystem().cachedHasFuel = true;
                    modularCar.GetFuelSystem().nextFuelCheckTime = float.MaxValue;
                }
                if (!modularCar.engineController.IsStarting && driver != null)
                    modularCar.engineController.TryStartEngine(driver);
            }

            void ControlTrottle()
            {
                if (previusVehicle != null && previusVehicle.baseEntity != null && !previusVehicle.baseEntity.IsDestroyed && Vector3.Distance(previusVehicle.baseEntity.transform.position, baseEntity.transform.position) > 35) SetSpeed(-1);
                else
                {
                    if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed) SetSpeed(0.5f);
                    else
                    {
                        float distance = Vector3.Distance(modularCar.transform.position, followVehicle.transform.position);
                        SetSpeed(GetSpeed(distance, 1.5f, 0.315f, supportModularConfig.frontVehicleDistance));
                    }
                }
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop) return;

                float maxSpeed = followVehicle == null ? 4 : 6;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    return;
                }

                else if (gasP > 0 && stop) StartMoving();

                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    if (rigidbody.velocity.magnitude > ++maxSpeed) gasP = -0.3f;
                    else gasP = 0;
                }

                rigidbody.AddForce(new Vector3(modularCar.transform.forward.x, 0, modularCar.transform.forward.z) * gasP, ForceMode.VelocityChange);
            }

            void ControlTurn()
            {
                float turning;

                Vector3 lhs = global::BradleyAPC.Direction2D(convoyPath.currentPath[currentPoint + 1], modularCar.transform.position);
                float num2 = Vector3.Dot(lhs, modularCar.transform.right);
                float num3 = Vector3.Dot(lhs, modularCar.transform.right);
                float num4 = Vector3.Dot(lhs, -modularCar.transform.right);

                if (Vector3.Dot(lhs, -modularCar.transform.forward) > num2)
                {
                    if (num3 >= num4) turning = 1f;
                    else turning = -1f;
                }
                else turning = Mathf.Clamp(num2 * 3f, -1f, 1f);

                InputState inputState = CreateInput();
                if (turning < -0.5f) inputState.current.buttons = 8;

                else if (turning > 0.5f) inputState.current.buttons = 16;
                else inputState.current.buttons = 0;

                if (rigidbody.velocity.magnitude < 0.3f) inputState.current.buttons = 0;

                if (driver != null && inputState != null) modularCar.PlayerServerInput(inputState, driver);
            }

            InputState CreateInput()
            {
                InputState inputState = new InputState();
                inputState.previous.mouseDelta = new Vector3(0, 0, 0);
                inputState.current.aimAngles = new Vector3(0, 0, 0);
                inputState.current.mouseDelta = new Vector3(0, 0, 0);
                return inputState;
            }
        }

        sealed class ConvoyBradley : ConvoyVehicle
        {
            internal BradleyConfig bradleyConfig;
            internal BradleyAPC bradley;

            internal static ConvoyBradley CreateBradley(int firstPoint, int secondPoint, BradleyConfig bradleyConfig, ConvoyPath convoyPath)
            {
                Vector3 position = convoyPath.currentPath[firstPoint];
                DeleteEntitiesInSpawnPosition(position);

                Quaternion rotation = Quaternion.LookRotation(convoyPath.currentPath[secondPoint] - convoyPath.currentPath[firstPoint]);
                BradleyAPC bradley = CustomBradley.CreateCustomBradley(position, rotation);


                ConvoyBradley convoyBradley = bradley.gameObject.AddComponent<ConvoyBradley>();
                convoyBradley.InitBradley(bradleyConfig, convoyPath, secondPoint);
                return convoyBradley;
            }

            void InitBradley(BradleyConfig bradleyConfig, ConvoyPath convoyPath, int currentPathIndex)
            {
                this.bradleyConfig = bradleyConfig;
                base.InitVehicle(bradleyConfig.npcName, bradleyConfig.numberOfNpc, convoyPath);

                bradley = GetComponent<BradleyAPC>();
                bradley.pathLooping = true;
                bradley._maxHealth = bradleyConfig.hp;
                bradley.health = bradleyConfig.hp;
                bradley.maxCratesToSpawn = bradleyConfig.countCrates;
                bradley.viewDistance = bradleyConfig.viewDistance;
                bradley.searchRange = bradleyConfig.searchDistance;
                bradley.coaxAimCone *= bradleyConfig.coaxAimCone;
                bradley.coaxFireRate *= bradleyConfig.coaxFireRate;
                bradley.coaxBurstLength = bradleyConfig.coaxBurstLength;
                bradley.nextFireTime = bradleyConfig.nextFireTime;
                bradley.topTurretFireRate = bradleyConfig.topTurretFireRate;

                bradley.ClearPath();
                bradley.currentPath = convoyPath.currentPath;
                bradley.currentPathIndex = currentPathIndex;
                bradley.SetDestination(convoyPath.currentPath[currentPathIndex]);
                bradley.finalDestination = Vector3.zero;
                bradley.pathLooping = true;
            }

            internal override int GetCurrentPointIndex()
            {
                return bradley.currentPathIndex;
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (bradley.currentPathIndex >= convoyPath.currentPath.Count - 2) ins.convoyController.ReverseConvoy();
                if (UnityEngine.Physics.RaycastAll(transform.position, bradley.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.convoyController.ReverseConvoy();
            }

            internal override void Rotate()
            {
                base.Rotate();
                bradley.currentPath = convoyPath.currentPath;
                bradley.currentPathIndex = convoyPath.currentPath.Count - bradley.currentPathIndex;
            }

            void FixedUpdate()
            {
                if (convoyPath.round)
                {
                    if (bradley.currentPathIndex >= convoyPath.currentPath.Count - 1)
                        bradley.currentPathIndex = 1;
                }
                else if (bradley.currentPathIndex >= convoyPath.currentPath.Count - 2)
                {
                    ins.convoyController.ReverseConvoy();
                    return;
                }

                Vector3 nextPint = bradley.currentPath[bradley.currentPathIndex];
                float destanationDistance = Vector3.Distance(new Vector3(bradley.transform.position.x, 0, bradley.transform.position.z), new Vector3(nextPint.x, 0, nextPint.z));

                if (destanationDistance < 6f)
                    bradley.currentPathIndex++;

                if (previusVehicle != null && previusVehicle.baseEntity != null && !previusVehicle.baseEntity.IsDestroyed && Vector3.Distance(previusVehicle.baseEntity.transform.position, baseEntity.transform.position) > 35)
                {
                    SetSpeed(-1);
                }
                else
                {
                    if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed)
                    {
                        SetSpeed(800);
                    }
                    else
                    {
                        float distance = Vector3.Distance(bradley.transform.position, followVehicle.baseEntity.transform.position);
                        float speed = GetSpeed(distance, 2000, 200, bradleyConfig.frontVehicleDistance);
                        SetSpeed(speed);
                    }
                }
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop)
                {
                    return;
                }

                float maxSpeed = followVehicle == null ? 5 : 7.5f;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    return;
                }
                else if (gasP > 0 && stop)
                {
                    StartMoving();
                }
                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    bradley.leftThrottle = 0;
                    bradley.rightThrottle = 0;
                    bradley.moveForceMax = 0;
                }
                else
                {
                    bradley.moveForceMax = gasP;
                }
            }
        }

        class CustomBradley : BradleyAPC
        {
            private TimeSince timeSinceSeemingStuck = 0;
            private TimeSince timeSinceStuckReverseStart = float.MaxValue;

            internal static BradleyAPC CreateCustomBradley(Vector3 position, Quaternion rotation)
            {
                BradleyAPC bradley = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", position, rotation) as BradleyAPC;
                bradley.skinID = 755446;
                bradley.enableSaving = false;
                //bradley.Spawn();
                //return bradley;

                CustomBradley customBradley = bradley.gameObject.AddComponent<CustomBradley>();
                ins.CopySerializableFields(bradley, customBradley);
                bradley.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(bradley, true);

                customBradley.Spawn();
                customBradley.Init();
                return customBradley;
            }

            void Init()
            {
                UpdateHurtTriggers();
            }

            void UpdateHurtTriggers()
            {
                TriggerHurtNotChild[] triggerHurts = GetComponentsInChildren<TriggerHurtNotChild>();

                foreach (TriggerHurtNotChild triggerHurt in triggerHurts)
                    triggerHurt.enabled = false;
            }

            new void FixedUpdate()
            {
                DoSimpleAI();
                DoPhysicsMove();
                DoWeapons();
                DoHealing();
            }

            public new void DoSimpleAI()
            {
                EnableLightAtNight();
                UpdateTarget();
                MoveControl();
                DoWeaponAiming();
                SendNetworkUpdate();
            }

            void EnableLightAtNight()
            {
                SetFlag(Flags.Reserved5, TOD_Sky.Instance.IsNight);
            }

            void UpdateTarget()
            {
                if (targetList.Count > 0)
                {
                    if (targetList[0].IsValid() && targetList[0].IsVisible())
                        mainGunTarget = targetList[0].entity as BaseCombatEntity;
                    else
                        mainGunTarget = null;

                    UpdateMovement_Hunt();
                }
                else
                {
                    mainGunTarget = null;
                    UpdateMovement_Patrol();
                }
            }

            void MoveControl()
            {
                AdvancePathMovement(force: false);

                Vector3 targetDirection = Direction2D(destination, transform.position);
                float rightDirection = Vector3.Dot(targetDirection, transform.right);
                turning = Mathf.Clamp(rightDirection * 3f, -1f, 1f);

                CheckStuck();
                if (timeSinceStuckReverseStart < 3f)
                {
                    throttle = -0.75f;
                    turning = 1f;
                }
                else
                {
                    float throttleScaleFromTurn = 1f - Mathf.InverseLerp(0f, 0.3f, Mathf.Abs(turning));
                    float climbScale = Mathf.InverseLerp(0.1f, 0.4f, Vector3.Dot(base.transform.forward, Vector3.up));
                    throttle = 0.1f + 1f * throttleScaleFromTurn + climbScale;
                }
            }

            void CheckStuck()
            {
                float forwardVelocity = Vector3.Dot(myRigidBody.velocity, transform.forward);
                if (throttle <= 0f || forwardVelocity >= 0.5f)
                {
                    timeSinceSeemingStuck = 0f;
                }
                else if (timeSinceSeemingStuck > 10f)
                {
                    timeSinceStuckReverseStart = 0f;
                    timeSinceSeemingStuck = 0f;
                }
            }
        }

        abstract class ConvoyVehicle : FacepunchBehaviour
        {
            int numberOfNpc;
            List<MountPointInfo> baseMountables = new List<MountPointInfo>();
            MountPointInfo driverMountPointInfo;

            internal BaseEntity baseEntity;
            protected ConvoyPath convoyPath;
            protected ConvoyVehicle previusVehicle;
            protected ConvoyVehicle followVehicle;
            protected Rigidbody rigidbody;
            protected bool stop = true;
            internal bool allConvoyStop = true;

            internal HashSet<ScientistNPC> scientists = new HashSet<ScientistNPC>();
            internal HashSet<ulong> roamNpcsNetIDs = new HashSet<ulong>();
            internal int countDieNpc;

            NpcConfig npcConfig;

            protected static void DeleteEntitiesInSpawnPosition(Vector3 pos)
            {
                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(pos, 2f))
                {
                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity.IsExists() && entity.net != null && ins._config.barriers.Contains(entity.ShortPrefabName) && ins.convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value) == null) entity.Kill();
                }
            }

            protected static float GetSpeed(float distance, float maxSpeed, float multiplicator, float frontVehicleDistance)
            {
                float speed;
                if (distance <= frontVehicleDistance) return -1;
                speed = (distance - 5 - frontVehicleDistance) * multiplicator;
                if (speed < 0) return 0;
                if (speed > maxSpeed) return maxSpeed;
                return speed;
            }

            protected void InitVehicle(string npcConfigPresetName, int numberOfNpc, ConvoyPath convoyPath)
            {
                baseEntity = GetComponent<BaseEntity>();
                npcConfig = ins._config.NPC.FirstOrDefault(x => x.name == npcConfigPresetName);
                this.numberOfNpc = numberOfNpc;
                this.convoyPath = convoyPath;

                rigidbody = baseEntity.gameObject.GetComponent<Rigidbody>();
                rigidbody.mass = 3500;
                rigidbody.centerOfMass = new Vector3(0, -0.2f, 0);

                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rigidbody.isKinematic = true;

                Invoke(DelayedInitVehicle, 0.5f);
            }

            void DelayedInitVehicle()
            {
                DefineMountPoints();
                StartMoving();
                DefineFollowEntity();
                if (!convoyPath.round) InvokeRepeating(CheckRotate, 0.5f, 0.1f);
                if (ins._config.deleteBarriers) InvokeRepeating(CheckBarriers, 1.5f, 1.5f);
            }

            void DefineMountPoints()
            {
                BaseVehicle baseVehicle = baseEntity.gameObject.GetComponent<BaseVehicle>();

                if (baseVehicle != null)
                {
                    baseMountables = baseVehicle.allMountPoints.ToList();
                    driverMountPointInfo = baseMountables.FirstOrDefault(x => x != null && x.isDriver);
                }
            }

            internal virtual void Rotate()
            {
                rigidbody.velocity = Vector3.zero;
                transform.RotateAround(transform.position, transform.up, 180);
                DefineFollowEntity();
            }

            internal abstract void CheckRotate();

            internal abstract int GetCurrentPointIndex();

            internal void CheckBarriers()
            {
                if (followVehicle != null) return;
                Vector3 checkPosition = baseEntity.transform.position + baseEntity.transform.forward * 4f;
                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(checkPosition, 3f))
                {
                    BaseEntity entity = collider.ToBaseEntity();

                    if (!entity.IsExists())
                        continue;

                    if (entity is TreeEntity)
                        entity.Kill();

                    if (ins._config.barriers.Contains(entity.ShortPrefabName) && ins.convoyController.GetConvoyVehicleByEntityNetId(entity.net.ID.Value) == null)
                    {
                        BaseVehicle vehicle = entity as BaseVehicle;

                        if (vehicle != null)
                        {
                            if (vehicle.AnyMounted())
                                return;

                            EntityFuelSystem fuelSystem = vehicle.GetFuelSystem();

                            if (fuelSystem != null && fuelSystem.HasFuel())
                                return;
                        }

                        entity.Kill();
                    }
                }
            }

            internal void DefineFollowEntity()
            {
                int index = ins.convoyController.convoyVehicles.IndexOf(this);

                if (index == 0) followVehicle = null;
                else followVehicle = ins.convoyController.convoyVehicles[index - 1];

                if (index >= ins.convoyController.convoyVehicles.Count - 1) previusVehicle = null;
                else previusVehicle = ins.convoyController.convoyVehicles[index + 1];
            }

            internal void StartMoving()
            {
                roamNpcsNetIDs.Clear();

                if (allConvoyStop)
                {
                    scientists.Clear();
                    CreateDriver();
                    CreatePassengers();
                }

                allConvoyStop = false;
                rigidbody.isKinematic = false;
                stop = false;
            }

            void CreateDriver()
            {
                JObject driverNPCConfigJObject = NpcManager.GetNpcJObjectConfig(npcConfig, true, true);
                CereateMountedNpc(driverMountPointInfo, driverNPCConfigJObject);
            }

            protected virtual void CreatePassengers()
            {
                int npcCount = numberOfNpc - countDieNpc - 1;

                if (npcCount <= 0)
                    return;
                else if (npcCount > baseMountables.Count)
                    npcCount = baseMountables.Count;

                JObject passengerNPCConfigJObject = NpcManager.GetNpcJObjectConfig(npcConfig, !ins._config.isAggressive, true);

                for (int i = 0; i < npcCount; i++)
                {
                    if (i >= baseMountables.Count)
                        return;

                    MountPointInfo mountPointInfo = baseMountables[i];

                    if (mountPointInfo == null || (driverMountPointInfo != null && mountPointInfo.mountable == driverMountPointInfo.mountable))
                    {
                        ++npcCount;
                        continue;
                    }

                    CereateMountedNpc(mountPointInfo, passengerNPCConfigJObject);
                }
            }

            void CereateMountedNpc(MountPointInfo mountPointInfo, JObject npcConfig)
            {
                if (mountPointInfo == null || mountPointInfo.mountable == null)
                    return;

                ScientistNPC scientist = NpcManager.CreateNpc(npcConfig, baseEntity.transform.position);

                scientist.MountObject(mountPointInfo.mountable);
                mountPointInfo.mountable.MountPlayer(scientist);
                scientists.Add(scientist);
            }

            internal void StopMoving(bool NPC = true, bool allConvoyStop = false)
            {
                this.allConvoyStop = allConvoyStop;
                stop = true;
                rigidbody.isKinematic = true;

                if (NPC)
                    CreateRoamNpc();
            }

            internal bool IsConvoyVehicleInSafeZone()
            {
                if (baseEntity.triggers == null)
                    return false;

                for (int i = 0; i < baseEntity.triggers.Count; i++)
                {
                    TriggerSafeZone triggerSafeZone = baseEntity.triggers[i] as TriggerSafeZone;
                    if (triggerSafeZone != null)
                        return true;
                }

                return false;
            }

            void CreateRoamNpc()
            {
                if (!stop)
                    return;

                KillScientists();
                int count = numberOfNpc - countDieNpc;

                if (count <= 0)
                    return;

                JObject npcConfigJObject = NpcManager.GetNpcJObjectConfig(npcConfig, takeWeaponFromNpc: false, isMounted: false);

                int y0 = 2;
                int deltay = 2;
                float x = 2;

                if (baseEntity is BradleyAPC)
                {
                    y0 = 3;
                    deltay = 3;
                    x = 3;
                }

                int y = y0;
                for (int i = 0; i < count; i++)
                {
                    Vector3 localPosition = new Vector3(x, 0, y);
                    Vector3 globalPosition = GlobalPositionDefiner.GetGlobalPosition(baseEntity.transform, localPosition);
                    ScientistNPC scientist = NpcManager.CreateNpc(npcConfigJObject, globalPosition);
                    if (scientist != null)
                    {
                        ins.convoyController.roamNpc.Add(scientist);
                        roamNpcsNetIDs.Add(scientist.net.ID.Value);
                    }

                    x = -x;

                    if (x > 0)
                        y -= deltay;

                    if (Math.Abs(y) > y0)
                    {
                        y = y0;
                        x *= 1.5f;
                    }
                }
            }

            internal void KillScientists()
            {
                foreach (ScientistNPC scientist in scientists)
                {
                    if (scientist != null && !scientist.IsDestroyed) scientist.Kill();
                }
                scientists.Clear();
            }

            internal virtual void OnDestroy()
            {
                CancelInvoke(CheckRotate);
                CancelInvoke(CheckBarriers);
                KillScientists();
            }
        }

        class ConvoyHeli : FacepunchBehaviour
        {
            internal PatrolHelicopterAI patrolHelicopterAI;
            internal PatrolHelicopter patrolHelicopter;
            internal HeliConfig heliConfig;
            int ounsideTime = 0;

            internal static ConvoyHeli CreateHelicopter(HeliConfig heliConfig, Vector3 position)
            {
                PatrolHelicopter patrolHelicopter = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", position + new Vector3(0, heliConfig.height, 0), Quaternion.identity) as PatrolHelicopter;
                patrolHelicopter.enableSaving = false;
                patrolHelicopter.skinID = 755446;
                patrolHelicopter.Spawn();
                patrolHelicopter.transform.position = position;

                ConvoyHeli convoyHeli = patrolHelicopter.gameObject.AddComponent<ConvoyHeli>();
                convoyHeli.InitHelicopter(patrolHelicopter, heliConfig);

                return convoyHeli;
            }

            internal void InitHelicopter(PatrolHelicopter patrolHelicopter, HeliConfig heliConfig)
            {
                this.heliConfig = heliConfig;
                this.patrolHelicopter = patrolHelicopter;

                patrolHelicopterAI = patrolHelicopter.myAI;
                patrolHelicopter.startHealth = heliConfig.hp;
                patrolHelicopter.InitializeHealth(heliConfig.hp, heliConfig.hp);
                patrolHelicopter.maxCratesToSpawn = heliConfig.cratesAmount;
                patrolHelicopter.bulletDamage = heliConfig.bulletDamage;
                patrolHelicopter.bulletSpeed = heliConfig.bulletSpeed;
                patrolHelicopter.myAI.isRetiring = true;
                var weakspots = patrolHelicopter.weakspots;

                if (weakspots != null && weakspots.Length > 1)
                {
                    weakspots[0].maxHealth = heliConfig.mainRotorHealth;
                    weakspots[0].health = heliConfig.mainRotorHealth;
                    weakspots[1].maxHealth = heliConfig.rearRotorHealth;
                    weakspots[1].health = heliConfig.rearRotorHealth;
                }

                InvokeRepeating(UpdateDestination, 1f, 1f);
            }

            internal void SetTarget(BasePlayer player)
            {
                if (!player.IsRealPlayer())
                    return;

                patrolHelicopterAI.SetTargetDestination(player.transform.position);
                patrolHelicopterAI._targetList.Add(new PatrolHelicopterAI.targetinfo(player, player));
            }

            internal void OnConvoyStopOrStart()
            {
                patrolHelicopterAI.ExitCurrentState();
            }

            internal ulong GetHeliNetId()
            {
                return patrolHelicopter.net.ID.Value;
            }

            internal void UpdateDestination()
            {
                if (ins.convoyController == null || patrolHelicopter.myAI._currentState == PatrolHelicopterAI.aiState.DEATH || patrolHelicopter.myAI._currentState == PatrolHelicopterAI.aiState.STRAFE)
                    return;

                Vector3 targetPosition = ins.convoyController.GetConvoyPosition() + new Vector3(0, heliConfig.height, 0);
                float distanceToTargetPosition = Vector3.Distance(targetPosition, patrolHelicopter.transform.position);

                if (ins.convoyController.IsConvoyStop())
                {
                    if (patrolHelicopterAI.leftGun.HasTarget() || patrolHelicopterAI.rightGun.HasTarget())
                    {
                        if (distanceToTargetPosition > heliConfig.distance)
                        {
                            ounsideTime++;
                            if (ounsideTime > heliConfig.outsideTime)
                                patrolHelicopter.myAI.State_Move_Enter(targetPosition);
                        }
                        else
                        {
                            ounsideTime = 0;
                        }
                    }
                    else if (distanceToTargetPosition > heliConfig.distance)
                    {
                        patrolHelicopterAI.State_Move_Enter(targetPosition);
                        ounsideTime = 0;
                    }
                    else
                    {
                        ounsideTime = 0;
                    }
                }
                else
                {
                    if (patrolHelicopterAI._currentState == PatrolHelicopterAI.aiState.ORBIT)
                        patrolHelicopterAI.ExitCurrentState();

                    patrolHelicopter.myAI.State_Move_Enter(targetPosition);

                    if (distanceToTargetPosition < 35)
                        patrolHelicopterAI.SetIdealRotation(patrolHelicopterAI.GetYawRotationTo(targetPosition), 100);
                }
            }

            internal void KillHeli()
            {
                if (patrolHelicopter.IsExists())
                    patrolHelicopter.Kill();
            }
        }

        sealed class ConvoyPath
        {
            internal bool round;
            internal List<Vector3> currentPath = new List<Vector3>();
            internal int countPointsBetweenCars;
            internal int spawnPointIndex;

            internal static ConvoyPath DefineConvoyPath(int vehicleCount)
            {
                Vector3[] convoyPathArray = null;

                if (ins._config.customRootName != "")
                    convoyPathArray = TryFindCustomRoad(vehicleCount);

                if (convoyPathArray == null)
                    convoyPathArray = TryFindRegulatRoad(vehicleCount);

                if (convoyPathArray == null)
                    return null;

                bool isRound = IsRoundRoad(convoyPathArray);

                if (isRound && UnityEngine.Random.Range(0, 100) < 50)
                    convoyPathArray = InversePath(convoyPathArray);

                ConvoyPath convoyPath = new ConvoyPath
                {
                    currentPath = convoyPathArray.ToList(),
                    round = isRound,
                    countPointsBetweenCars = GetCountPointsBetweenCars(convoyPathArray)
                };
                convoyPath.Init(vehicleCount);

                return convoyPath;
            }

            static Vector3[] TryFindCustomRoad(int vehicleCount)
            {
                List<List<string>> goodRoads = ins.roots[ins._config.customRootName].WhereList(x => CheckCustomRoad(x, vehicleCount));

                List<string> currentpathString = goodRoads.GetRandom();
                if (currentpathString == null)
                    return null;

                Vector3[] path = new Vector3[currentpathString.Count];
                for (int i = 0; i < currentpathString.Count; i++)
                    path[i] = currentpathString[i].ToVector3();

                return path;
            }

            static bool CheckCustomRoad(List<string> customRoute, int vehicleCount)
            {
                Vector3[] path = new Vector3[customRoute.Count];
                for (int i = 0; i < customRoute.Count; i++)
                    path[i] = customRoute[i].ToVector3();

                return CheckRoad(path, vehicleCount);
            }

            static Vector3[] TryFindRegulatRoad(int vehicleCount)
            {
                PathList pathList = null;

                if (ins._config.rounRoadPriority && pathList == null)
                    pathList = GetRoundRoadPathList(vehicleCount);

                if (pathList == null)
                    pathList = GetRandomRegularRoadPathList(vehicleCount);

                CheckRoadAngels(pathList);

                if (pathList != null)
                    return pathList.Path.Points;

                return null;
            }

            static PathList GetRoundRoadPathList(int vehicleCount)
            {
                List<PathList> suitableRoads = TerrainMeta.Path.Roads.WhereList(x => IsRoundRoad(x.Path.Points) && IsAsphaltRoad(x.Path.Points) && CheckRoad(x.Path.Points, vehicleCount));
                PathList newPathList = suitableRoads.GetRandom();
                return newPathList;
            }

            static PathList GetRandomRegularRoadPathList(int vehicleCount)
            {
                List<PathList> suitableRoads = TerrainMeta.Path.Roads.WhereList(x => CheckRoad(x.Path.Points, vehicleCount) && IsAsphaltRoad(x.Path.Points) && !IsRoadBlocked(TerrainMeta.Path.Roads.IndexOf(x)) && !IsSafeZoneRoad(x.Path.Points));
                return suitableRoads.GetRandom();
            }

            static bool IsRoundRoad(Vector3[] path)
            {
                return Vector3.Distance(path[0], path[path.Length - 1]) < 2f;
            }

            static bool IsAsphaltRoad(Vector3[] path)
            {
                return Physics.RaycastAll(new Ray(path[path.Length / 2] + new Vector3(0, 1, 0), Vector3.down), 4f).Any(y => y.collider.name.Contains("Road Mesh"));
            }

            static bool IsSafeZoneRoad(Vector3[] path)
            {
                return IsSafeZoneCollider(path[path.Length / 2]) || IsSafeZoneCollider(path[0]) || IsSafeZoneCollider(path[path.Length - 1]);
            }

            static bool IsSafeZoneCollider(Vector3 position)
            {
                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(position, 10f))
                {
                    if (collider.name.Contains("Safe")) return true;
                }
                return false;
            }

            static bool IsRoadBlocked(int roadIndex)
            {
                return ins._config.blockRoads.Contains(roadIndex);
            }

            static bool CheckRoadAngels(PathList path)
            {
                for (int i = 1; i < path.Path.Tangents.Length; i++)
                {
                    if (Vector3.Angle(path.Path.Tangents[i], path.Path.Tangents[i - 1]) > 30) return false;
                }
                return true;
            }

            static bool CheckRoad(Vector3[] path, int vehicleCount)
            {
                float roadLenght = GetRoadLength(path);
                if (roadLenght < ins._config.roadLength)
                    return false;

                int countPointsBetweenCars = GetCountPointsBetweenCars(path);
                if (vehicleCount * countPointsBetweenCars >= path.Length)
                    return false;

                return true;
            }

            static float GetRoadLength(Vector3[] path)
            {
                float length = 0;

                for (int i = 1; i < path.Length; i++)
                    length += Vector3.Distance(path[i], path[i - 1]);

                return length;
            }

            static int GetCountPointsBetweenCars(Vector3[] path)
            {
                int count = 1;

                while (count < path.Length - 1 && Vector3.Distance(path[0], path[count]) < 10f)
                    count++;

                return count;
            }

            static Vector3[] InversePath(Vector3[] path)
            {
                Vector3[] newPath = new Vector3[path.Length];
                for (int i = path.Length - 1; i >= 0; --i)
                    newPath[i] = path[path.Length - 1 - i];

                return newPath;
            }

            void Init(int vehicleCount)
            {
                if (round)
                    UpdateRoundRoad();
                GetConvoySpawnPoint(vehicleCount);
            }

            void UpdateRoundRoad()
            {
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Vector3 currentPoint = currentPath[i];
                    Vector3 previousPoint = i == 0 ? currentPath[currentPath.Count - 2] : currentPath[i - 1];
                    Vector3 nextPoint = currentPath[i + 1];

                    Vector3 previousDirection = Vector3Ex.Direction(previousPoint, currentPoint);
                    Vector3 nextDirection = Vector3Ex.Direction(nextPoint, currentPoint);

                    float angle = Vector3.Angle(previousDirection, nextDirection);
                    if (angle < 15)
                        CutOverlaySector(i);
                }
            }

            void CutOverlaySector(int overlaySectorCenterIndex)
            {
                Vector3 currentPoint = currentPath[overlaySectorCenterIndex];
                List<int> removePoints = new List<int>
                {
                    overlaySectorCenterIndex
                };

                for (int i = 1; i < 20; i++)
                {
                    int previousPointIndex = overlaySectorCenterIndex - i;
                    int nextPointIndex = overlaySectorCenterIndex + i;

                    if (nextPointIndex >= currentPath.Count)
                        nextPointIndex = i - currentPath.Count;

                    if (previousPointIndex < 0)
                        previousPointIndex = -i;

                    if (previousPointIndex >= currentPath.Count || nextPointIndex >= currentPath.Count || previousPointIndex < 0 || nextPointIndex < 0)
                        break;

                    Vector3 previousPoint = currentPath[previousPointIndex];
                    Vector3 nextPoint = currentPath[nextPointIndex];

                    Vector3 previousDirection = Vector3Ex.Direction(previousPoint, currentPoint);
                    Vector3 nextDirection = Vector3Ex.Direction(nextPoint, currentPoint);

                    float angle = Vector3.Angle(previousDirection, nextDirection);
                    if (angle < 7.5f)
                    {
                        removePoints.Add(previousPointIndex);
                        removePoints.Add(nextPointIndex);
                    }
                    else
                        break;
                }
                currentPath.RemoveRange(removePoints.Min(x => x), removePoints.Count);
            }

            void GetConvoySpawnPoint(int vehicleCount)
            {
                if (round)
                    spawnPointIndex = UnityEngine.Random.Range(0, currentPath.Count - 1);
                else
                {
                    int maxSpawnIndes = currentPath.Count - 3 - vehicleCount * countPointsBetweenCars;
                    spawnPointIndex = maxSpawnIndes > 0 ? UnityEngine.Random.Range(0, maxSpawnIndes) : 0;
                }
            }
        }

        sealed class ZoneController : FacepunchBehaviour
        {
            static ZoneController zoneController;
            static HashSet<ulong> pveModeOwners = new HashSet<ulong>();
            static ulong pveModeOwner;
            static float timeScienceLastZoneCreated;

            SphereCollider sphereCollider;
            HashSet<BaseEntity> spheres = new HashSet<BaseEntity>();
            Coroutine guiCoroune;

            HashSet<BasePlayer> playersInZone = new HashSet<BasePlayer>();

            internal static bool AnyPlayerInZone()
            {
                if (zoneController == null) return false;

                return zoneController.playersInZone.Any(x => x.IsRealPlayer() && x.IsConnected && !x.IsSleeping());
            }

            internal static void CreateZone(Vector3 position)
            {
                if (zoneController != null)
                    UnityEngine.GameObject.Destroy(zoneController.gameObject);

                GameObject gameObject = new GameObject();
                gameObject.transform.position = position;
                gameObject.layer = (int)Rust.Layer.Reserved1;

                zoneController = gameObject.AddComponent<ZoneController>();
                zoneController.Init();
            }

            internal static void ClearZoneData()
            {
                DeleteZone(true);

                pveModeOwners.Clear();
                pveModeOwner = 0;
                timeScienceLastZoneCreated = 0;
            }

            internal static void DeleteZone(bool cdTruePve)
            {
                if (ins._config.pveMode.pve && ins.plugins.Exists("PveMode"))
                {
                    pveModeOwners = (HashSet<ulong>)ins.PveMode.Call("GetEventOwners", ins.Name);
                    if (pveModeOwners == null) pveModeOwners = new HashSet<ulong>();

                    pveModeOwner = (ulong)ins.PveMode.Call("GetEventOwner", ins.Name);
                    ins.PveMode.Call("EventRemovePveMode", ins.Name, cdTruePve);
                }
                if (zoneController != null)
                    Destroy(zoneController.gameObject);
            }

            internal static bool IsPlayerInZone(ulong userID)
            {
                return zoneController != null && zoneController.playersInZone.Any(x => x != null && x.userID == userID);
            }

            internal static void OnPlayerLeaveZone(BasePlayer player)
            {
                if (zoneController == null) return;
                zoneController.playersInZone.Remove(player);
                if (ins._config.GUI.IsGUI) CuiHelper.DestroyUi(player, "TextMain");
            }

            internal static string GetEventOwnerPlayerName()
            {
                if (zoneController == null)
                {
                    if (pveModeOwner != 0)
                    {
                        if (Time.realtimeSinceStartup - timeScienceLastZoneCreated < ins._config.pveMode.timeExitOwner)
                        {
                            BasePlayer player = BasePlayer.FindByID(pveModeOwner);
                            if (player != null)
                                return player.displayName;
                        }
                        else
                            pveModeOwner = 0;
                    }
                    return "";
                }

                timeScienceLastZoneCreated = Time.realtimeSinceStartup;
                ulong ownerId = (ulong)ins.PveMode.Call("GetEventOwner", ins.Name);
                if (ownerId != 0)
                {
                    BasePlayer player = BasePlayer.FindByID(ownerId);
                    if (player != null)
                        return player.displayName;
                }
                return "";
            }

            void Init()
            {
                CreateTriggerSphere();

                if (ins._config.pveMode.pve && ins.plugins.Exists("PveMode"))
                    CreatePveModeZone();
                else if (ins._config.eventZone.isDome)
                    CreateSphere();

                if (ins._config.GUI.IsGUI)
                    guiCoroune = ServerMgr.Instance.StartCoroutine(GuiCoroune());
            }

            void CreateTriggerSphere()
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = ins._config.eventZone.radius;
            }

            void CreatePveModeZone()
            {
                JObject config = new JObject
                {
                    ["Damage"] = ins._config.pveMode.damage,
                    ["ScaleDamage"] = new JArray { ins._config.pveMode.scaleDamage.Select(x => new JObject { ["Type"] = x.Type, ["Scale"] = x.Scale }) },
                    ["LootCrate"] = ins._config.pveMode.lootCrate,
                    ["HackCrate"] = ins._config.pveMode.hackCrate,
                    ["LootNpc"] = ins._config.pveMode.lootNpc,
                    ["DamageNpc"] = ins._config.pveMode.damageNpc,
                    ["DamageTank"] = ins._config.pveMode.damageTank,
                    ["DamageHelicopter"] = ins._config.pveMode.damageHeli,
                    ["TargetNpc"] = ins._config.pveMode.targetNpc,
                    ["TargetTank"] = ins._config.pveMode.targetTank,
                    ["TargetHelicopter"] = ins._config.pveMode.targetHeli,
                    ["CanEnter"] = ins._config.pveMode.canEnter,
                    ["CanEnterCooldownPlayer"] = ins._config.pveMode.canEnterCooldownPlayer,
                    ["TimeExitOwner"] = ins._config.pveMode.timeExitOwner,
                    ["AlertTime"] = ins._config.pveMode.alertTime,
                    ["RestoreUponDeath"] = ins._config.pveMode.restoreUponDeath,
                    ["CooldownOwner"] = ins._config.pveMode.cooldownOwner,
                    ["Darkening"] = ins._config.pveMode.darkening
                };

                HashSet<ulong> npcs = new HashSet<ulong>();
                HashSet<ulong> bradleys = new HashSet<ulong>();
                HashSet<ulong> helicopters = new HashSet<ulong>();
                HashSet<ulong> crates = new HashSet<ulong>();

                BasePlayer playerOwner = null;
                if (pveModeOwner != 0)
                {
                    playerOwner = BasePlayer.FindByID(pveModeOwner);
                }

                if (ins.convoyController.convoyHeli != null && ins.convoyController.convoyHeli.patrolHelicopter != null)
                    helicopters.Add(ins.convoyController.convoyHeli.patrolHelicopter.net.ID.Value);

                foreach (ScientistNPC scientistNPC in ins.convoyController.roamNpc)
                    if (scientistNPC != null && scientistNPC.net != null)
                        npcs.Add(scientistNPC.net.ID.Value);

                foreach (ConvoyVehicle convoyVehicle in ins.convoyController.convoyVehicles)
                {
                    if (convoyVehicle is ConvoyBradley) bradleys.Add(convoyVehicle.baseEntity.net.ID.Value);
                }

                foreach (ConvoyTruck truck in ins.convoyController.trucks)
                {
                    if (truck != null && truck.crate != null)
                        crates.Add(truck.crate.net.ID.Value);
                }

                ins.PveMode.Call("EventAddPveMode", ins.Name, config, gameObject.transform.position, ins._config.eventZone.radius, crates, npcs, bradleys, helicopters, pveModeOwners, playerOwner);
            }

            void CreateSphere()
            {
                for (int i = 0; i < ins._config.eventZone.darkening; i++)
                {
                    BaseEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", gameObject.transform.position);
                    SphereEntity entity = sphere.GetComponent<SphereEntity>();
                    entity.currentRadius = ins._config.eventZone.radius * 2;
                    entity.lerpSpeed = 0f;
                    sphere.enableSaving = false;
                    sphere.Spawn();
                    spheres.Add(sphere);
                }
            }

            IEnumerator GuiCoroune()
            {
                while (true)
                {
                    foreach (BasePlayer player in playersInZone) MessageGUI(player, GetMessage("GUI", player.UserIDString, NotifyManager.GetTimeMessage(player.UserIDString, ins.convoyController.GetEventTime())));
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsRealPlayer())
                {
                    playersInZone.Add(player);
                    if (ins._config.GUI.IsGUI) MessageGUI(player, GetMessage("GUI", player.UserIDString, NotifyManager.GetTimeMessage(player.UserIDString, ins.convoyController.GetEventTime())));
                    if (ins._config.eventZone.isCreateZonePVP) NotifyManager.SendMessageToPlayer(player, "EnterPVP", ins._config.prefix);
                }
            }

            void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player != null && player.userID.IsSteamId())
                {
                    OnPlayerLeaveZone(player);
                    if (ins._config.eventZone.isCreateZonePVP)
                    {
                        if (ins.plugins.Exists("DynamicPVP") && (bool)ins.DynamicPVP.Call("IsPlayerInPVPDelay", player.userID)) return;
                        NotifyManager.SendMessageToPlayer(player, "ExitPVP", ins._config.prefix);
                    }
                }
            }

            void MessageGUI(BasePlayer player, string text)
            {
                CuiHelper.DestroyUi(player, "TextMain");

                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = ins._config.GUI.AnchorMin, AnchorMax = ins._config.GUI.AnchorMax },
                    CursorEnabled = false,
                }, "Hud", "TextMain");

                container.Add(new CuiElement
                {
                    Parent = "TextMain",
                    Components =
                {
                    new CuiTextComponent() { Color = "1 1 1 1", FadeIn = 0f, Text = text, FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                    new CuiOutlineComponent { Distance = "1 1", Color = "0 0 0 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
                });

                CuiHelper.AddUi(player, container);
            }

            void OnDestroy()
            {
                if (guiCoroune != null)
                    ServerMgr.Instance.StopCoroutine(guiCoroune);

                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (player != null)
                        CuiHelper.DestroyUi(player, "TextMain");

                foreach (BaseEntity sphere in spheres)
                    if (sphere != null && !sphere.IsDestroyed)
                        sphere.Kill();
            }
        }

        sealed class ConvoyMarker : FacepunchBehaviour
        {
            MapMarkerGenericRadius mapmarker;
            VendingMachineMapMarker vendingMarker;
            Coroutine updateCounter;

            float lastEventOvnerNameCheckTime = 0;
            string eventOwnerName = "";

            internal static ConvoyMarker CreateConvoyMarker()
            {
                GameObject gameObject = new GameObject();
                gameObject.layer = (int)Rust.Layer.Reserved1;

                ConvoyMarker convoyMarker = gameObject.AddComponent<ConvoyMarker>();
                convoyMarker.Init();
                return convoyMarker;
            }

            void Init()
            {
                Vector3 position = ins.convoyController.GetConvoyPosition();
                CreateRadiusMarker(position);
                CreateVendingMarker(position);
                updateCounter = ServerMgr.Instance.StartCoroutine(MarkerUpdateCounter());
            }

            void CreateRadiusMarker(Vector3 position)
            {
                if (!ins._config.marker.useRingMarker)
                    return;

                mapmarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
                mapmarker.enableSaving = false;
                mapmarker.Spawn();
                mapmarker.radius = ins._config.marker.Radius;
                mapmarker.alpha = ins._config.marker.Alpha;
                mapmarker.color1 = new Color(ins._config.marker.Color1.r, ins._config.marker.Color1.g, ins._config.marker.Color1.b);
                mapmarker.color2 = new Color(ins._config.marker.Color2.r, ins._config.marker.Color2.g, ins._config.marker.Color2.b);
            }

            void CreateVendingMarker(Vector3 position)
            {
                if (!ins._config.marker.useShopMarker)
                    return;

                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position) as VendingMachineMapMarker;
                vendingMarker.Spawn();
                vendingMarker.markerShopName = $"Convoy ({ins.convoyController.GetEventTime()} s)";
            }

            IEnumerator MarkerUpdateCounter()
            {
                while (ins.convoyController != null)
                {
                    if (ins._config.pveMode.pve && ins.plugins.Exists("PveMode"))
                    {
                        if (Time.realtimeSinceStartup - lastEventOvnerNameCheckTime >= 5)
                        {
                            eventOwnerName = ZoneController.GetEventOwnerPlayerName();
                            lastEventOvnerNameCheckTime = Time.realtimeSinceStartup;
                        }
                    }

                    Vector3 position = ins.convoyController.GetConvoyPosition();

                    if (position == Vector3.zero)
                    {
                        DeleteMarker();
                    }

                    if (mapmarker.IsExists())
                    {
                        mapmarker.transform.position = position;
                        mapmarker.SendUpdate();
                        mapmarker.SendNetworkUpdate();
                    }

                    if (vendingMarker.IsExists())
                    {
                        string displayEventOwnerName = eventOwnerName != "" ? GetMessage("Marker_EventOwner", null, eventOwnerName) : "";
                        string text = $"{ins.convoyController.convoySetting.displayName} {NotifyManager.GetTimeMessage(null, ins.convoyController.GetEventTime())} " + displayEventOwnerName;

                        vendingMarker.transform.position = position;
                        vendingMarker.markerShopName = text;
                        vendingMarker.SendNetworkUpdate();

                        if (ins._config.pveMode.pve) vendingMarker.SetFlag(BaseEntity.Flags.Busy, eventOwnerName == "");
                    }

                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            internal void DeleteMarker()
            {
                Destroy(this.gameObject);
            }

            void OnDestroy()
            {
                if (updateCounter != null) ServerMgr.Instance.StopCoroutine(updateCounter);
                if (mapmarker.IsExists()) mapmarker.Kill();
                if (vendingMarker.IsExists()) vendingMarker.Kill();
            }
        }

        sealed class RootCar : FacepunchBehaviour
        {
            internal static RootCar rootCar;
            internal BasicCar basicCar;
            internal List<Vector3> root = new List<Vector3>();
            BasePlayer player;

            internal static void RootStop()
            {
                if (rootCar != null && !rootCar.basicCar.IsDestroyed) rootCar.basicCar.Kill();
            }

            internal static void CreateRootCar(BasePlayer player)
            {
                if (rootCar != null)
                {
                    NotifyManager.SendMessageToPlayer(player, $"{ins._config.prefix} The route is <color=#738d43>already</color> being recorded!");
                    return;
                }
                NotifyManager.SendMessageToPlayer(player, $"{ins._config.prefix} To build a route, drive a car along it and write to the chat: <color=#738d43>convoyrootsave [rootgroupname]</color>\nTo reset the route, print to the chat: <color=#738d43>convoyrootstop</color>");
                BasicCar car = GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", player.transform.position + new Vector3(0, 0.3f, 0), player.eyes.GetLookRotation()) as BasicCar;
                car.enableSaving = false;
                car.Spawn();
                rootCar = car.gameObject.AddComponent<RootCar>();
                rootCar.InitSedan(player);

                MountPointInfo mountPointInfo = car.mountPoints[0];
                player.MountObject(mountPointInfo.mountable);
                mountPointInfo.mountable.MountPlayer(player);
            }

            internal static void SaveRoot(BasePlayer player, string rootName)
            {
                if (rootCar.root.Count < 50) NotifyManager.SendMessageToPlayer(player, $"{ins._config.prefix} The route is too short!");
                else
                {
                    List<string> root = new List<string>();
                    foreach (Vector3 vector in rootCar.root) root.Add(vector.ToString());
                    if (!ins.roots.ContainsKey(rootName)) ins.roots.Add(rootName, new List<List<string>>());
                    ins.roots[rootName].Add(root);
                    ins.SaveData();
                    RootStop();
                    NotifyManager.SendMessageToPlayer(player, $"{ins._config.prefix} Route added to group <color=#738d43>{rootName}</color>");
                }
            }

            void InitSedan(BasePlayer player)
            {
                basicCar = GetComponent<BasicCar>();
                root.Add(basicCar.transform.position);
            }

            void FixedUpdate()
            {
                if (Vector3.Distance(basicCar.transform.position, root[root.Count - 1]) > 3) root.Add(basicCar.transform.position);
            }
        }

        static class LootManager
        {
            internal static void UpdateConvoyLockedByEntCrate(LockedByEntCrate lockedByEntCrate, bool openImmediately, int typeOfLootTable, LootTableConfig lootTableConfig)
            {
                if (lockedByEntCrate == null) return;

                if (openImmediately)
                {
                    if (lockedByEntCrate.lockingEnt != null && lockedByEntCrate.lockingEnt.ToBaseEntity().IsExists())
                        lockedByEntCrate.lockingEnt.ToBaseEntity().Kill();
                    lockedByEntCrate.SetLockingEnt(null);
                }
                UpdateLootContainerLootTable(lockedByEntCrate.inventory, lootTableConfig, typeOfLootTable);
            }

            internal static void UpdateNpcLootContainer(NPCPlayerCorpse corpse, NpcConfig npcConfig)
            {
                ItemContainer container = corpse.containers[0];

                for (int i = container.itemList.Count - 1; i >= 0; i--)
                {
                    Item item = container.itemList[i];
                    if (npcConfig.wearItems.Any(x => x.shortName == item.info.shortname))
                    {
                        item.RemoveFromContainer();
                        item.Remove();
                    }
                }
                UpdateLootContainerLootTable(container, npcConfig.lootTable, npcConfig.typeLootTable);
                if (npcConfig.deleteCorpse && corpse != null && !corpse.IsDestroyed) corpse.Kill();
            }

            internal static void UpdateLootContainerLootTable(ItemContainer itemContainer, LootTableConfig lootTableConfig, int typeOfLootTable)
            {
                if (typeOfLootTable != 1 && typeOfLootTable != 4)
                    return;
                if (typeOfLootTable == 1)
                    ClearItemsContainer(itemContainer);

                int countLoot = UnityEngine.Random.Range(lootTableConfig.minItemsAmount, lootTableConfig.maxItemsAmount + 1);
                if (countLoot > lootTableConfig.items.Where(x => x.chance > 0).Count)
                    countLoot = lootTableConfig.items.Count;

                if (typeOfLootTable == 4)
                    itemContainer.capacity += countLoot;
                else
                    itemContainer.capacity = countLoot;

                FillContainer(itemContainer, lootTableConfig.items, countLoot);
                itemContainer.capacity = itemContainer.itemList.Count;
            }

            static void FillContainer(ItemContainer itemContainer, List<LootItemConfig> lootItems, int countLoot)
            {
                int countLootInContainer = 0;
                while (countLootInContainer < countLoot)
                {
                    HashSet<LootItemConfig> suitableItems = lootItems.Where(y => !itemContainer.itemList.Any(x => x.info.shortname == y.shortName));
                    if (suitableItems == null || suitableItems.Count == 0) return;

                    foreach (LootItemConfig item in suitableItems)
                    {
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.chance)
                        {
                            int amount = UnityEngine.Random.Range(item.minAmount, item.maxAmount + 1);
                            Item newItem = item.isBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.shortName, amount, item.skinID);
                            if (item.isBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.shortName).itemid;
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
            }

            public static void ClearItemsContainer(ItemContainer container)
            {
                for (int i = container.itemList.Count - 1; i >= 0; i--)
                {
                    Item item = container.itemList[i];
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }
        }

        static class NpcManager
        {
            internal static bool CheckNPCSpawn()
            {
                if (!ins.plugins.Exists("NpcSpawn"))
                {
                    ins.PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt!");
                    return false;
                }
                else return true;
            }

            internal static bool IsConvoyNpc(string displayName)
            {
                return GetNpcConfigByDisplayName(displayName) != null;
            }

            internal static ScientistNPC CreateNpc(JObject npcConfig, Vector3 position)
            {
                return (ScientistNPC)ins.NpcSpawn.Call("SpawnNpc", position, npcConfig);
            }

            internal static NpcConfig GetNpcConfigByDisplayName(string displayName)
            {
                return ins._config.NPC.FirstOrDefault(x => x.name == displayName);
            }

            internal static JObject GetNpcJObjectConfig(NpcConfig config, bool takeWeaponFromNpc, bool isMounted)
            {
                HashSet<string> states = new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
                JArray beltItems = new JArray
                {
                    config.beltItems.Where(x => !takeWeaponFromNpc && (!isMounted || x.shortName != "grenade.f1")).Select(x => new JObject
                    {
                        ["ShortName"] = x.shortName,
                        ["Amount"] = x.amount,
                        ["SkinID"] = x.skinID,
                        ["Mods"] = new JArray { x.Mods.ToHashSet() },
                        ["Ammo"] = x.ammo
                    })
                };

                if (!isMounted && config.beltItems.Any(x => x.shortName == "rocket.launcher" || x.shortName == "explosive.timed")) states.Add("RaidState");

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
                    ["BeltItems"] = beltItems,
                    ["Kit"] = config.kit,
                    ["Health"] = config.health,
                    ["RoamRange"] = config.roamRange,
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

        static class EconomyManager
        {
            static readonly Dictionary<ulong, double> _playersBalance = new Dictionary<ulong, double>();

            internal static void CheckIfPlayerKillConvoyVehicle(ulong vehicleNetId, string vehicleType, BasePlayer player)
            {
                ConvoyVehicle convoyVehicle = ins.convoyController.GetConvoyVehicleByEntityNetId(vehicleNetId);
                if (convoyVehicle == null || player == null) return;
                ActionEconomy(player.userID, vehicleType);
            }

            internal static void ActionEconomy(ulong playerId, string type, string arg = "")
            {
                switch (type)
                {
                    case "Bradley":
                        AddBalance(playerId, ins._config.economyConfig.bradley);
                        break;
                    case "Npc":
                        AddBalance(playerId, ins._config.economyConfig.npc);
                        break;
                    case "LockedCrate":
                        AddBalance(playerId, ins._config.economyConfig.lockedCrate);
                        break;
                    case "Heli":
                        AddBalance(playerId, ins._config.economyConfig.heli);
                        break;
                    case "Sedan":
                        AddBalance(playerId, ins._config.economyConfig.sedan);
                        break;
                    case "Modular":
                        AddBalance(playerId, ins._config.economyConfig.modularCar);
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
                if (!ins._config.economyConfig.enable || _playersBalance.Count == 0) return;
                foreach (KeyValuePair<ulong, double> dic in _playersBalance)
                {
                    if (dic.Value < ins._config.economyConfig.minEconomyPiont) continue;
                    int intCount = Convert.ToInt32(dic.Value);
                    if (ins._config.economyConfig.plugins.Contains("Economics") && ins.plugins.Exists("Economics") && dic.Value > 0) ins.Economics.Call("Deposit", dic.Key.ToString(), dic.Value);
                    if (ins._config.economyConfig.plugins.Contains("Server Rewards") && ins.plugins.Exists("ServerRewards") && intCount > 0) ins.ServerRewards.Call("AddPoints", dic.Key, intCount);
                    if (ins._config.economyConfig.plugins.Contains("IQEconomic") && ins.plugins.Exists("IQEconomic") && intCount > 0) ins.IQEconomic.Call("API_SET_BALANCE", dic.Key, intCount);
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
                DefineEventWinner();
                if (max >= ins._config.economyConfig.minCommandPoint) foreach (string command in ins._config.economyConfig.commands) ins.Server.Command(command.Replace("{steamid}", $"{winnerId}"));
                _playersBalance.Clear();
            }

            static void DefineEventWinner()
            {
                if (_playersBalance.Count == 0) return;
                float maxPoint = (float)_playersBalance.Max(x => (float)x.Value).Value;
                var winnerPair = _playersBalance.FirstOrDefault(x => x.Value == maxPoint);
                if (winnerPair.Value > 0) Interface.CallHook("OnConvoyEventWin", winnerPair.Key);
            }
        }

        static class NotifyManager
        {
            internal static void PrintError(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null) ins.PrintError(ClearColorAndSize(GetMessage(langKey, null, args)));
                else ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static void PrintLogMessage(string langKey, params object[] args)
            {
                ins.Puts(ClearColorAndSize(GetMessage(langKey, null, args)));
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
                foreach (BasePlayer player in BasePlayer.activePlayerList) if (player != null) SendMessageToPlayer(player, langKey, args);
                SendDiscordMessage(langKey, args);
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] is int) args[i] = GetTimeMessage(player.UserIDString, (int)args[i]);
                }

                if (ins._config.IsChat) ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
                if (ins._config.GUIAnnouncements.isGUIAnnouncements) ins.GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(GetMessage(langKey, player.UserIDString, args)), ins._config.GUIAnnouncements.bannerColor, ins._config.GUIAnnouncements.textColor, player, ins._config.GUIAnnouncements.apiAdjustVPosition);
                if (ins._config.Notify.IsNotify) player.SendConsoleCommand($"notify.show {ins._config.Notify.Type} {ClearColorAndSize(GetMessage(langKey, player.UserIDString, args))}");
            }

            static void SendDiscordMessage(string langKey, params object[] args)
            {
                if (CanSendDiscordMessage() && ins._config.discord.keys.Contains(langKey))
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] is int) args[i] = GetTimeMessage(null, (int)args[i]);
                    }

                    object fields = new[] { new { name = ins.Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                    ins.DiscordMessages?.Call("API_SendFancyMessage", ins._config.discord.webhookUrl, "", ins._config.discord.embedColor, JsonConvert.SerializeObject(fields), null, ins);
                }
            }

            static bool CanSendDiscordMessage() => ins._config.discord.isDiscord && !string.IsNullOrEmpty(ins._config.discord.webhookUrl) && ins._config.discord.webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            internal static string GetTimeMessage(string userIDString, int seconds)
            {
                string message = "";

                TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
                if (timeSpan.Hours > 0) message += $" {timeSpan.Hours} {GetMessage("Hours", userIDString)}";
                if (timeSpan.Minutes > 0) message += $" {timeSpan.Minutes} {GetMessage("Minutes", userIDString)}";
                if (message == "") message += $" {timeSpan.Seconds} {GetMessage("Seconds", userIDString)}";

                return message;
            }
        }

        sealed class DeathHeliOrBradleyData
        {
            internal string presetName;
            internal Vector3 deathPosition;
        }

        static class GlobalPositionDefiner
        {
            internal static Vector3 GetGlobalPosition(Transform parentTransform, Vector3 position)
            {
                return parentTransform.transform.TransformPoint(position);
            }

            internal static Quaternion GetGlobalRotation(Transform parentTransform, Vector3 rotation)
            {
                return parentTransform.rotation * Quaternion.Euler(rotation);
            }
        }
        #endregion Classes 

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "Cet événement est actif maintenant. Terminez l'événement en cours ! écrivez <color=#ce3f27>/convoystop</color>) !",
                ["СonfigurationNotFound_Exeption"] = "La configuration de l'événement <color=#ce3f27>n'a pas pu</color> être trouvée !",
                ["RootNotFound_Exeption"] = "<color=#ce3f27>Could not</color> find the route for the convoy! ({0})",

                ["PreStart"] = "{0} Dans <color=#738d43>{1}</color>, la cargaison sera transportée le long de la route !",
                ["ConvoyAttacked"] = "{0} {1} <color=#ce3f27>a attaqué</color> un convoi",
                ["EventStart"] = "{0} {1} <color=#738d43>démarré</color> le déplacement",

                ["SecurityNpcKill"] = "{0} Les PNJ sont tous <color=#738d43>morts</color> !",
                ["VehiclesKill"] = "{0} Les véhicules qui les accompagnent sont <color=#738d43>détruits</color> !",


                ["Failed"] = "The cargo truck has been <color=#ce3f27>destroyed</color>! The loot is <color=#ce3f27>lost</color>",
                ["StartHackCrate"] = "{0} {1} a commencé à <color=#738d43>pirater</color> la caisse verrouillée !",
                ["PreFinish"] = "{0} L'événement se terminera dans <color=#ce3f27>{1}</color> secondes !",
                ["Finish"] = "{0} The event is <color=#ce3f27>over</color>",
                ["CantHackCrate"] = "{0} Pour ouvrir la caisse, tuez tous les <color=#ce3f27>véhicules qui l'accompagnent</color> !",
                ["EventActive"] = "{0} Cet événement est actif maintenant. Pour terminer cet événement, écrivez <color=#ce3f27/convoystop</color>) !",
                ["EnterPVP"] = "{0} Vous <color=#ce3f27>êtes entré</color> dans une zone PVP, les autres joueurs <color=#ce3f27>peuvent vous attaquer</color> !",
                ["ExitPVP"] = "{0} Vous <color=#738d43>êtes sortis</color> de la zone PVP, les autres joueurs <color=#738d43>ne peuvent plus vous attaquer</color> !",
                ["GUI"] = "La cargaison sera détruite dans <color=#ce3f27>{0}</color> secondes",
                ["SendEconomy"] = "{0} Vous <color=#738d43>avez gagné</color> <color=#55aaff>{1}</color> points en participant à l'événement !",
                ["Hours"] = "h.",
                ["Minutes"] = "m.",
                ["Seconds"] = "s.",
                ["Distance"] = "Rapprochez-vous <color=#ce3f27>plus près</color> !",
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "Ивент в данный момент активен, сначала завершите текущий ивент (<color=#ce3f27>/convoystop</color>)!",
                ["СonfigurationNotFound_Exeption"] = "<color=#ce3f27>Не удалось</color> найти конфигурацию ивента!",
                ["RootNotFound_Exeption"] = "<color=#ce3f27>Не удалось</color> построить маршрут для конвоя {0}!",

                ["PreStart"] = "{0} Через <color=#738d43>{1}</color>. начнется перевозка груза по автодороге!",
                ["ConvoyAttacked"] = "{0} {1} <color=#ce3f27>напал</color> на конвой",
                ["EventStart"] = "{0} {1} <color=#738d43>начал</color> движение!",

                ["SecurityNpcKill"] = "{0} Охрана конвоя была <color=#738d43>уничтожена</color>!",
                ["VehiclesKill"] = "{0} Все сопровождающие транспортные средства <color=#738d43>уничтожены</color>!",


                ["Failed"] = "{0} Грузовик <color=#ce3f27>уничтожен</color>! Добыча <color=#ce3f27>потеряна</color>!",
                ["StartHackCrate"] = "{0} {1} <color=#738d43>начал</color> взлом заблокированного ящика!",
                ["PreFinish"] = "{0} Ивент будет окончен через <color=#ce3f27>{1}</color>",
                ["Finish"] = "{0} Перевозка груза <color=#ce3f27>окончена</color>!",
                ["CantHackCrate"] = "{0} Для того чтобы открыть ящик убейте все <color=#ce3f27>сопровождающие</color> транспортные средства!",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#ce3f27>/convoystop</color>)!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["GUI"] = "Груз будет уничтожен через <color=#ce3f27>{0}</color>",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",
                ["Hours"] = "ч.",
                ["Minutes"] = "м.",
                ["Seconds"] = "с.",
                ["Distance"] = "Подойдите <color=#ce3f27>ближе</color>!",
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "This event is active now. Finish the current event! (<color=#ce3f27>/convoystop</color>)!",
                ["СonfigurationNotFound_Exeption"] = "The event configuration <color=#ce3f27>could not</color> be found!",
                ["RootNotFound_Exeption"] = "<color=#ce3f27>Could not</color> find the route for the convoy! ({0})",

                ["PreStart"] = "{0} In <color=#738d43>{1}</color> the cargo will be transported along the road!",
                ["ConvoyAttacked"] = "{0} {1} <color=#ce3f27>attacked</color> a convoy",
                ["EventStart"] = "{0} {1} <color=#738d43>started</color> moving",


                ["SecurityNpcKill"] = "{0} The NPCs are <color=#738d43>destroyed</color>!",
                ["VehiclesKill"] = "{0} The Accompanying vehicles are <color=#738d43>destroyed</color>!",

                ["Failed"] = "{0} The cargo truck has been <color=#ce3f27>destroyed</color>! The loot is <color=#ce3f27>lost</color>!",
                ["StartHackCrate"] = "{0} {1} started <color=#738d43>hacking</color> the locked crate!",
                ["PreFinish"] = "{0} The event will be over in <color=#ce3f27>{1}</color>",
                ["Finish"] = "{0} The event is <color=#ce3f27>over</color>!",
                ["CantHackCrate"] = "{0} To open the crate, kill all the <color=#ce3f27>accompanying</color> vehicles!",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#ce3f27/convoystop</color>)!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have gone out</color> the PVP zone, now other players <color=#738d43>can’t damage</color> you!",
                ["GUI"] = "The cargo will be destroyed in <color=#ce3f27>{0}</color>",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> points in economics for participating in the event",
                ["Hours"] = "h.",
                ["Minutes"] = "m.",
                ["Seconds"] = "s.",
                ["Distance"] = "Come <color=#ce3f27>closer</color>!",

                ["Marker_EventOwner"] = "Event Owner: {0}",

                ["EventStart_Log"] = "The event has begun! (Preset name - {0})",
                ["EventStop_Log"] = "The event is over!",
            }, this);
        }

        internal static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        internal static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Data 

        Dictionary<string, List<List<string>>> roots = new Dictionary<string, List<List<string>>>();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Title, roots);

        private void LoadData() => roots = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<List<string>>>>(Title);

        #endregion Data 

        #region Config  

        PluginConfig _config;

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        public class ConvoySetting
        {
            [JsonProperty(en ? "Name" : "Название пресета", Order = 0)] public string name { get; set; }
            [JsonProperty(en ? "Name displayed on the map (For custom marker)" : "Отображаемое на карте название (для кастомного маркера)", Order = 1)] public string displayName { get; set; }
            [JsonProperty(en ? "Speed" : "Скорость конвоя", Order = 2)] public float speed { get; set; }
            [JsonProperty(en ? "Automatic startup" : "Автоматический запуск", Order = 3)] public bool on { get; set; }
            [JsonProperty(en ? "Probability of a preset [0.0-100.0]" : "Вероятность пресета [0.0-100.0]", Order = 4)] public float chance { get; set; }
            [JsonProperty(en ? "Order of vehicles" : "Порядок транспортных средств", Order = 5)] public List<string> vehiclesOrder { get; set; }
            [JsonProperty(en ? "Enable the helicopter" : "Включить вертолет", Order = 6)] public bool heliOn { get; set; }
            [JsonProperty(en ? "Heli preset" : "Пресет вертолета", Order = 7)] public string heliConfigurationName { get; set; }
        }

        public class ModularConfig : SupportModularConfig
        {
            [JsonProperty(en ? "Change the hacking time of locked crate [true/false]" : "Изменять время взлома заблокированного ящика [true/false]", Order = 100)] public bool changeUnlockTime { get; set; }
            [JsonProperty(en ? "Time to unlock the crates [sec.]" : "Время до открытия заблокированного ящика [sec.]", Order = 100)] public float crateUnlockTime { get; set; }
            [JsonProperty(en ? "Location of the locked crate" : "Расположение заблокированного ящика", Order = 101)] public CoordConfig crateLocation { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)", Order = 102)] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута", Order = 103)] public LootTableConfig lootTable { get; set; }
        }

        public class SupportModularConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета", Order = 0)] public string presetName { get; set; }
            [JsonProperty(en ? "Prefab Name" : "Название префаба машины", Order = 1)] public string prefabName { get; set; }
            [JsonProperty(en ? "Scale damage" : "Множитель урона", Order = 2)] public float damageMultiplier { get; set; }
            [JsonProperty(en ? "Modules" : "Модули", Order = 3)] public List<string> modules { get; set; }
            [JsonProperty(en ? "Distance to the vehicle in front" : "Дистанция до впереди идущего транспортного средства", Order = 4)] public float frontVehicleDistance { get; set; }
            [JsonProperty(en ? "NPC preset" : "Пресет НПС", Order = 5)] public string npcName { get; set; }
            [JsonProperty(en ? "Number of NPCs" : "Количество НПС", Order = 6)] public int numberOfNpc { get; set; }
        }

        public class SedanConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета", Order = 0)] public string presetName { get; set; }
            [JsonProperty("HP", Order = 1)] public float hp { get; set; }
            [JsonProperty(en ? "Distance to the vehicle in front" : "Дистанция до впереди идущего транспортного средства", Order = 2)] public float frontVehicleDistance { get; set; }
            [JsonProperty(en ? "NPC preset" : "Пресет НПС", Order = 3)] public string npcName { get; set; }
            [JsonProperty(en ? "Number of NPCs" : "Количество НПС", Order = 4)] public int numberOfNpc { get; set; }
        }

        public class BradleyConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета", Order = 0)] public string presetName { get; set; }
            [JsonProperty("HP", Order = 1)] public float hp { get; set; }
            [JsonProperty(en ? "The viewing distance" : "Дальность обзора", Order = 2)] public float viewDistance { get; set; }
            [JsonProperty(en ? "Radius of search" : "Радиус поиска", Order = 3)] public float searchDistance { get; set; }
            [JsonProperty(en ? "The multiplier of Machine-gun aim cone" : "Множитель разброса пулемёта", Order = 4)] public float coaxAimCone { get; set; }
            [JsonProperty(en ? "The multiplier of Machine-gun fire rate" : "Множитель скорострельности пулемёта", Order = 5)] public float coaxFireRate { get; set; }
            [JsonProperty(en ? "Amount of Machine-gun burst shots" : "Кол-во выстрелов очереди пулемёта", Order = 6)] public int coaxBurstLength { get; set; }
            [JsonProperty(en ? "The time between shots of the main gun [sec.]" : "Время между залпами основного орудия [sec.]", Order = 7)] public float nextFireTime { get; set; }
            [JsonProperty(en ? "The time between shots of the main gun in a fire rate [sec.]" : "Время между выстрелами основного орудия в залпе [sec.]", Order = 8)] public float topTurretFireRate { get; set; }
            [JsonProperty(en ? "Numbers of crates" : "Кол-во ящиков после уничтожения", Order = 9)] public int countCrates { get; set; }
            [JsonProperty(en ? "Distance to the vehicle in front" : "Дистанция до впереди идущего транспортного средства", Order = 10)] public float frontVehicleDistance { get; set; }
            [JsonProperty(en ? "NPC preset" : "Пресет НПС", Order = 11)] public string npcName { get; set; }
            [JsonProperty(en ? "Number of NPCs" : "Количество НПС", Order = 12)] public int numberOfNpc { get; set; }

            [JsonProperty(en ? "Open crates after spawn [true/false]" : "Открывать ящики сразу после спавна [true/false]", Order = 13)] public bool offDelay { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)", Order = 14)] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута", Order = 15)] public LootTableConfig lootTable { get; set; }
        }

        public class HeliConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty("HP")] public float hp { get; set; }
            [JsonProperty(en ? "HP of the main rotor" : "HP главного винта")] public float mainRotorHealth { get; set; }
            [JsonProperty(en ? "HP of tail rotor" : "HP хвостового винта")] public float rearRotorHealth { get; set; }
            [JsonProperty(en ? "Numbers of crates" : "Количество ящиков")] public int cratesAmount { get; set; }
            [JsonProperty(en ? "Flying height" : "Высота полета")] public float height { get; set; }
            [JsonProperty(en ? "Bullet speed" : "Скорость пуль")] public float bulletSpeed { get; set; }
            [JsonProperty(en ? "Bullet Damage" : "Урон пуль")] public float bulletDamage { get; set; }
            [JsonProperty(en ? "The distance to which the helicopter can move away from the convoy" : "Дистанция, на которую вертолет может отдаляться от конвоя")] public float distance { get; set; }
            [JsonProperty(en ? "Open crates after spawn [true/false]" : "Открывать ящики после спавна [true/false]")] public bool offDelay { get; set; }
            [JsonProperty(en ? "The time for which the helicopter can leave the convoy to attack the target [sec.]" : "Время, на которое верталет может покидать конвой для атаки цели [sec.]")] public float outsideTime { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)")] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable { get; set; }

        }

        public class NpcConfig
        {
            [JsonProperty(en ? "Name" : "Название")] public string name { get; set; }
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
            [JsonProperty(en ? "Wear items" : "Одежда")] public List<NpcWear> wearItems { get; set; }
            [JsonProperty(en ? "Belt items" : "Быстрые слоты")] public List<NpcBelt> beltItems { get; set; }
            [JsonProperty(en ? "Turret damage scale" : "Множитель урона от турелей")] public float turretDamageScale { get; set; }
            [JsonProperty(en ? "Kit" : "Kit")] public string kit { get; set; }
            [JsonProperty(en ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool disableRadio { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную)")] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable { get; set; }
        }

        public class CoordConfig
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

        public class LootTableConfig
        {
            [JsonProperty(en ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int minItemsAmount { get; set; }
            [JsonProperty(en ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int maxItemsAmount { get; set; }
            [JsonProperty(en ? "List of items" : "Список предметов")] public List<LootItemConfig> items { get; set; }
        }

        public class LootItemConfig
        {
            [JsonProperty("ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "Minimum" : "Минимальное кол-во")] public int minAmount { get; set; }
            [JsonProperty(en ? "Maximum" : "Максимальное кол-во")] public int maxAmount { get; set; }
            [JsonProperty(en ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float chance { get; set; }
            [JsonProperty(en ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool isBluePrint { get; set; }
            [JsonProperty(en ? "SkinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")] public string name { get; set; }
        }

        public class DomeConfig
        {
            [JsonProperty(en ? "Create a PVP zone in the convoy stop zone? (only for those who use the TruePVE plugin)[true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool isCreateZonePVP { get; set; }
            [JsonProperty(en ? "Use the dome? [true/false]" : "Использовать ли купол? [true/false]")] public bool isDome { get; set; }
            [JsonProperty(en ? "Darkening the dome" : "Затемнение купола")] public int darkening { get; set; }
            [JsonProperty(en ? "Radius" : "Радиус")] public float radius { get; set; }
            [JsonProperty(en ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool blockRestoreUponDeath { get; set; }
        }

        public class GUIConfig
        {
            [JsonProperty(en ? "Use the Countdown GUI? [true/false]" : "Использовать ли GUI обратного отсчета? [true/false]")] public bool IsGUI { get; set; }
            [JsonProperty("AnchorMin")] public string AnchorMin { get; set; }
            [JsonProperty("AnchorMax")] public string AnchorMax { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float r { get; set; }
            [JsonProperty("g")] public float g { get; set; }
            [JsonProperty("b")] public float b { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(en ? "Do you use the Marker? [true/false]" : "Использовать ли маркер? [true/false]")] public bool IsMarker { get; set; }
            [JsonProperty(en ? "Use a shop marker? [true/false]" : "Добавить маркер магазина? [true/false]")] public bool useShopMarker { get; set; }
            [JsonProperty(en ? "Use a circular marker? [true/false]" : "Добавить круговой маркер? [true/false]")] public bool useRingMarker { get; set; }
            [JsonProperty(en ? "Radius" : "Радиус")] public float Radius { get; set; }
            [JsonProperty(en ? "Alpha" : "Прозрачность")] public float Alpha { get; set; }
            [JsonProperty(en ? "Marker color" : "Цвет маркера")] public ColorConfig Color1 { get; set; }
            [JsonProperty(en ? "Outline color" : "Цвет контура")] public ColorConfig Color2 { get; set; }
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(en ? "Do you use the GUI Announcements? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool isGUIAnnouncements { get; set; }
            [JsonProperty(en ? "Banner color" : "Цвет баннера")] public string bannerColor { get; set; }
            [JsonProperty(en ? "Text color" : "Цвет текста")] public string textColor { get; set; }
            [JsonProperty(en ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(en ? "Do you use the Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool IsNotify { get; set; }
            [JsonProperty(en ? "Type" : "Тип")] public string Type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(en ? "Do you use the Discord? [true/false]" : "Использовать ли Discord? [true/false]")] public bool isDiscord { get; set; }
            [JsonProperty("Webhook URL")] public string webhookUrl;
            [JsonProperty(en ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int embedColor { get; set; }
            [JsonProperty(en ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> keys { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(en ? "Use the PVE mode of the plugin? [true/false]" : "Использовать PVE режим работы плагина? [true/false]")] public bool pve { get; set; }
            [JsonProperty(en ? "Display the name of the event owner on a marker on the map? [true/false]" : "Отображать имя владелца ивента на маркере на карте? [true/false]")] public bool showEventOwnerNameOnMap { get; set; }
            [JsonProperty(en ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float damage { get; set; }
            [JsonProperty(en ? "Damage coefficients for calculate to become the Event Owner" : "Коэффициенты урона для подсчета, чтобы стать владельцем события")] public HashSet<ScaleDamageConfig> scaleDamage { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool lootCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event hack locked crates? [true/false]" : "Может ли не владелец ивента взламывать заблокированные ящики? [true/false]")] public bool hackCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool lootNpc { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool damageNpc { get; set; }
            [JsonProperty(en ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool targetNpc { get; set; }
            [JsonProperty(en ? "Can Bradley attack a non-owner of the event? [true/false]" : "Может ли Bradley атаковать не владельца ивента? [true/false]")] public bool targetTank { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event do damage to Bradley? [true/false]" : "Может ли не владелец ивента наносить урон по Bradley? [true/false]")] public bool damageTank { get; set; }
            [JsonProperty(en ? "Can Helicopter attack a non-owner of the event? [true/false]" : "Может ли Вертолет атаковать не владельца ивента? [true/false]")] public bool targetHeli { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event do damage to Helicopter? [true/false]" : "Может ли не владелец ивента наносить урон по Вертолету? [true/false]")] public bool damageHeli { get; set; }
            [JsonProperty(en ? "Allow the non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")] public bool canEnter { get; set; }
            [JsonProperty(en ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")] public bool canEnterCooldownPlayer { get; set; }
            [JsonProperty(en ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")] public int timeExitOwner { get; set; }
            [JsonProperty(en ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")] public int alertTime { get; set; }
            [JsonProperty(en ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool restoreUponDeath { get; set; }
            [JsonProperty(en ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double cooldownOwner { get; set; }
            [JsonProperty(en ? "Darkening the dome (0 - disables the dome)" : "Затемнение купола (0 - отключает купол)")] public int darkening { get; set; }
        }

        public class ScaleDamageConfig
        {
            [JsonProperty(en ? "Type of target" : "Тип цели")] public string Type { get; set; }
            [JsonProperty(en ? "Damage Multiplier" : "Множитель урона")] public float Scale { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(en ? "Enable economy" : "Включить экономику?")] public bool enable { get; set; }
            [JsonProperty(en ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> plugins { get; set; }
            [JsonProperty(en ? "The minimum value that a player must collect to get points for the economy" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double minEconomyPiont { get; set; }
            [JsonProperty(en ? "The minimum value that a winner must collect to make the commands work" : "Минимальное значение, которое победитель должен заработать, чтобы сработали команды")] public double minCommandPoint { get; set; }
            [JsonProperty(en ? "Killing an NPC" : "Убийство NPC")] public double npc { get; set; }
            [JsonProperty(en ? "Killing an Bradley" : "Уничтожение Bradley")] public double bradley { get; set; }
            [JsonProperty(en ? "Killing an Heli" : "Уничтожение вертолета")] public double heli { get; set; }
            [JsonProperty(en ? "Killing an sedan" : "Уничтожение седана")] public double sedan { get; set; }
            [JsonProperty(en ? "Killing an mpdular Car" : "Уничтожение модульной машины")] public double modularCar { get; set; }
            [JsonProperty(en ? "Hacking a locked crate" : "Взлом заблокированного ящика")] public double lockedCrate { get; set; }
            [JsonProperty(en ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> commands { get; set; }
        }

        public class BetterNpcConfig
        {
            [JsonProperty(en ? "Allow Npc spawn after destroying Bradley" : "Разрешить спавн Npc после уничтожения Бредли")] public bool bradleyNpc { get; set; }
            [JsonProperty(en ? "Allow Npc spawn after destroying Heli" : "Разрешить спавн Npc после уничтожения Вертолета")] public bool heliNpc { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия плагина", Order = 0)] public string version { get; set; }
            [JsonProperty(en ? "Prefix of chat messages" : "Префикс в чате", Order = 1)] public string prefix { get; set; }
            [JsonProperty(en ? "Enable automatic event holding [true/false]" : "Включить автоматическое проведение ивента [true/false]", Order = 2)] public bool autoEvent { get; set; }
            [JsonProperty(en ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]", Order = 3)] public int minTimeBetweenEvent { get; set; }
            [JsonProperty(en ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]", Order = 4)] public int maxTimeBetweenEvent { get; set; }
            [JsonProperty(en ? "Duration of the event [sec.]" : "Длительность ивента [sec.]", Order = 5)] public int eventTime { get; set; }
            [JsonProperty(en ? "Use a chat? [true/false]" : "Использовать ли чат? [true/false]", Order = 6)] public bool IsChat { get; set; }
            [JsonProperty(en ? "Time before the starting of the event after receiving a chat message [sec.]" : "Время до начала ивента после сообщения в чате [sec.]", Order = 7)] public int preStartTime { get; set; }
            [JsonProperty(en ? "The time until the end of the event, when a message is displayed about the time until the end of the event [sec]" : "Время до конца ивента, когда выводится сообщение о сокром окончании ивента [sec]", Order = 9)] public HashSet<int> timeNotifications { get; set; }
            [JsonProperty(en ? "If there is a ring road on the map, then the event will be held on it" : "Если на карте есть кольцевая дорога, то ивент будет проводиться на ней", Order = 10)] public bool rounRoadPriority { get; set; }
            [JsonProperty(en ? "The minimum length of the road on which the event can be held" : "Минимальное длина дороги, на которой может проводиться ивент", Order = 11)] public int roadLength { get; set; }
            [JsonProperty(en ? "Custom route name" : "Пресет кастомного маршрута", Order = 13)] public string customRootName { get; set; }
            [JsonProperty(en ? "The time for which the convoy stops moving after receiving damage [sec.]" : "Время, на которое останавливается конвой, после получения урона [sec.]", Order = 14)] public int damamageStopTime { get; set; }
            [JsonProperty(en ? "Maximum distance for dealing damage to the convoy" : "Максимальное расстояние для нанесения урона конвою", Order = 15)] public float maxDamageDistance { get; set; }
            [JsonProperty(en ? "The turrets of the players who attacked the convoy will shoot at the convoy? [true/false]" : "Турели игроков которые атаковали конвой, будут стрелять по конвою? [true/false]", Order = 24)] public bool enablePlayerTurret { get; set; }
            [JsonProperty(en ? "The convoy attacks first [true/false]" : "Конвой атакует первым [true/false]", Order = 16)] public bool isAggressive { get; set; }
            [JsonProperty(en ? "If an NPC has been killed, it will not spawn at the next stop of the convoy [true/false]" : "Если NPC был убит, то он не будет поялвляться при следующей остановке конвоя [true/false]", Order = 17)] public bool blockSpawnDieNpc { get; set; }
            [JsonProperty(en ? "It is necessary to stop the convoy to open the crate" : "Необходимо остановить конвой, чтобы открыть ящик", Order = 18)] public bool needStopConvoy { get; set; }
            [JsonProperty(en ? "It is necessary to kill all vehicles to open the crate" : "Необходимо убить все машины, чтобы открыть ящик", Order = 19)] public bool needKillCars { get; set; }
            [JsonProperty(en ? "It is necessary to kill all NPC to open the crate" : "Необходимо убить всех NPC, чтобы открыть ящик", Order = 20)] public bool needKillNpc { get; set; }
            [JsonProperty(en ? "Remove obstacles in front of the convoy [true/false]" : "Удалять преграды перед конвоем [true/false]", Order = 21)] public bool deleteBarriers { get; set; }
            [JsonProperty(en ? "Destroy the convoy after opening all the crates or destroying all the trucks [true/false]" : "Уничтожать конвой после открытия всех ящиков или уничтожения всех грузовиков [true/false]", Order = 23)] public bool killConvoyAfterLoot { get; set; }
            [JsonProperty(en ? "Time to destroy the convoy after opening all the crates [sec]" : "Время до уничтожения конвоя после открытия всех ящиков [sec]", Order = 24)] public int killTimeConvoyAfterLoot { get; set; }
            [JsonProperty(en ? "When the truck is destroyed, the crate will remain [true/false]" : "При уничтножении грузовика ящик будет падать на землю [true/false]", Order = 24)] public bool dropCrateAfterKillTruck { get; set; }
            [JsonProperty(en ? "The event will not end if there are players nearby [true/false]" : "Ивент не будет заканчиваться, если рядом есть игроки [true/false]", Order = 24)] public bool dontStopEventIfPlayerInZone { get; set; }
            [JsonProperty(en ? "Damage multiplier from Bradley to buildings (0 - do not change)" : "Множитель урона от бредли по постройкам (0 - не изменять)", Order = 24)] public float bradleyBuildingDamageScale { get; set; }
            [JsonProperty(en ? "Enable logging of the start and end of the event? [true/false]" : "Включить логирование начала и окончания ивента? [true/false]", Order = 24)] public bool enableStartStopLogs { get; set; }
            


            [JsonProperty(en ? "Blocked roads (command /convoyroadblock)" : "Заблокированные дороги (команда /convoyroadblock)", Order = 25)] public List<int> blockRoads { get; set; }
            [JsonProperty(en ? "Convoy Presets" : "Пресеты конвоя", Order = 26)] public List<ConvoySetting> convoys { get; set; }
            [JsonProperty(en ? "Marker Setting" : "Настройки маркера", Order = 27)] public MarkerConfig marker { get; set; }
            [JsonProperty(en ? "Event zone" : "Настройка зоны ивента", Order = 28)] public DomeConfig eventZone { get; set; }
            [JsonProperty("GUI", Order = 29)] public GUIConfig GUI { get; set; }
            [JsonProperty(en ? "Bradley Configurations" : "Кофигурации бредли", Order = 30)] public List<BradleyConfig> bradleyConfiguration { get; set; }
            [JsonProperty(en ? "Sedan Configurations" : "Кофигурации седанов", Order = 31)] public List<SedanConfig> sedanConfiguration { get; set; }
            [JsonProperty(en ? "Truck Configurations" : "Кофигурации грузовиков", Order = 32)] public List<ModularConfig> modularConfiguration { get; set; }
            [JsonProperty(en ? "Modular Configurations" : "Кофигурации модульных машин", Order = 33)] public List<SupportModularConfig> supportModularConfiguration { get; set; }
            [JsonProperty(en ? "Heli Configurations" : "Кофигурации вертолетов", Order = 34)] public List<HeliConfig> heliesConfiguration { get; set; }
            [JsonProperty(en ? "NPC Configurations" : "Кофигурации NPC", Order = 35)] public List<NpcConfig> NPC { get; set; }
            [JsonProperty(en ? "List of obstacles" : "Список преград", Order = 99)] public List<string> barriers { get; set; }
            [JsonProperty(en ? "Discord setting (only for DiscordMessages)" : "Настройка оповещений в Discord (только для DiscordMessages)", Order = 100)] public DiscordConfig discord { get; set; }
            [JsonProperty(en ? "Notify setting" : "Настройка Notify", Order = 101)] public NotifyConfig Notify { get; set; }
            [JsonProperty(en ? "GUI Announcements setting" : "Настройка GUI Announcements", Order = 102)] public GUIAnnouncementsConfig GUIAnnouncements { get; set; }
            [JsonProperty(en ? "BetterNpc Setting" : "Настройка плагина BetterNpc", Order = 103)] public BetterNpcConfig betterNpcConfig { get; set; }
            [JsonProperty(en ? "Setting Up the economy" : "Настройка экономики", Order = 104)] public EconomyConfig economyConfig { get; set; }
            [JsonProperty(en ? "PVE Mode Setting (only for users PveMode plugin)" : "Настройка PVE режима работы плагина (только для тех, кто использует плагин PveMode)", Order = 105)] public PveModeConfig pveMode { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = "2.4.1",
                    prefix = "[Convoy]",
                    autoEvent = true,
                    minTimeBetweenEvent = 3600,
                    maxTimeBetweenEvent = 3600,
                    eventTime = 3600,
                    IsChat = true,
                    preStartTime = 300,
                    timeNotifications = new HashSet<int>
                    {
                        300,
                        60,
                        30,
                        5
                    },
                    rounRoadPriority = true,
                    roadLength = 200,
                    customRootName = "",
                    damamageStopTime = 300,
                    maxDamageDistance = 100f,
                    enablePlayerTurret = true,
                    isAggressive = true,
                    blockSpawnDieNpc = false,
                    needStopConvoy = true,
                    needKillCars = true,
                    needKillNpc = false,
                    deleteBarriers = true,
                    killConvoyAfterLoot = true,
                    killTimeConvoyAfterLoot = 300,
                    dropCrateAfterKillTruck = false,
                    dontStopEventIfPlayerInZone = false,
                    bradleyBuildingDamageScale = 1f,
                    enableStartStopLogs = false,
                    

                    barriers = new List<string>
                    {
                        "minicopter.entity",
                        "scraptransporthelicopter",
                        "rowboat",
                        "rhib",
                        "1module_passengers_armored",
                        "2module_car_spawned.entity",
                        "3module_car_spawned.entity",
                        "4module_car_spawned.entity",
                        "hotairballoon",
                        "saddletest",
                        "testridablehorse",
                        "servergibs_bradley",
                        "loot_barrel_1",
                        "loot_barrel_2",
                        "loot-barrel-2",
                        "loot-barrel-1",
                        "oil_barrel",
                        "snowmobile",
                        "tomahasnowmobile",
                        "trainwagona.entity",
                        "trainwagonb.entity",
                        "trainwagonc.entity",
                        "trainwagond.entity",
                        "workcart_aboveground.entity",
                        "workcart_aboveground2.entity",
                        "locomotive.entity",
                        "trainwagonunloadable.entity",
                        "trainwagonunloadablefuel.entity",
                        "trainwagonunloadableloot.entity",
                        "xmasportalentry",
                        "stone-ore",
                        "sulfur-ore",
                        "metal-ore",
                    },
                    blockRoads = new List<int>(),
                    convoys = new List<ConvoySetting>
                    {
                        new ConvoySetting
                        {
                            name = "standart",
                            displayName = en ? "Convoy" : "Конвой",
                            speed = 4,
                            chance = 75,
                            on = true,
                            vehiclesOrder = new List<string>
                            {
                                "modular_1",
                                "sedan_1",
                                "truck_1",
                                "sedan_1",
                                "modular_1",
                            },
                            heliOn = false,
                            heliConfigurationName = "heli_1"
                        },
                        new ConvoySetting
                        {
                            name = "hard",
                            displayName = en ? "Reinforced convoy" : "Усиленный конвой",
                            speed = 5,
                            chance = 25,
                            on = true,
                            vehiclesOrder = new List<string>
                            {
                                "bradley_1",
                                "modular_1",
                                "sedan_1",
                                "truck_1",
                                "truck_1",
                                "sedan_1",
                                "modular_1",
                                "bradley_1"
                            },
                            heliOn = true,
                            heliConfigurationName = "heli_1"
                        },
                    },
                    marker = new MarkerConfig
                    {
                        IsMarker = true,
                        useRingMarker = true,
                        useShopMarker = true,
                        Radius = 0.2f,
                        Alpha = 0.6f,
                        Color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                        Color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                    },
                    eventZone = new DomeConfig
                    {
                        isCreateZonePVP = false,
                        isDome = false,
                        darkening = 5,
                        radius = 70f
                    },
                    GUI = new GUIConfig
                    {
                        IsGUI = true,
                        AnchorMin = "0 0.9",
                        AnchorMax = "1 0.95"
                    },
                    bradleyConfiguration = new List<BradleyConfig>
                    {
                        new BradleyConfig
                        {
                            presetName = "bradley_1",
                            hp = 1000f,
                            viewDistance = 100.0f,
                            searchDistance = 100.0f,
                            coaxAimCone = 1.1f,
                            coaxFireRate = 1.0f,
                            coaxBurstLength = 10,
                            nextFireTime = 10f,
                            topTurretFireRate = 0.25f,
                            countCrates = 3,
                            frontVehicleDistance = 10,
                            npcName = "Tankman",
                            numberOfNpc = 6,
                            offDelay = false,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    sedanConfiguration = new List<SedanConfig>
                    {
                        new SedanConfig
                        {
                            presetName = "sedan_1",
                            hp = 500f,
                            frontVehicleDistance = 10,
                            npcName = "ConvoyNPC",
                            numberOfNpc = 4
                        }
                    },
                    modularConfiguration = new List<ModularConfig>
                    {
                        new ModularConfig
                        {
                            presetName = "truck_1",
                            prefabName = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab",
                            crateLocation = new CoordConfig
                            {
                                position = "(0, 0.65, -2.35)",
                                rotation = "(0, 180, 0)"
                            },
                            damageMultiplier = 0.5f,
                            modules = new List<string> { "vehicle.1mod.engine", "vehicle.1mod.cockpit.armored", "vehicle.1mod.passengers.armored", "vehicle.1mod.flatbed" },
                            frontVehicleDistance = 10,
                            npcName = "ConvoyNPC",
                            numberOfNpc = 4,
                            changeUnlockTime = true,
                            crateUnlockTime = 10,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    supportModularConfiguration = new List<SupportModularConfig>
                    {
                        new SupportModularConfig
                        {
                            presetName = "modular_1",
                            prefabName = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab",
                            damageMultiplier = 1f,
                            modules = new List<string> { "vehicle.1mod.engine", "vehicle.1mod.cockpit.armored", "vehicle.1mod.passengers.armored", "vehicle.1mod.passengers.armored" },
                            frontVehicleDistance = 10,
                            npcName = "ConvoyNPC",
                            numberOfNpc = 4
                        }
                    },
                    heliesConfiguration = new List<HeliConfig>
                    {
                        new HeliConfig
                        {
                            presetName = "heli_1",
                            hp = 10000f,
                            cratesAmount = 3,
                            mainRotorHealth = 750f,
                            rearRotorHealth = 375f,
                            height = 50f,
                            bulletDamage = 20f,
                            bulletSpeed = 250f,
                            distance = 350f,
                            offDelay = false,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                minItemsAmount = 1,
                                maxItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    NPC = new List<NpcConfig>
                    {
                        new NpcConfig
                        {
                            name = "ConvoyNPC",
                            health = 200f,
                            deleteCorpse = true,
                            wearItems = new List<NpcWear>
                            {
                                new NpcWear
                                {
                                    shortName = "metal.plate.torso",
                                    skinID = 1988476232
                                },
                                new NpcWear
                                {
                                    shortName = "riot.helmet",
                                    skinID = 1988478091
                                },
                                new NpcWear
                                {
                                    shortName = "pants",
                                    skinID = 1582399729
                                },
                                new NpcWear
                                {
                                    shortName = "tshirt",
                                    skinID = 1582403431
                                },
                                new NpcWear
                                {
                                    shortName = "shoes.boots",
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
                                    Mods = new List<string>(),
                                    ammo = ""
                                },
                                new NpcBelt
                                {
                                    shortName = "grenade.f1",
                                    amount = 10,
                                    skinID = 0,
                                    Mods = new List<string>(),
                                    ammo = ""
                                }
                            },
                            turretDamageScale = 1f,
                            kit = "",
                            disableRadio = false,
                            roamRange = 5f,
                            chaseRange = 110f,
                            attackRangeMultiplier = 1f,
                            senseRange = 60f,
                            memoryDuration = 60f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = false,
                            visionCone = 135f,
                            speed = 7.5f,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                minItemsAmount = 2,
                                maxItemsAmount = 4,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortName = "rifle.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "shotgun.pump",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "pistol.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "largemedkit",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "smg.2",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "pistol.python",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "smg.thompson",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "shotgun.waterpipe",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "shotgun.double",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.rifle.explosive",
                                        minAmount = 8,
                                        maxAmount = 8,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "pistol.revolver",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.rifle.hv",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.rifle.incendiary",
                                        minAmount = 8,
                                        maxAmount = 8,
                                        chance = 0.5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.pistol.hv",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 0.5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.pistol.fire",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.shotgun.slug",
                                        minAmount = 4,
                                        maxAmount = 8,
                                        chance = 5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.shotgun.fire",
                                        minAmount = 4,
                                        maxAmount = 14,
                                        chance = 5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.shotgun",
                                        minAmount = 6,
                                        maxAmount = 12,
                                        chance = 8f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "bandage",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 17f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "syringe.medical",
                                        minAmount = 1,
                                        maxAmount = 2,
                                        chance = 34f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.rifle",
                                        minAmount = 12,
                                        maxAmount = 36,
                                        chance = 51f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.pistol",
                                        minAmount = 15,
                                        maxAmount = 45,
                                        chance = 52f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            name = "Tankman",
                            health = 500f,
                            deleteCorpse = true,
                            wearItems = new List<NpcWear>
                            {
                                new NpcWear
                                {
                                    shortName = "metal.plate.torso",
                                    skinID = 1988476232
                                },
                                new NpcWear
                                {
                                    shortName = "riot.helmet",
                                    skinID = 1988478091
                                },
                                new NpcWear
                                {
                                    shortName = "pants",
                                    skinID = 1582399729
                                },
                                new NpcWear
                                {
                                    shortName = "tshirt",
                                    skinID = 1582403431
                                },
                                new NpcWear
                                {
                                    shortName = "shoes.boots",
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
                                    Mods = new List<string>(),
                                    ammo = ""
                                },
                                new NpcBelt
                                {
                                    shortName = "rocket.launcher",
                                    amount = 1,
                                    skinID = 0,
                                    Mods = new List<string>(),
                                    ammo = ""
                                }
                            },
                            turretDamageScale = 1f,
                            kit = "",
                            disableRadio = false,
                            roamRange = 5f,
                            chaseRange = 110f,
                            attackRangeMultiplier = 1f,
                            senseRange = 60f,
                            memoryDuration = 60f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = false,
                            visionCone = 135f,
                            speed = 7.5f,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                minItemsAmount = 2,
                                maxItemsAmount = 4,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortName = "rifle.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "shotgun.pump",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "pistol.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "largemedkit",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "smg.2",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "pistol.python",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "smg.thompson",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "shotgun.waterpipe",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "shotgun.double",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.rifle.explosive",
                                        minAmount = 8,
                                        maxAmount = 8,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "pistol.revolver",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.rifle.hv",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.rifle.incendiary",
                                        minAmount = 8,
                                        maxAmount = 8,
                                        chance = 0.5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.pistol.hv",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 0.5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.pistol.fire",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.shotgun.slug",
                                        minAmount = 4,
                                        maxAmount = 8,
                                        chance = 5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.shotgun.fire",
                                        minAmount = 4,
                                        maxAmount = 14,
                                        chance = 5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.shotgun",
                                        minAmount = 6,
                                        maxAmount = 12,
                                        chance = 8f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "bandage",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 17f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "syringe.medical",
                                        minAmount = 1,
                                        maxAmount = 2,
                                        chance = 34f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.rifle",
                                        minAmount = 12,
                                        maxAmount = 36,
                                        chance = 51f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new LootItemConfig
                                    {
                                        shortName = "ammo.pistol",
                                        minAmount = 15,
                                        maxAmount = 45,
                                        chance = 52f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    discord = new DiscordConfig
                    {
                        isDiscord = false,
                        webhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                        embedColor = 13516583,
                        keys = new HashSet<string>
                        {
                            "PreStart",
                            "EventStart",
                            "PreFinish",
                            "Finish",
                            "StartHackCrate"
                        }
                    },
                    Notify = new NotifyConfig
                    {
                        IsNotify = false,
                        Type = "0"
                    },
                    GUIAnnouncements = new GUIAnnouncementsConfig
                    {
                        isGUIAnnouncements = false,
                        bannerColor = "Grey",
                        textColor = "White",
                        apiAdjustVPosition = 0.03f
                    },
                    betterNpcConfig = new BetterNpcConfig
                    {
                        bradleyNpc = false,
                        heliNpc = false
                    },
                    economyConfig = new EconomyConfig
                    {
                        enable = false,
                        plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                        minCommandPoint = 0,
                        minEconomyPiont = 0,
                        npc = 0.3,
                        bradley = 1,
                        heli = 1,
                        sedan = 0.3,
                        modularCar = 0.3,
                        lockedCrate = 0.5,
                        commands = new HashSet<string>()
                    },
                    pveMode = new PveModeConfig
                    {
                        pve = false,
                        showEventOwnerNameOnMap = true,
                        damage = 500f,
                        scaleDamage = new HashSet<ScaleDamageConfig>
                        {
                            new ScaleDamageConfig { Type = "NPC", Scale = 1f },
                            new ScaleDamageConfig { Type = "Bradley", Scale = 1f },
                            new ScaleDamageConfig { Type = "Helicopter",  Scale = 1 }
                        },
                        lootCrate = false,
                        hackCrate = false,
                        lootNpc = false,
                        damageNpc = false,
                        targetNpc = false,
                        damageHeli = false,
                        targetHeli = false,
                        damageTank = false,
                        targetTank = false,
                        canEnter = false,
                        canEnterCooldownPlayer = true,
                        timeExitOwner = 300,
                        alertTime = 60,
                        restoreUponDeath = true,
                        cooldownOwner = 86400,
                        darkening = 5
                    },
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.ConvoyExtensionMethods
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

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
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