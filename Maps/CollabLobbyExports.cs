using MonoMod.ModInterop;

namespace Celeste.Mods.CollabUtils2.Maps
{
    [ModExportName("CollabUtils2.LobbyHelper")]
    public sealed class CollabLobbyExports
    {
        private readonly CollabIdentityRegistry identities;

        public CollabLobbyExports(CollabIdentityRegistry identities)
        {
            this.identities = identities;
        }

        public bool IsCollabLevelSet(string levelSet) => identities.IsCollabLevelSet(levelSet);

        public bool IsCollabLobby(string sid) => identities.IsCollabLobby(sid);
    }
}
