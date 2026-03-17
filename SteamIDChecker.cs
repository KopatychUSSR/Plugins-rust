using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("SteamIDChecker", "Mercury", "0.0.1")]
    [Description("SteamIDChecker")]
    class SteamIDChecker : RustPlugin
    {
        object CanUserLogin(string name, string id) => ItsBot(ulong.Parse(id)) ? "BOT_DETECTED" : null;
        bool ItsBot(ulong ID)
        {
            if (ID > 86560000000000000 || ID < 76560000000000000 || !ID.IsSteamId())
            {
                BasePlayer player = BasePlayer.FindByID(ID);
                if (player == null) return true;
                if (player.IsAlive())
                    player.Hurt(1000f, Rust.DamageType.Cold);
                return true;
            }
            else return false;
        }
    }
}
