using Celeste.Everest.Modules.Persistence;

namespace Celeste.Mods.CollabUtils2.Persistence
{
    public class CollabSession : EverestModuleSession
    {
        public string LobbySID { get; set; } = null;
        public string LobbyRoom { get; set; } = null;
        public float LobbySpawnPointX { get; set; } = 0;
        public float LobbySpawnPointY { get; set; } = 0;
        public string GymExitMapSID { get; set; } = null;
        public bool GymExitSaveAllowed { get; set; } = false;
        public bool SaveAndReturnToLobbyAllowed { get; set; } = false;
    }
}
