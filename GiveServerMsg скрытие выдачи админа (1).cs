namespace Oxide.Plugins
{
    [Info("GiveServerMsg", "MR.BUFF", "0.1", ResourceId = 2336)]
    [Description("Hide server give message")]

    class GiveServerMsg : RustPlugin
    {
        object OnServerMessage(string m, string n) => m.Contains("gave") && n == "SERVER" ? (object)true : null;
    }
}
