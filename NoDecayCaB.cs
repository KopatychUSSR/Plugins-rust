using Rust;

namespace Oxide.Plugins
{
	[Info("NoDecayCaB", "https://topplugin.ru/", "1.0.2")]
	class NoDecayCaB : RustPlugin
	{
		private void OnEntityTakeDamage(BaseCombatEntity combatentity, HitInfo hitinfo)
		{
			if ((combatentity is MiniCopter || combatentity is MotorRowboat || combatentity is RidableHorse) && hitinfo != null)
			{
				if (hitinfo.damageTypes.Get(DamageType.Decay) > 0)
				{
					BaseEntity entity = combatentity as BaseEntity;
					if (entity != null)
					{
						BuildingPrivlidge buildingprivlidge = entity.GetBuildingPrivilege();
						if (buildingprivlidge != null)
						{
							hitinfo.damageTypes.Scale(Rust.DamageType.Decay, 0);
						}
					}
				}
			}
		}
	}
}