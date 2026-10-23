using Celeste.Mods.CollabUtils2.UI.LobbyMaps;

namespace Celeste.Mods.CollabUtils2.Modules
{
    public sealed partial class CollabModule
    {
        private CollabLobbyMapLifecycle lobbyMapLifecycle;

        private void LoadLobbyMaps()
        {
            lobbyMapLifecycle = new CollabLobbyMapLifecycle(SourceEngine);
            lobbyMapLifecycle.Load();
        }

        private void UnloadLobbyMaps()
        {
            lobbyMapLifecycle?.Dispose();
            lobbyMapLifecycle = null;
        }

        public override void LoadContent(bool firstLoad)
        {
            CollabHeartSpriteRegistry.For(SourceEngine).Reload();
        }
    }
}
