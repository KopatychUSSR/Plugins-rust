using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using System.Linq;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("MetalEvent", "Ridamees", "1.7.1")]
    [Description("Spawns an unlimited metal node/ore for a limited time")]

    class MetalEvent : RustPlugin
    {
		private const string vendingPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
		private const string genericPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
		private Dictionary<int, List<BasePlayer>> clusterPlayerLists = new Dictionary<int, List<BasePlayer>>();
		private List<VendingMachineMapMarker> vendingMarkers = new List<VendingMachineMapMarker>();
		private List<MapMarkerGenericRadius> genericMarkers = new List<MapMarkerGenericRadius>();
		private List<List<BaseEntity>> metalNodeClusters = new List<List<BaseEntity>>();
		private const string UI_METAL_EVENT = "MetalEventUI";
		private const string CUSTOM_GAME_TIP = "CustomGameTip";
		private bool destroyMetalClusterAtEnd;
		private bool isEventActive = false;
		private bool resetNodesSpawnPoints;
		private bool onlyCommandStartEvent;
		private int minPlayersToStartEvent;
		private string eventStartMessage; 
		private bool playStartEventSound;
		private bool enableChatMessages;
 		private string eventEndMessage;
		private int minEventInterval;
		private int maxEventInterval;
		private float gameTipDuration;
		private float eventStartTime;
    	private bool enableGameTips;
		private PluginConfig config;
		private SpawnData spawnData;
		private int maxMetalNodes;
		private Timer eventTimer;
		private string spawnMode;

        private class PluginConfig
        {
			[JsonProperty("Clear non-Monument Spawn Locations on map Wipe")] public bool ResetNodesSpawnPoints { get; set; } = true;
			[JsonProperty("Only Command Start Event")] public bool OnlyCommandStartEvent { get; set; } = false;
			[JsonProperty("Minimum Players to Start Event")] public int MinPlayersToStartEvent { get; set; } = 1;
			[JsonProperty("Prevent Overlap with Sulfur/Stone Event")] public bool PreventOverlap { get; set; } = true; 
			[JsonProperty("Prevent Overlap Retry Timer (seconds)")] public float PreventOverlapRetry { get; set; } = 180f; 

			[JsonProperty("Event Minimum Interval (seconds)")] public int MinEventInterval { get; set; } = 3600;
    		[JsonProperty("Event Maximum Interval (seconds)")] public int MaxEventInterval { get; set; } = 7200;
			[JsonProperty("Event Duration Seconds")] public int EventDuration { get; set; } = 1230;
			[JsonProperty("Event Start Notification Sound")] public bool PlayStartEventSound { get; set; } = true;
			[JsonProperty("Event Start Explosion Effects")] public bool SpawnExplosionEffect { get; set; } = true;
			[JsonProperty("Event Chat Messages")] public bool EnableChatMessages { get; set; } = true;
			[JsonProperty("Event GameTip Messages")] public bool EnableGameTips { get; set; } = true;
			[JsonProperty("Event GameTip Duration (seconds)")] public float GameTipDuration { get; set; } = 7f;
			[JsonProperty("Event Start Message")] public string EventStartMessage { get; set; } = "<color=red>Metal Event</color> <color=white>Has Started! Check The Map!</color>";
        	[JsonProperty("Event End Message")] public string EventEndMessage { get; set; } = "<color=red>Metal Event</color> <color=white>Has Ended!</color>";
			
			[JsonProperty("UI Enabled")] public bool ShowMetalEventUI { get; set; } = true;
			[JsonProperty("UI Location X")] public float UILocationX { get; set; } = 0.37f;
    		[JsonProperty("UI Location Y")] public float UILocationY { get; set; } = 0.8f;
			[JsonProperty("UI Handwriting Font")] public bool fontsEnabled { get; set; } = true;
			[JsonProperty("UI Style (1 = With UI Msg, 2 = No UI Msg)")] public int UIStyle { get; set; } = 1;
			[JsonProperty("UI Message")] public string UiText { get; set; } = "Unlimited metal node marked on map!";
			[JsonProperty("Proximity UI Visibility Mode")] public bool ProximityUI { get; set; } = false;
			[JsonProperty("Proximity UI Visibility Range")] public int ProximityRange { get; set; } = 350;
			[JsonProperty("Proximity UI 'Players Here: X'")] public bool ProximityPlayersUI { get; set; } = true;
			[JsonProperty("Proximity UI 'Players Here' Visibility Range")] public int ProximityRangePVP { get; set; } = 70;
			
			[JsonProperty("MapMarker Enabled")] public bool MarkerEnabled { get; set; } = true;
			[JsonProperty("MapMarker Colour")] public string MarkerColour { get; set; } = "#050505";
			[JsonProperty("MapMarker Colour2")] public string MarkerColour2 { get; set; } = "#FF0000";
			[JsonProperty("MapMarker Radius")] public float MapMarkerSize { get; set; } = 0.25f;
			[JsonProperty("MapMarker Alpha")] public float MarkerAlpha { get; set; } = 0.7f;
			[JsonProperty("MapMarker Name/Message")] public string MarkerMessage { get; set; } = "UNLIMITED METAL NODE";

			[JsonProperty("Metal Ore Minimum Gather Amount")] public int MinMetalOreAmount { get; set; } = 1;
     		[JsonProperty("Metal Ore Maximum Gather Amount")] public int MaxMetalOreAmount { get; set; } = 10;
			[JsonProperty("HQM Ore Minimum Gather Amount")] public int MinHQMetalOreAmount { get; set; } = 0;
     		[JsonProperty("HQM Ore Maximum Gather Amount")] public int MaxHQMetalOreAmount { get; set; } = 3;

			[JsonProperty("Delete MetalNode at Event End")] public bool DestroyMetalClusterAtEnd { get; set; } = true;
			[JsonProperty("MetalNode is Gatherable")] public bool GatherYield { get; set; } = true;
			[JsonProperty("MetalNode Size 1-100")] public int MaxMetalNodes { get; set; } = 30;
			[JsonProperty("MetalNode Size Radius")] public float NodeRadius { get; set; } = 0.10f;
			[JsonProperty("Lightning during Event")] public bool EventLightning { get; set; } = true; 
			[JsonProperty("Lightning Min Interval (seconds)")] public float EventLightningMin { get; set; } = 1f; 
			[JsonProperty("Lightning Max Interval (seconds)")] public float EventLightningMax { get; set; } = 60f;

			[JsonProperty("MetalNode Spawnmodes ('All' or 'Random')")] public string SpawnMode { get; set; } = "Random";
			[JsonProperty("MetalNode Spawnmode 'Random' Amount")] public int RandomCount { get; set; } = 2;
			[JsonProperty("Monument Spawn Locations")]
			public Dictionary<string, bool> DefaultSpawnLocations { get; set; } = new Dictionary<string, bool>
			{
				{ "xlarge/launch_site_1.prefab", true },
				{ "medium/nuclear_missile_silo.prefab", true },
				{ "large/military_tunnel_1.prefab", true },
				{ "large/airfield_1.prefab", true },
				{ "small/sphere_tank.prefab", true },
				{ "large/water_treatment_plant_1.prefab", true },
				{ "railside/trainyard_1.prefab", true },
				{ "medium/radtown_small_3.prefab", true },
				{ "roadside/gas_station_1.prefab", true },
				{ "roadside/supermarket_1.prefab", true },
				{ "large/powerplant_1.prefab", true },
				{ "lighthouse/lighthouse.prefab", true },
				{ "roadside/warehouse.prefab", true },
				{ "medium/junkyard_1.prefab", true },
				{ "small/satellite_dish.prefab", true },
				{ "harbor/harbor_1.prefab", true },
				{ "harbor/harbor_2.prefab", true },
				{ "harbor/ferry_terminal_1.prefab", true },
				{ "arctic_bases/arctic_research_base_a.prefab", true },
				{ "military_bases/desert_military_base_a.prefab", false },
				{ "military_bases/desert_military_base_b.prefab", false },
				{ "military_bases/desert_military_base_c.prefab", false },
				{ "military_bases/desert_military_base_d.prefab", false },
				{ "OilrigAI", false },
				{ "OilrigAI2", false }
			};
			[JsonProperty("Monument Spawn Limits")]
			public Dictionary<string, int> MonumentLimits { get; set; } = new Dictionary<string, int>
			{
				{ "xlarge/launch_site_1.prefab", 1 },
				{ "medium/nuclear_missile_silo.prefab", 1 },
				{ "large/military_tunnel_1.prefab", 1 },
				{ "large/airfield_1.prefab", 1 },
				{ "small/sphere_tank.prefab", 1 },
				{ "large/water_treatment_plant_1.prefab", 1 },
				{ "railside/trainyard_1.prefab", 1 },	
				{ "medium/radtown_small_3.prefab", 1 },
				{ "roadside/gas_station_1.prefab", 3 },
				{ "roadside/supermarket_1.prefab", 3 },
				{ "large/powerplant_1.prefab", 1 },
				{ "lighthouse/lighthouse.prefab", 2 },
				{ "roadside/warehouse.prefab", 3 },
				{ "medium/junkyard_1.prefab", 1 },
				{ "small/satellite_dish.prefab", 1 },
				{ "harbor/harbor_1.prefab", 1 },
				{ "harbor/harbor_2.prefab", 1 },
				{ "harbor/ferry_terminal_1.prefab", 1 },
				{ "arctic_bases/arctic_research_base_a.prefab", 1 },
				{ "military_bases/desert_military_base_a.prefab", 1 },
				{ "military_bases/desert_military_base_b.prefab", 1 },
				{ "military_bases/desert_military_base_c.prefab", 1 },
				{ "military_bases/desert_military_base_d.prefab", 1 },
				{ "OilrigAI", 1 },
				{ "OilrigAI2", 1 }
			};
			[JsonProperty("Monument Spawn Location Offsets")]
			public Dictionary<string, Vector3> DefaultSpawnLocationOffsets { get; set; } = new Dictionary<string, Vector3>
			{
				{ "xlarge/launch_site_1.prefab", new Vector3(150f, 3.5f, -7.5f) },
				{ "medium/nuclear_missile_silo.prefab", new Vector3(53.3f, -13.2f, 1.1f) },
				{ "large/military_tunnel_1.prefab", new Vector3(-0.5f, 18.35f, 25f) },
				{ "large/airfield_1.prefab", new Vector3(20f, 0.5f, -27.5f) },
				{ "small/sphere_tank.prefab", new Vector3(0f, 72f, 0f) },
				{ "large/water_treatment_plant_1.prefab", new Vector3(-51.9f, 1.5f, -98.8f) },
				{ "railside/trainyard_1.prefab", new Vector3(-36.3f, 9.1f, -33.0f) },
				{ "medium/radtown_small_3.prefab", new Vector3(-21.5f, 1.1f, -4.5f) },
				{ "roadside/gas_station_1.prefab", new Vector3(8.4f, 9.5f, 5.0f) },
				{ "roadside/supermarket_1.prefab", new Vector3(10.0f, 6f, -3.5f) },
				{ "large/powerplant_1.prefab", new Vector3(-8.0f, 0.5f, 38.0f) },
				{ "lighthouse/lighthouse.prefab", new Vector3(0f, 57.7f, 0.36f) },
				{ "roadside/warehouse.prefab", new Vector3(20f, 0.3f, -7.7f) },
				{ "medium/junkyard_1.prefab", new Vector3(24.0f, 0.3f, 0.0f) },
				{ "small/satellite_dish.prefab", new Vector3(8.0f, 6.5f, -14.7f) },
				{ "harbor/harbor_1.prefab", new Vector3(8.1f, 8.6f, 6.3f) },	
				{ "harbor/harbor_2.prefab", new Vector3(41.5f, 5.3f, -20.0f) },
				{ "harbor/ferry_terminal_1.prefab", new Vector3(-38.1f, 5.2f, 18.6f) },
				{ "arctic_bases/arctic_research_base_a.prefab", new Vector3(-32.8f, 1.85f, 5.5f) },
				{ "military_bases/desert_military_base_a.prefab", new Vector3(0f, 2f, 0f) },
				{ "military_bases/desert_military_base_b.prefab", new Vector3(0f, 2f, 0f) },
				{ "military_bases/desert_military_base_c.prefab", new Vector3(0f, 2f, 0f) },
				{ "military_bases/desert_military_base_d.prefab", new Vector3(0f, 2f, 0f) },
				{ "OilrigAI", new Vector3(11.0f, 30.3f, -25.0f) },
				{ "OilrigAI2", new Vector3(-1.3f, 39.15f, -14.95f) }
			};
		}
		private class SpawnData
		{
   			public Dictionary<string, Vector3> SpawnPositions { get; set; } = new Dictionary<string, Vector3>();
		}
		private bool IsPlayerInProximity(BasePlayer player)
		{
			foreach (var cluster in metalNodeClusters)
			{
				foreach (var node in cluster)
				{
					if (node != null && node.IsDestroyed == false)
					{
						if (Vector3.Distance(node.transform.position, player.transform.position) <= config.ProximityRange)
						{
							return true;
						}
					}
				}
			}
			return false;
		}
		private void UpdatePlayerClusters()
		{
    		foreach (var cluster in metalNodeClusters)
    		{
        		int clusterIndex = metalNodeClusters.IndexOf(cluster);
        		List<BasePlayer> playersInProximity = new List<BasePlayer>();
        		foreach (var node in cluster)
        		{
            		foreach (var player in BasePlayer.activePlayerList)
            		{
						if (node != null && player != null)
        				{
							if (Vector3.Distance(node.transform.position, player.transform.position) <= config.ProximityRangePVP)
							{
								if (!playersInProximity.Contains(player))
								{
									playersInProximity.Add(player);
								}
							}
							else if (playersInProximity.Contains(player))
							{
								playersInProximity.Remove(player);
							}
						}
            		}
        		}
        		clusterPlayerLists[clusterIndex] = playersInProximity;
    		}
		}
		private bool IsPlayerInProximityPVP(BasePlayer player)
		{
			foreach (var cluster in metalNodeClusters)
			{
				foreach (var node in cluster)
				{
					if (node != null && node.IsDestroyed == false)
					{
						if (Vector3.Distance(node.transform.position, player.transform.position) <= config.ProximityRangePVP)
						{
							return true;
						}
					}
				}
			}
			return false;
		}
        protected override void LoadDefaultConfig()
        {
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config, true);
        }
		private void LoadConfigVariables()
    	{
			LoadDefaultConfig();
			minEventInterval = config.MinEventInterval;
    		maxEventInterval = config.MaxEventInterval;
        	eventStartMessage = config.EventStartMessage;
        	eventEndMessage = config.EventEndMessage;
        	enableGameTips = config.EnableGameTips;
        	enableChatMessages = config.EnableChatMessages;
			gameTipDuration = config.GameTipDuration;
			destroyMetalClusterAtEnd = config.DestroyMetalClusterAtEnd;
			playStartEventSound = config.PlayStartEventSound;
			spawnMode = config.SpawnMode;
			onlyCommandStartEvent = config.OnlyCommandStartEvent;
			minPlayersToStartEvent = config.MinPlayersToStartEvent;
			resetNodesSpawnPoints = config.ResetNodesSpawnPoints;
    	}
		private void Init()
				{
			LoadConfigVariables();
			spawnData = Interface.Oxide.DataFileSystem.ReadObject<SpawnData>("MetalEventSpawnData");
			if (spawnData == null)
			{
				spawnData = new SpawnData();
				Interface.Oxide.DataFileSystem.WriteObject("MetalEventSpawnData", spawnData);
			}
			if (!onlyCommandStartEvent)
			{
				SetRandomEventTimer();
			}
			permission.RegisterPermission("metalevent.admin", this);
		}

		private void OnServerInitialized()
		{
			PopulateDefaultSpawnPositions();
			Interface.Oxide.DataFileSystem.WriteObject("MetalEventSpawnData", spawnData);
		}

		private void PopulateDefaultSpawnPositions()
		{
			Dictionary<string, int> prefabCount = new Dictionary<string, int>();

			foreach (var kvp in config.DefaultSpawnLocations)
			{
				string monumentPrefabPath = kvp.Key;
				bool isDefaultSpawn = kvp.Value;

				if (isDefaultSpawn)
				{
					if (!config.MonumentLimits.ContainsKey(monumentPrefabPath))
						continue;

					int limit = config.MonumentLimits[monumentPrefabPath];
					if (limit == 0)
						continue;

					MonumentInfo[] monuments = TerrainMeta.Path.Monuments.Where(info => info.gameObject.name.EndsWith(monumentPrefabPath)).ToArray();
					int count = monuments.Length;

					if (count > 0)
					{
						if (!prefabCount.ContainsKey(monumentPrefabPath))
						{
							prefabCount[monumentPrefabPath] = 0;
						}

						for (int i = 0; i < Math.Min(count, limit); i++)
						{
							MonumentInfo monument = monuments[i];
							Vector3 spawnPosition = monument.transform.TransformPoint(config.DefaultSpawnLocationOffsets.GetValueOrDefault(monumentPrefabPath, Vector3.zero));
							string spawnLocation = $"{monumentPrefabPath}_{prefabCount[monumentPrefabPath]}";
							spawnData.SpawnPositions[spawnLocation] = spawnPosition;
							prefabCount[monumentPrefabPath]++;
						}
					}
				}
			}

			// Remove spawn positions that are set to false in DefaultSpawnLocations
			RemoveInactiveSpawns();
		}

		private void RemoveInactiveSpawns()
		{
			// Collect keys to remove to avoid modifying the collection while iterating
			List<string> keysToRemove = new List<string>();

			foreach (var kvp in config.DefaultSpawnLocations)
			{
				string monumentPrefabPath = kvp.Key;
				bool isDefaultSpawn = kvp.Value;

				if (!isDefaultSpawn)
				{
					foreach (var spawnLocation in spawnData.SpawnPositions.Keys)
					{
						// Check if the spawnLocation starts with the monumentPrefabPath
						if (spawnLocation.StartsWith(monumentPrefabPath))
						{
							keysToRemove.Add(spawnLocation);
						}
					}
				}
			}

			// Remove the collected keys
			foreach (var key in keysToRemove)
			{
				spawnData.SpawnPositions.Remove(key);
				Puts($"Removed inactive spawn location: {key}");
			}
		}
		private bool IsDefaultSpawn(string monumentName)
		{
    		if (config.DefaultSpawnLocations.ContainsKey(monumentName))
			{
				return config.DefaultSpawnLocations[monumentName];
			}
			return false;
		}
		private void SetRandomEventTimer()
		{
			if (!onlyCommandStartEvent)
			{
				int eventInterval = UnityEngine.Random.Range(minEventInterval, maxEventInterval + 1);
		    	eventTimer = timer.Once(eventInterval, StartEvent);
			}
		}
		Vector3 FindGround(Vector3 position)
		{
    		return position;
		}
		private bool IsEventActive()
		{
			return isEventActive;
		}
		bool IsSulfurEventActive()
		{
			var sulfurEventPlugin = plugins.Find("SulfurEvent");
			if (sulfurEventPlugin != null && sulfurEventPlugin.IsLoaded)
			{
				var sulfurEvent = sulfurEventPlugin?.Call<bool>("IsEventActive");
				return sulfurEvent ?? false;
			}
			return false;
		}
		bool IsStoneEventActive()
		{
			var stoneEventPlugin = plugins.Find("StoneEvent");
			if (stoneEventPlugin != null && stoneEventPlugin.IsLoaded)
			{
				var stoneEvent = stoneEventPlugin?.Call<bool>("IsEventActive");
				return stoneEvent ?? false;
			}
			return false;
		}
		private List<Vector3> positions;
        void StartEvent()
		{
			if (config.PreventOverlap && (IsSulfurEventActive() || IsStoneEventActive()))
			{
				Puts("Skipping metal event start because sulfur or stone event is already active. Trying again soon.");
				timer.Once(config.PreventOverlapRetry, () =>
				{
					StartEvent();
				});
				return;
			}
			if (BasePlayer.activePlayerList.Count < minPlayersToStartEvent)
			{
				Puts("Not enough players to start MetalEvent.");
				return;
			}
			if (isEventActive)
			{
				Puts("The event is already active.");
				return;
			}
			positions = GetSpawnPositions();
			if (positions.Count == 0)
			{
				Puts("Unable to start MetalEvent event. No spawn position set.");
				return;
			}
			PopulateDefaultSpawnPositions();
			Subscribe(nameof(OnMeleeAttack));
			Subscribe(nameof(OnPlayerConnected));
			isEventActive = true;
			eventStartTime = Time.realtimeSinceStartup;
			Puts("MetalEvent Event Started");
			SendGameTipToAllPlayers(eventStartMessage);
			PlayStartEventSound();
			metalNodeClusters.Clear();
			genericMarkers.Clear();
			vendingMarkers.Clear();
			foreach (var position in positions)
			{
				Effect.server.Run("assets/bundled/prefabs/fx/ore_break.prefab", position);
				Effect.server.Run("assets/prefabs/misc/orebonus/effects/bonus_finish.prefab", position);
				Effect.server.Run("assets/bundled/prefabs/fx/ore_break.prefab", position);
				Effect.server.Run("assets/prefabs/misc/orebonus/effects/hotspot_death.prefab", position);
				Effect.server.Run("assets/bundled/prefabs/fx/ore_break.prefab", position);
				Effect.server.Run("assets/prefabs/misc/junkpile/effects/despawn.prefab", position);
				Effect.server.Run("assets/bundled/prefabs/fx/ore_break.prefab", position);
				CreateMarker(position, config.MarkerMessage);
				CreateVendingMarker(position, config.MarkerMessage);
				StartLightningEffect(position);
			}
			timer.Once(2f, () =>
			{
				foreach (var position in positions)
				{
					SpawnLargeMetalNodePyramid(position, 9);
					UpdateEventUI();
					Puts($"A large metal node has spawned at position {position}.");
					float explosionRadius = 3.5f; // adjust as needed
					float nodeClusterHeight = 2.5f;
					Vector3 explosionPosition = new Vector3(position.x, position.y + nodeClusterHeight / 2, position.z);
					foreach (var basePlayer in BasePlayer.activePlayerList)
					{
						float distanceToPlayer = Vector3.Distance(explosionPosition, basePlayer.transform.position);
						if (distanceToPlayer <= explosionRadius)
						{
							float damage = (5 - (distanceToPlayer / explosionRadius)) * 100;
							basePlayer.Hurt(damage);
						}
					}
					if (config.SpawnExplosionEffect)
					{
						Effect.server.Run("assets/content/vehicles/mlrs/effects/pfx_mlrs_rocket_explosion_ground.prefab", explosionPosition);
						Effect.server.Run("assets/content/vehicles/mlrs/effects/pfx_mlrs_rocket_explosion_air.prefab", explosionPosition);
						Effect.server.Run("assets/content/vehicles/mlrs/effects/pfx_mlrs_rocket_explosion_air.prefab", explosionPosition);
						Effect.server.Run("assets/prefabs/npc/m2bradley/effects/bradley_explosion.prefab", explosionPosition);
						Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_airburst_explosion.prefab", explosionPosition);
						Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_explosion.prefab", explosionPosition);
						Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", explosionPosition);
						Effect.server.Run("assets/prefabs/npc/sam_site_turret/effects/rocket_sam_explosion.prefab", explosionPosition);
						Effect.server.Run("assets/content/effects/weather/pfx_lightning_strong.prefab", explosionPosition);
						Effect.server.Run("assets/content/effects/weather/pfx_lightning_mild.prefab", explosionPosition);
						Effect.server.Run("assets/content/effects/weather/pfx_lightning_medium.prefab", explosionPosition);
						Effect.server.Run("assets/prefabs/npc/m2bradley/sound/bradley-explosion-debris.asset", explosionPosition);
					}
				}
			});
			timer.Once(config.EventDuration, () =>
			{
				StopEvent();
				if (!onlyCommandStartEvent)
				{
					SetRandomEventTimer();
				}
			});
		}
		List<string> lightningPrefabs = new List<string>()
		{
			"assets/content/effects/weather/pfx_lightning_medium.prefab",
			"assets/content/effects/weather/pfx_lightning_mild.prefab",
			"assets/content/effects/weather/pfx_lightning_strong.prefab"
		};
		List<Timer> lightningTimers = new List<Timer>();
		void StartLightningEffect(Vector3 position)
		{
			if (!config.EventLightning) return;

			float minDelay = config.EventLightningMin;
			float maxDelay = config.EventLightningMax;

			Action lightningAction = null;
			lightningAction = () =>
			{
				float delay = UnityEngine.Random.Range(minDelay, maxDelay);
				Timer lightningTimer = timer.Once(delay, () =>
				{
					string randomPrefab = lightningPrefabs[UnityEngine.Random.Range(0, lightningPrefabs.Count)];
					Effect.server.Run(randomPrefab, position);
					lightningAction();
				});
				lightningTimers.Add(lightningTimer);
			};

			lightningAction();
		}
		void UpdateEventUI()
		{
			if (!isEventActive || !config.ShowMetalEventUI) return;
			foreach (var basePlayer in BasePlayer.activePlayerList)
			{
				ShowMetalEventUI(basePlayer);
			}
			timer.Once(2.5f, () =>
			{
				UpdateEventUI();
			});
		}
		List<Vector3> GetAllSpawnPositions()
		{
    		List<Vector3> positions = new List<Vector3>();
    		foreach (var spawnPointPosition in spawnData.SpawnPositions.Values)
    		{
        		positions.Add(FindGround(spawnPointPosition));
    		}
    		return positions;
		}
		Vector3 GetRandomSpawnPosition()
		{
    		if (spawnData.SpawnPositions.Count == 0)
    		{
        		Puts("Warning: No spawn positions have been set.");
        		return Vector3.zero;
    		}
    		string spawnPointName = spawnData.SpawnPositions.Keys.ToList()[UnityEngine.Random.Range(0, spawnData.SpawnPositions.Count)];
    		return FindGround(spawnData.SpawnPositions[spawnPointName]);
		}
		private void PlayStartEventSound()
		{
			if (!playStartEventSound) return;
			foreach (var basePlayer in BasePlayer.activePlayerList)
			{
				Effect.server.Run("assets/bundled/prefabs/fx/invite_notice.prefab", basePlayer, 0, Vector3.zero, Vector3.forward);
				Effect.server.Run("assets/bundled/prefabs/fx/item_unlock.prefab", basePlayer, 0, Vector3.zero, Vector3.forward);
			}
		}
		void OnPlayerConnected(BasePlayer player)
		{
   	 		if (isEventActive && config.ShowMetalEventUI)
    		{
       			ShowMetalEventUI(player);
    		}
		}
		void StopEvent()
		{
			if (!isEventActive) return;
			isEventActive = false;
			Puts($"MetalEvent Event Ended");
			SendGameTipToAllPlayers(eventEndMessage);
			if (destroyMetalClusterAtEnd)
			{
				foreach (var metalNodeCluster in metalNodeClusters)
				{
					foreach (var node in metalNodeCluster)
					{
						node.Kill();
					}
				}
			}
			foreach (var mapmarker in genericMarkers)
            mapmarker.Kill();
        	foreach (var vending in vendingMarkers)
            vending.Kill();
			foreach (var basePlayer in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(basePlayer, UI_METAL_EVENT);
			}
			foreach (var timer in lightningTimers)
			{
				timer.Destroy();
			}
			lightningTimers.Clear();
			clusterPlayerLists.Clear();
        	genericMarkers.Clear();
        	vendingMarkers.Clear();
			metalNodeClusters.Clear();
			Unsubscribe(nameof(OnMeleeAttack));
    		Unsubscribe(nameof(OnPlayerConnected));
		}
		private void SendGameTipToAllPlayers(string message)
    	{
        	if (enableGameTips)
        	{
            	ShowGameTipToAllPlayers(message, gameTipDuration);
        	}
        	if (enableChatMessages)
        	{
           	 	PrintToChat(message);
        	}
    	}
		private void ShowGameTipToAllPlayers(string message, float displayDuration)
		{
    		foreach (var basePlayer in BasePlayer.activePlayerList)
    		{
        		basePlayer.SendConsoleCommand("gametip.hidegametip");
        		basePlayer.SendConsoleCommand("gametip.showgametip", message);
        		timer.Once(gameTipDuration, () => basePlayer.SendConsoleCommand("gametip.hidegametip"));
    		}
		}
		private int GetPlayerClusterIndex(BasePlayer player)
		{
			if (player == null)
				return -1;
			foreach (var cluster in metalNodeClusters)
			{
				if (cluster == null)
					continue;
				int clusterIndex = metalNodeClusters.IndexOf(cluster);
				foreach (var node in cluster)
				{
					if (node != null && !node.IsDestroyed && node.transform != null && player.transform != null)
					{
						if (Vector3.Distance(node.transform.position, player.transform.position) <= config.ProximityRangePVP)
						{
							return clusterIndex;
						}
					}
				}
			}
			return -1;
		}
		private void ShowMetalEventUI(BasePlayer player)
		{
    		if (!config.ShowMetalEventUI) return;
			if (config.ProximityUI)
        	{
            if (!IsPlayerInProximity(player))
            	{
                	CuiHelper.DestroyUi(player, UI_METAL_EVENT);
                	return;
            	}
        	}
			foreach (var genericMarker in genericMarkers)
    			{
        			genericMarker.SendUpdate(player);
    			}	
    		float timeRemaining = config.EventDuration - (Time.realtimeSinceStartup - eventStartTime);
    		string timerText = $"{Mathf.FloorToInt(timeRemaining / 60)}m {Mathf.FloorToInt(timeRemaining % 60)}s";
			if (timeRemaining >= 3600f) // 3600 seconds = 1 hour
			{
    			int hours = Mathf.FloorToInt(timeRemaining / 3600);
    			int minutes = Mathf.FloorToInt((timeRemaining % 3600) / 60);
    			int seconds = Mathf.FloorToInt(timeRemaining % 60);
    			timerText = $"{hours}h {minutes}m {seconds}s";
			}
			else if (timeRemaining >= 60f)
			{
    			int minutes = Mathf.FloorToInt(timeRemaining / 60);
    			int seconds = Mathf.FloorToInt(timeRemaining % 60);
    			timerText = $"{minutes}m {seconds}s";
			}
			else
			{
    			int seconds = Mathf.FloorToInt(timeRemaining);
    			timerText = $"{seconds}s";
			}
    		var elements = new CuiElementContainer();
			float uiWidth = 0.26f;
    		float uiHeight = 0.14f;
    		string anchorMin = $"{config.UILocationX} {config.UILocationY}";
    		string anchorMax = $"{config.UILocationX + uiWidth} {config.UILocationY + uiHeight}";
			string distance = "0.55 -0.55";
			string OutlineColor = "0 0 0 1";
    		var mainPanel = elements.Add(new CuiPanel
			{
    			Image = { Color = "35 35 35 0" },
    			RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
    			CursorEnabled = false,
			}, "Under", UI_METAL_EVENT);
			if (config.ProximityPlayersUI && IsPlayerInProximityPVP(player))
			{
				int clusterIndex = GetPlayerClusterIndex(player);
				int playersNear = clusterPlayerLists.ContainsKey(clusterIndex) ? clusterPlayerLists[clusterIndex].Count : 0;
				elements.Add(new CuiElement
				{
					Parent = mainPanel,
					Components =
					{
						new CuiTextComponent
						{
							Text = $"Players Here: {playersNear}",
							FontSize = 17,
							Align = TextAnchor.MiddleCenter,
							Font = config.fontsEnabled ? "permanentmarker.ttf" : "robotocondensed-bold.ttf"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.1 0.10",
							AnchorMax = "0.9 0.4"
						},
						new CuiOutlineComponent
						{
							Color = OutlineColor,
							Distance = distance,
							UseGraphicAlpha = true
						}
					}
				});
			}
    		switch(config.UIStyle)
    		{
        		case 1:
				elements.Add(new CuiElement
				{
					Parent = mainPanel,
					Components =
					{
						new CuiTextComponent
						{
							Text = "<color=red>Metal </color><color=red>Event</color>",
							FontSize = 19,
							Align = TextAnchor.MiddleCenter,
							Font = config.fontsEnabled ? "permanentmarker.ttf" : "robotocondensed-bold.ttf"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.1 0.70",
							AnchorMax = "0.9 0.99"
						},
						new CuiOutlineComponent
						{
							Color = OutlineColor,
							Distance = distance,
							UseGraphicAlpha = true
						}
					}
				});
				elements.Add(new CuiElement
				{
					Parent = mainPanel,
					Components =
					{
						new CuiTextComponent
						{
							Text = $"{config.UiText}",
							FontSize = 14,
							Align = TextAnchor.MiddleCenter,
							Font = config.fontsEnabled ? "permanentmarker.ttf" : "robotocondensed-bold.ttf"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.1 0.45",
							AnchorMax = "0.9 0.85"
						},
						new CuiOutlineComponent
						{
							Color = OutlineColor,
							Distance = distance,
							UseGraphicAlpha = true
						}
					}
				});
				elements.Add(new CuiElement
				{
					Parent = mainPanel,
					Components =
					{
						new CuiTextComponent
						{
							Text = timerText,
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Font = config.fontsEnabled ? "permanentmarker.ttf" : "robotocondensed-bold.ttf"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.1 0.2",
							AnchorMax = "0.9 0.7"
						},
						new CuiOutlineComponent
						{
							Color = OutlineColor,
							Distance = distance,
							UseGraphicAlpha = true
						}
					}
				});
					break;
        		case 2:
				elements.Add(new CuiElement
				{
					Parent = mainPanel,
					Components =
					{
						new CuiTextComponent
						{
							Text = "<color=red>Metal </color><color=red>Event</color>",
							FontSize = 19,
							Align = TextAnchor.MiddleCenter,
							Font = config.fontsEnabled ? "permanentmarker.ttf" : "robotocondensed-bold.ttf"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.1 0.50",
							AnchorMax = "0.9 0.79"
						},
						new CuiOutlineComponent
						{
							Color = OutlineColor,
							Distance = distance,
							UseGraphicAlpha = true
						}
					}
				});
				elements.Add(new CuiElement
				{
					Parent = mainPanel,
					Components =
					{
						new CuiTextComponent
						{
							Text = timerText,
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Font = config.fontsEnabled ? "permanentmarker.ttf" : "robotocondensed-bold.ttf"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.1 0.25",
							AnchorMax = "0.9 0.6"
						},
						new CuiOutlineComponent
						{
							Color = OutlineColor,
							Distance = distance,
							UseGraphicAlpha = true
						}
					}
				});
            		break;
        		default:
            		elements.Add(new CuiElement
				{
					Parent = mainPanel,
					Components =
					{
						new CuiTextComponent
						{
							Text = "<color=red>Metal </color><color=red>Event</color>",
							FontSize = 19,
							Align = TextAnchor.MiddleCenter,
							Font = config.fontsEnabled ? "permanentmarker.ttf" : "robotocondensed-bold.ttf"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.1 0.50",
							AnchorMax = "0.9 0.79"
						},
						new CuiOutlineComponent
						{
							Color = OutlineColor,
							Distance = distance,
							UseGraphicAlpha = true
						}
					}
				});
				elements.Add(new CuiElement
				{
					Parent = mainPanel,
					Components =
					{
						new CuiTextComponent
						{
							Text = timerText,
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Font = config.fontsEnabled ? "permanentmarker.ttf" : "robotocondensed-bold.ttf"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.1 0.25",
							AnchorMax = "0.9 0.6"
						},
						new CuiOutlineComponent
						{
							Color = OutlineColor,
							Distance = distance,
							UseGraphicAlpha = true
						}
					}
				});
            		break;
    		}
			UpdatePlayerClusters();
			CuiHelper.DestroyUi(player, UI_METAL_EVENT);
    		CuiHelper.AddUi(player, elements);
		}
		private List<Vector3> GetSpawnPositions()
		{
			List<Vector3> positions = new List<Vector3>();
			Puts($"Spawn mode: {config.SpawnMode}");
			Puts($"Number of spawn positions: {spawnData.SpawnPositions.Count}");
			if (config.SpawnMode.ToLower() == "all" && spawnData.SpawnPositions.Count > 0)
			{
				foreach (Vector3 spawnPointPosition in spawnData.SpawnPositions.Values)
				{
					positions.Add(FindGround(spawnPointPosition));
				}
			}
			else if (config.SpawnMode.ToLower() == "random")
			{
				int spawnCount = Mathf.Min(config.RandomCount, spawnData.SpawnPositions.Count);
				var spawnPointNames = spawnData.SpawnPositions.Keys.ToList();
				for (int i = 0; i < spawnCount; i++)
				{
					string spawnPointName = spawnPointNames[UnityEngine.Random.Range(0, spawnPointNames.Count)];
					positions.Add(FindGround(spawnData.SpawnPositions[spawnPointName]));
					spawnPointNames.Remove(spawnPointName);
				}
			}
			Puts($"Number of positions selected: {positions.Count}");
			return positions;
		}
        void SpawnLargeMetalNodePyramid(Vector3 center, int baseLayerNodeCount)
		{
    		List<BaseEntity> metalNodeCluster = new List<BaseEntity>();
    		float nodeRadius = config.NodeRadius;
    		for (int layer = 0; layer < baseLayerNodeCount; layer++)
    		{
        		int layerNodeCount = baseLayerNodeCount - layer;
        		float layerRadius = layerNodeCount * nodeRadius;
        		for (int i = 0; i < layerNodeCount; i++)
        		{
            		if (metalNodeCluster.Count >= config.MaxMetalNodes)
            		{
                		break;
            		}
            		float angle = (Mathf.PI * 2 * i) / layerNodeCount;
            		float x = center.x + layerRadius * Mathf.Cos(angle);
            		float z = center.z + layerRadius * Mathf.Sin(angle);
            		float y = center.y + layer * nodeRadius * 2;
            		Vector3 metalNodePosition = new Vector3(x, y, z);
            		BaseEntity metalNodeEntity = GameManager.server.CreateEntity("assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab", metalNodePosition);
            		if (metalNodeEntity == null) continue;
                	metalNodeEntity.Spawn();
                	metalNodeCluster.Add(metalNodeEntity);
        		}
    		}
			metalNodeClusters.Add(metalNodeCluster);
		}
		void OnMeleeAttack(BasePlayer attacker, HitInfo info)
		{
			if (info.WeaponPrefab is BaseMelee)
			{
				if (EntityInAnyCluster(info.HitEntity) && isEventActive)
				{
					if (config.GatherYield && attacker != null)
					{
						ItemDefinition hqMetalOreDef = ItemManager.FindItemDefinition("hq.metal.ore");
						if (hqMetalOreDef != null)
						{
							int bonusAmount = UnityEngine.Random.Range(config.MinHQMetalOreAmount, config.MaxHQMetalOreAmount + 1);
							if (bonusAmount > 0)
							{
								Item bonusItem = ItemManager.Create(hqMetalOreDef, bonusAmount);
								if (bonusItem != null)
								{
									attacker.GiveItem(bonusItem);
								}
							}
						}
						ItemDefinition metalOreDef = ItemManager.FindItemDefinition("metal.ore");
						if (metalOreDef != null)
						{
							int metalOreAmount = UnityEngine.Random.Range(config.MinMetalOreAmount, config.MaxMetalOreAmount + 1);
							if (metalOreAmount > 0)
							{
								Item metalOreItem = ItemManager.Create(metalOreDef, metalOreAmount);
								if (metalOreItem != null)
								{
									Effect.server.Run("assets/prefabs/misc/orebonus/effects/bonus_finish.prefab", info.HitEntity.transform.position);
									attacker.GiveItem(metalOreItem);
								}
							}
						}
					}
					info.damageTypes = new Rust.DamageTypeList();
					info.HitMaterial = 0;
					info.PointStart = info.PointEnd = Vector3.zero;
				}
			}
		}
		bool EntityInAnyCluster(BaseEntity entity)
		{
    		foreach (var cluster in metalNodeClusters)
    		{
        		if (cluster.Contains(entity)) 
            		return true;
    		}
    		return false;
		}
		void CreateMarker(Vector3 position, string name)
		{
			if (!config.MarkerEnabled) return;
			var mainMarker = GameManager.server.CreateEntity(genericPrefab, position) as MapMarkerGenericRadius;
			if (mainMarker == null) return;
			mainMarker.radius = config.MapMarkerSize;
			mainMarker.alpha = config.MarkerAlpha;
			Color markerColor;
			if (ColorUtility.TryParseHtmlString(config.MarkerColour, out markerColor))
			{
				mainMarker.color1 = markerColor;
			}
			else
			{
				Puts($"Invalid MarkerColour value: {config.MarkerColour}. Using default color.");
				mainMarker.color1 = Color.yellow;
			}
			Color markerColor2;
			if (ColorUtility.TryParseHtmlString(config.MarkerColour2, out markerColor2))
			{
				mainMarker.color2 = markerColor2;
			}
			else
			{
				Puts($"Invalid MarkerColour2 value: {config.MarkerColour2}. Using default color.");
				mainMarker.color2 = Color.red;
			}
			mainMarker.Spawn();
			genericMarkers.Add(mainMarker);
			mainMarker.SendUpdate();
		}
		void CreateVendingMarker(Vector3 position, string displayName)
		{
			if (!config.MarkerEnabled) return;
			var vending = GameManager.server.CreateEntity(vendingPrefab, position) as VendingMachineMapMarker;
			if (vending == null) return;
			vending.enabled = true;
			vending.markerShopName = displayName;
			vending.Spawn();
			vendingMarkers.Add(vending);
		}
		[ChatCommand("metalevent")]
		private void HandleMetalEventCommand(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "metalevent.admin"))
			{
				SendReply(player, "You do not have permission to use this command.");
				return;
			}
			if (args.Length == 0)
			{
				ShowAvailableCommands(player);
				return;
			}
			string subCommand = args[0].ToLower();
			switch (subCommand)
			{
				case "start":
					CreateMetalEventChat(player);
					break;
				case "stop":
					StopMetalEventChat(player);
					break;
				case "add":
					if (args.Length > 1)
						SetMetalEventSpawn(player, args[1]);
					else
						SendReply(player, "Please specify a name for the spawn point.");
					break;
				case "list":
					ListMetalEventSpawn(player);
					break;
				case "clear":
					ClearMetalEventSpawnChat(player);
					break;
				case "delete":
					if (args.Length > 1)
						DeleteMetalEventSpawn(player, args[1]);
					else
						SendReply(player, "Please specify the name of the spawn point to delete.");
					break;
				default:
					SendReply(player, "Invalid command. Use '/metalevent' to view available commands.");
					break;
			}
		}
		private void ShowAvailableCommands(BasePlayer player)
		{
			string availableCommands = "Available commands: \n" +
									"/metalevent start - Start MetalEvent \n" +
									"/metalevent stop - Stop MetalEvent \n" +
									"/metalevent add <name> - Set new MetalEvent spawn position \n" +
									"/metalevent list - List all spawn positions \n" +
									"/metalevent clear - Clear all spawn positions \n" +
									"/metalevent delete <name> - Delete a spawn position \n" +
									"/metalevent - View all commands \n" +
									"Console Commands - MetalStart, MetalStop, MetalList, MetalListClear";
			SendReply(player, availableCommands);
		}
		private void SetMetalEventSpawn(BasePlayer player, string spawnPointName)
		{
			if (permission.UserHasPermission(player.UserIDString, "metalevent.admin"))
			{
				if (string.IsNullOrEmpty(spawnPointName))
				{
					SendReply(player, "Please specify a name for the spawn point.");
					return;
				}
				spawnData.SpawnPositions[spawnPointName] = player.transform.position;
				Interface.Oxide.DataFileSystem.WriteObject("MetalEventSpawnData", spawnData);
				Puts($"Set spawn position '{spawnPointName}' to {player.transform.position.x}, {player.transform.position.y}, {player.transform.position.z}");
				SendReply(player, $"Set Unlimited Metal Node '{spawnPointName}' spawn position.");
			}
			else
			{
				SendReply(player, "You do not have permission to use this command.");
			}
		}
        private void ListMetalEventSpawn(BasePlayer player)
		{
			if (permission.UserHasPermission(player.UserIDString, "metalevent.admin"))
			{
				if (spawnData.SpawnPositions.Count > 0)
				{
					int index = 1;
					foreach (KeyValuePair<string, Vector3> entry in spawnData.SpawnPositions)
					{
						SendReply(player, $"{index}. Name: {entry.Key}, Position: {entry.Value}");
						index++;
					}
				}
				else
				{
					SendReply(player, "No spawn points have been set.");
				}
			}
			else
			{
				SendReply(player, "You do not have permission to use this command.");
			}
		}
        private void ClearMetalEventSpawnChat(BasePlayer player)
		{
			if (permission.UserHasPermission(player.UserIDString, "metalevent.admin"))
			{
				spawnData.SpawnPositions.Clear();
				Interface.Oxide.DataFileSystem.WriteObject("MetalEventSpawnData", spawnData);
				SendReply(player, "All MetalEvent spawn points have been cleared.");
			}
			else
			{
				SendReply(player, "You do not have permission to use this command.");
			}
		}
		private void CreateMetalEventChat(BasePlayer player)
		{
			if (permission.UserHasPermission(player.UserIDString, "metalevent.admin"))
			{
				StartEvent();
			}
			else
			{
				SendReply(player, "You do not have permission to use this command.");
			}
		}
		private void StopMetalEventChat(BasePlayer player)
		{
			if (permission.UserHasPermission(player.UserIDString, "metalevent.admin"))
			{
				StopEvent();
			}
			else
			{
				SendReply(player, "You do not have permission to use this command.");
			}
		}
		private void OnNewSave(string filename)
		{
			if (resetNodesSpawnPoints)
			{
				ClearMetalEventSpawn(null);
			}
		}
		[ConsoleCommand("metallistclear")]
		private void ClearMetalEventSpawn(ConsoleSystem.Arg arg)
		{
			if (arg == null || arg.Connection?.player == null)
			{
				// Called from the server console or OnNewSave
				spawnData.SpawnPositions.Clear();
				Interface.Oxide.DataFileSystem.WriteObject("MetalEventSpawnData", spawnData);
				Puts("All MetalEvent spawn points have been cleared.");
			}
			else
			{
				var player = arg.Connection.player as BasePlayer;
				if (player != null && player.IsAdmin)
				{
					spawnData.SpawnPositions.Clear();
					Interface.Oxide.DataFileSystem.WriteObject("MetalEventSpawnData", spawnData);
					SendReply(player, "All MetalEvent spawn points have been cleared.");
				}
				else
				{
					SendReply(player, "You do not have permission to use this command.");
				}
			}
		}
		[ConsoleCommand("metallist")]
		private void MetalListConsole(ConsoleSystem.Arg arg)
		{
			if (spawnData.SpawnPositions.Count > 0)
			{
				int index = 1;
				foreach (KeyValuePair<string, Vector3> entry in spawnData.SpawnPositions)
				{
					Puts($"{index}. Name: {entry.Key}, Position: {entry.Value}");
					index++;
				}
			}
		}
		[ConsoleCommand("metalstart")]
		private void CreateMetalEvent(ConsoleSystem.Arg arg)
		{
    		var player = arg.Connection?.player as BasePlayer;
    		if (player != null && player.IsAdmin)
    		{
        		StartEvent();
    		}
    		else if (player == null)
    		{
        		StartEvent();
    		}
		}		
		[ConsoleCommand("metalstop")]
		private void StopMetalEvent(ConsoleSystem.Arg arg)
		{
    		var player = arg.Connection?.player as BasePlayer;
    		if (player != null && player.IsAdmin)
    		{
        		StopEvent();
    		}
    		else if (player == null)
    		{
        		StopEvent();
    		}
		}
		private void DeleteMetalEventSpawn(BasePlayer player, string spawnPointName)
		{
			if (permission.UserHasPermission(player.UserIDString, "metalevent.admin"))
			{
				if (string.IsNullOrEmpty(spawnPointName))
				{
					SendReply(player, "Please specify the name of the spawn point to delete.");
					return;
				}
				if (spawnData.SpawnPositions.ContainsKey(spawnPointName))
				{
					spawnData.SpawnPositions.Remove(spawnPointName);
					Interface.Oxide.DataFileSystem.WriteObject("MetalEventSpawnData", spawnData);
					Puts($"Removed spawn position '{spawnPointName}'");
					SendReply(player, $"Deleted Unlimited Metal Node '{spawnPointName}' spawn position.");
				}
				else
				{
					SendReply(player, $"No spawn point found with the name '{spawnPointName}'.");
				}
			}
			else
			{
				SendReply(player, "You do not have permission to use this command.");
			}
		}
		private void Unload()
		{
			eventTimer?.Destroy();
			Unsubscribe(nameof(OnMeleeAttack));
    		Unsubscribe(nameof(OnPlayerConnected));
			foreach (var basePlayer in BasePlayer.activePlayerList)
			{
				basePlayer.SendConsoleCommand("gametip.hidegametip");
				CuiHelper.DestroyUi(basePlayer, UI_METAL_EVENT);
			}
			foreach (var cluster in metalNodeClusters)
			{
    			foreach (BaseEntity node in cluster)
    			{
        			node.Kill();
    			}
			}
			foreach (var mapmarker in genericMarkers)
        	{
            	if(mapmarker != null)
            	{
                	mapmarker.Kill();
            	}
        	}
			foreach (var vending in vendingMarkers)
        	{
            	if(vending != null)
            	{
                	vending.Kill();
            	}
        	}
			foreach (var timer in lightningTimers)
			{
				timer.Destroy();
			}
			lightningTimers.Clear();
		}
    }
}


