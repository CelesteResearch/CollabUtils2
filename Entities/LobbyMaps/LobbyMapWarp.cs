using System.Collections;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Components.Talking;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Mods.CollabUtils2.Maps.LobbyMaps;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.UI.LobbyMaps;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Facing;

namespace Celeste.Mods.CollabUtils2.Entities.LobbyMaps
{
    [SceneTracked]
    [CustomEntity("CollabUtils2/LobbyMapWarp")]
    public class LobbyMapWarp : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly bool playActivateSprite;
        private readonly bool activateSpriteFlipX;
        private readonly FacingDirection playerFacing;
        private LobbyMapMarkerInfo info;
        internal LobbyMapMarkerInfo MarkerInfo => info;

        public LobbyMapWarp(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            string warpSpritePath = data.Attr("warpSpritePath", "decals/1-forsakencity/bench_concrete");
            playActivateSprite = data.Bool("playActivateSprite", true);
            activateSpriteFlipX = data.Bool("activateSpriteFlipX");
            playerFacing = data.Enum("playerFacing", FacingDirection.Right);
            Depth = data.Int("depth", 2000);
            var module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            LobbyMapMarkerInfo.TryParse(module, data, null, out info);
            if (!string.IsNullOrWhiteSpace(warpSpritePath))
            {
                var image = new SpriteImage(
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[warpSpritePath])
                {
                    FlipX = data.Bool("warpSpriteFlipX")
                };
                image.JustifyOrigin(0.5f, 1f);
                Add(image);
            }

            var talkRect = new IntRect(-16, -32, 32, 32);
            Collidable = true;
            Collider = new HitboxColliderGeometry(talkRect.Width, talkRect.Height, talkRect.X, talkRect.Y);
            Add(new OriginalTalkComponent((IOriginalTalkProcess)engine, talkRect,
                new Vector2(0, data.Float("interactOffsetY", -16)), OnTalk) { PlayerMustBeFacing = false });
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            var level = (OriginalLevelSceneRoot)scene;
            var areas = ((IOriginalAreaKeyEnvironment)engine).AreaKeys;
            info.SID = areas.GetSID(areas.GetSessionArea(level.Session));
            info.Room = level.Session.Level;
        }

        public void OnTalk(RoomPlayerSession player)
        {
            if (player.Scene is not OriginalLevelSceneRoot level || !level.CanRetry) return;
            LobbyMapUI.SetLocked(engine, true);
            foreach (LobbyMapController controller in ((ISourceScene)level).SourceScene.Entities
                     .GetTrackedEntities(typeof(LobbyMapController)))
            {
                controller.VisitManager?.ActivateWarp(info.MarkerId);
                break;
            }

            if (playActivateSprite) Add(new SimulationCoroutine(engine, ActivateRoutine(player)));
            else level.Scheduler.QueueSourceEntity(new LobbyMapUI(engine));
        }

        private IEnumerator ActivateRoutine(RoomPlayerSession player)
        {
            if (player == null) yield break;
            LobbyMapUI.SetLocked(engine, true, Scene, player);
            yield return player.DummyWalkToExact((int)Position.X, false, 1f, true);
            if (!ValidPlayer(player))
            {
                if (!player.Dead) LobbyMapUI.SetLocked(engine, false, Scene, player);
                yield break;
            }

            player.SourceFacing = playerFacing;
            player.Sprite.Visible = player.Hair.Visible = false;
            var factory = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.SpriteFactory;
            var playerSprite = factory.Create("CollabUtils2_sitBench");
            playerSprite.FlipX = activateSpriteFlipX;
            Add(playerSprite);
            playerSprite.Play("sit");
            var hairSprite = factory.Create("CollabUtils2_sitBench");
            hairSprite.FlipX = activateSpriteFlipX;
            hairSprite.Color = player.Hair.Color;
            Add(hairSprite);
            hairSprite.Play("sitHair");
            ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/char/madeline/backpack_drop");
            while (playerSprite.Animating && ValidPlayer(player))
            {
                LobbyMapUI.SetLocked(engine, true, Scene, player);
                yield return null;
            }

            if (ValidPlayer(player))
                ((OriginalLevelSceneRoot)player.Scene).Scheduler.QueueSourceEntity(new LobbyMapUI(engine));
            else LobbyMapUI.SetLocked(engine, false, Scene, player);
            yield return 0.5f;
            playerSprite.RemoveSelf();
            hairSprite.RemoveSelf();
            player.Sprite.Visible = player.Hair.Visible = true;
        }

        private bool ValidPlayer(RoomPlayerSession player)
            => ((ISourceVectorEnvironment)engine).VectorArithmetic.LengthSquared(Center - player.Center) < 16 * 16 &&
               !player.Dead && player.OnGround();
    }
}
