using System.Text;
using Oxide.Core;
using Object = System.Object;
using System.Linq;
using UnityEngine;
using System;
using Oxide.Game.Rust.Cui;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("IQSorter", "Mercury", "1.10.9")]
    [Description("Easy sorting with the UI menu")]
    public class IQSorter : RustPlugin
    {
        private readonly Dictionary<String, Int32> categoryIndex = new();
        private static ImageUI _imageUI;

        private class TemplateButton
        {
            public String iconName;
            public String textInventory;
            public String textContainer;
            public String command;
        }

        private class InterfaceBuilder
        {
            
            public static InterfaceBuilder Instance;
            public const String IQ_HEADER_PLAYER = "IQ_HEADER_PLAYER";
            public const String IQ_HEADER_CONTAINER = "IQ_HEADER_CONTAINER";
            public Dictionary<String, String> Interfaces;

            
            public InterfaceBuilder()
            {
                Instance = this;
                Interfaces = new Dictionary<String, String>();
                
                Building_PanelHeader();
                Building_ButtonTemplate();
                
                Building_TitleHeaderUpper();
                Building_TitleHeaderLower();
            }

            public static void AddInterface(String name, String json)
            {
                if (Instance.Interfaces.ContainsKey(name))
                {
                    _.PrintError($"Error! Tried to add existing cui elements! -> {name}");
                    return;
                }
		   		 		  						  	   		  		 			  	   		  		  		   		 
                Instance.Interfaces.Add(name, json);
            }
		   		 		  						  	   		  		 			  	   		  		  		   		 
            public static String GetInterface(String name)
            {
                String json = String.Empty;
                if (Instance.Interfaces.TryGetValue(name, out json) == false)
                {
                    _.PrintWarning($"Warning! UI elements not found by name! -> {name}");
                }

                return json;
            }

            public static void DestroyAll()
            {
                for (Int32 i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    BasePlayer player = BasePlayer.activePlayerList[i];
                    CuiHelper.DestroyUi(player, IQ_HEADER_PLAYER);
                    CuiHelper.DestroyUi(player, IQ_HEADER_CONTAINER);
                }
            }
    
            private void Building_PanelHeader()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0 0 0 0" },
                    RectTransform ={ AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                },"Hud.Menu","%NAME_PANEL%", "%NAME_PANEL%");
                
                AddInterface("UI_HEADER_PANEL", container.ToJson());
            }
            
            private void Building_TitleHeaderUpper()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "TITLE_UPPER_%NAME_PANEL%",
                    Parent = "%NAME_PANEL%",
                    DestroyUi = "TITLE_UPPER_%NAME_PANEL%",
                    Components = {
                        new CuiTextComponent { Text = "%TEXT_TITLE%", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = "0.8941177 0.854902 0.8196079 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                    }
                });
                
                AddInterface("UI_TITLE_UPPER", container.ToJson());
            }
            
            private void Building_TitleHeaderLower()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "TITLE_LOWER_%NAME_PANEL%",
                    Parent = "%NAME_PANEL%",
                    DestroyUi = "TITLE_LOWER_%NAME_PANEL%",
                    Components = {
                        new CuiTextComponent { Text = "%TEXT_TITLE%", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8941177 0.854902 0.8196079 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                    }
                });
                
                AddInterface("UI_TITLE_LOWER", container.ToJson());
            }
            
            
            private void Building_ButtonTemplate()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 0.03", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                    RectTransform ={ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                },"%NAME_PANEL%","%BUTTON_MAIN%_%NAME_PANEL%", "%BUTTON_MAIN%_%NAME_PANEL%");

                container.Add(new CuiElement
                {
                    Name = "ICON_%BUTTON_MAIN%_%NAME_PANEL%",
                    Parent = "%BUTTON_MAIN%_%NAME_PANEL%",
                    DestroyUi = "ICON_%BUTTON_MAIN%_%NAME_PANEL%",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = "%IMAGE_BUTTON%" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64 -8.333", OffsetMax = "-47.333 8.333" }
                    }
                });
		   		 		  						  	   		  		 			  	   		  		  		   		 
                container.Add(new CuiElement
                {
                    Name = "TEXT_%BUTTON_MAIN%_%NAME_PANEL%",
                    Parent = "%BUTTON_MAIN%_%NAME_PANEL%",
                    DestroyUi = "TEXT_%BUTTON_MAIN%_%NAME_PANEL%",
                    Components = {
                        new CuiTextComponent { Text = "%TITLE_BUTTON%", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8941177 0.854902 0.8196079 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.333 -8.333", OffsetMax = "64 8.333" }
                    }
                });
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_BUTTON%", Color = "0 0 0 0" },
                    Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0 0 0 0" }
                },  "%BUTTON_MAIN%_%NAME_PANEL%", "ACTION_BUTTON_%BUTTON_MAIN%_%NAME_PANEL%", "ACTION_BUTTON_%BUTTON_MAIN%_%NAME_PANEL%");
		   		 		  						  	   		  		 			  	   		  		  		   		 
                
                AddInterface("UI_BUTTON_TEMPLATE", container.ToJson());
            }
           
        }

                
		
        private readonly List<TemplateButton> buttonsCasheInfo = new()
        {
            new TemplateButton
            {
                iconName = "UI_ICO_SORTED",
                textInventory = "UI_SORTED_BUTTON",
                textContainer = "UI_SORTED_BUTTON",
                command = "ui_sorter sorted {0}",
            },
            new TemplateButton
            {
                iconName = "UI_ICO_PICK",
                textInventory = "UI_PUT_ALL",
                textContainer = "UI_TAKE_ALL",
                command = "ui_sorter move.all {0}",
            },
            new TemplateButton 
            {
                iconName = "UI_ICO_SIMULAR",
                textInventory = "UI_PUT_SIMILAR",
                textContainer = "UI_TAKE_SIMILAR",
                command = "ui_sorter move.similar {0}",
            },
        };
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
	        sb.Clear();
	        if (args != null)
	        {
		        sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
		        return sb.ToString();
	        }
	        return lang.GetMessage(LangKey, this, userID);
        }
        
        
                
        private List<Int32> GetSimilarItemIDs(Int32 itemID)
        {
            List<Int32> similarItemIds = new List<Int32> { itemID };

            if (itemDlcReplaced.TryGetValue(itemID, out List<Int32> items))
                similarItemIds.AddRange(items);
		   		 		  						  	   		  		 			  	   		  		  		   		 
            return similarItemIds;
        }
        private const Boolean LanguageEn = false;

                
                
        private void MoveAllItems(BasePlayer player, ItemContainer fromContainer, ItemContainer toContainer)
        {
            if(toContainer.entityOwner != null && toContainer.entityOwner.OwnerID == 0) return; 
            
            Interface.CallHook("OnStartMoveAllItems", player, fromContainer, toContainer);
		   		 		  						  	   		  		 			  	   		  		  		   		 
            List<Item> casheList = Facepunch.Pool.Get<List<Item>>();
            casheList = fromContainer.itemList.OrderByDescending(x => x.position).ToList();

            Int32 spaceLeft = Math.Min(toContainer.capacity, casheList.Count);
            for (Int32 i = casheList.Count - 1; i >= 0 && spaceLeft > 0; i--)
            {
                Item storageItem = casheList[i];
                if (storageItem.MoveToContainer(toContainer))
                    spaceLeft--;
            }
            
            Facepunch.Pool.FreeUnmanaged(ref casheList);
            
            Interface.CallHook("OnMovedAllItems", player, fromContainer, toContainer);
        }
        
        
        [ConsoleCommand("ui_sorter")] 
        private void UISorterCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permissionUse)) return;

            String action = arg.Args[0];
            if (!Boolean.TryParse(arg.Args[1], out Boolean isContainer)) return;

            switch (action)
            {
                case "sorted":
                {
                    if (isContainer)
                    {
                        foreach (ItemContainer lootContainer in player.inventory.loot.containers)
                            SortedStorageContainer(player, lootContainer);
                        return;
                    }
                    
                    SortedStorageContainer(player, player.inventory.containerMain);
                    break;
                }
                case "move.all":
                {
                    if (isContainer)
                    {
                        foreach (ItemContainer toContainer in player.inventory.loot.containers)
                            MoveAllItems(player,toContainer, player.inventory.containerMain);
                        return;
                    }
              
                    foreach (ItemContainer toContainer in player.inventory.loot.containers)
                        MoveAllItems(player,player.inventory.containerMain, toContainer);
                    
                    break;
                }
                case "move.similar":
                {
                    if (isContainer)
                    {
                        foreach (ItemContainer toContainer in player.inventory.loot.containers)
                            MoveSimilarItems(player,toContainer, player.inventory.containerMain);
                        return;
                    }
                    
                    foreach (ItemContainer toContainer in player.inventory.loot.containers)
                        MoveSimilarItems(player,player.inventory.containerMain, toContainer);
                    break;
                }
            }
        }
                
                private static Configuration config = new Configuration();
        
        private void Unload()
        {
            if (_ == null) return;
            _ = null;
            
            InterfaceBuilder.DestroyAll();
            
            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
            }

            if (cachedUI != null)
            {
                cachedUI.Clear();
                cachedUI = null;
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        
                
        
        private void SortedStorageContainer(BasePlayer player, ItemContainer itemContainer)
        {
            if (itemContainer == null) return;
            
            Interface.CallHook("OnStartSortedContainer", player, itemContainer);

            Boolean isCupboard = itemContainer.entityOwner != null &&
                                 itemContainer.entityOwner.ShortPrefabName.Contains("cupboard");
            
            List<Item> casheItemList = Facepunch.Pool.Get<List<Item>>();
            
            casheItemList = isCupboard ? itemContainer.itemList.Where(x => !cupboardIgnoreItems.Contains(x.info.shortname)).ToList() : itemContainer.itemList.ToList();
            Dictionary<Item, Item.Flag> itemFlags = casheItemList.ToDictionary(item => item, item => item.flags);
            
            foreach (Item item in casheItemList)
            {
                item.RemoveFromContainer();
                item.MoveToContainer(itemContainer);
            }
            
            casheItemList = isCupboard ? itemContainer.itemList.Where(x => !cupboardIgnoreItems.Contains(x.info.shortname)).ToList() : itemContainer.itemList.ToList();
            
            Dictionary<String, Int32> shortnameCounts = new();
            foreach (Item item in casheItemList)
            {
                String shortname = item.info.shortname;
                shortnameCounts.TryAdd(shortname, 0);
                shortnameCounts[shortname]++;
            }

            List<String> sortedShortnames = shortnameCounts.OrderByDescending(pair => pair.Value)
                .Select(pair => pair.Key)
                .ToList();
		   		 		  						  	   		  		 			  	   		  		  		   		 
            casheItemList.Sort((item1, item2) =>
            {
                String category1 = item1.info.category.ToString();
                String category2 = item2.info.category.ToString();
                Int32 categoryIndex1 = categoryIndex.GetValueOrDefault(category1, int.MaxValue);
                Int32 categoryIndex2 = categoryIndex.GetValueOrDefault(category2, int.MaxValue);

                if (categoryIndex1 != categoryIndex2)
                    return categoryIndex1.CompareTo(categoryIndex2);

                Int32 index1 = sortedShortnames.IndexOf(item1.info.shortname);
                Int32 index2 = sortedShortnames.IndexOf(item2.info.shortname);
                Int32 shortnameComparison = index1 != index2 ? index1.CompareTo(index2) : 0;

                if (ItemIdCorrecteds.Contains(index1) || ItemIdCorrecteds.Contains(index2))
                    return categoryIndex1.CompareTo(categoryIndex2);
                
                return shortnameComparison != 0 ? shortnameComparison : item2.amount.CompareTo(item1.amount);
            });

            Int32 counter = 0;
            foreach (Item storageItem in casheItemList.ToArray())
            {
                if(isCupboard && counter >= 24) continue;
                storageItem.position = counter++;
            }

            foreach (Item item in casheItemList)
                if (itemFlags.TryGetValue(item, out Item.Flag flag))
                    item.flags = flag;
            
            itemContainer.MarkDirty();
            
            Interface.CallHook("OnSortedContainer", player, itemContainer);

            Facepunch.Pool.FreeUnmanaged(ref casheItemList);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning(LanguageEn ? $"Error reading #54327 configuration 'oxide/config/{Name}', creating a new configuration!!" : $"Ошибка чтения #54327 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        
        
                
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is not (StorageContainer or BaseCorpse or SamSite or AutoTurret or DroppedItemContainer)) return;
            if (entity.GetParentEntity() is TrainCarUnloadable) return;
            if (entity is Locker or Workbench or ResearchTable or RepairBench or ShopFront or Recycler or BigWheelBettingTerminal or SlotMachineStorage or CardGamePlayerStorage) return;
            if (!permission.UserHasPermission(player.UserIDString, permissionUse)) return;
            
            if (config.buildingAuth && !player.IsBuildingAuthed() && !(player.GetParentEntity() is Tugboat tugboat && tugboat.IsAuthed(player)))
                return;
            
            if (entity is VendingMachine)
            {
                VendingMachine machine = entity as VendingMachine;
                if (machine != null)
                {
                    if (machine.CanPlayerAdmin(player) && machine.PlayerBehind(player))
                    {
                        DrawUI_Panel(player);
                        DrawUI_Panel(player, true);
                    }
                }
                return;
            }
            DrawUI_Panel(player);

            Boolean isOven = entity is BaseOven;
            DrawUI_Panel(player, true, isOven);
        }
        
        private List<String> cupboardIgnoreItems = new()
        {
            "building.planner",
            "wiretool",
            "hosetool",
            "hammer",
            "pipetool",
            "spraycan",
            "hosetool",
            "toolgun"
        };

        private static IQSorter _;
        

                
        
        
        private void MoveSimilarItems(BasePlayer player,ItemContainer fromContainer, ItemContainer toContainer)
        {
            Interface.CallHook("OnStartMoveSimilarItems", player, fromContainer, toContainer);

            List<Item> casheList = Facepunch.Pool.Get<List<Item>>();
            casheList = fromContainer.itemList.ToList();
            
            Int32 spaceLeft = Math.Min(toContainer.capacity, casheList.Count);
            
            for (Int32 i = casheList.Count - 1; i >= 0 && spaceLeft > 0; i--)
            {
                Item item = casheList[i];
                List<Int32> similarItemIds = GetSimilarItemIDs(item.info.itemid);

                foreach (Int32 itemId in similarItemIds)
                {
                    if (toContainer.GetAmount(itemId, false) > 0)
                    {
                        item.MoveToContainer(toContainer);
                        spaceLeft--;
                        break; // Переход к следующему предмету после успешного перемещения
                    }
                }
            }

            Facepunch.Pool.FreeUnmanaged(ref casheList);
            
            Interface.CallHook("OnMovedSimilarItems", player, fromContainer, toContainer);
        }

        private String DrawUI_Button(BasePlayer player, Boolean isContainer, String namePanel, Int32 buttonCount, Int32 y)
        {
            if (player == null) return null;
            String Interface = InterfaceBuilder.GetInterface("UI_BUTTON_TEMPLATE");
            if (Interface == null) return null;

            String iconButton = buttonsCasheInfo[buttonCount].iconName;
            String textButton = isContainer ? buttonsCasheInfo[buttonCount].textContainer : buttonsCasheInfo[buttonCount].textInventory;
            String commandButton = String.Format(buttonsCasheInfo[buttonCount].command, isContainer);
            
            Interface = Interface.Replace("%NAME_PANEL%", namePanel);
            Interface = Interface.Replace("%OFFSET_MIN%", $"-64 {10 - (y * 19)}");
            Interface = Interface.Replace("%OFFSET_MAX%", $"64 {27 - (y * 19)}");
            Interface = Interface.Replace("%BUTTON_MAIN%", $"{namePanel}_{y}_{buttonCount}");
            Interface = Interface.Replace("%TITLE_BUTTON%", GetLang(textButton, player.UserIDString));
            Interface = Interface.Replace("%IMAGE_BUTTON%", _imageUI.GetImage(iconButton));
            Interface = Interface.Replace("%COMMAND_BUTTON%", commandButton);

            return Interface;
        }
        /// <summary>
        /// - Исправлена сортировка в шкафу, когда какой-то предмет из списка инструментов мог уехать в общий список
        /// </summary>
		   		 		  						  	   		  		 			  	   		  		  		   		 
        
        private Dictionary<String, List<String>> cachedUI = new();
        private readonly List<Int32> ItemIdCorrecteds = new List<Int32> { 1074866732, 2004072627, 2018009552, 2006957888, 930560607, 1123047824, 1130765085, 442289265, 090353317, 15301168, 118372687 }; //Игнорирование itemID (удаление дубликатов)

                
        
        private void Init()
        {
            _ = this;
            
            for (Int32 i = 0; i < config.categoryOrder.Count; i++)
                categoryIndex.Add(config.categoryOrder[i], i);
        }
        
                
        
        private static StringBuilder sb = new StringBuilder();

                
        
        private void DrawUI_Panel(BasePlayer player, Boolean isContainer = false, Boolean isOven = false)
        {
            if (_interface == null || player == null) return;

            Object resultHook = Oxide.Core.Interface.CallHook("OnOpenSorted", player, isContainer, isOven);
            if (resultHook != null)
                return;
            
            List<String> cachedUI = GetOrSetCacheUI(player, isContainer, isOven);
            if (cachedUI != null)
            {
                foreach (String uiCached in cachedUI)
                    CuiHelper.AddUi(player, uiCached);
                
                return;
            }
		   		 		  						  	   		  		 			  	   		  		  		   		 
            String Interface = InterfaceBuilder.GetInterface("UI_HEADER_PANEL");
            if (Interface == null) return;

            String namePanel = isContainer || isOven ? InterfaceBuilder.IQ_HEADER_CONTAINER : InterfaceBuilder.IQ_HEADER_PLAYER;
            String offsetMin = isOven ? $"{193 + config.positionOven.XPositionOffset} {53.719 - config.positionOven.YPositionOffset}" : isContainer ? $"{198.833 + config.positionStorageContainer.XPositionOffset} {53.719 - config.positionStorageContainer.YPositionOffset}" : $"{52.267 + config.positionInventory.XPositionOffset} {488.452 - config.positionInventory.YPositionOffset}";
            String offsetMax = isOven ? $"{321 + config.positionOven.XPositionOffset} {108.281 - config.positionOven.YPositionOffset}" : isContainer ? $"{326.833 + config.positionStorageContainer.XPositionOffset} {108.281 - config.positionStorageContainer.YPositionOffset}" : $"{180.267 + config.positionInventory.XPositionOffset} {543.015 - config.positionInventory.YPositionOffset}";

            Interface = Interface.Replace("%NAME_PANEL%", namePanel);
            Interface = Interface.Replace("%OFFSET_MIN%", offsetMin);
            Interface = Interface.Replace("%OFFSET_MAX%", offsetMax);

            List<String> newUI = GetOrSetCacheUI(player, isContainer, isOven, Interface);

            newUI.Add(DrawUI_TitlePanel(player, namePanel, isContainer, isOven));

            for (Int32 y = 0, button = isOven ? 1 : 0; button < buttonsCasheInfo.Count; button++, y++)
                newUI.Add(DrawUI_Button(player, isContainer, namePanel, button, y));

            cachedUI = newUI;
            
            foreach (String uiCached in cachedUI)
                CuiHelper.AddUi(player, uiCached);
            
            Oxide.Core.Interface.CallHook("OnOpenedSorted", player, isContainer, isOven);
        }
        
        private String DrawUI_TitlePanel(BasePlayer player, String namePanel, Boolean isContainer, Boolean isOven)
        {
            if (player == null) return null;
            String templateUI = isContainer || isOven ? "UI_TITLE_LOWER" : "UI_TITLE_UPPER";
            String Interface = InterfaceBuilder.GetInterface(templateUI);
            if (Interface == null) return null;

            String titlePanel = isOven ? "UI_TITLE_OVEN" : isContainer ? "UI_TITLE_CONTAINER" : "UI_TITLE_INVENTORY";
            String offsetMin = isContainer ? "-64 -47.102" : "-64 29.038";
            String offsetMax = isContainer ? isOven ? "64 10.615" : "64 -28.365" : "64 47.775";
            
            Interface = Interface.Replace("%NAME_PANEL%", namePanel);
            Interface = Interface.Replace("%TEXT_TITLE%", GetLang(titlePanel, player.UserIDString));
            Interface = Interface.Replace("%OFFSET_MIN%", offsetMin);
            Interface = Interface.Replace("%OFFSET_MAX%", offsetMax);

            return Interface;
        }
        
        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            BasePlayer player = inventory._baseEntity;

            if (player == null)
                return;
            
            CuiHelper.DestroyUi(player, InterfaceBuilder.IQ_HEADER_PLAYER);
            CuiHelper.DestroyUi(player, InterfaceBuilder.IQ_HEADER_CONTAINER);
        }

        
        private List<String> GetOrSetCacheUI(BasePlayer player, Boolean isContainer, Boolean isOven, String interfaceJson = null)
        {
            String langKeyPlayer = lang.GetLanguage(player.UserIDString);
            String keyCache = $"{isOven}_{isContainer}_{langKeyPlayer}";

            if (cachedUI.TryGetValue(keyCache, out List<String> ui))
                return ui;

            if (interfaceJson == null) return null;

            List<String> newUI = new List<String> { interfaceJson };
            cachedUI[keyCache] = newUI;
            return newUI;
        }
                
        
        
        private class ImageUI
        {
            private const String _path = "IQSystem/IQSorter/Images/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
	             { "UI_ICO_PICK", new ImageData() },
	             { "UI_ICO_SIMULAR", new ImageData() },
	             { "UI_ICO_SORTED", new ImageData() },
            };

            private enum ImageStatus
            {
                NotLoaded,
                Loaded,
                Failed
            }

            private class ImageData
            {
                public ImageStatus Status = ImageStatus.NotLoaded;
                public String Id { get; set; }
            }

            public String GetImage(String name)
            {
                if (_images.TryGetValue(name, out ImageData image) && image.Status == ImageStatus.Loaded)
                    return image.Id;
                return null;
            }

            public void DownloadImage()
            {
                KeyValuePair<String, ImageData>? image = null;
                foreach (KeyValuePair<String, ImageData> img in _images)
                {
                    if (img.Value.Status == ImageStatus.NotLoaded)
                    {
                        image = img;
                        break;
                    }
                }

                if (image != null)
                {
                    ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
                }
                else
                {
                    List<String> failedImages = new List<String>();

                    foreach (KeyValuePair<String, ImageData> img in _images)
                    {
                        if (img.Value.Status == ImageStatus.Failed)
                        {
                            failedImages.Add(img.Key);
                        }
                    }

                    if (failedImages.Count > 0)
                    {
                        String images = string.Join(", ", failedImages);
                        _.PrintError(LanguageEn
                            ? $"Failed to load the following images: {images}. Perhaps you did not upload them to the '{_printPath}' folder. You can download it here - https://drive.google.com/drive/folders/1p5aaswjBOjbd8el9gXzh0baI291Ekali?usp=sharing"
                            : $"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'. Скачать можно тут - https://drive.google.com/drive/folders/1p5aaswjBOjbd8el9gXzh0baI291Ekali?usp=sharing");
                        Interface.Oxide.UnloadPlugin(_.Name);
                    }
                    else
                    {
                        _.Puts(LanguageEn
                            ? $"{_images.Count} images downloaded successfully!"
                            : $"{_images.Count} изображений успешно загружено!");
                        
                        _interface = new InterfaceBuilder();
                    }
                }
            }
            
            public void UnloadImages()
            {
                foreach (KeyValuePair<string, ImageData> item in _images)
                    if(item.Value.Status == ImageStatus.Loaded)
                        if (item.Value?.Id != null)
                            FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

                _images?.Clear();
            }

            private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
            {
                String url = "file://" + Interface.Oxide.DataDirectory + "/" + _path + image.Key + ".png";

                using UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
                yield return www.SendWebRequest();

                if (www.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                {
                    image.Value.Status = ImageStatus.Failed;
                }
                else
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(www);
                    image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                    image.Value.Status = ImageStatus.Loaded;
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                DownloadImage();
            }
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        private const String permissionUse = "iqsorter.use";
        private readonly Dictionary<Int32, List<Int32>> itemDlcReplaced = new Dictionary<Int32, List<Int32>>()
        {
            { 1545779598, new List<Int32> { -1335497659, -139037392 } },
            { -1335497659, new List<Int32> { 1545779598, -139037392 } }, 
            { -139037392, new List<Int32> { 1545779598, -1335497659 } },  
            { -1315992997, new List<Int32> { 442886268 } },  
            { 442886268, new List<Int32> { -1315992997 } },
            { 857465230, new List<Int32> { -75401165 } },
            { -194953424, new List<Int32> { 110116923 } },  
            { 110116923, new List<Int32> { -194953424 } }, 
            { 1110385766, new List<Int32> { -1478855279 } },
            { -1478855279, new List<Int32> { 1110385766 } }, 
        };
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Setting up the shift of the UI position in the storage container" : "Настройка сдвига позиции UI в хранилищах")]
            public PositionCorrected positionStorageContainer = new PositionCorrected();

            internal class PositionCorrected
            {
                [JsonProperty(LanguageEn ? "Left offset (-N), Right offset (N)" : "Сдвиг влево (-N), сдвиг вправо (N)")]
                public Single XPositionOffset;
                [JsonProperty(LanguageEn ? "Up offset (-N), Down offset (N)" : "Сдвиг вверх (-N), сдвиг вниз (N)")]
                public Single YPositionOffset;
            }
            [JsonProperty(LanguageEn ? "Setting up the shift of the UI position in the oven" : "Настройка сдвига позиции UI в печах")]
            public PositionCorrected positionOven = new PositionCorrected();
            [JsonProperty(LanguageEn ? "Setting up the shift of the UI position in the inventory" : "Настройка сдвига позиции UI в инвентаре")]
            public PositionCorrected positionInventory = new PositionCorrected();
            
            public static Configuration GetNewConfiguration() 
            {
                return new Configuration
                {
                    buildingAuth = false,
                    positionInventory = new PositionCorrected
                    {
                        XPositionOffset = 0.0f,
                        YPositionOffset = 0.0f,
                    },
                    positionStorageContainer = new PositionCorrected
                    {
                        XPositionOffset = 0.0f,
                        YPositionOffset = 0.0f,
                    },
                    positionOven = new PositionCorrected
                    {
                        XPositionOffset = 0.0f,
                        YPositionOffset = 0.0f,
                    },
                    categoryOrder = new List<String>{ "Weapon", "Attire", "Tool", "Ammunition", "Construction", "Items", "Resources", "Component", "Medical", "Food", "Traps", "Electrical", "Mics", "Fun" },
                };
            }
            [JsonProperty(LanguageEn ? "Setting up the sorting order" : "Настройка порядка сортировки")]
            public List<String> categoryOrder;
            [JsonProperty(LanguageEn ? "A building permit is required to use the function" : "Требуется разрешение на строительство для использования функций")]
            public Boolean buildingAuth;
        }
        
                
        private static InterfaceBuilder _interface;

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_SORTED_BUTTON"] = "SORT",
                ["UI_PUT_ALL"] = "PUT ALL ITEMS",
                ["UI_PUT_SIMILAR"] = "PUT SIMILAR ITEMS",
                ["UI_TAKE_SIMILAR"] = "TAKE SIMILAR ITEMS",
                ["UI_TAKE_ALL"] = "TAKE ALL ITEMS",
                ["UI_TITLE_INVENTORY"] = "INVENTORY FUNCT",
                ["UI_TITLE_CONTAINER"] = "STORAGE FUNCT",
                ["UI_TITLE_OVEN"] = "OVEN FUNCT",
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_SORTED_BUTTON"] = "СОРТИРОВАТЬ",
                ["UI_PUT_ALL"] = "ПОЛОЖИТЬ ВСЕ",
                ["UI_PUT_SIMILAR"] = "ПОЛОЖИТЬ ПОХОЖИЕ",
                ["UI_TAKE_SIMILAR"] = "ЗАБРАТЬ ПОХОЖИЕ",
                ["UI_TAKE_ALL"] = "ЗАБРАТЬ ВСЕ",
                ["UI_TITLE_INVENTORY"] = "ФУНКЦИИ ИНВЕНТАРЯ",
                ["UI_TITLE_CONTAINER"] = "ФУНКЦИИ ХРАНИЛИЩА",
                ["UI_TITLE_OVEN"] = "ФУНКЦИИ ПЕЧИ",
            }, this, "ru");
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permissionUse,this);

            _imageUI = new ImageUI();
            _imageUI.DownloadImage();
        }

            }
}
