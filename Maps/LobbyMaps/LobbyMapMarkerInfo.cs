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
    public struct LobbyMapMarkerInfo
    {
        /// <summary>
        /// The icon in the Gui atlas.
        /// </summary>
        public string Icon;

        /// <summary>
        /// A key into Dialog.Clean for markers that include a title.
        /// </summary>
        public string DialogKey;

        /// <summary>
        /// A unique id for this marker. Also used to identify and sort warps.
        /// </summary>
        public string MarkerId;

        /// <summary>
        /// The type of marker. This allows for filtering in the controller.
        /// </summary>
        public LobbyMapMarkerType Type;

        /// <summary>
        /// The position of the marker on the map.
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// The SID for the lobby map.
        /// </summary>
        public string SID;

        /// <summary>
        /// The room within the map that this marker belongs to.
        /// </summary>
        public string Room;

        /// <summary>
        /// The name of the map to load when using a marker type of <see cref="LobbyMapMarkerType.Map"/>.
        /// </summary>
        public string Map;

        /// <summary>
        /// Extracted data about the referenced map if it exists.
        /// </summary>
        public LobbyMapTargetMapInfo MapInfo;

        /// <summary>
        /// The wipe to use for warp markers.
        /// </summary>
        public string WipeType;

        /// <summary>
        /// If this marker is a warp, this defines whether the player must interact with it before it
        /// becomes visible on the map.
        /// </summary>
        public bool WarpRequiresActivation;

        public static bool TryParse(CollabModule module, EntityData data, LobbyMapControllerInfo controllerInfo,
            out LobbyMapMarkerInfo value)
        {
            value = default;

            // CU2 simple marker entity
            if (data.Name == "CollabUtils2/LobbyMapMarker")
            {
                value.Type = LobbyMapMarkerType.Custom;
                value.DialogKey = data.Attr("dialogKey");
                value.Icon = data.Attr("icon");
            }
            // CU2 warp entity
            else if (data.Name == "CollabUtils2/LobbyMapWarp")
            {
                value.Type = LobbyMapMarkerType.Warp;
                value.DialogKey = data.Attr("dialogKey");
                value.MarkerId = data.Attr("warpId");
                value.Icon = data.Attr("icon");
                value.WipeType = data.Attr("wipeType", "Celeste.Mountain");
                value.WarpRequiresActivation = data.Bool("warpRequiresActivation");
            }
            // CU2 rainbow berry
            else if (data.Name == "CollabUtils2/RainbowBerry")
            {
                value.Type = LobbyMapMarkerType.RainbowBerry;
            }
            // CU2 heart door
            else if (data.Name == "CollabUtils2/MiniHeartDoor")
            {
                value.Type = LobbyMapMarkerType.HeartGate;
            }
            // CU2 journal trigger
            else if (data.Name == "CollabUtils2/JournalTrigger")
            {
                value.Type = LobbyMapMarkerType.Journal;
            }
            // CU2 map trigger or something from the CustomMarkers property
            else if (data.Name == "CollabUtils2/ChapterPanelTrigger" ||
                     controllerInfo != null && controllerInfo.CustomMarkers.Contains(data.Name))
            {
                value.Map = data.Attr("map");
                value.Type = module.Identities.GetLobbyForGym(value.Map) != null ? LobbyMapMarkerType.Gym :
                    module.Identities.IsHeartSide(value.Map) ? LobbyMapMarkerType.HeartSide : LobbyMapMarkerType.Map;
            }
            // not a valid map marker, skip
            else
            {
                return false;
            }

            // grab the entity's centre
            value.Position = new Vector2(data.Position.X + data.Width / 2f, data.Position.Y + data.Height / 2f);

            // if we haven't explictly set a marker id, default to the type and entity id
            if (string.IsNullOrWhiteSpace(value.MarkerId))
            {
                value.MarkerId = $"{value.Type}_{data.ID}";
            }

            // if we haven't explicitly set an icon, use the one defined in the controller for the given type
            // or fall back to some defaults
            if (string.IsNullOrWhiteSpace(value.Icon))
            {
                switch (value.Type)
                {
                    case LobbyMapMarkerType.Warp:
                        value.Icon = controllerInfo?.WarpIcon ?? "CollabUtils2/lobbies/warp";
                        break;
                    case LobbyMapMarkerType.RainbowBerry:
                        value.Icon = controllerInfo?.RainbowBerryIcon ?? "CollabUtils2/lobbies/rainbowBerry";
                        break;
                    case LobbyMapMarkerType.HeartGate:
                        value.Icon = controllerInfo?.HeartGateIcon ?? "CollabUtils2/lobbies/heartgate";
                        break;
                    case LobbyMapMarkerType.Gym:
                        value.Icon = controllerInfo?.GymIcon ?? "CollabUtils2/lobbies/gym";
                        break;
                    case LobbyMapMarkerType.Map:
                        value.Icon = controllerInfo?.MapIcon ?? "CollabUtils2/lobbies/map";
                        break;
                    case LobbyMapMarkerType.HeartSide:
                        value.Icon = controllerInfo?.HeartSideIcon ?? "CollabUtils2/lobbies/heartside";
                        break;
                    case LobbyMapMarkerType.Journal:
                        value.Icon = controllerInfo?.JournalIcon ?? "CollabUtils2/lobbies/journal";
                        break;
                }
            }

            // if this marker represents an enterable map, try to read the data for that map
            if (value.Type == LobbyMapMarkerType.Map && !string.IsNullOrWhiteSpace(value.Map) ||
                value.Type == LobbyMapMarkerType.HeartSide)
            {
                value.MapInfo = new LobbyMapTargetMapInfo(module, value.Map);
            }

            // successfully parsed the entity
            return true;
        }
    }
}
