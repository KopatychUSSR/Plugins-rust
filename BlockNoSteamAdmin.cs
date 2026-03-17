using Network;
using System;

namespace Oxide.Plugins
{
	[Info("Block NoSteam Admin", "rostov114", "0.1.0")]
	class BlockNoSteamAdmin : RustPlugin
	{
		private object CanClientLogin(Network.Connection connection)
		{
			if (connection.authLevel > 0)
			{
				if (connection.token.Length == 0x000000EA || connection.token.Length == 0x000000F0)
				{
					uint offsetUnsignedInteger = BitConverter.ToUInt32(connection.token, 0x00000048);
					if (offsetUnsignedInteger == 0x000001E0)
					{
						return "Steam Auth Failed";
					}
				}
			}

			return null;
		}
	}
}