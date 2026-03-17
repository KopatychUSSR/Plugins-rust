// 15FPS RUST привецвует Спасибо за покупку!
// самый лучший самый милый это ты

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Plugins {
	[Info("panel15fps", "15fps", "0.0.1")]
      //  Спецыально для 15FPS RUST
	[Description("Инфопанель для сервера")]
	class ZenPanel : RustPlugin {
		#region Поля

		[PluginReference] private Plugin ImageLibrary, ServerRewards, Economics;

		private TOD_Sky Sky;

		private readonly Dictionary<string, string> iconsUrls = new Dictionary<string, string> {
			{"ZenPanelIconTime", "https://i.imgur.com/CBKTj8B.png"},
			{"ZenPanelIconOnline", "https://i.imgur.com/lWoUIw2.png"},
			{"ZenPanelBckgr", "https://i.imgur.com/eAUEICV.png"}
		};

		private class StateIcon {
			public string Url;
			public string Name;
			public Type Type;
			public int Count;
			public Func<BaseNetworkable, bool> Test;

			public StateIcon() {
				Count = 0;
			}
		}

		private class UIDrawInfo {
      //  15FPS RUST
			public CuiElementContainer ui;
			public string name;

			public UIDrawInfo(CuiElementContainer ui, string name) {
      //  15FPS RUST
				this.ui = ui;
				this.name = name;
			}
		}

		List<StateIcon> stateIcons = new List<StateIcon> {
			new StateIcon {
				Name = "ZenPanelIconCargoPlane",
				Url = "https://i.imgur.com/CL41EJS.png",
				Type = typeof(CargoPlane),
				Test = e => e is CargoPlane
			},
			new StateIcon {
				Name = "ZenPanelIconPatrolHelicopter",
				Url = "https://i.imgur.com/HtAifod.png",
				Type = typeof(BaseHelicopter),
				Test = e => e is BaseHelicopter
			},
			new StateIcon {
				Name = "ZenPanelIconTank",
				Url = "https://i.imgur.com/j5cMDpt.png",
				Type = typeof(BradleyAPC),
				Test = e => e is BradleyAPC
			},
			new StateIcon {
				Name = "ZenPanelIconCH47",
				Url = "https://i.imgur.com/jRIF5y8.png",
				Type = typeof(CH47Helicopter),
				Test = e => e is CH47Helicopter
			},
			new StateIcon {
				Name = "ZenPanelIconCargoShip",
				Url = "https://i.imgur.com/xU8IUWO.png",
				Type = typeof(CargoShip),
				Test = e => e is CargoShip
			}
		};

		private readonly Dictionary<string, Timer> timers = new Dictionary<string, Timer> {
			{"clockTimer", null},
			{"iconTimer", null},
			{"onlineTimer", null},
			{"launchTimer", null},
			{"panelTimer", null},
			{"adviceTimer", null},
			{"economicsTimer", null}
		};

		private enum OffsetDataType {
			timer,
			online,
			icons
		}

		private class OffsetData {
			private Dictionary<BasePlayer, Dictionary<OffsetDataType, float>> widths;

			public void SetWidth(BasePlayer player, OffsetDataType type, float width = 0f) {
				if (!widths.ContainsKey(player)) {
					widths.Add(player, new Dictionary<OffsetDataType, float>());
				}

				if (!widths[player].ContainsKey(type)) {
					widths[player].Add(type, 0f);
				}

				widths[player][type] = width;
			}

			public float GetWidth(BasePlayer player, OffsetDataType type) {
				if (!widths.ContainsKey(player)) return 0f;

				return !widths[player].ContainsKey(type) ? 0f : widths[player][type];
			}

			public OffsetData() {
				widths = new Dictionary<BasePlayer, Dictionary<OffsetDataType, float>>();
			}
		}

		private OffsetData offsetData = new OffsetData();

		private bool panelReady;
		private string currTime;
		private string currOnline;
		private string currAdvice = "";
		private int currAdviceIdx;

		#endregion

		#region Конфигурация

		public class Configuration {
			[JsonProperty(PropertyName = "Версия конфига (не менять)")]
			public int version;

			[JsonProperty(PropertyName = "Команда настройки")]
			public string cmdOptions;

			[JsonProperty(PropertyName = "Советы")]
			public ConfigurationAdvice advice;

			[JsonProperty(PropertyName = "Логотип")]
			public ConfigurationLogo logo;

			[JsonProperty(PropertyName = "Скрытие элементов")]
			public ConfigurationHide hide;

			[JsonProperty(PropertyName = "Частота обновления")]
			public ConfigurationTimers timers;

			[JsonProperty(PropertyName = "Экономика")]
			public ConfigurationEconimc economic;
		}

		public class ConfigurationEconimc {
			[JsonProperty(PropertyName = "Ссылка на иконку (256x256)")]
			public string url;

			[JsonProperty(PropertyName = "Отображать")]
			public bool allow;

			[JsonProperty(PropertyName = "ServerRewards")]
			public bool serverRewards;

			[JsonProperty(PropertyName = "Economics")]
			public bool economics;
		}

		public class ConfigurationAdvice {
			[JsonProperty(PropertyName = "Показывать")]
			public bool allow;

			[JsonProperty(PropertyName = "Список")]
			public List<string> list;
		}

		public class ConfigurationLogo {
			[JsonProperty(PropertyName = "Включен")]
			public bool allow;

			[JsonProperty(PropertyName = "Ссылка")]
			public string url;

			[JsonProperty(PropertyName = "Размер X")]
			public int x;

			[JsonProperty(PropertyName = "Размер Y")]
			public int y;

			[JsonProperty(PropertyName = "Отступ верх-право X")]
			public int offsetX;

			[JsonProperty(PropertyName = "Отступ верх-право Y")]
			public int offsetY;
		}

		public class ConfigurationHide {
			[JsonProperty(PropertyName = "Разрешить")]
			public bool allow;

			[JsonProperty(PropertyName = "Только по пермишену zenpanel.canhide")]
			public bool usePermision;

			[JsonProperty(PropertyName = "Разрешить скрывать логотип")]
			public bool canHideLogo;
		}

		public class ConfigurationTimers {
			[JsonProperty(PropertyName = "Часы")] public float clock;

			[JsonProperty(PropertyName = "Онлайн")]
			public float online;

			[JsonProperty(PropertyName = "Советы")]
			public float advice;

			[JsonProperty(PropertyName = "Экономика")]
			public float economics;

			[JsonProperty(PropertyName = "Обновление UI")]
			public float main;
		}

		private Configuration config;

		protected override void LoadDefaultConfig() {
			config = new Configuration {
				version = 1,
				cmdOptions = "panel",
				advice = new ConfigurationAdvice {
					allow = true,
					list = new List<string> {
						"<color=#00ff00>Реклама сервера!</color>",
						"А еще <color=#ff0000>можно использовать цвета</color> :)",
						"Сколько угодно строчек",
						"Например группа сервера в ВК или ДС"
					}
				},
				logo = new ConfigurationLogo {
					allow = true,
					url = "https://i.imgur.com/c2e8gYD.png",
					x = 150,
					y = 29,
					offsetX = 10,
					offsetY = 5
				},
				hide = new ConfigurationHide {
					allow = true,
					canHideLogo = false,
					usePermision = false
				},
				timers = new ConfigurationTimers {
					online = 3f,
					clock = 1f,
					advice = 10f,
					economics = 2f,
					main = 60f
				},
				economic = new ConfigurationEconimc {
					url = "https://i.imgur.com/AmVZH1s.png",
					allow = true,
					economics = true,
					serverRewards = true
				}
			};
			SaveConfig();
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		#endregion

		#region Хранение данных

		private List<string> layersToHide = new List<string> {
			"IconsAll",
			"ZenPanelIconCargoPlane",
			"ZenPanelIconPatrolHelicopter",
			"ZenPanelIconTank",
			"ZenPanelIconCH47",
			"ZenPanelIconCargoShip",
			"Clock",
			"Online",
			"Advice",
			"Logo",
			"Money",
			"All"
		};

		public class PlayersDataOptions {
			public int clockType = 0;
		}

		public class PlayersData {
			public class PlayersDataRow {
				public List<string> hide;
				public PlayersDataOptions options;

				public PlayersDataRow() {
					hide = new List<string>();
					options = new PlayersDataOptions();
				}
			}

			public Dictionary<ulong, PlayersDataRow> data;

			public bool hasPlayer(BasePlayer player) {
				return data.ContainsKey(player.userID);
			}

			private void CheckData(BasePlayer player) {
				if (data.ContainsKey(player.userID)) return;

				data.Add(player.userID, new PlayersDataRow());
			}

			public List<string> GetHide(BasePlayer player) {
				CheckData(player);
				return data[player.userID].hide;
			}

			public PlayersDataOptions GetOptions(BasePlayer player) {
				CheckData(player);
				return data[player.userID].options;
			}

			public PlayersData() {
				data = new Dictionary<ulong, PlayersDataRow>();
			}
		}

		public PlayersData playersData;

		private void LoadData() {
			try {
				playersData = Interface.Oxide.DataFileSystem.ReadObject<PlayersData>($"{Title}.Players");
			} catch { }

			if (playersData != null) return;

			playersData = new PlayersData();
			SaveData();
		}

		private void SaveData() {
			Interface.Oxide.DataFileSystem.WriteObject($"{Title}.Players", playersData);
		}

		#endregion

		#region Вспомогательные утилиты

		private string GetImg(string name) {
			return (string)ImageLibrary?.Call("GetImage", name) ?? "";
		}

		private static int GetComponentCount(Type name) {
			return UnityEngine.Object.FindObjectsOfType(name).ToList().Count;
		}

		private static List<BasePlayer> GetActivePlayers() {
			var ret = new List<BasePlayer>();

			foreach (var p in BasePlayer.activePlayerList)
				if (p.IsValid() && p.net.connection != null)
					ret.Add(p);

			return ret;
		}

		private static void DestroyUI(BasePlayer player) {
			CuiHelper.DestroyUi(player, "ZenPanelUIMain");
			CuiHelper.DestroyUi(player, "ZenPanelUITimerText");
			CuiHelper.DestroyUi(player, "ZenPanelUIOnlineText");
			CuiHelper.DestroyUi(player, "ZenPanelUIIcons");
			CuiHelper.DestroyUi(player, "ZenPanelUIAdvice");
			CuiHelper.DestroyUi(player, "ZenPanelUILogo");
			CuiHelper.DestroyUi(player, "ZenPanelUIEconomics");
		}

		private static void DestroyUIAll(BasePlayer pl = null) {
			if (pl == null)
				foreach (var player in GetActivePlayers())
					DestroyUI(player);
			else
				DestroyUI(pl);
		}

		private static void DestroyTimers(IEnumerable<Timer> dtimers) {
			foreach (var tmr in dtimers) {
				if (tmr == null || tmr.Destroyed) return;

				tmr.Destroy();
			}
		}

		#endregion

		#region Отрисовка

		private void DrawUI(Func<BasePlayer, UIDrawInfo> cb, BasePlayer player) {
      //  15FPS RUST
			if (!player.IsConnected) return;

			if (player.IsReceivingSnapshot) {
				timer.Once(1f, () => DrawUI(cb, player));
				return;
			}

			var dInfo = cb(player);
      //  15FPS RUST
			if (string.IsNullOrEmpty(dInfo.name)) return;
      //  15FPS RUST

			var json = dInfo.ui?.ToJson();
      //  15FPS RUST
			CuiHelper.DestroyUi(player, dInfo.name);
      //  15FPS RUST FAVORITE RUST
			if (json == null) return;

			CuiHelper.AddUi(player, json);
		}

		private IEnumerator DrawAllUI_Corutine(Func<BasePlayer, UIDrawInfo> cb) {
      //  15FPS RUST
			var activePlayerList = BasePlayer.activePlayerList.ToArray().ToList();

			foreach (var player in activePlayerList) {
				DrawUI(cb, player);
				yield return null;
			}

			yield return null;
		}

		private void DrawAllUI(Func<BasePlayer, UIDrawInfo> cb, BasePlayer tPlayer = null) {
      //  15FPS RUST
			if (tPlayer != null) {
				DrawUI(cb, tPlayer);
				return;
			}

			Rust.Global.Runner.StartCoroutine(DrawAllUI_Corutine(cb));
		}

		private void RefreshAllUI(BasePlayer player = null) {
			DrawAllUI(DrawPanel, player);
			DrawAllUI(DrawClock, player);
			DrawAllUI(DrawOnline, player);
			DrawAllUI(DrawIcons, player);
			if (config.economic.allow) DrawAllUI(DrawEconomics, player);
			if (config.advice.allow) DrawAllUI(DrawAdvice, player);
			if (config.logo.allow) DrawAllUI(DrawLogo, player);
		}

		private static CuiPanel GetPanel() {
			return new CuiPanel {
				Image = {
					Color = "0 0 0 0"
				},
				RectTransform = {
					AnchorMin = "0 1",
					AnchorMax = "0 1",
					OffsetMin = "0 -40",
					OffsetMax = "390 0"
				}
			};
		}

		private bool CanHide(BasePlayer player) {
			return config.hide.allow && (!config.hide.usePermision || permission.UserHasPermission(player.UserIDString, $"{Title}.canhide"));
		}

		private bool GetHideState(BasePlayer player, string layer) {
			return CanHide(player) && playersData.GetHide(player).Contains(layer);
		}

		private UIDrawInfo DrawPanel(BasePlayer player) {
      //  15FPS RUST
			const string uiName = "ZenPanelUIMain";
			if (GetHideState(player, "All")) return new UIDrawInfo(null, uiName);
      //  15FPS RUST

			var ui = new CuiElementContainer {
				{GetPanel(), "Hud", uiName},
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiRawImageComponent {
							Color = "1 1 1 1",
							Png = GetImg("ZenPanelBckgr")
						},
						new CuiRectTransformComponent {
							AnchorMin = "0 1.1",
							AnchorMax = "0 1.1"
						}
					}
				}
			};

			if (!GetHideState(player, "Clock")) {
				var opt = playersData.GetOptions(player);
				ui.Add(new CuiElement {
					Parent = uiName,
					Components = {
						new CuiRawImageComponent {
							Png = GetImg("ZenPanelIconTime")
						},
						new CuiRectTransformComponent {
							AnchorMin = opt.clockType == 0 ? "0.02 0.2" : "0.0275 0.32",
							AnchorMax = opt.clockType == 0 ? "0.085 0.8" : "0.07 0.72"
						}
					}
				});
			}

			return new UIDrawInfo(ui, uiName);
      //  15FPS RUST
		}

		private UIDrawInfo DrawClock(BasePlayer player) {
      //  15FPS RUST
			const string uiName = "ZenPanelUITimerText";

			if (GetHideState(player, "Clock")) {
				offsetData.SetWidth(player, OffsetDataType.timer, 0.2275f);
				return new UIDrawInfo(null, uiName);
      //  15FPS RUST
			}

			var opt = playersData.GetOptions(player);

			offsetData.SetWidth(player, OffsetDataType.timer, opt.clockType == 0 ? 0f : 0.06f);

			var ui = new CuiElementContainer {
				{GetPanel(), "Hud", uiName},
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiTextComponent {
							Color = "1 1 1 1",
							Text = currTime,
							FontSize = opt.clockType == 0 ? 24 : 16,
							Align = TextAnchor.MiddleLeft
						},
						new CuiOutlineComponent {
							Color = "0.2 0.2 0.2 0.3",
							Distance = "1 -1"
						},
						new CuiRectTransformComponent {
							AnchorMin = opt.clockType == 0 ? "0.0975 0.05" : "0.09 0.05",
							AnchorMax = "1 1"
						}
					}
				}
			};
			return new UIDrawInfo(ui, uiName);
      //  15FPS RUST
		}

		private UIDrawInfo DrawOnline(BasePlayer player) {
      //  15FPS RUST
			const string uiName = "ZenPanelUIOnlineText";

			if (GetHideState(player, "Online")) {
				offsetData.SetWidth(player, OffsetDataType.online, 0.2f);
				return new UIDrawInfo(null, uiName);
      //  +380685583221
			}

			offsetData.SetWidth(player, OffsetDataType.online);

			var onlineOffset = 0.045f;

			if (currOnline.Length > 3) {
				onlineOffset -= (currOnline.Length-3)*0.0125f;
			}

			onlineOffset -= offsetData.GetWidth(player, OffsetDataType.timer);

			var ui = new CuiElementContainer {
				{GetPanel(), "Hud", uiName},
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiTextComponent {
							Text = currOnline,
							FontSize = 16,
							Align = TextAnchor.MiddleLeft
						},
						new CuiOutlineComponent {
							Color = "0.22 0.22 0.22 0.3",
							Distance = "1 -1"
						},
						new CuiRectTransformComponent {
							AnchorMin = $"{0.325f+onlineOffset} 0.08",
							AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 0"
						}
					}
				},
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiRawImageComponent {
							Png = GetImg("ZenPanelIconOnline")
						},
						new CuiRectTransformComponent {
							AnchorMin = $"{0.27f+onlineOffset} 0.32",
							AnchorMax = $"{0.31f+onlineOffset} 0.72"
						}
					}
				}
			};

			return new UIDrawInfo(ui, uiName);
      //  Слив плагина карается
		}

		private string GetEconomicsData(BasePlayer player) {
			if (config.economic.serverRewards) {
				var ret = ServerRewards?.Call<object>("CheckPoints", player.userID);
				if (ret == null) return "0";

				return (int)ret+"";
			}

			if (config.economic.economics) {
				var ret = Economics?.Call<double>("Balance", player.userID);
				if (ret == null) return "0";

				return (double)ret+"";
			}

			return "";
		}

		private UIDrawInfo DrawEconomics(BasePlayer player) {
      //  че́ залез пошол нахуй 
			const string uiName = "ZenPanelUIEconomics";
			if (!config.economic.allow || GetHideState(player, "Money")) return new UIDrawInfo(null, uiName);
      //  Мизантроп форева

			var ui = new CuiElementContainer {
				{
					new CuiPanel {
						Image = {
							Color = "0 0 0 0"
						},
						RectTransform = {
							AnchorMin = "0 1",
							AnchorMax = "0 1",
							OffsetMin = "0 -80",
							OffsetMax = "390 -40"
						}
					},
					"Hud", uiName
				},
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiRawImageComponent {
							Png = GetImg("ZenPanelMoney")
						},
						new CuiRectTransformComponent {
							AnchorMin = "0.0275 0.5",
							AnchorMax = "0.07 0.9"
						}
					}
				},
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiTextComponent {
							Color = "1 1 1 1",
							Text = GetEconomicsData(player),
							FontSize = 16,
							Align = TextAnchor.MiddleLeft
						},
						new CuiOutlineComponent {
							Color = "0.2 0.2 0.2 0.3",
							Distance = "1 -1"
						},
						new CuiRectTransformComponent {
							AnchorMin = "0.09 0.4",
							AnchorMax = "1 1"
						}
					}
				}
			};

			return new UIDrawInfo(ui, uiName);
      //  чтот не работает обратитесь к 15FPS RUST
		}

		private UIDrawInfo DrawIcons(BasePlayer player) {
      //  Я люблю Rust
			const string uiName = "ZenPanelUIIcons";

			var initialMin = 0.49f-offsetData.GetWidth(player, OffsetDataType.timer)-offsetData.GetWidth(player, OffsetDataType.online);
			var initialMax = 0.57f-offsetData.GetWidth(player, OffsetDataType.timer)-offsetData.GetWidth(player, OffsetDataType.online);

			if (GetHideState(player, "IconsAll")) return new UIDrawInfo(null, uiName);
      //  Alkad Пидор Гнилой хостинг

			var cnt = 0;
			var ui = new CuiElementContainer {
				{GetPanel(), "Hud", uiName}
			};

			foreach (var stateIcon in stateIcons) {
				if (stateIcon.Count == 0) continue;
				if (GetHideState(player, stateIcon.Name)) continue;

				ui.Add(new CuiElement {
					Components = {
						new CuiImageComponent {
							Png = GetImg(stateIcon.Name)
						},
						new CuiRectTransformComponent {
							AnchorMin = $"{initialMin+0.1f*cnt} 0.34",
							AnchorMax = $"{initialMax+0.1f*cnt} 0.73",
							OffsetMin = "0 0",
							OffsetMax = "0 0"
						}
					},
					Parent = uiName
				});
				cnt++;
			}

			return new UIDrawInfo(ui, uiName);
      //  послание из будующего
		}

		private UIDrawInfo DrawLogo(BasePlayer player) {
      //  идете нахуй 
			const string uiName = "ZenPanelUILogo";
			if (GetHideState(player, "Logo") && config.hide.canHideLogo) return new UIDrawInfo(null, uiName);
      //  и не лезьте в код

			var ui = new CuiElementContainer {
				{
					new CuiPanel {
						Image = {
							Color = "0 0 0 0"
						},
						RectTransform = {
							AnchorMin = "1 1",
							AnchorMax = "1 1",
							OffsetMin = $"{(config.logo.x+config.logo.offsetX)*-1} {(config.logo.y+config.logo.offsetY)*-1}",
							OffsetMax = $"{config.logo.offsetX*-1} {config.logo.offsetY*-1}"
						}
					},
					"Hud", uiName
				},
				new CuiElement {
					Parent = uiName,
					Name = uiName+"asfd",
					Components = {
						new CuiRawImageComponent {
							Png = GetImg("ZenPanelLogo")
						},
						new CuiRectTransformComponent {
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 0"
						}
					}
				}
			};
			return new UIDrawInfo(ui, uiName);
      //  дальше будут частушки
		}

		private UIDrawInfo DrawAdvice(BasePlayer player) {
      //  девки в озере купались
			const string uiName = "ZenPanelUIAdvice";
			if (GetHideState(player, "Advice")) return new UIDrawInfo(null, uiName);
      //  хуй резиновый нашли

			var ui = new CuiElementContainer {
				{
					new CuiPanel {
						Image = {
							Color = "1 1 1 0"
						},
						RectTransform = {
							AnchorMin = "0.5 0",
							AnchorMax = "0.5 0",
							OffsetMin = "-420 0",
							OffsetMax = "400 18"
						}
					},
					"Overlay", uiName
				},
				new CuiElement {
					Name = uiName+"Text",
					Parent = uiName,
					Components = {
						new CuiTextComponent {
							Text = currAdvice,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter,
							FadeIn = 0.5f,
							Color = "1 1 1 1"
						},
						new CuiOutlineComponent {
							Color = "0.4 0.4 0.4 0.6",
							Distance = "1 -1",
							UseGraphicAlpha = true
						},
						new CuiRectTransformComponent {
							AnchorMin = "0 0",
							AnchorMax = "1 1"
						}
					},
					FadeOut = 0.5f
				}
			};

			return new UIDrawInfo(ui, uiName);
      //  целый день они ебались
		}

		#endregion

		#region Обновление данных

		private void RefreshClockData() {
			var time = Sky.Cycle?.DateTime.ToString("HH:mm");
			if (time == null) return;

			currTime = time;
		}

		private void RefreshOnlineData() {
			currOnline = $"{GetActivePlayers().Count}/{ConVar.Server.maxplayers}";
		}

		private void RefreshIconsData() {
			foreach (var stateIcon in stateIcons) {
				stateIcon.Count = GetComponentCount(stateIcon.Type);
			}
		}

		private void RefreshAdviceData() {
			if (config.advice.list.Count == 0) return;

			currAdvice = config.advice.list[currAdviceIdx];

			if (currAdviceIdx >= config.advice.list.Count-1)
				currAdviceIdx = 0;
			else
				currAdviceIdx++;
		}

		private void RefreshAllData() {
			RefreshClockData();
			RefreshOnlineData();
			RefreshIconsData();
			if (config.advice.allow) RefreshAdviceData();
		}

		#endregion

		#region Запуск таймеров

		private void RunServiceTimers() {
			RefreshAllUI();

			timers["clockTimer"] = timer.Every(config.timers.clock,
				() => {
					RefreshClockData();
					DrawAllUI(DrawClock);
				});
			timers["onlineTimer"] = timer.Every(config.timers.online,
				() => {
					RefreshOnlineData();
					DrawAllUI(DrawOnline);
				});
			timers["iconTimer"] = timer.Every(1f, () => DrawAllUI(DrawIcons));

			if (config.advice.allow) {
				timers["adviceTimer"] = timer.Every(config.timers.advice,
					() => {
						RefreshAdviceData();
						DrawAllUI(DrawAdvice);
					});
			}

			if (config.economic.allow) {
				timers["economicsTimer"] = timer.Every(config.timers.economics, () => { DrawAllUI(DrawEconomics); });
			}

			timers["panelTimer"] = timer.Every(config.timers.main,
				() => {
					RefreshAllData();
					RefreshAllUI();
				});
		}

		#endregion

		#region Команды

		private static double GetAMin(int cnt) {
			cnt--;
			return 0.8f-cnt*0.08-0.01*cnt;
		}

		private static string GetAMax(int cnt) {
			cnt--;
			return 0.88f-cnt*0.08-0.01*cnt+"";
		}

		private void DrawSettingsUI(BasePlayer player) {
			const string uiName = "ZenPanelSettings";
			const string uiGreen = "0.61 0.75 0.24 0.4";
			const string uiRed = "0.80 0.29 0.25 0.4";
			const string uiGray = "0.4 0.4 0.4 0.4";

			var hide = playersData.GetHide(player);
			var options = playersData.GetOptions(player);

			var iconsAllEnabled = !hide.Contains("IconsAll");
			var ui = new CuiElementContainer {
				{
					new CuiPanel {
						Image = {
							Color = "0 0 0 0"
						},
						RectTransform = {
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-200 -200",
							OffsetMax = "200 200"
						}
					},
					"Hud", uiName
				},
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiImageComponent {
							Color = "0 0 0 0.6"
						},
						new CuiRectTransformComponent {
							AnchorMin = "0 0.9",
							AnchorMax = "1 1"
						}
					}
				},
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiTextComponent {
							Color = "1 1 1 1",
							Text = getMsg(player, "uiOptTitle"),
							FontSize = 18,
							Align = TextAnchor.UpperCenter
						},
						new CuiRectTransformComponent {
							AnchorMin = "0 0.9",
							AnchorMax = "1 0.975"
						}
					}
				}, {
					new CuiButton {
						Text = {
							Text = "X",
							FontSize = 12,
							Font = "robotocondensed-bold.ttf",
							Align = TextAnchor.MiddleCenter
						},
						Button = {
							Color = "0.79 0.29 0.25 1.00",
							Command = "ZenPanelHideOptions"
						},
						RectTransform = {
							AnchorMin = "0.915 0.915",
							AnchorMax = "0.9825 0.9825"
						}
					},
					uiName
				}
			};
			var i = 0;
			var idx = 0;

			if (CanHide(player)) {
				idx++;
				ui.Add(new CuiButton {
						Text = {
							Text = getMsg(player, "uiOptEvents"),
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Color = "1 1 1 1"
						},
						Button = {
							Color = iconsAllEnabled ? uiGreen : uiRed,
							Command = $"ZenPanelSetHide IconsAll {(iconsAllEnabled ? "0" : "1")}"
						},
						RectTransform = {
							AnchorMin = "0.015 0.8",
							AnchorMax = $"{(iconsAllEnabled ? 0.5175 : 0.985)} 0.88"
						}
					},
					uiName);

				if (iconsAllEnabled)
					foreach (var stateIcon in stateIcons) {
						ui.Add(new CuiButton {
								Text = {
									Text = "",
									FontSize = 18,
									Align = TextAnchor.MiddleCenter,
									Color = "1 1 1 1"
								},
								Button = {
									ImageType = Image.Type.Simple,
									Color = !hide.Contains(stateIcon.Name) ? uiGreen : uiRed,
									Command = $"ZenPanelSetHide {stateIcon.Name} {(!hide.Contains(stateIcon.Name) ? "0" : "1")}"
								},
								RectTransform = {
									AnchorMin = $"{0.525+i*0.0935} 0.8",
									AnchorMax = $"{0.61+i*0.0935} 0.88"
								}
							},
							uiName,
							uiName+stateIcon.Name);
						ui.Add(new CuiElement {
							Parent = uiName+stateIcon.Name,
							Components = {
								new CuiImageComponent {
									Png = GetImg(stateIcon.Name)
								},
								new CuiRectTransformComponent {
									AnchorMin = "0.1 0.3",
									AnchorMax = "0.9 0.7"
								}
							}
						});
						i++;
					}
			}

			idx++;
			ui.Add(new CuiButton {
					Text = {
						Text = getMsg(player, "uiOptClock"),
						FontSize = 18,
						Align = TextAnchor.MiddleCenter,
						Color = "1 1 1 1"
					},
					Button = {
						ImageType = Image.Type.Simple,
						Color = CanHide(player)
							? !hide.Contains("Clock")
								? uiGreen
								: uiRed
							: "0 0 0 0",
						Command = CanHide(player) ? $"ZenPanelSetHide Clock {(!hide.Contains("Clock") ? "0" : "1")}" : ""
					},
					RectTransform = {
						AnchorMin = $"0.015 {GetAMin(idx)}",
						AnchorMax = $"{(CanHide(player) && hide.Contains("Clock") ? 0.985 : 0.5175)} {GetAMax(idx)}"
					}
				},
				uiName);

			if (!CanHide(player) || !hide.Contains("Clock")) {
				ui.Add(new CuiButton {
						Text = {
							Text = getMsg(player, "uiOptClockBig"),
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Color = "1 1 1 1"
						},
						Button = {
							ImageType = Image.Type.Simple,
							Color = options.clockType == 0 ? uiGreen : uiGray,
							Command = "ZenPanelSetOptions Clock 0"
						},
						RectTransform = {
							AnchorMin = $"0.525 {GetAMin(idx)}",
							AnchorMax = $"0.75125 {GetAMax(idx)}"
						}
					},
					uiName);
				ui.Add(new CuiButton {
						Text = {
							Text = getMsg(player, "uiOptClockSmall"),
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Color = "1 1 1 1"
						},
						Button = {
							ImageType = Image.Type.Simple,
							Color = options.clockType == 1 ? uiGreen : uiGray,
							Command = "ZenPanelSetOptions Clock 1"
						},
						RectTransform = {
							AnchorMin = $"0.75875 {GetAMin(idx)}",
							AnchorMax = $"0.985 {GetAMax(idx)}"
						}
					},
					uiName);
			}

			if (CanHide(player)) {
				idx++;
				ui.Add(new CuiButton {
						Text = {
							Text = getMsg(player, "uiOptOnline"),
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Color = "1 1 1 1"
						},
						Button = {
							ImageType = Image.Type.Simple,
							Color = !hide.Contains("Online") ? uiGreen : uiRed,
							Command = $"ZenPanelSetHide Online {(!hide.Contains("Online") ? "0" : "1")}"
						},
						RectTransform = {
							AnchorMin = $"0.015 {GetAMin(idx)}",
							AnchorMax = $"0.985 {GetAMax(idx)}"
						}
					},
					uiName);
				idx++;
				ui.Add(new CuiButton {
						Text = {
							Text = getMsg(player, "uiOptAdvice"),
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Color = "1 1 1 1"
						},
						Button = {
							ImageType = Image.Type.Simple,
							Color = !hide.Contains("Advice") ? uiGreen : uiRed,
							Command = $"ZenPanelSetHide Advice {(!hide.Contains("Advice") ? "0" : "1")}"
						},
						RectTransform = {
							AnchorMin = $"0.015 {GetAMin(idx)}",
							AnchorMax = $"0.985 {GetAMax(idx)}"
						}
					},
					uiName);

				if (config.economic.allow) {
					idx++;
					ui.Add(new CuiButton {
							Text = {
								Text = getMsg(player, "uiOptMoney"),
								FontSize = 18,
								Align = TextAnchor.MiddleCenter,
								Color = "1 1 1 1"
							},
							Button = {
								ImageType = Image.Type.Simple,
								Color = !hide.Contains("Money") ? uiGreen : uiRed,
								Command = $"ZenPanelSetHide Money {(!hide.Contains("Money") ? "0" : "1")}"
							},
							RectTransform = {
								AnchorMin = $"0.015 {GetAMin(idx)}",
								AnchorMax = $"0.985 {GetAMax(idx)}"
							}
						},
						uiName);
				}

				if (config.hide.canHideLogo) {
					idx++;
					ui.Add(new CuiButton {
							Text = {
								Text = getMsg(player, "uiOptLogo"),
								FontSize = 18,
								Align = TextAnchor.MiddleCenter,
								Color = "1 1 1 1"
							},
							Button = {
								ImageType = Image.Type.Simple,
								Color = !hide.Contains("Logo") ? uiGreen : uiRed,
								Command = $"ZenPanelSetHide Logo {(!hide.Contains("Logo") ? "0" : "1")}"
							},
							RectTransform = {
								AnchorMin = $"0.015 {GetAMin(idx)}",
								AnchorMax = $"0.985 {GetAMax(idx)}"
							}
						},
						uiName);
				}

				idx++;
				ui.Add(new CuiButton {
						Text = {
							Text = getMsg(player, "uiOptAll"),
							FontSize = 18,
							Align = TextAnchor.MiddleCenter,
							Color = "1 1 1 1"
						},
						Button = {
							ImageType = Image.Type.Simple,
							Color = !hide.Contains("All") ? uiGreen : uiRed,
							Command = $"ZenPanelSetHide All {(!hide.Contains("All") ? "0" : "1")}"
						},
						RectTransform = {
							AnchorMin = $"0.015 {GetAMin(idx)}",
							AnchorMax = $"0.985 {GetAMax(idx)}"
						}
					},
					uiName);
			}

			ui.Insert(1,
				new CuiElement {
					Parent = uiName,
					Components = {
						new CuiImageComponent {
							Color = "0 0 0 0.4"
						},
						new CuiRectTransformComponent {
							AnchorMin = $"0 {GetAMin(idx)-0.0175f}",
							AnchorMax = "1 1"
						},
						new CuiNeedsCursorComponent()
					}
				});

			CuiHelper.DestroyUi(player, uiName);
			CuiHelper.AddUi(player, ui.ToJson());
		}

		[ConsoleCommand("ZenPanelHideOptions")]
		private void cmdShowOptions(ConsoleSystem.Arg args) {
			var player = args.Player();
			if (player == null) return;

			CuiHelper.DestroyUi(player, "ZenPanelSettings");
		}

		[ConsoleCommand("ZenPanelSetOptions")]
		private void cmdSetOptions(ConsoleSystem.Arg args) {
			var player = args.Player();
			if (player == null || !args.HasArgs(2)) return;

			var options = playersData.GetOptions(player);

			if (args.Args[0] == "Clock") {
				int val;
				int.TryParse(args.Args[1], out val);
				options.clockType = val;
			}

			SaveData();
			RefreshAllUI(player);
			DrawSettingsUI(player);
		}

		[ConsoleCommand("ZenPanelSetHide")]
		private void cmdSetHide(ConsoleSystem.Arg args) {
			var player = args.Player();
			if (player == null || !args.HasArgs(2)) return;

			var hideData = playersData.GetHide(player);

			var layerName = layersToHide.Find(l => l == args.Args[0]);
			if (string.IsNullOrEmpty(layerName)) return;

			var state = args.Args[1] != "1";

			if (state) {
				if (!hideData.Contains(layerName)) hideData.Add(layerName);
			} else {
				if (hideData.Contains(layerName)) hideData.Remove(layerName);
			}

			if (args.Args[0] == "All") {
				foreach (var layer in layersToHide) {
					if (state) {
						if (!hideData.Contains(layer)) hideData.Add(layer);
					} else {
						if (hideData.Contains(layer)) hideData.Remove(layer);
					}
				}
			}

			SaveData();
			RefreshAllUI(player);
			DrawSettingsUI(player);
		}

		private void cmdShowOptions(IPlayer iplayer, string command, string[] args) {
			var player = BasePlayer.Find(iplayer.Id);
			if (player == null) return;

			DrawSettingsUI(player);
		}

		#endregion

		#region Хуки Oxide

		private void Loaded() {
			config = Config.ReadObject<Configuration>();

			if (config.version != 1) {
				PrintError("Неверная версия конфигурационного файла! Плагин выгружен!");
				Interface.Oxide.UnloadPlugin(Title);
			}

			LoadData();
			if (!config.hide.allow) return;

			if (config.hide.usePermision) permission.RegisterPermission($"{Title}.canhide", this);
		}

		void OnServerInitialized() {
			Sky = TOD_Sky.Instance;

			if (ImageLibrary == null) {
				PrintError("[ImageLibrary] not found, unloading!");
				Interface.Oxide.UnloadPlugin(Title);
				return;
			}

			if (Economics == null && config.economic.economics) {
				PrintWarning("[Economics] not found, disabled!");
				config.economic.economics = false;
			}

			if (ServerRewards == null && config.economic.serverRewards) {
				PrintWarning("[ServerRewards] not found, disabled!");
				config.economic.serverRewards = false;
			}

			if (!config.economic.economics && !config.economic.serverRewards) config.economic.allow = false;

			if (config.logo.allow) iconsUrls.Add("logo", config.logo.url);

			RefreshAllData();

			foreach (var icon in iconsUrls) ImageLibrary.Call("AddImage", icon.Value, icon.Key);
			foreach (var stateIcon in stateIcons) ImageLibrary.Call("AddImage", stateIcon.Url, stateIcon.Name);
			if (config.logo.allow) ImageLibrary.Call("AddImage", config.logo.url, "ZenPanelLogo");
			if (config.economic.allow) ImageLibrary.Call("AddImage", config.economic.url, "ZenPanelMoney");

			AddCovalenceCommand(config.cmdOptions, "cmdShowOptions");

			timers["launchTimer"] = timer.Every(0.5f,
				() => {
					if (!panelReady)
						if (!(bool)ImageLibrary.Call("IsReady"))
							return;

					panelReady = true;
					timers["launchTimer"].Destroy();
					RunServiceTimers();
				});
		}

		void OnPlayerInit(BasePlayer player) {
			if (!player.IsConnected) return;

			if (player.IsReceivingSnapshot) {
				timer.Once(1f, () => OnPlayerInit(player));
				return;
			}

			if (!playersData.hasPlayer(player)) {
				playersData.GetHide(player);
				playersData.GetOptions(player);
				SaveData();
			}

			RefreshAllUI(player);
		}

		void Unload() {
			SaveData();
			DestroyTimers(timers.Values.ToList());
			DestroyUIAll();

			foreach (var player in BasePlayer.activePlayerList) {
				CuiHelper.DestroyUi(player, "ZenPanelSettings");
			}
		}

		void OnEntitySpawned(BaseNetworkable entity) {
			foreach (var stateIcon in stateIcons) {
				if (!stateIcon.Test(entity)) continue;

				stateIcon.Count++;
			}
		}

		void OnEntityKill(BaseNetworkable entity) {
			foreach (var stateIcon in stateIcons) {
				if (!stateIcon.Test(entity)) continue;

				if (stateIcon.Count > 0) stateIcon.Count--;
			}
		}

		#endregion

		#region Многоязычность

		private string getMsg(BasePlayer player, string key, params object[] args) {
			return string.Format(lang.GetMessage(key, this, player.UserIDString), args);
		}

		private new void LoadDefaultMessages() {
			lang.RegisterMessages(new Dictionary<string, string> {
					["uiOptTitle"] = "Panel settings",
					["uiOptEvents"] = "Events",
					["uiOptClock"] = "Clock",
					["uiOptClockBig"] = "Big",
					["uiOptClockSmall"] = "Small",
					["uiOptOnline"] = "Online",
					["uiOptAdvice"] = "Advices",
					["uiOptMoney"] = "Balance",
					["uiOptLogo"] = "Logo",
					["uiOptAll"] = "Switch all"
				},
				this);

			lang.RegisterMessages(new Dictionary<string, string> {
					["uiOptTitle"] = "Настройка отображения инфопанели",
					["uiOptEvents"] = "События",
					["uiOptClock"] = "Часы",
					["uiOptClockBig"] = "Большие",
					["uiOptClockSmall"] = "Маленькие",
					["uiOptOnline"] = "Онлайн",
					["uiOptAdvice"] = "Советы",
					["uiOptMoney"] = "Баланс",
					["uiOptLogo"] = "Логотип",
					["uiOptAll"] = "Переключить все"
				},
				this,
				"ru");

			lang.RegisterMessages(new Dictionary<string, string> {
					["uiOptTitle"] = "Configuración del Panel",
					["uiOptEvents"] = "Eventos",
					["uiOptClock"] = "Reloj",
					["uiOptClockBig"] = "Grande",
					["uiOptClockSmall"] = "Chico",
					["uiOptOnline"] = "En Línea",
					["uiOptAdvice"] = "Mensajes",
					["uiOptMoney"] = "Dinero",
					["uiOptLogo"] = "Logo",
					["uiOptAll"] = "Cambiar ON/OFF"
				},
				this,
				"es-ES");
		}

		#endregion
	}
}
