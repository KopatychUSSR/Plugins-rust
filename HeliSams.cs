using Facepunch;
using Rust;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;



namespace Oxide.Plugins
{

    [Info("Heli Sams", "Xavier", "1.0.3")]
	[Description("Lets your Samsites target Patrol Helicopters")]
	
	public class HeliSams : RustPlugin
    {

		static HeliSams plugin;

		private bool ConfigChanged;
		private DynamicConfigFile data;
		private StoredData storedData;
		
		private static BaseVehicleSeat Seat;
		
		// Dictionaries and Lists
		private List<ulong> samIDs = new List<ulong>();
		
		private class StoredData
        {
            public List<ulong> samIDs = new List<ulong>();
		}
		
		private void SaveData()
        {
            storedData.samIDs = samIDs;
			data.WriteObject(storedData);
		}
		
		int SamSiteAttackDistance = 300;
		float CheckForTargetEveryXSeconds = 5f;
		
		void LoadVariables()
		{
			
			// Configs here
			SamSiteAttackDistance = Convert.ToInt32(GetConfig("SamSite Settings", "Default distance to start targetting", "300"));
			CheckForTargetEveryXSeconds = Convert.ToSingle(GetConfig("SamSite Settings", "Frequency Check for Patrol Helicopters in Seconds", "5"));
			
			if (ConfigChanged)
			{
				SaveConfig();
			}
			else
			{
				ConfigChanged = false;
				return;
			}
		}
		
		private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }
		
		protected override void LoadDefaultConfig()
		{
			LoadVariables();
		}
		
		void Init()
		{
			LoadVariables();
			plugin = this;
			
			data = Interface.Oxide.DataFileSystem.GetFile(Name);
			
			try
            {
                storedData = data.ReadObject<StoredData>();
				samIDs = storedData.samIDs;
				}
            catch
            {
                storedData = new StoredData();
            }
		}
		
		private void OnServerSave() => SaveData();
		
		void Unload()
        {
			foreach (var sam in UnityEngine.Object.FindObjectsOfType<SamSite>())
            {
				var ss = sam.GetComponent<HeliTargeting>();
				if (ss)
					ss.UnloadDestroy();
			}
			
			SaveData();
			plugin= null;
		}
		
		void OnServerInitialized()
		{
			foreach (var sam in UnityEngine.Object.FindObjectsOfType<SamSite>())
			{
				if (samIDs.Contains(sam.net.ID))
				{
					sam.gameObject.AddComponent<HeliTargeting>();
				}
			}
		}
		
		
		#region Hooks
		void OnEntitySpawned(SamSite entity)
		{
			if (entity.OwnerID == 0) return;
			entity.gameObject.AddComponent<HeliTargeting>();
			samIDs.Add(entity.net.ID);
		}
		
		void OnEntityKill(SamSite entity)
		{
			samIDs.Remove(entity.net.ID);
		}

		#endregion
		
		private class HeliTargeting : MonoBehaviour
		{
			
			private SamSite samsite;
			private BaseEntity entity;
			
			private void Awake()
			{
				entity = gameObject.GetComponent<BaseEntity>();
				samsite = entity.GetComponent<SamSite>();
				
				InvokeRepeating("FindTargets", plugin.CheckForTargetEveryXSeconds, 1.0f);
			}
			
			internal void FindTargets()
			{
				if (!samsite.IsPowered()) return;
				
				if (samsite.currentTarget == null)
				{
					
					// Almost exact code as per Rust
					List<BaseCombatEntity> nearby = new List<BaseCombatEntity>();
					Vis.Entities(((Component) samsite.eyePoint).transform.position, plugin.SamSiteAttackDistance, nearby);
					
					BaseCombatEntity baseCombatEntity1 = (BaseCombatEntity) null;
					
					foreach (BaseCombatEntity baseCombatEntity2 in nearby)
					{
						var prefabname = baseCombatEntity2?.PrefabName ?? string.Empty;
						if (string.IsNullOrEmpty(prefabname)) return;
						
						if (samsite.EntityCenterPoint((BaseEntity) baseCombatEntity2).y >= ((Component) samsite.eyePoint).transform.position.y && baseCombatEntity2.IsVisible(((Component) samsite.eyePoint).transform.position, plugin.SamSiteAttackDistance * 2f)  && prefabname.Contains("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab") || prefabname.Contains("assets/prefabs/npc/ch47/ch47scientists.entity.prefab"))
							baseCombatEntity1 = baseCombatEntity2;
					}
					
					samsite.currentTarget = baseCombatEntity1;
				}
				
				if (samsite.currentTarget != null)
				{
					float distance = Vector3.Distance(samsite.transform.position, samsite.currentTarget.transform.position);
					
					if (distance <= plugin.SamSiteAttackDistance)
					{
						samsite.InvokeRandomized(new Action(samsite.WeaponTick), 0.0f, 0.5f, 0.2f); //Taken from Assembly-CSharp
					}
					if (distance > plugin.SamSiteAttackDistance)
					{
						samsite.currentTarget = null;
						samsite.CancelInvoke(new Action(samsite.WeaponTick));
					}
				}
			}

			public void UnloadDestroy()
            {
                Destroy(this);	
            }
			
			public void Destroy()
			{
				if (plugin.samIDs.Contains(samsite.net.ID))
                    plugin.samIDs.Remove(samsite.net.ID);
				CancelInvoke("FindTargets");
			}
			
		}
		

		
	}
}