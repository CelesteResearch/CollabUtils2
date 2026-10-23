using System.Linq;
using Celeste.Everest.Modules.Capabilities;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.CollabUtils2.Entities.LobbyMaps;
using Celeste.Mods.CollabUtils2.Maps.LobbyMaps;
using Celeste.Mods.CollabUtils2.UI.LobbyMaps;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.CollabUtils2.Modules
{
    public sealed partial class CollabModule : ISourceCollabLobbyMaps
    {
        public bool TryGetActiveWarps(ISourceScene scene, out string[] activeWarps)
        {
            activeWarps = null;
            if (scene is not OriginalLevelSceneRoot level ||
                !ReferenceEquals(level.EngineContext, SourceEngine)) return false;
            var controller = level.SourceScene.Entities.GetTrackedEntities(typeof(LobbyMapController))
                .OfType<LobbyMapController>().FirstOrDefault();
            if (controller == null) return false;
            var visitedTiles = LobbyMapUI.GetVisitedTiles(controller.Info, controller.VisitManager);
            if (visitedTiles == null) return false;
            activeWarps = level.SourceScene.Entities.GetTrackedEntities(typeof(LobbyMapWarp)).OfType<LobbyMapWarp>()
                .Select(warp => warp.MarkerInfo)
                .Where(marker => IsWarpVisited(visitedTiles, marker) && marker.Type == LobbyMapMarkerType.Warp &&
                                 (!marker.WarpRequiresActivation ||
                                  controller.VisitManager.ActivatedWarps.Contains(marker.MarkerId)))
                .Select(marker => marker.MarkerId).ToArray();
            return true;
        }

        private static bool IsWarpVisited(LobbyVisitedByteGrid grid, LobbyMapMarkerInfo marker)
            => grid.TryGet((int)(marker.Position.X / 8), (int)(marker.Position.Y / 8), out byte value) && value > 0x7F;

        public void ClearLobbyMap(string sid)
        {
            if (SourceEngine.Frame.Scenes.CurrentScene is OriginalLevelSceneRoot level)
            {
                var keys = ((IOriginalAreaKeyEnvironment)SourceEngine).AreaKeys;
                if (keys.GetSID(keys.GetSessionArea(level.Session)) == sid &&
                    level.SourceScene.Entities.GetTrackedEntities(typeof(LobbyMapController))
                        .OfType<LobbyMapController>().FirstOrDefault() is { } controller)
                {
                    if (controller.VisitManager is { } manager) manager.Reset(true);
                    if (controller.VisitManager is { } saveManager) saveManager.Save();
                }
            }

            string[] keysToRemove = SaveData.VisitedLobbyPositions.Keys.Where(key => key.StartsWith(sid)).ToArray();
            foreach (string key in keysToRemove) SaveData.VisitedLobbyPositions.Remove(key);
        }
    }
}
