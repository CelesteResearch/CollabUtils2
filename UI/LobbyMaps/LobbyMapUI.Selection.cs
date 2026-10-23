using Celeste.Game.Chapters.Flow;
using Celeste.Framework.Diagnostics.Commands;
using Celeste.Everest;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Everest.Content.Maps.Metadata;
using Celeste.Mods.CollabUtils2.Entities.LobbyMaps;
using Celeste.Core.Mathematics.Easing;
using MonoMod.ModInterop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Core.Components;
using Celeste.Core.Components.Rendering;
using Celeste.Core.Randomness;
using Celeste.Core.Mathematics;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Execution.Engine;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Session;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Game.Save.Runtime;
using Celeste.Game.Controls.Source;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tagging;
using Celeste.Scene.Components.Oscillation;
using Celeste.Everest.Input;
using Celeste.Everest.Modules.Runtime;
using Celeste.Input.Runtime;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Maps.LobbyMaps;
using Celeste.Simulation.Gameplay.Player.Intro;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using Component = Celeste.Core.Components.SourceComponentState;
using Player = Celeste.Simulation.RoomPlayerSession;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Scene = Celeste.Scene.Scenes.SourceSceneState;
using EntityData = Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData;
using LevelData = Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData;
using MapData = Celeste.Game.Content.Maps.OriginalChapterMapData;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using Sprite = Celeste.Graphics.Animation.Sprite;
using Wiggler = Celeste.Scene.Components.Oscillation.WigglerComponent;
using Color = Celeste.Core.Display.PackedColor;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Texture2D = Celeste.Graphics.Rendering.Textures.SourceTexture2DResource;
using VirtualJoystick = Celeste.Everest.Input.SourceEverestVirtualJoystick;
using VirtualButton = Celeste.Everest.Input.SourceEverestVirtualButton;
using Coroutine = Celeste.Core.Scheduling.SimulationCoroutine;
using ScreenWipe = Celeste.Simulation.Presentation.Scene.Transitions.ScreenWipePresentation;
using FadeWipe = Celeste.Simulation.Presentation.Scene.Transitions.FadeWipePresentation;
using MountainWipe = Celeste.Simulation.Presentation.Scene.Transitions.MountainWipePresentation;
using EntityID = Celeste.Core.Maps.MapEntityId;
using Binding = Celeste.Input.Bindings.SourceInputBinding;
using AreaKey = Celeste.Game.Execution.Areas.OriginalAreaKey;
using Session = Celeste.Simulation.Runtime.Session.ChapterCompletionSession;
using BlendState = Celeste.Core.Rendering.States.SourceBlendState;
using Blend = Celeste.Core.Rendering.SourceBlendFactor;
using SpriteSortMode = Celeste.Core.Rendering.States.SourceSpriteSortMode;
using SurfaceFormat = Celeste.Graphics.Rendering.Textures.SourceSurfaceFormat;

namespace Celeste.Mods.CollabUtils2.UI.LobbyMaps
{
    public partial class LobbyMapUI
    {
        #region Lobby Configuration

        /// <summary>
        /// Returns true if the given position has been revealed in the current lobby.
        /// </summary>
        private bool isVisited(Vector2 position, byte threshold = 0x7F) =>
            visitedTiles.TryGet((int)(position.X / 8), (int)(position.Y / 8), out var value) && value > threshold;

        /// <summary>
        /// Calculates the correct origin within the overlay texture for a given level coordinate.
        /// </summary>
        /// <param name="point"></param>
        private Vector2 originForPosition(Vector2 point)
        {
            var tileX = point.X / 8f;
            var tileY = point.Y / 8f;
            return new Vector2(tileX / (lobbyMapInfo?.RoomWidth ?? 1), tileY / (lobbyMapInfo?.RoomHeight ?? 1));
        }

        /// <summary>
        /// Loads all LobbyMapController data from maps in this levelset, if at least one warp has been unlocked.
        /// </summary>
        private void getLobbyControllers(Level level)
        {
            // we can only return lobbies that have at least one visited position, or this one
            var collabName = module.Identities.GetCollabNameForSID(Areas.GetSID(Areas.GetSessionArea(level.Session)));
            var visitedLobbySIDs = module.SaveData.VisitedLobbyPositions.Keys.Where(k => k.StartsWith(collabName))
                .ToList();
            var thisLobbyKey = $"{Areas.GetSID(Areas.GetSessionArea(level.Session))}.{level.Session.Level}";
            if (!visitedLobbySIDs.Contains(thisLobbyKey))
            {
                visitedLobbySIDs.Add(thisLobbyKey);
            }

            // parse all the markers in all the lobbies that have at least one warp activated
            lobbySelections.Clear();
            foreach (var key in visitedLobbySIDs)
            {
                // get the room and sid
                var room = string.Empty;
                var sid = key;
                if (key.LastIndexOf('.') > key.LastIndexOf('/'))
                {
                    room = key.Substring(key.LastIndexOf('.') + 1);
                    sid = key.Substring(0, key.LastIndexOf('.'));
                }

                // get the visit manager and skip if no warps found, unless it's this lobby
                if (key != thisLobbyKey)
                {
                    var lobbyVisitManager = getLobbyVisitManager(level, sid, room);
                    if (!lobbyVisitManager.ActivatedWarps.Any()) continue;
                }

                // get the map data from the lobby sid
                var mapData = Areas.GetAreaBySID(sid)?.Mode.FirstOrDefault()?.MapData;
                if (mapData == null) continue;

                // find the map controller in the specified room, or the first in the map if no room specified
                const string mapControllerName = "CollabUtils2/LobbyMapController";
                var levelData = string.IsNullOrWhiteSpace(room) ? null : mapData.Get(room);
                var entityData = levelData == null
                    ? mapData.Levels.Select(l => findEntityData(l, mapControllerName)).FirstOrDefault()
                    : findEntityData(levelData, mapControllerName);

                // parse the markers in the room if a controller was found
                if (entityData != null)
                {
                    var selection = new LobbyMapSelection(module, entityData, mapData);
                    var markers = new List<LobbyMapMarkerInfo>();
                    foreach (var data in selection.Data.Level.Entities.Concat(selection.Data.Level.Triggers))
                    {
                        if (LobbyMapMarkerInfo.TryParse(module, data, selection.Info, out var value))
                        {
                            value.SID = selection.SID;
                            value.Room = selection.Room;
                            markers.Add(value);
                        }
                    }

                    selection.Markers = markers.ToArray();
                    lobbySelections.Add(selection);
                }
            }

            // sort lobbies by SID and room
            lobbySelections.Sort((lhs, rhs) =>
            {
                // sort first by sid
                var compareSid = string.CompareOrdinal(lhs.SID, rhs.SID);
                if (compareSid != 0) return compareSid;

                // then by room index
                var compareRoomIndex = Math.Sign(lhs.RoomIndex - rhs.RoomIndex);
                if (compareRoomIndex != 0) return compareRoomIndex;

                // then by room name
                return string.CompareOrdinal(lhs.Room, rhs.Room);
            });

            selectedWarpIndexes = new int[lobbySelections.Count];

            // select the current lobby
            selectedLobbyIndex = lobbySelections.FindIndex(s =>
                s.SID == Areas.GetSID(Areas.GetSessionArea(level.Session)) && s.Room == level.Session.Level);

            // verify selection
            if (selectedLobbyIndex < 0)
            {
                ((IEverestLoggerProcessEnvironment)engine).Logger.Log(EverestLogLevel.Warn, "CollabUtils2/LobbyMapUI",
                    $"getLobbyControllers: Couldn't find map for {Areas.GetSID(Areas.GetSessionArea(level.Session))}/{level.Session.Level}, defaulting to first");
                selectedLobbyIndex = 0;
            }
        }

        /// <summary>
        /// Finds or creates a <see cref="LobbyVisitManager"/> for the specified lobby.
        /// </summary>
        private LobbyVisitManager getLobbyVisitManager(object scene, string sid, string room)
        {
            if (((ISourceScene)scene).SourceScene.Entities.GetTrackedEntity<LobbyMapController>() is LobbyMapController
                lmc &&
                (lmc.VisitManager?.MatchesKey(sid, room) ?? false))
            {
                return lmc.VisitManager;
            }

            return new LobbyVisitManager(module, sid, room);
        }

        /// <summary>
        /// Configures the UI for the currently selected lobby.
        /// </summary>
        public bool updateSelectedLobby(bool first = false)
        {
            // validate lobby index
            if (selectedLobbyIndex < 0 || selectedLobbyIndex >= lobbySelections.Count)
            {
                ((IEverestLoggerProcessEnvironment)engine).Logger.Log(EverestLogLevel.Warn, "CollabUtils2/LobbyMapUI",
                    $"updateSelectedLobby: Invalid lobby selection {selectedLobbyIndex}");
                return false;
            }

            if (!(engine.Frame.Scenes.CurrentScene is Level level) ||
                !(level.SourceScene.Entities.GetTrackedEntity<Player>() is Player player))
            {
                ((IEverestLoggerProcessEnvironment)engine).Logger.Log(EverestLogLevel.Warn, "CollabUtils2/LobbyMapUI",
                    $"updateSelectedLobby: Couldn't find level or player");
                return false;
            }

            var selection = lobbySelections[selectedLobbyIndex];
            var markers = lobbySelections[selectedLobbyIndex].Markers;
            lobbyMapInfo = selection.Info;

            // get or create a visit manager
            visitManager = getLobbyVisitManager(Scene, selection.SID, selection.Room);

            // generate the 2d array of visited tiles
            if (openedWithRevealMap || visitManager.VisitedAll)
            {
                visitedTiles = new LobbyVisitedByteGrid(0, 0);
            }
            else
            {
                visitedTiles = generateVisitedTiles(lobbyMapInfo, visitManager);
                var visibleMarkers = markers.Where(m => isVisited(m.Position)).ToArray();
                if (visibleMarkers.Length == markers.Length && lobbyMapInfo.RevealWhenAllMarkersFound)
                {
                    visitManager.VisitAll();
                }

                markers = visibleMarkers;
            }

            // find warps
            activeWarps.Clear();
            activeWarps.AddRange(markers
                .Where(f => f.Type == LobbyMapMarkerType.Warp &&
                            (!f.WarpRequiresActivation || visitManager.ActivatedWarps.Contains(f.MarkerId))));

            // sort by marker id, comparing ints if possible
            activeWarps.Sort((lhs, rhs) =>
                int.TryParse(lhs.MarkerId.Trim(), out var lhsInt) && int.TryParse(rhs.MarkerId.Trim(), out var rhsInt)
                    ? Math.Sign(lhsInt - rhsInt)
                    : string.CompareOrdinal(lhs.MarkerId, rhs.MarkerId));

            // regenerate marker components
            var rainbowBerryUnlocked = isRainbowBerryUnlocked(lobbyMapInfo.LevelSet);
            markerComponents.ForEach(c => c.RemoveSelf());
            markerComponents.Clear();
            markerComponents.AddRange(markers
                .Where(f =>
                {
                    if (!lobbyMapInfo.ShouldShowMarker(f)) return false;
                    if (f.Type == LobbyMapMarkerType.Warp && f.WarpRequiresActivation &&
                        !visitManager.ActivatedWarps.Contains(f.MarkerId)) return false;
                    if (f.Type == LobbyMapMarkerType.RainbowBerry && !rainbowBerryUnlocked) return false;
                    return true;
                })
                .OrderByDescending(f => f.Type)
                .Select(createMarkerComponent));
            markerComponents.ForEach(Add);

            // if this is the first time we've selected a lobby, select the nearest warp if not view only
            if (!viewOnly && first)
            {
                selectedWarpIndexes[selectedLobbyIndex] = 0;
                var nearestWarpLengthSquared = float.MaxValue;
                for (int i = 0; i < activeWarps.Count; i++)
                {
                    var warpPosition = activeWarps[i].Position + level.LevelOffset;
                    var lengthSquared =
                        ((ISourceVectorEnvironment)engine).VectorArithmetic.LengthSquared(
                            warpPosition - player.Position);
                    if (lengthSquared < nearestWarpLengthSquared)
                    {
                        nearestWarpLengthSquared = lengthSquared;
                        selectedWarpIndexes[selectedLobbyIndex] = i;
                    }
                }

                // keep track of where we started
                initialLobbyIndex = selectedLobbyIndex;
                initialWarpIndex = selectedWarpIndexes[selectedLobbyIndex];
            }

            // get the map texture
            mapTexture = Graphics.GuiAtlas[lobbyMapInfo.MapTexture].Texture.Texture;

            // generate the overlay texture if we should
            overlayTexture?.Dispose();
            overlayTexture = null;
            if (!openedWithRevealMap && !visitManager.VisitedAll)
            {
                overlayTexture = new Texture2D(((ISourceGraphicsDeviceEnvironment)engine).GraphicsDevice,
                    lobbyMapInfo.RoomWidth, lobbyMapInfo.RoomHeight, false, SurfaceFormat.Alpha8);
                overlayTexture.SetData(visitedTiles.Data);
            }

            // set view
            if (zoomLevel < 0)
            {
                zoomLevel = defaultZoomLevel;
            }

            zoomLevel = ScalarMath.Clamp(zoomLevel, 0, zoomLevels.Length);
            actualScale = zoomLevels[zoomLevel];
            shouldCentreOrigin = zoomLevel == 0;

            var isCurrentLobby = selection.SID == Areas.GetSID(Areas.GetSessionArea(level.Session)) &&
                                 selection.Room == level.Session.Level;
            shouldShowMaddy = !viewOnly || isCurrentLobby;

            if (!viewOnly)
            {
                var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
                selectedOrigin = warpIndex >= 0 && warpIndex < activeWarps.Count
                    ? originForPosition(activeWarps[warpIndex].Position)
                    : new Vector2(0.5f);
                actualOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
            }
            else if (isCurrentLobby)
            {
                selectedOrigin = originForPosition(player.Position - new Vector2(level.Bounds.X, level.Bounds.Y));
                actualOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
            }
            else
            {
                selectedOrigin = actualOrigin = new Vector2(0.5f);
            }

            translateTimeRemaining = 0f;
            scaleTimeRemaining = 0f;

            // calculate multiplier by aspect ratios
            var padded = mapBounds;
            var mapAspectRatio = (float)mapTexture.Width / mapTexture.Height;
            var boundsAspectRatio = (float)padded.Width / padded.Height;
            scaleMultiplier = mapAspectRatio > boundsAspectRatio
                ? (float)padded.Width / mapTexture.Width
                : (float)padded.Height / mapTexture.Height;

            // update marker positions
            updateMarkers();

            // add heart component
            var heartFrame = heartSprite?.CurrentAnimationFrame ?? 0;
            heartSprite?.RemoveSelf();
            heartSprite = null;

            if (lobbyMapInfo.ShowHeartCount)
            {
                // try to get a custom id
                var id = CollabHeartSpriteRegistry.For(engine).GetGuiHeartSpriteId(selection.SID + "_lobbyMap", 0)
                         ?? CollabHeartSpriteRegistry.For(engine).GetGuiHeartSpriteId(selection.SID, 0);

                if (id == null)
                {
                    heartSprite = Graphics.GuiSpriteBank.Create("heartgem0");
                }
                else
                {
                    heartSprite = CollabHeartSpriteRegistry.For(engine).Create(id);
                }

                heartSprite.Scale = Vector2.One / 2f;
                heartSprite.Position = new Vector2(windowBounds.Left + 10, windowBounds.Top + 10);
                heartSprite.Justify = Vector2.Zero;
                heartSprite.Play("spin");
                heartSprite.SetAnimationFrame(heartFrame);
                heartSprite.JustifyOrigin(Vector2.Zero);
                Add(heartSprite);

                var levelSetStats =
                    ((IOriginalSaveLevelSetEnvironment)engine).SaveLevelSets.GetLevelSetStatsFor(
                        ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave, lobbyMapInfo.LevelSet);
                heartCount = levelSetStats?.TotalHeartGems ?? 0;
            }

            return true;
        }

        /// <summary>
        /// Creates a component that represents the passed marker. Currently only supports an Image subclass.
        /// </summary>
        private Component createMarkerComponent(LobbyMapMarkerInfo markerInfo)
        {
            return new LobbyMapMarkerImage(engine, markerInfo);
        }

        #endregion
    }
}
