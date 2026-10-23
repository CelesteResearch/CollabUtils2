using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Rendering;
using Celeste.Mods.CollabUtils2.Maps.LobbyMaps;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.UI.LobbyMaps;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;

namespace Celeste.Mods.CollabUtils2.Entities.LobbyMaps
{
    [SceneTracked]
    [CustomEntity("CollabUtils2/LobbyMapController")]
    public class LobbyMapController : SceneEntityState
    {
        public readonly LobbyMapControllerInfo Info;
        public LobbyVisitManager VisitManager;
        private readonly ISourceEngineContext engine;
        private readonly CollabModule module;

        public LobbyMapController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            Info = new LobbyMapControllerInfo(module, data);
            Depth = -1000000;
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            if (scene is OriginalLevelSceneRoot level)
            {
                var areas = ((IOriginalAreaKeyEnvironment)engine).AreaKeys;
                VisitManager = new LobbyVisitManager(module, areas.GetSID(areas.GetSessionArea(level.Session)),
                    level.Session.Level);
            }
        }

        private RoomPlayerSession FindPlayer(OriginalLevelSceneRoot level)
        {
            foreach (RoomPlayerSession player in ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(
                         typeof(RoomPlayerSession)))
                return player;
            return null;
        }

        private bool MapOpen(OriginalLevelSceneRoot level)
        {
            foreach (object ui in ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(typeof(LobbyMapUI)))
                return true;
            return false;
        }

        public override void Update()
        {
            base.Update();
            if (Scene is not OriginalLevelSceneRoot level || MapOpen(level)) return;
            var player = FindPlayer(level);
            if (player == null) return;
            if (module.Settings.DisplayLobbyMap.Pressed && level.CanRetry &&
                (player.Machine.State == 0 && player.OnGround() || player.Machine.State == 3) && !MapOpen(level))
            {
                foreach (LobbyMapWarp warp in ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(
                             typeof(LobbyMapWarp)))
                    if (SourceEntityCollision.Check(player, warp))
                    {
                        warp.OnTalk(player);
                        return;
                    }

                LobbyMapUI.SetLocked(engine, true, level, player);
                level.Scheduler.QueueSourceEntity(new LobbyMapUI(engine, true));
                return;
            }

            if (!((ISourceScene)level).SourceScene.Clock.OnInterval(0.2f, engine) || player.Machine.State == 11 ||
                module.SaveData.PauseVisitingPoints) return;
            if (!level.Paused && !level.Transitioning && VisitManager?.VisitedAll == false)
            {
                var point = new Vector2(
                    Math.Min((float)Math.Floor((player.Center.X - level.Bounds.X) / 8f),
                        (float)Math.Round(level.Bounds.Width / 8f, MidpointRounding.AwayFromZero) - 1),
                    Math.Min((float)Math.Floor((player.Center.Y - level.Bounds.Y) / 8f),
                        (float)Math.Round(level.Bounds.Height / 8f, MidpointRounding.AwayFromZero) + 1));
                VisitManager.VisitPoint(point);
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);
            if (!module.SaveData.ShowVisitedPoints || VisitManager == null ||
                Scene is not OriginalLevelSceneRoot level) return;
            var player = FindPlayer(level);
            if (player == null) return;
            bool paused = module.SaveData.PauseVisitingPoints;
            var arithmetic = ((ISourceVectorEnvironment)engine).VectorArithmetic;
            for (int index = 0; index < VisitManager.VisitedPoints.Count; index++)
            {
                var visited = VisitManager.VisitedPoints[index];
                if (!paused && visited.DistanceSquared >= 100 * 100) break;
                var center = visited.Point * 8 + Vector2.One * 4 + new Vector2(level.Bounds.Left, level.Bounds.Top);
                if (!paused && index < 3)
                    SourcePrimitiveDrawing.Line(context, this, center, player.Center, PackedColor.Green, arithmetic);
                SourcePrimitiveDrawing.Rect(context, this, center.X - 2, center.Y - 2, 4, 4,
                    index == 0 ? PackedColor.Blue : PackedColor.Red);
            }
        }

        public override void SceneEnd(object scene)
        {
            VisitManager?.Save();
            base.SceneEnd(scene);
        }
    }
}
