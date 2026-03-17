namespace Oxide.Plugins {
	[Info("CupBoardLimit", "Lime", "0.1.1", ResourceId = 223991)]
	[Description("Ограничения для хранения ресурсов в шкафу")]
	class CupBoardLimit : RustPlugin {
		bool CheckItem(Item item, int i) {
			if (item?.info == null) return false;
			switch (item.info.shortname) {
				case "metal.refined":   return true;
				case "stones":          return true;
				case "metal.fragments": return true;
				case "wood":            return true;
				case "scrap":			return true
				default:                return false;
			}
		}

		object CanLootEntity(BasePlayer player, StorageContainer container) {
			if (container.ShortPrefabName != "cupboard.tool.deployed") return null;
			container.inventory.canAcceptItem = CheckItem;
			return null;
		}
	}
}
