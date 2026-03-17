using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Death Messages", "https://vk.com/rustnastroika", "2.1.0")]
    class DeathMessages : RustPlugin
    {
        private static PluginConfig _config;
        private List<DeathMessage> _notes = new List<DeathMessage>();
        private Dictionary<ulong, HitInfo> _lastHits = new Dictionary<ulong, HitInfo>();

        #region Classes / Enums

        class PluginConfig
        {
            public int Cooldown { get; set; }

            public int FontSize { get; set; }

            public bool ShowDeathAnimals { get; set; }
            public bool ShowDeathSleepers { get; set; }
            public bool Log { get; set; }

            public string ColorAttacker { get; set; }
            public string ColorVictim { get; set; }
            public string ColorWeapon { get; set; }
            public string ColorDistance { get; set; }
            public string ColorBodyPart { get; set; }

            public double Distance { get; set; }

            public string HelicopterName { get; set; }
			public string BradleyAPCName { get; set; }
			
			
			

            public Dictionary<string, string> Weapons { get; set; }

            public Dictionary<string, string> Structures { get; set; }

            public Dictionary<string, string> Traps { get; set; }

            public Dictionary<string, string> Turrets { get; set; }

            public Dictionary<string, string> Animals { get; set; }

            public Dictionary<string, string> Messages { get; set; }

            public Dictionary<string, string> BodyParts { get; set; }
        }

        enum AttackerType
        {
            Player,
            Helicopter,
            Animal,
            Turret,
            Structure,
            Trap,
            Invalid,
			NPC,
			BradleyAPC
        }

        enum VictimType
        {
            Player,
            Helicopter,
            Animal,
            Invalid,
			NPC,
			BradleyAPC
        }

        enum DeathReason
        {
            Turret,
            Helicopter,
			HelicopterDeath,
			BradleyAPC,
			BradleyAPCDeath,
            Structure,
            Trap,
            Animal,
            AnimalDeath,
            Generic,
            Hunger,
            Thirst,
            Cold,
            Drowned,
            Heat,
            Bleeding,
            Poison,
            Suicide,
            Bullet,
            Arrow,
            Flamethrower,
            Slash,
            Blunt,
            Fall,
            Radiation,
            Stab,
            Explosion,
            Unknown
        }

        class Attacker
        {
            public Attacker(BaseEntity entity)
            {
                Entity = entity;
                Type = InitializeType();
                Name = InitializeName();
            }

            public BaseEntity Entity { get; }

            public string Name { get; }

            public AttackerType Type { get; }

            private AttackerType InitializeType()
            {
                if (Entity == null)
                    return AttackerType.Invalid;

                if (Entity is NPCPlayer)
                    return AttackerType.NPC;

                if (Entity is BasePlayer)
                    return AttackerType.Player;
				
                if (Entity is BaseHelicopter)
                    return AttackerType.Helicopter;
				
				 if (Entity is BradleyAPC)
                    return AttackerType.BradleyAPC;

                if (Entity.name.Contains("agents/"))
                    return AttackerType.Animal;

                if (Entity.name.Contains("barricades/") || Entity.name.Contains("wall.external.high"))
                    return AttackerType.Structure;

                if (Entity.name.Contains("beartrap.prefab") || Entity.name.Contains("landmine.prefab") || Entity.name.Contains("spikes.floor.prefab"))
                    return AttackerType.Trap;

                if (Entity.name.Contains("autoturret_deployed.prefab") || Entity.name.Contains("flameturret.deployed.prefab"))
                    return AttackerType.Turret;

                return AttackerType.Invalid;
            }

            private string InitializeName()
            {
                if (Entity == null)
                    return null;

                switch (Type)
                {
                    case AttackerType.Player:
                        return Entity.ToPlayer().displayName;
						
					case AttackerType.NPC:
						return "NPC";

                    case AttackerType.Helicopter:
                        return "Patrol Helicopter";
						
					case AttackerType.BradleyAPC:
						return "BradleyAPCName";

                    case AttackerType.Turret:
                    case AttackerType.Trap:
                    case AttackerType.Animal:
                    case AttackerType.Structure:
                        return FormatName(Entity.name);
                }

                return string.Empty;
            }
        }

        class Victim
        {
            public Victim(BaseCombatEntity entity)
            {
                Entity = entity;
                Type = InitializeType();
                Name = InitializeName();
            }

            public BaseCombatEntity Entity { get; }

            public string Name { get; }

            public VictimType Type { get; }

            private VictimType InitializeType()
            {
                if (Entity == null)
                    return VictimType.Invalid;

                if (Entity is NPCPlayer)
                    return VictimType.NPC;

                if (Entity is BasePlayer)
                    return VictimType.Player;

                if (Entity is BaseHelicopter)
                    return VictimType.Helicopter;
				
				
				if (Entity is BradleyAPC)
					return VictimType.BradleyAPC;

                if (Entity.name.Contains("agents/"))
                    return VictimType.Animal;

                return VictimType.Invalid;
            }

            private string InitializeName()
            {
                switch (Type)
                {
                    case VictimType.Player:
                        return Entity.ToPlayer().displayName;
						
					case VictimType.NPC:
						return "NPC";

                    case VictimType.Helicopter:
                        return "Patrol Helicopter";
						
					case VictimType.BradleyAPC:
						return "BradleyAPC";

                    case VictimType.Animal:
                        return FormatName(Entity.name);
                }

                return string.Empty;
            }
        }

        class DeathMessage
        {
            public DeathMessage(Attacker attacker, Victim victim, string weapon, string damageType, string bodyPart, double distance)
            {
                Attacker = attacker;
                Victim = victim;
                Weapon = weapon;
                DamageType = damageType;
                BodyPart = bodyPart;
                Distance = distance;

                Reason = InitializeReason();
                Message = InitializeDeathMessage();

				
                if (_config.Distance <= 0)
                {
                    Players = BasePlayer.activePlayerList.ToList<BasePlayer>();
                }				
                else
                {
					
                    var position = attacker?.Entity?.transform?.position;
                    if (position == null)
                        position = victim?.Entity?.transform?.position;

                    if (position != null)
                        Players = BasePlayer.activePlayerList.Where(x => x.Distance((UnityEngine.Vector3)position) <= _config.Distance).ToList();
                    else
                        Players = new List<BasePlayer>();
					
                }				

                if (victim.Type == VictimType.Player && !Players.Contains(victim.Entity.ToPlayer()))
                    Players.Add(victim.Entity.ToPlayer());

                if (attacker.Type == AttackerType.Player && !Players.Contains(attacker.Entity.ToPlayer()))
                    Players.Add(attacker.Entity.ToPlayer());
            }

            public List<BasePlayer> Players { get; }

            public Attacker Attacker { get; }

            public Victim Victim { get; }

            public string Weapon { get; }

            public string BodyPart { get; }

            public string DamageType { get; }

            public double Distance { get; }

            public DeathReason Reason { get; }

            public string Message { get; }

            private DeathReason InitializeReason()
            {
                if (Attacker.Type == AttackerType.Turret)
                    return DeathReason.Turret;
                else if (Attacker.Type == AttackerType.Helicopter)
                    return DeathReason.Helicopter;
				
				 else if (Attacker.Type == AttackerType.BradleyAPC)
                    return DeathReason.BradleyAPC;
				
                else if (Victim.Type == VictimType.Helicopter)
                    return DeathReason.HelicopterDeath;
				
				 else if (Victim.Type == VictimType.BradleyAPC)
                    return DeathReason.BradleyAPCDeath;
                else if (Attacker.Type == AttackerType.Structure)
                    return DeathReason.Structure;
                else if (Attacker.Type == AttackerType.Trap)
                    return DeathReason.Trap;
                else if (Attacker.Type == AttackerType.Animal)
                    return DeathReason.Animal;
                else if (Victim.Type == VictimType.Animal)
                    return DeathReason.AnimalDeath;
                else if (Weapon == "F1 Grenade" || Weapon == "Survey Charge" || Weapon == "Timed Explosive Charge" || Weapon == "Satchel Charge" || Weapon == "Beancan Grenade")
                    return DeathReason.Explosion;
                else if (Weapon == "Flamethrower")
                    return DeathReason.Flamethrower;
                else if (Victim.Type == VictimType.Player || Victim.Type == VictimType.NPC)
                    return GetDeathReason(DamageType);

                return DeathReason.Unknown;
            }

            private DeathReason GetDeathReason(string damage)
            {
                var reasons = (Enum.GetValues(typeof(DeathReason)) as DeathReason[]).Where(x => x.ToString().Contains(damage));

                if (reasons.Count() == 0)
                    return DeathReason.Unknown;

                return reasons.First();
            }

            private string InitializeDeathMessage()
            {
                string message = string.Empty;
                string reason = string.Empty;

                if (Victim.Type == VictimType.Player && Victim.Entity.ToPlayer().IsSleeping() && _config.Messages.ContainsKey(Reason + " Sleeping"))
                    reason = Reason + " Sleeping";
                else
                    reason = Reason.ToString();

                message = GetMessage(reason, _config.Messages);

                var attackerName = Attacker.Name;
				if (string.IsNullOrEmpty(attackerName) && Attacker.Entity == null && Weapon.Contains("Heli"))
					attackerName = _config.HelicopterName;
				// if (string.IsNullOrEmpty(attackerName) && Attacker.Entity == null && Weapon.Contains("Bradley"))
				// attackerName = _config.BradleyAPCName;

                switch (Attacker.Type)
                {
                    case AttackerType.Helicopter:
                        attackerName = _config.HelicopterName;
                        break;
						
						
						case AttackerType.BradleyAPC:
                        attackerName = _config.BradleyAPCName;
                        break;

                    case AttackerType.Turret:
                        attackerName = GetMessage(attackerName, _config.Turrets);
                        break;

                    case AttackerType.Trap:
                        attackerName = GetMessage(attackerName, _config.Traps);
                        break;

                    case AttackerType.Animal:
                        attackerName = GetMessage(attackerName, _config.Animals);
                        break;

                    case AttackerType.Structure:
                        attackerName = GetMessage(attackerName, _config.Structures);
                        break;
                }

                var victimName = Victim.Name;

                switch (Victim.Type)
                {
                    case VictimType.Helicopter:
                        victimName = _config.HelicopterName;
                        break;
						case VictimType.BradleyAPC:
                        victimName = _config.BradleyAPCName;
                        break;

                    case VictimType.Animal:
                        victimName = GetMessage(victimName, _config.Animals);
                        break;
                }

                message = message.Replace("{attacker}", $"<color={_config.ColorAttacker}>{attackerName}</color>");
                message = message.Replace("{victim}", $"<color={_config.ColorVictim}>{victimName}</color>");
                message = message.Replace("{distance}", $"<color={_config.ColorDistance}>{Math.Round(Distance, 0)}</color>");
                message = message.Replace("{weapon}", $"<color={_config.ColorWeapon}>{GetMessage(Weapon, _config.Weapons)}</color>");
                message = message.Replace("{bodypart}", $"<color={_config.ColorBodyPart}>{GetMessage(BodyPart, _config.BodyParts)}</color>");

                return message;
            }
        }

        #endregion

        #region Oxide Hooks

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(new PluginConfig
            {
                Cooldown = 15,
                FontSize = 15,
                Distance = -1,
                Log = true,
                ShowDeathAnimals = false,
                ShowDeathSleepers = true,

                ColorAttacker = "#ff9c00",
                ColorVictim = "#ff9c00",
                ColorDistance = "#ff9c00",
                ColorWeapon = "#ffffff",
                ColorBodyPart = "#ffffff",

                HelicopterName = "Вертолет",
				BradleyAPCName = "Танк",

                Weapons = new Dictionary<string, string>
                {
                    { "Assault Rifle", "Assault Rifle" },
                    { "Beancan Grenade", "Beancan" },
					{ "Bolt Action Rifle", "Bolt Action Rifle" },
					{ "Bone Club", "Bone Club" },
					{ "Bone Knife", "Bone Knife" },
					{ "Crossbow", "Crossbow" },
					{ "Custom SMG", "SMG" },
					{ "Double Barrel Shotgun", "Double Shotgun" },
					{ "Eoka Pistol", "Eoka" },
					{ "F1 Grenade", "F1" },
					{ "Flame Thrower", "Flame Thrower" },
					{ "Hunting Bow", "Hunting Bow" },
					{ "Longsword", "Longsword" },
					{ "LR-300 Assault Rifle", "LR-300" },
					{ "M249", "М249" },
					{ "M92 Pistol", "M92" },
					{ "Mace", "Mace" },
					{ "Machete", "Machete" },
					{ "MP5A4", "MP5A4" },
					{ "Pump Shotgun", "Shotgun" },
					{ "Python Revolver", "Python Revolver" },
					{ "Revolver", "Revolver" },
					{ "Salvaged Cleaver", "Salvaged Cleaver" },
					{ "Salvaged Sword", "Salvaged Sword" },
					{ "Semi-Automatic Pistol", "Semi-Automatic Pistol" },
					{ "Semi-Automatic Rifle", "Semi-Automatic Rifle" },
					{ "Stone Spear", "Stone Spear" },
					{ "Thompson", "Thompson" },
					{ "Waterpipe Shotgun", "Waterpipe Shotgun" },
					{ "Wooden Spear", "Wooden Spear" },
					{ "Hatchet", "Hatchet" },
					{ "Pick Axe", "Pick Axe" },
					{ "Salvaged Axe", "Salvaged Axe" },
					{ "Salvaged Hammer", "Salvaged Hammer" },
					{ "Salvaged Icepick", "Salvaged Icepick" },
					{ "Satchel Charge", "Satchel Charge" },
					{ "Stone Hatchet", "Stone Hatchet" },
					{ "Stone Pick Axe", "Stone Pick Axe" },
					{ "Survey Charge", "Survey Charge" },
					{ "Timed Explosive Charge", "С4" },
					{ "Torch", "Torch" },
					{ "RocketSpeed", "Скоростная ракета" },
					{ "Incendiary Rocket", "Зажигательная ракета" },
					{ "Rocket", "Обычная ракета" },
					{ "RocketHeli", "Напм" }
                },

                Structures = new Dictionary<string, string>
                {
                    { "Wooden Barricade", "Деревянная баррикада" },
                    { "Barbed Wooden Barricade", "Колючая деревянная баррикада" },
                    { "Metal Barricade", "Металлическая баррикада" },
                    { "High External Wooden Wall", "Высокая внешняя деревянная стена" },
                    { "High External Stone Wall", "Высокая внешняя каменная стена" },
                    { "High External Wooden Gate", "Высокие внешние деревянные ворота" },
                    { "High External Stone Gate", "Высокие внешние каменные ворота" }
                },

                Traps = new Dictionary<string, string>
                {
                    { "Snap Trap", "Капкан" },
                    { "Land Mine", "Мина" },
                    { "Wooden Floor Spikes", "Деревянные колья" }
                },

                Turrets = new Dictionary<string, string>
                {
                    { "Flame Turret", "Огнеметная турель" },
                    { "Auto Turret", "Автотурель" }
                },

                Animals = new Dictionary<string, string>
                {
                    { "Boar", "Кабан" },
                    { "Horse", "Лошадь" },
                    { "Wolf", "Волк" },
                    { "Stag", "Олень" },
                    { "Chicken", "Курица" },
                    { "Bear", "Медведь" }
                },

                BodyParts = new Dictionary<string, string>
                {
                    { "body", "Тело" },
                    { "pelvis", "Таз" },
                    { "hip", "Бедро" },
                    { "left knee", "Левое колено" },
                    { "right knee", "Правое колено" },
                    { "left foot", "Левая стопа" },
                    { "right foot", "Правая стопа" },
                    { "left toe", "Левый палец" },
                    { "right toe", "Правый палец" },
                    { "groin", "Пах" },
                    { "lower spine", "Нижний позвоночник" },
                    { "stomach", "Желудок" },
                    { "chest", "Грудь" },
                    { "neck", "Шея" },
                    { "left shoulder", "Левое плечо" },
                    { "right shoulder", "Правое плечо" },
                    { "left arm", "Левая рука" },
                    { "right arm", "Правая рука" },
                    { "left forearm", "Левое предплечье" },
                    { "right forearm", "Правое предплечье" },
                    { "left hand", "Левая ладонь" },
                    { "right hand", "Правая ладонь" },
                    { "left ring finger", "Левый безымянный палец" },
                    { "right ring finger", "Правый безымянный палец" },
                    { "left thumb", "Левый большой палец" },
                    { "right thumb", "Правый большой палец" },
                    { "left wrist", "Левое запястье" },
                    { "right wrist", "Правое запястье" },
                    { "head", "Голова" },
                    { "jaw", "Челюсть" },
                    { "left eye", "Левый глаз" },
                    { "right eye", "Правый глаз" }
                },

                Messages = new Dictionary<string, string>
                {
                    { "Arrow", "{attacker} убил {victim} ({weapon}, {distance} м.)" },
                    { "Blunt",  "{attacker} убил {victim} ({weapon})" },
                    { "Bullet", "{attacker} убил {victim} ({weapon}, {distance} м.)" },
                    { "Flamethrower", "{attacker} сжег заживо игрока {victim} ({weapon})" },
                    { "Drowned", "{victim} утонул." },
                    { "Explosion", "{attacker} взорвал игрока {victim} ({weapon})" },
                    { "Fall", "{victim} разбился." },
                    { "Generic", "Смерть забрала {victim} с собой." },
                    { "Heat", "{victim} сгорел заживо." },
                    { "Helicopter", "{victim} был убит патрульным вертолётом." },
					{ "BradleyAPC", "{victim} был убит Танком." },
					{ "BradleyAPCDeath", "{victim} был уничтожен игроком {attacker}" },
                    { "HelicopterDeath", "{attacker} сбил {victim} " },
                    { "Animal", "{attacker} добрался до {victim}" },
                    { "AnimalDeath", "{attacker} убил {victim} ({weapon}, {distance} м.)" },
                    { "Hunger", "{victim} умер от голода." },
                    { "Poison", "{victim} умер от отравления." },
                    { "Radiation", "{victim} умер от радиационного отравления" },
                    { "Slash", "{attacker} убил {victim} ({weapon})" },
                    { "Stab", "{attacker} убил {victim} ({weapon})" },
                    { "Structure", "{victim} умер от сближения с {attacker}" },
                    { "Suicide", "{victim} совершил самоубийство." },
                    { "Thirst", "{victim} умер от обезвоживания" },
                    { "Trap", "{victim} попался на ловушку {attacker}" },
                    { "Cold", "{victim} умер от холода" },
                    { "Turret", "{victim} был убит автоматической турелью" },
                    { "Unknown", "У {victim} что-то пошло не так." },
					{ "Bleeding", "{victim} умер от кровотечения" },

                    //  Sleeping
                    { "Blunt Sleeping", "{attacker} убил {victim} ({weapon})" },
                    { "Bullet Sleeping", "{attacker} убил {victim} ({weapon}, {distance} метров)" },
                    { "Flamethrower Sleeping", "{attacker} сжег игрока {victim} ({weapon})" },
                    { "Explosion Sleeping", "{attacker} убил {victim} ({weapon})" },
                    { "Generic Sleeping", "Смерть забрала {victim} с собой пока он спал." },
                    { "Helicopter Sleeping", "{victim} был убит {attacker} пока он спал." },
					{ "BradleyAPC Sleeping", "{victim} был убит {attacker} пока он спал." },
                    { "Animal Sleeping", "{victim} убил {attacker} пока он спал." },
                    { "Slash Sleeping", "{attacker} убил {victim} ({weapon})" },
                    { "Stab Sleeping", "{attacker} убил {victim} ({weapon})" },
                    { "Unknown Sleeping", "У игрока {victim} что-то пошло не так." },
					{ "Turret Sleeping", "{attacker} был убит автоматической турелью." }
                }
            }, true);

            PrintWarning("Благодарим за приобритение плагина на сайте RustPlugin.ru. Если вы приобрели этот плагин на другом ресурсе знайте - это лишает вас гарантированных обновлений!");
        }

        private void OnServerInitialized()
        {
            _config = Config.ReadObject<PluginConfig>();
        }
		
		private Dictionary<uint,BasePlayer> LastHeli=new Dictionary<uint,BasePlayer>();
		
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer)
                _lastHits[entity.ToPlayer().userID] = info;
			if (entity is BaseHelicopter && info.InitiatorPlayer != null)
				LastHeli[entity.net.ID]= info.InitiatorPlayer;
        }

        private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim == null)
                return;

            if (info == null)
                if (!(victim is BasePlayer) || !victim.ToPlayer().IsWounded() || !_lastHits.TryGetValue(victim.ToPlayer().userID, out info))
                    return;

            var _attacker = new Attacker(info.Initiator);
            var _victim = new Victim(victim);
			
            if (_victim.Type == VictimType.Invalid)
                return;
			
			if (_victim.Type == VictimType.Helicopter) 
			{
				if (LastHeli.ContainsKey(victim.net.ID))
				{
					_attacker=new Attacker(LastHeli[victim.net.ID]);
				}
			}
			
			if (_attacker.Type != AttackerType.Player && _victim.Type != VictimType.Player) return;
			
            if ((_victim.Type == VictimType.Animal || _attacker.Type == AttackerType.Animal) && !_config.ShowDeathAnimals)
				return;

            if (_victim.Type == VictimType.Player && _victim.Entity.ToPlayer().IsSleeping() && !_config.ShowDeathSleepers)
                return;

			//Puts($"weapon - {info?.Weapon?.GetItem()?.info?.displayName?.english ?? FormatName(info?.WeaponPrefab?.name)}");
            //Puts($"weaponPrefab - {info?.WeaponPrefab?.name}");
			//Puts("Entity name - " + _attacker?.Entity?.name);
			
            var _weapon = FirstUpper(info?.Weapon?.GetItem()?.info?.displayName?.english) ?? FormatName(info?.WeaponPrefab?.name);
            var _damageType = FirstUpper(victim.lastDamage.ToString());
            var _bodyPart = victim?.skeletonProperties?.FindBone(info.HitBone)?.name?.english ?? "";
            var _distance = info.ProjectileDistance;

            AddNote(new DeathMessage(_attacker, _victim, _weapon, _damageType, _bodyPart, _distance));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }

        #endregion

        #region Core

        private void AddNote(DeathMessage note)
        {
            _notes.Insert(0, note);
            if (_notes.Count > 8)
                _notes.RemoveRange(7, _notes.Count - 8);

            RefreshUI(note);
            timer.Once(_config.Cooldown, () =>
            {
                _notes.Remove(note);
                RefreshUI(note);
            });
        }

        #endregion

        #region UI

        private void RefreshUI(DeathMessage note)
        {
            foreach (var player in note.Players)
            {
                DestroyUI(player);
                InitilizeUI(player);
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ui.deathmessages");
        }

        private void InitilizeUI(BasePlayer player)
        {
            var notes = _notes.Where(x => x.Players.Contains(player)).Take(8);

            if (notes.Count() == 0)
                return;

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.8", AnchorMax = "0.99 0.995" }
            }, name: "ui.deathmessages");

            double index = 1;
            foreach (var note in notes)
            {
                InitilizeLabel(container, note.Message, $"0 {index - 0.2}", $"0.99 {index}");
                index -= 0.14;
            }

            CuiHelper.AddUi(player, container);
        }
		
       private string InitilizeLabel(CuiElementContainer container, string text, string anchorMin, string anchorMax)
		{
			string Name = CuiHelper.GetGuid();
			container.Add(new CuiElement
			{
				Name = Name,
				Parent = "ui.deathmessages",
				Components =
				{
					new CuiTextComponent { Align = UnityEngine.TextAnchor.MiddleRight, FontSize = _config.FontSize, Text = text },
					new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax },
					new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1.0 -0.5" }
				}
			});
			return Name;
		}

        #endregion

        #region Helpers

        private static string FirstUpper(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return string.Join(" ", str.Split(' ').Select(x => x.Substring(0, 1).ToUpper() + x.Substring(1, x.Length - 1)).ToArray());
        }

        private static string FormatName(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
                return string.Empty;

            var formatedPrefab = FirstUpper(prefab.Split('/').Last().Replace(".prefab", "").Replace(".entity", "").Replace(".weapon", "").Replace(".deployed", "").Replace("_", "."));

            switch (formatedPrefab)
            {
                case "Autoturret.deployed": return "Auto Turret";
                case "Flameturret": return "Flame Turret";

                case "Beartrap": return "Snap Trap";
                case "Landmine": return "Land Mine";
                case "Spikes.floor": return "Wooden Floor Spikes";

                case "Barricade.wood": return "Wooden Barricade";
                case "Barricade.woodwire": return "Barbed Wooden Barricade";
                case "Barricade.metal": return "Metal Barricade";
                case "Wall.external.high.wood": return "High External Wooden Wall";
                case "Wall.external.high.stone": return "High External Stone Wall";
                case "Gates.external.high.stone": return "High External Wooden Gate";
                case "Gates.external.high.wood": return "High External Stone Gate";

                case "Stone.hatchet": return "Stone Hatchet";
                case "Surveycharge": return "Survey Charge";
                case "Explosive.satchel": return "Satchel Charge";
                case "Explosive.timed": return "Timed Explosive Charge";
                case "Grenade.beancan": return "Beancan Grenade";
                case "Grenade.f1": return "F1 Grenade";
                case "Hammer.salvaged": return "Salvaged Hammer";
                case "Axe.salvaged": return "Salvaged Axe";
                case "Icepick.salvaged": return "Salvaged Icepick";
                case "Spear.stone": return "Stone Spear";
                case "Spear.wooden": return "Wooden Spear";
                case "Knife.bone": return "Bone Knife";
                case "Rocket.basic": return "Rocket";
				case "Flamethrower": return "Flamethrower";
				case "Rocket.hv": return "RocketSpeed";
				case "Rocket.heli": return "RocketHeli";
				

                default: return formatedPrefab;
            }
        }

        private static string GetMessage(string name, Dictionary<string, string> source)
        {
            if (source.ContainsKey(name))
                return source[name];

            return name;
        }

        #endregion
    }
}
