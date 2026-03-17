using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System.Reflection;
using System;
using System.IO;

namespace Oxide.Plugins
{
    [Info("Мгновенное Возрождение", "RustExpert", "1.0.5")]
    [Description("Мгновенное возрождение на спальниках с привилегией без кулдауна")]
    public class InstantRespawn : RustPlugin
    {
        #region Конфигурация
        
        private ConfigData config;
        private FieldInfo unlockTimeField;
        private string LogFilePath => $"data/{Name}_logs.json";
        private LogData logData = new LogData();
        private Dictionary<ulong, KillData> killCooldowns = new Dictionary<ulong, KillData>();
        
        private class ConfigData
        {
            public string Permission = "instantrespawn.use";
            public string PermissionUnlimitedBags = "instantrespawn.unlimited";
            public string PermissionBypassKillCooldown = "instantrespawn.bypasskillcd";
            public int MaxBagsDefault = 5;
            public bool LogToConsole = true;
            public bool LogToFile = true;
            public int MaxLogEntries = 1000;
            public bool EnableKillCooldown = true;
            public float KillCooldownSeconds = 300f;
            public int MaxKillViolations = 3;
            public float ViolationResetHours = 24f;
            public bool BanForKillAbuse = false;
            public float BanDurationHours = 24f;
            public List<ulong> ExcludedBagOwners = new List<ulong>();
        }
        
        private class KillData
        {
            public float LastKillTime { get; set; }
            public int ViolationCount { get; set; }
            public float LastViolationTime { get; set; }
            
            public KillData()
            {
                LastKillTime = 0f;
                ViolationCount = 0;
                LastViolationTime = 0f;
            }
        }
        
        private class LogData
        {
            public List<LogEntry> Entries { get; set; } = new List<LogEntry>();
        }
        
        private class LogEntry
        {
            public string Type { get; set; }
            public ulong UserId { get; set; }
            public string PlayerName { get; set; }
            public string Message { get; set; }
            public string Timestamp { get; set; }
            public string BagType { get; set; }
            public Vector3 Position { get; set; }
            
            public LogEntry(string type, BasePlayer player, string message, string bagType = "", Vector3 position = new Vector3())
            {
                Type = type;
                UserId = player.userID;
                PlayerName = player.displayName;
                Message = message;
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                BagType = bagType;
                Position = position;
            }
        }
        
        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
            PrintWarning("Создана конфигурация по умолчанию");
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) throw new System.Exception("Не удалось загрузить конфигурацию");
                SaveConfig();
            }
            catch
            {
                PrintWarning("Ошибка загрузки конфигурации! Создана новая конфигурация");
                LoadDefaultConfig();
            }
        }
        
        protected override void SaveConfig() => Config.WriteObject(config);
        
        #endregion
        
        #region Локализация
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermission", "У вас нет прав на использование этой функции!"},
                {"BagLimitReached", "Вы достигли лимита спальников ({0})!"},
                {"BagAdded", "Спальник успешно размещен! ({0}/{1})"},
                {"BagAddedUnlimited", "Спальник успешно размещен! (Без ограничений)"},
                {"BagRemoved", "Спальник удален! ({0}/{1})"},
                {"BagRemovedUnlimited", "Спальник удален! (Без ограничений)"},
                {"CooldownReset", "Кулдаун на ваших спальниках сброшен!"},
                {"KillCooldown", "Вам нужно подождать {0} секунд, прежде чем снова использовать команду самоубийства!"},
                {"KillViolation", "Предупреждение! Частое использование самоубийства. {0}/{1} предупреждений!"},
                {"KillBanned", "Вы заблокированы за злоупотребление командой самоубийства на {0} часов!"}
            }, this);
        }
        
        private string GetMessage(string key, string playerId = null) => 
            lang.GetMessage(key, this, playerId);
        
        #endregion
        
        #region Логирование
        
        // Загрузка лог-файла
        private void LoadLogs()
        {
            if (!config.LogToFile) return;
            
            try
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(LogFilePath))
                {
                    logData = Interface.Oxide.DataFileSystem.ReadObject<LogData>(LogFilePath);
                    PrintWarning($"Загружено {logData.Entries.Count} записей логов");
                }
                else
                {
                    logData = new LogData();
                    SaveLogs();
                }
            }
            catch (Exception ex)
            {
                PrintError($"Ошибка при загрузке логов: {ex.Message}");
                logData = new LogData();
            }
        }
        
        // Сохранение лог-файла
        private void SaveLogs()
        {
            if (!config.LogToFile) return;
            
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(LogFilePath, logData);
            }
            catch (Exception ex)
            {
                PrintError($"Ошибка при сохранении логов: {ex.Message}");
            }
        }
        
        // Добавление записи в лог
        private void AddLog(string type, BasePlayer player, string message, string bagType = "", Vector3 position = new Vector3())
        {
            if (!config.LogToFile || player == null) return;
            
            if (config.LogToConsole)
            {
                Puts($"[Лог] {player.displayName} ({player.UserIDString}): {message}");
            }
            
            logData.Entries.Add(new LogEntry(type, player, message, bagType, position));
            
            // Ограничение количества записей
            if (logData.Entries.Count > config.MaxLogEntries)
            {
                logData.Entries.RemoveRange(0, logData.Entries.Count - config.MaxLogEntries);
            }
            
            // Сохраняем логи каждые 10 записей для оптимизации
            if (logData.Entries.Count % 10 == 0)
            {
                SaveLogs();
            }
        }
        
        #endregion
        
        #region Oхуки
        
        private void Init()
        {
            LoadConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(config.Permission, this);
            permission.RegisterPermission(config.PermissionUnlimitedBags, this);
            permission.RegisterPermission(config.PermissionBypassKillCooldown, this);
            
            // Получение доступа к приватному полю unlockTime для прямого изменения
            unlockTimeField = typeof(SleepingBag).GetField("unlockTime", 
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                
            if (unlockTimeField == null)
            {
                PrintError("Не удалось получить доступ к полю unlockTime! Плагин не будет работать корректно.");
            }
            else
            {
                PrintWarning("Плагин успешно инициализирован!");
            }
            
            // Загружаем логи
            LoadLogs();
        }
        
        private void OnServerSave()
        {
            // Сохраняем логи при сохранении сервера
            if (config.LogToFile)
            {
                SaveLogs();
            }
            
            // Сбрасываем устаревшие данные о нарушениях
            float currentTime = Time.realtimeSinceStartup;
            
            List<ulong> keysToReset = new List<ulong>();
            
            foreach (var kvp in killCooldowns)
            {
                if (kvp.Value.LastViolationTime > 0f && (currentTime - kvp.Value.LastViolationTime) / 3600f >= config.ViolationResetHours)
                {
                    keysToReset.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToReset)
            {
                if (killCooldowns.ContainsKey(key))
                {
                    killCooldowns[key].ViolationCount = 0;
                    Puts($"Сброшен счетчик нарушений для игрока {key}");
                }
            }
        }
        
        private void Unload()
        {
            // Сохраняем логи при выгрузке плагина
            if (config.LogToFile)
            {
                SaveLogs();
            }
        }
        
        private void OnServerInitialized()
        {
            // Сбрасываем кулдаун при запуске сервера для всех онлайн игроков
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, config.Permission))
                {
                    ResetBagsCooldown(player);
                    AddLog("server_init", player, "Сброс кулдауна при старте сервера");
                }
            }
        }
        
        // Обработка создания спального мешка
        private object CanDeploySleepingBag(SleepingBag bag, BasePlayer player)
        {
            if (player == null || bag == null) return null;
            
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                return null;
                
            // Проверка лимита спальников для игрока, если у него нет привилегии на безлимитные спальники
            if (!permission.UserHasPermission(player.UserIDString, config.PermissionUnlimitedBags))
            {
                int currentBags = GetPlayerBagsCount(player.userID);
                if (currentBags >= config.MaxBagsDefault)
                {
                    player.ChatMessage(string.Format(GetMessage("BagLimitReached", player.UserIDString), config.MaxBagsDefault));
                    AddLog("limit_reached", player, $"Достигнут лимит спальников ({config.MaxBagsDefault})");
                    return false;
                }
                
                player.ChatMessage(string.Format(GetMessage("BagAdded", player.UserIDString), currentBags + 1, config.MaxBagsDefault));
            }
            else
            {
                player.ChatMessage(GetMessage("BagAddedUnlimited", player.UserIDString));
            }
            
            return null;
        }
        
        // Сброс кулдауна после размещения спального мешка
        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            if (gameObject == null) return;
            
            var bag = gameObject.GetComponent<SleepingBag>();
            if (bag == null) return;
            
            var player = plan.GetOwnerPlayer();
            if (player == null) return;
            
            if (permission.UserHasPermission(player.UserIDString, config.Permission))
            {
                timer.Once(0.5f, () => 
                {
                    ResetBagsCooldown(player);
                    AddLog("bag_placed", player, "Размещен новый спальник", bag.ShortPrefabName, bag.transform.position);
                });
            }
        }
        
        // Убирает кулдаун с спальников игрока при смерти
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                return;
                
            if (config.LogToConsole)
                Puts($"Игрок {player.displayName} умер, сбрасываем кулдаун спальников");
                
            ResetBagsCooldown(player);
            
            // Логируем, от чего умер игрок
            string deathCause = info != null ? (info.Initiator != null ? info.Initiator.ToString() : "Неизвестно") : "Неизвестно";
            AddLog("player_death", player, $"Смерть игрока. Причина: {deathCause}");
        }
        
        // Обработка использования спального мешка
        private void OnPlayerRespawn(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                return;
                
            // Сбрасываем кулдаун на всех спальниках
            timer.Once(0.1f, () => 
            {
                ResetBagsCooldown(player);
                
                // Получаем тип спального мешка, если возможно
                string bagType = "Неизвестно";
                var sleepingBag = entity as SleepingBag;
                if (sleepingBag != null)
                {
                    bagType = sleepingBag.ShortPrefabName;
                }
                
                AddLog("player_respawn", player, $"Возрождение на спальнике типа: {bagType}", bagType, entity.transform.position);
            });
            
            if (config.LogToConsole)
                Puts($"Игрок {player.displayName} возродился, сбрасываем кулдаун спальников");
        }
        
        // Сброс времени возрождения при удалении спальника
        private void OnEntityKill(BaseEntity entity)
        {
            var bag = entity as SleepingBag;
            if (bag == null) return;
            
            BasePlayer owner = BasePlayer.FindByID(bag.OwnerID);
            if (owner == null) return;
            
            if (!permission.UserHasPermission(owner.UserIDString, config.Permission))
                return;
                
            if (!permission.UserHasPermission(owner.UserIDString, config.PermissionUnlimitedBags))
            {
                int currentBags = GetPlayerBagsCount(owner.userID) - 1;
                owner.ChatMessage(string.Format(GetMessage("BagRemoved", owner.UserIDString), currentBags, config.MaxBagsDefault));
            }
            else
            {
                owner.ChatMessage(GetMessage("BagRemovedUnlimited", owner.UserIDString));
            }
            
            // Сбрасываем кулдаун на оставшихся спальниках
            timer.Once(0.5f, () => ResetBagsCooldown(owner));
        }
        
        // Сбрасывает кулдаун на всех спальниках при подключении
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                return;
                
            ResetBagsCooldown(player);
        }
        
        // Обрабатываем события сервера при изменении назначенной кровати
        private void OnBedAssign(SleepingBag bag, BasePlayer player)
        {
            if (player == null || bag == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                return;
                
            timer.Once(0.5f, () => ResetBagsCooldown(player));
        }
        
        // Обрабатываем запрос на возрождение 
        private void OnPlayerRespawning(BasePlayer player)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
                return;
            
            // Сбрасываем кулдаун для всех спальников игрока
            SleepingBag[] bags = SleepingBag.FindForPlayer(player.userID, true);
            
            AddLog("respawning", player, $"Подготовка к возрождению. Найдено спальников: {bags.Length}");
            
            foreach (var bag in bags)
            {
                if (bag == null) continue;
                
                if (config.ExcludedBagOwners.Contains(bag.OwnerID))
                    continue;
                
                // Прямая установка времени кулдауна на 0
                if (unlockTimeField != null)
                {
                    unlockTimeField.SetValue(bag, 0f);
                }
                
                bag.secondsBetweenReuses = 0f;
                bag.SendNetworkUpdate();
                
                if (config.LogToConsole)
                    Puts($"Сброшен кулдаун для спальника {bag.ShortPrefabName} игрока {player.displayName} перед возрождением");
            }
        }
        
        // Обработка команды самоубийства (kill) и консольных команд
        private object OnPlayerSuicide(BasePlayer player)
        {
            if (player == null || !config.EnableKillCooldown) return null;
            
            // Пропускаем проверку, если у игрока есть право обхода кулдауна
            if (permission.UserHasPermission(player.UserIDString, config.PermissionBypassKillCooldown))
                return null;
                
            // Получаем или создаем данные о кулдауне для игрока
            KillData killData;
            if (!killCooldowns.TryGetValue(player.userID, out killData))
            {
                killData = new KillData();
                killCooldowns.Add(player.userID, killData);
            }
            
            // Проверяем, прошло ли достаточно времени с момента последнего использования
            float currentTime = Time.realtimeSinceStartup;
            float timeSinceLastKill = currentTime - killData.LastKillTime;
            
            // Если прошло меньше заданного времени кулдауна, отменяем самоубийство
            if (killData.LastKillTime > 0f && timeSinceLastKill < config.KillCooldownSeconds)
            {
                float remainingTime = config.KillCooldownSeconds - timeSinceLastKill;
                player.ChatMessage(string.Format(GetMessage("KillCooldown", player.UserIDString), Math.Round(remainingTime)));
                
                // Увеличиваем счетчик нарушений
                killData.ViolationCount++;
                killData.LastViolationTime = currentTime;
                
                // Логируем нарушение
                AddLog("kill_violation", player, $"Попытка обойти кулдаун самоубийства. Осталось {Math.Round(remainingTime)} секунд. Нарушение #{killData.ViolationCount}");
                
                // Предупреждаем игрока о возможных последствиях
                if (killData.ViolationCount >= 1)
                {
                    player.ChatMessage(string.Format(GetMessage("KillViolation", player.UserIDString), killData.ViolationCount, config.MaxKillViolations));
                }
                
                // Банаем за чрезмерное злоупотребление, если включено
                if (config.BanForKillAbuse && killData.ViolationCount >= config.MaxKillViolations)
                {
                    // Конвертируем часы в секунды для бана
                    float banDuration = config.BanDurationHours * 3600f;
                    
                    // Отправляем сообщение игроку перед баном
                    player.ChatMessage(string.Format(GetMessage("KillBanned", player.UserIDString), config.BanDurationHours));
                    
                    // Логируем бан
                    AddLog("kill_banned", player, $"Заблокирован за злоупотребление командой самоубийства на {config.BanDurationHours} часов");
                    
                    // Баним игрока на указанное время
                    timer.Once(0.5f, () => 
                    {
                        Server.Command($"banid {player.UserIDString} {banDuration} \"Автоматический бан за дюп лута через самоубийство\"");
                    });
                }
                
                // Отменяем самоубийство
                return false;
            }
            
            // Обновляем время последнего самоубийства
            killData.LastKillTime = currentTime;
            
            // Сбрасываем нарушения, если с момента последнего нарушения прошло много времени
            if (killData.LastViolationTime > 0f && (currentTime - killData.LastViolationTime) / 3600f >= config.ViolationResetHours)
            {
                killData.ViolationCount = 0;
            }
            
            // Логируем обычное самоубийство
            AddLog("player_suicide", player, "Используется команда самоубийства");
            
            // Не блокируем самоубийство
            return null;
        }
        
        // Хук для блокировки консольных вариантов команды kill
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            // Проверяем, что команда выполняется игроком и это команда kill или suicide
            if (arg.Player() != null && (arg.cmd.FullName.Equals("kill") || arg.cmd.FullName.Equals("suicide") || arg.cmd.FullName.Equals("client.endloot")))
            {
                // Используем тот же механизм проверки, что и для обычного самоубийства
                return OnPlayerSuicide(arg.Player());
            }
            
            return null;
        }
        
        #endregion
        
        #region Методы
        
        // Получение количества спальников игрока
        private int GetPlayerBagsCount(ulong playerId)
        {
            SleepingBag[] bags = SleepingBag.FindForPlayer(playerId, true);
            return bags.Length;
        }
        
        // Сброс таймера возрождения на всех спальниках
        private void ResetBagsCooldown(BasePlayer player)
        {
            if (unlockTimeField == null) return;
            
            SleepingBag[] bags = SleepingBag.FindForPlayer(player.userID, true);
            
            if (bags.Length == 0)
            {
                if (config.LogToConsole)
                    Puts($"У игрока {player.displayName} нет спальников");
                    
                AddLog("no_bags", player, "У игрока нет спальников");
                return;
            }
            
            int resetCount = 0;
            
            foreach (SleepingBag bag in bags)
            {
                if (bag == null) continue;
                
                if (config.ExcludedBagOwners.Contains(bag.OwnerID))
                    continue;
                
                // Напрямую устанавливаем значение приватного поля unlockTime
                unlockTimeField.SetValue(bag, 0f);
                
                // Устанавливаем время между повторными использованиями на 0
                bag.secondsBetweenReuses = 0f;
                
                // Отправляем обновление клиенту
                bag.SendNetworkUpdate();
                
                resetCount++;
                
                if (config.LogToConsole)
                    Puts($"Сброшен кулдаун на спальнике {bag.ShortPrefabName} для игрока {player.displayName}");
            }
            
            AddLog("reset_cooldown", player, $"Сброшен кулдаун на {resetCount} спальниках");
        }
        
        #endregion
        
        #region Команды
        
        [ChatCommand("resetbags")]
        private void CmdResetBags(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
            {
                player.ChatMessage(GetMessage("NoPermission", player.UserIDString));
                AddLog("permission_denied", player, "Попытка сброса кулдауна без прав");
                return;
            }
            
            ResetBagsCooldown(player);
            player.ChatMessage(GetMessage("CooldownReset", player.UserIDString));
            AddLog("command_resetbags", player, "Ручной сброс кулдауна спальников через команду");
        }
        
        [ConsoleCommand("instantrespawn.viewlogs")]
        private void CmdViewLogs(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, "instantrespawn.admin"))
            {
                SendReply(arg, "У вас нет прав на просмотр логов");
                return;
            }
            
            if (!config.LogToFile || logData.Entries.Count == 0)
            {
                SendReply(arg, "Логи отсутствуют или отключены");
                return;
            }
            
            // Выбор количества отображаемых записей
            int count = 10;
            if (arg.HasArgs(1))
            {
                int.TryParse(arg.Args[0], out count);
                if (count <= 0) count = 10;
            }
            
            // Вывод последних X записей
            int start = Math.Max(0, logData.Entries.Count - count);
            SendReply(arg, $"Последние {Math.Min(count, logData.Entries.Count)} записей из {logData.Entries.Count}:");
            
            for (int i = start; i < logData.Entries.Count; i++)
            {
                var entry = logData.Entries[i];
                SendReply(arg, $"[{entry.Timestamp}] {entry.PlayerName} ({entry.UserId}): {entry.Type} - {entry.Message}");
            }
        }
        
        [ConsoleCommand("instantrespawn.clearlogs")]
        private void CmdClearLogs(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, "instantrespawn.admin"))
            {
                SendReply(arg, "У вас нет прав на очистку логов");
                return;
            }
            
            if (!config.LogToFile)
            {
                SendReply(arg, "Логирование в файл отключено");
                return;
            }
            
            int oldCount = logData.Entries.Count;
            logData.Entries.Clear();
            SaveLogs();
            
            SendReply(arg, $"Логи очищены. Удалено {oldCount} записей.");
        }
        
        [ConsoleCommand("instantrespawn.resetviolations")]
        private void CmdResetViolations(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, "instantrespawn.admin"))
            {
                SendReply(arg, "У вас нет прав на сброс нарушений");
                return;
            }
            
            if (!config.EnableKillCooldown)
            {
                SendReply(arg, "Функция контроля самоубийств отключена");
                return;
            }
            
            // Сброс для конкретного игрока
            if (arg.HasArgs(1))
            {
                string userIdOrName = arg.Args[0];
                BasePlayer player = null;
                
                // Проверяем, является ли аргумент SteamID
                ulong userId;
                if (ulong.TryParse(userIdOrName, out userId))
                {
                    player = BasePlayer.FindByID(userId);
                }
                else
                {
                    // Ищем по имени
                    player = BasePlayer.Find(userIdOrName);
                }
                
                if (player != null)
                {
                    if (killCooldowns.ContainsKey(player.userID))
                    {
                        killCooldowns[player.userID].ViolationCount = 0;
                        killCooldowns[player.userID].LastViolationTime = 0f;
                        SendReply(arg, $"Сброшены нарушения для игрока {player.displayName} ({player.UserIDString})");
                        
                        AddLog("reset_violations", player, "Администратор сбросил счетчик нарушений самоубийства");
                    }
                    else
                    {
                        SendReply(arg, "У игрока нет зарегистрированных нарушений");
                    }
                }
                else
                {
                    SendReply(arg, "Игрок не найден");
                }
                return;
            }
            
            // Сброс для всех игроков
            int count = killCooldowns.Count;
            killCooldowns.Clear();
            SendReply(arg, $"Сброшены нарушения для всех игроков (всего: {count})");
        }
        
        [ConsoleCommand("instantrespawn.killstatus")]
        private void CmdKillStatus(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, "instantrespawn.admin"))
            {
                SendReply(arg, "У вас нет прав на просмотр статуса кулдауна самоубийства");
                return;
            }
            
            if (!config.EnableKillCooldown)
            {
                SendReply(arg, "Функция контроля самоубийств отключена");
                return;
            }
            
            // Статус для конкретного игрока
            if (arg.HasArgs(1))
            {
                string userIdOrName = arg.Args[0];
                BasePlayer player = null;
                
                // Проверяем, является ли аргумент SteamID
                ulong userId;
                if (ulong.TryParse(userIdOrName, out userId))
                {
                    player = BasePlayer.FindByID(userId);
                }
                else
                {
                    // Ищем по имени
                    player = BasePlayer.Find(userIdOrName);
                }
                
                if (player != null)
                {
                    KillData killData;
                    if (killCooldowns.TryGetValue(player.userID, out killData))
                    {
                        float currentTime = Time.realtimeSinceStartup;
                        float timeSinceLastKill = currentTime - killData.LastKillTime;
                        float remainingCooldown = Math.Max(0, config.KillCooldownSeconds - timeSinceLastKill);
                        
                        SendReply(arg, $"Игрок: {player.displayName} ({player.UserIDString})");
                        SendReply(arg, $"Нарушений: {killData.ViolationCount}/{config.MaxKillViolations}");
                        SendReply(arg, $"Оставшееся время кулдауна: {Math.Round(remainingCooldown)} сек");
                        
                        if (killData.LastViolationTime > 0)
                        {
                            float hoursSinceViolation = (currentTime - killData.LastViolationTime) / 3600f;
                            float resetHoursLeft = Math.Max(0, config.ViolationResetHours - hoursSinceViolation);
                            SendReply(arg, $"Сброс нарушений через: {Math.Round(resetHoursLeft, 1)} ч");
                        }
                    }
                    else
                    {
                        SendReply(arg, "У игрока нет данных о самоубийствах");
                    }
                }
                else
                {
                    SendReply(arg, "Игрок не найден");
                }
                return;
            }
            
            // Общая статистика
            SendReply(arg, $"Всего игроков с данными о самоубийствах: {killCooldowns.Count}");
            SendReply(arg, $"Кулдаун самоубийства: {config.KillCooldownSeconds} сек");
            SendReply(arg, $"Максимум нарушений до бана: {config.MaxKillViolations}");
            
            // Игроки с высоким уровнем нарушений
            List<KeyValuePair<ulong, KillData>> highViolators = new List<KeyValuePair<ulong, KillData>>();
            
            foreach (var kvp in killCooldowns)
            {
                if (kvp.Value.ViolationCount > 0)
                {
                    highViolators.Add(kvp);
                }
            }
            
            // Сортируем по количеству нарушений
            highViolators.Sort((a, b) => b.Value.ViolationCount.CompareTo(a.Value.ViolationCount));
            
            // Показываем топ нарушителей (максимум 5)
            if (highViolators.Count > 0)
            {
                SendReply(arg, "Топ нарушителей:");
                int count = Math.Min(5, highViolators.Count);
                
                for (int i = 0; i < count; i++)
                {
                    var violator = highViolators[i];
                    BasePlayer player = BasePlayer.FindByID(violator.Key);
                    string playerName = player != null ? player.displayName : "Оффлайн";
                    
                    SendReply(arg, $"{i+1}. {playerName} ({violator.Key}): {violator.Value.ViolationCount} нарушений");
                }
            }
        }
        
        #endregion
    }
} 