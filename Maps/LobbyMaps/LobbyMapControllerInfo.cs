using System;
using System.Linq;
using System.Numerics;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Modules;

namespace Celeste.Mods.CollabUtils2.Maps.LobbyMaps
{
    public class LobbyMapControllerInfo
    {
        private static readonly char[] commaSeparator = { ',' };

        #region EntityData Fields

        /// <summary>
        /// The map background texture to read from <see cref="GFX.Gui"/>.
        /// Example: SJ2021/1-Beginner/beginnermap
        /// </summary>
        public string MapTexture;

        /// <summary>
        /// The total number of maps in the lobby, used to display the miniheart tally.
        /// </summary>
        public int TotalMaps;

        /// <summary>
        /// The index to use when sorting rooms within a single map.  Defaults to 0.
        /// If there is more than one room in a map with the same index, they will be sorted by room name.
        /// </summary>
        public int RoomIndex;

        /// <summary>
        /// An array of custom entity names that should be considered <see cref="LobbyMapMarkerType.Map"/> markers.
        /// These entities must have a "map" attribute containing the SID of the target map.
        /// </summary>
        public string[] CustomMarkers;

        public string WarpIcon;
        public string RainbowBerryIcon;
        public string HeartGateIcon;
        public string GymIcon;
        public string MapIcon;
        public string JournalIcon;
        public string HeartSideIcon;

        public bool ShowWarps;
        public bool ShowRainbowBerry;
        public bool ShowHeartGate;
        public bool ShowGyms;
        public bool ShowMaps;
        public bool ShowJournals;
        public bool ShowHeartSide;
        public bool ShowHeartCount;

        public bool RevealWhenAllMarkersFound;

        #endregion

        public int RoomWidth;
        public int RoomHeight;
        public string LevelSet;

        public LobbyMapControllerInfo(CollabModule module, EntityData data, OriginalChapterMapData mapData = null)
        {
            MapTexture = data.Attr("mapTexture");
            TotalMaps = data.Int("totalMaps");
            RoomIndex = data.Int("roomIndex", 0);

            WarpIcon = data.Attr("warpIcon", "CollabUtils2/lobbies/warp");
            RainbowBerryIcon = data.Attr("rainbowBerryIcon", "CollabUtils2/lobbies/rainbowBerry");
            HeartGateIcon = data.Attr("heartGateIcon", "CollabUtils2/lobbies/heartgate");
            GymIcon = data.Attr("gymIcon", "CollabUtils2/lobbies/gym");
            MapIcon = data.Attr("mapIcon", "CollabUtils2/lobbies/map");
            JournalIcon = data.Attr("journalIcon", "CollabUtils2/lobbies/journal");
            HeartSideIcon = data.Attr("heartSideIcon", "CollabUtils2/lobbies/heartside");

            ShowWarps = data.Bool("showWarps", true);
            ShowRainbowBerry = data.Bool("showRainbowBerry", true);
            ShowHeartGate = data.Bool("showHeartGate", true);
            ShowGyms = data.Bool("showGyms", true);
            ShowMaps = data.Bool("showMaps", true);
            ShowJournals = data.Bool("showJournals", true);
            ShowHeartSide = data.Bool("showHeartSide", true);
            ShowHeartCount = data.Bool("showHeartCount", true);

            RevealWhenAllMarkersFound = data.Bool("revealWhenAllMarkersFound");

            var customMarkers = data.Attr("customMarkers");
            CustomMarkers = !string.IsNullOrWhiteSpace(customMarkers)
                ? customMarkers.Split(commaSeparator, StringSplitOptions.RemoveEmptyEntries)
                : new string[0];

            if (RoomWidth <= 0) RoomWidth = ((OriginalLevelEntityData)data).Level.TileBounds.Width;
            if (RoomHeight <= 0) RoomHeight = ((OriginalLevelEntityData)data).Level.TileBounds.Height;
            if (mapData != null)
            {
                LevelSet = module.Identities.GetLobbyLevelSet(
                    ((IOriginalAreaKeyEnvironment)module.Panels.Engine).AreaKeys.GetSID(mapData.Area));
            }
        }

        public bool ShouldShowMarker(LobbyMapMarkerInfo marker)
        {
            switch (marker.Type)
            {
                case LobbyMapMarkerType.Custom: return true;
                case LobbyMapMarkerType.Warp: return ShowWarps;
                case LobbyMapMarkerType.RainbowBerry: return ShowRainbowBerry;
                case LobbyMapMarkerType.HeartGate: return ShowHeartGate;
                case LobbyMapMarkerType.Gym: return ShowGyms;
                case LobbyMapMarkerType.Map: return ShowMaps;
                case LobbyMapMarkerType.Journal: return ShowJournals;
                case LobbyMapMarkerType.HeartSide: return ShowHeartSide;
                default: return true;
            }
        }
    }
}
