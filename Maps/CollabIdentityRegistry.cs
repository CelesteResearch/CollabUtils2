using System.Collections.Generic;
using System.Linq;
using Celeste.Game.Execution.Areas;

namespace Celeste.Mods.CollabUtils2.Maps
{
    public sealed class CollabIdentityRegistry
    {
        private readonly HashSet<string> names = new HashSet<string>();
        private readonly IOriginalAreaKeyPolicy areas;

        public CollabIdentityRegistry(IOriginalAreaKeyPolicy areas)
        {
            this.areas = areas;
        }

        public void Register(string fileContents) => names.Add(fileContents.Trim());
        public void Clear() => names.Clear();

        public string GetCollabNameForSID(string sid)
        {
            return names.FirstOrDefault(name => sid.StartsWith(name + "/"));
        }

        public string GetLobbyLevelSet(string sid)
        {
            string collab = names.FirstOrDefault(name => sid.StartsWith(name + "/0-Lobbies/")
                                                         && sid != name + "/0-Lobbies/0-Prologue");
            return collab == null ? null : collab + "/" + sid.Substring((collab + "/0-Lobbies/").Length);
        }

        public bool IsCollabLevelSet(string levelSet)
        {
            return names.Any(name => levelSet.StartsWith(name + "/") && levelSet != name + "/0-Lobbies");
        }

        public string GetLobbyForLevelSet(string levelSet)
        {
            string collab = GetCollabNameForSID(levelSet);
            if (collab == null) return null;
            string expectedLobby = collab + "/0-Lobbies/" + levelSet.Substring((collab + "/").Length);
            return areas.GetAreaBySID(expectedLobby) == null ? null : expectedLobby;
        }

        public string GetLobbyForMap(string sid)
        {
            var area = areas.GetAreaBySID(sid);
            return area == null ? null : GetLobbyForLevelSet(area.LevelSet);
        }

        public bool IsCollabMap(string sid) => GetLobbyForMap(sid) != null;
        public bool IsCollabLobby(string sid) => GetLobbyLevelSet(sid) != null;

        public string GetLobbyForGym(string gymSID)
        {
            if (!names.Any(name => gymSID.StartsWith(name + "/0-Gyms/"))) return null;
            string expectedLobby = gymSID.Replace("/0-Gyms/", "/0-Lobbies/");
            return areas.GetAreaBySID(expectedLobby) == null ? null : expectedLobby;
        }

        public bool IsHeartSide(string sid)
        {
            return GetCollabNameForSID(sid) != null && sid.EndsWith("/ZZ-HeartSide");
        }
    }
}
