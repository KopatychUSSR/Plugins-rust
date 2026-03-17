namespace Oxide.Plugins
{
    [Info("CardPermission","Baks","1.0")]
    public class CardPermission : RustPlugin
    {
        #region Variables

        private string perm = "cardpermission.on";
        

        #endregion

        #region Function

        void OnServerInitialized()
        {
            permission.RegisterPermission(perm,this);
        }
        
        void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString,perm))
            return;
            int a = 0;
            a = cardReader.accessLevel;
            cardReader.accessLevel = card.accessLevel;
            timer.Once(1, () =>
            {
               cardReader.accessLevel = a;
            } );
                
                

            
        }

        #endregion
    }
}