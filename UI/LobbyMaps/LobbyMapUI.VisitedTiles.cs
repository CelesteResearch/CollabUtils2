using Celeste.Mods.CollabUtils2.Maps.LobbyMaps;

namespace Celeste.Mods.CollabUtils2.UI.LobbyMaps
{
    public partial class LobbyMapUI
    {
        internal static LobbyVisitedByteGrid GetVisitedTiles(LobbyMapControllerInfo config, LobbyVisitManager manager)
            => generateVisitedTiles(config, manager);
    }
}
