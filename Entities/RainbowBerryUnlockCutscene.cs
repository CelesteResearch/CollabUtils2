using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Components.Animation;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Scheduling;
using Celeste.Audio.Fmod.Events;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Levels.Cutscenes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Particles.Sources;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Runtime;
using Celeste.Simulation.Presentation.Particles;

namespace Celeste.Mods.CollabUtils2.Entities
{
    public sealed class RainbowBerryUnlockCutscene : OriginalCutsceneEntityState
    {
        private readonly IOriginalStrawberryProcess process;
        private RainbowBerry strawberry;
        private HoloRainbowBerry holoBerry;
        private int silverBerryCount;
        private ParticleSystemPresentation system;
        private FmodEventReference snapshot;
        private FmodEventReference sfx;

        private SpriteImage[] silverBerries;

        public RainbowBerryUnlockCutscene(ISourceEngineContext engine, RainbowBerry strawberry,
            HoloRainbowBerry holoBerry, int silverBerryCount) : base(engine)
        {
            process = (IOriginalStrawberryProcess)engine;
            this.strawberry = strawberry;
            this.holoBerry = holoBerry;
            this.silverBerryCount = silverBerryCount;
        }

        public override void OnBegin(OriginalLevelSceneRoot level)
        {
            Add(new SimulationCoroutine(process, Cutscene(level)));
        }

        private IEnumerator Cutscene(OriginalLevelSceneRoot level)
        {
            var players = level.Scheduler.Entities.GetTrackedEntities(typeof(RoomPlayerSession));
            RoomPlayerSession player = players.Count == 0 ? null : (RoomPlayerSession)players[0];
            if (player != null)
            {
                // wait until player is in control (respawn animation done, etc).
                while (!PlayerFrameMotion.InControl(player.Machine.State))
                {
                    yield return null;
                }

                player.Machine.State = 11;
            }

            // start animation, mute sound
            sfx = process.AudioSession.Play("event:/game/general/seed_complete_main", Position, process.SaveContext);
            snapshot = process.AudioSession.CreateSnapshot("snapshot:/music_mains_mute");

            // spawn silver berries, not visible yet
            silverBerries = new SpriteImage[silverBerryCount];
            for (int i = 0; i < silverBerryCount; i++)
            {
                silverBerries[i] = new SpriteImage(process.GameGraphics.GameAtlas["CollabUtils2/silverBerry/idle00"]);
                silverBerries[i].Color = PackedColor.White * 0;
                silverBerries[i].CenterOrigin();
                if (player != null)
                {
                    silverBerries[i].Position = player.Position;
                }

                Add(silverBerries[i]);
            }

            // freeze the level after a bit
            Depth = -2000000 - 3;
            strawberry.Depth = -2000000 - 2;
            holoBerry.Depth = 2000000 - 2;
            holoBerry.Particles.Depth = -2000000 - 2;
            strawberry.Tags |= ((IOriginalGameTagsEnvironment)process).Tags.FrozenUpdate.Value;
            yield return 0.35f;
            Tags = ((IOriginalGameTagsEnvironment)process).Tags.FrozenUpdate.Value;
            level.Frozen = true;

            // darken the level, pause SFX
            level.FormationBackdrop.Display = true;
            level.FormationBackdrop.Alpha = 0.5f;
            level.Displacement.Clear();
            level.Displacement.Enabled = false;
            process.AudioSession.BusPaused("bus:/gameplay_sfx/ambience", true);
            process.AudioSession.BusPaused("bus:/gameplay_sfx/char", true);
            process.AudioSession.BusPaused("bus:/gameplay_sfx/game/general/yes_pause", true);
            process.AudioSession.BusPaused("bus:/gameplay_sfx/game/chapters", true);
            yield return 0.1f;

            system = new ParticleSystemPresentation(-2000000 - 2, 50, process,
                (ISourceParticleTextureEnvironment)process);
            system.Tags = ((IOriginalGameTagsEnvironment)process).Tags.FrozenUpdate.Value;
            level.Scheduler.QueueSourceEntity(system);

            // start the spin
            float angleSep = (float)Math.PI * 2f / silverBerryCount;
            float angle = (float)Math.PI / 2f;
            foreach (SpriteImage silverBerry in silverBerries)
            {
                startSpinAnimation(silverBerry, silverBerry.Position, strawberry.Position, angle, 4f);
                angle -= angleSep;
            }

            // focus camera on rainbow berry and wait
            Vector2 cameraTarget = strawberry.Position - new Vector2(160f, 90f);
            cameraTarget = new Vector2(ScalarMath.Clamp(cameraTarget.X, level.Bounds.Left, level.Bounds.Right - 320),
                ScalarMath.Clamp(cameraTarget.Y, level.Bounds.Top, level.Bounds.Bottom - 180));
            yield return 0.1f;
            Add(new SimulationCoroutine(process,
                CameraTo(process, cameraTarget, 2f, process.EasingArithmetic.CubeInOut)));
            yield return 3.9f;

            // combine all silvers into the rainbow
            process.RumbleSession.Rumble(0, 2);
            process.AudioSession.Play("event:/game/general/seed_complete_berry", strawberry.Position,
                process.SaveContext);
            foreach (SpriteImage silverBerry in silverBerries)
            {
                startCombineAnimation(silverBerry, strawberry.Position, 0.6f, system);
            }

            yield return 0.6f;

            // remove the silver berries, and make the rainbow berry appear
            process.RumbleSession.Rumble(1, 1);
            foreach (SpriteImage silverBerry in silverBerries)
            {
                silverBerry.RemoveSelf();
            }

            holoBerry.RemoveSelf();
            strawberry.CollectedSeeds();
            yield return 0.5f;

            // pan back to the player
            yield return CameraTo(process, player.CameraTarget, 1f, process.EasingArithmetic.CubeOut);

            // cutscene is over!
            level.EndCutscene();
            OnEnd(level);
        }

        public override void OnEnd(OriginalLevelSceneRoot level)
        {
            if (WasSkipped)
            {
                process.AudioSession.Stop(sfx);
            }

            var players = level.Scheduler.Entities.GetTrackedEntities(typeof(RoomPlayerSession));
            RoomPlayerSession player = players.Count == 0 ? null : (RoomPlayerSession)players[0];
            if (player != null)
            {
                player.Machine.State = 0;
            }

            level.Scheduler.OnEndOfFrame += delegate
            {
                if (WasSkipped)
                {
                    if (silverBerries != null)
                    {
                        foreach (SpriteImage silverBerry in silverBerries)
                        {
                            silverBerry.RemoveSelf();
                        }
                    }

                    holoBerry.RemoveSelf();
                    strawberry.CollectedSeeds();
                    level.Camera.Position = player.CameraTarget;
                }

                strawberry.Depth = -100;
                strawberry.Tags &= ~((IOriginalGameTagsEnvironment)process).Tags.FrozenUpdate.Value;
                level.Frozen = false;
                level.FormationBackdrop.Display = false;
                level.Displacement.Enabled = true;
            };
            RemoveSelf();
        }

        private void endSfx()
        {
            process.AudioSession.BusPaused("bus:/gameplay_sfx/ambience", false);
            process.AudioSession.BusPaused("bus:/gameplay_sfx/char", false);
            process.AudioSession.BusPaused("bus:/gameplay_sfx/game/general/yes_pause", false);
            process.AudioSession.BusPaused("bus:/gameplay_sfx/game/chapters", false);
            process.AudioSession.ReleaseSnapshot(snapshot);
        }

        private void startSpinAnimation(SpriteImage silverBerry, Vector2 averagePos, Vector2 centerPos,
            float angleOffset, float time)
        {
            float spinLerp = 0f;
            Vector2 start = silverBerry.Position;

            // first tween to make the lerp progress
            SourceTweenState tween = SourceTweenState.Create(process.TweenPool, SourceTweenState.TweenMode.Oneshot,
                process.EasingArithmetic.CubeInOut, time / 2f, start: true);
            tween.OnUpdate = t => { spinLerp = t.Eased; };
            Add(tween);

            // second tween to make the berry spin and fade in
            tween = SourceTweenState.Create(process.TweenPool, SourceTweenState.TweenMode.Oneshot,
                process.EasingArithmetic.CubeInOut, time, start: true);
            tween.OnUpdate = t =>
            {
                float angleRadians = (float)Math.PI / 2f + angleOffset -
                                     process.ScalarArithmetic.Lerp(0f, 32.2013245f, t.Eased);
                Vector2 rotationCenter = Vector2.Lerp(averagePos, centerPos, spinLerp);
                Vector2 berryPosition = rotationCenter + Vector2Geometry.AngleToVector(angleRadians, 25f);
                silverBerry.Position = Vector2.Lerp(start, berryPosition, spinLerp);
                silverBerry.Color = PackedColor.White * spinLerp;
            };
            Add(tween);
        }

        private void startCombineAnimation(SpriteImage silverBerry, Vector2 centerPos, float time,
            ParticleSystemPresentation particleSystem)
        {
            Vector2 position = silverBerry.Position;
            float startAngle = Vector2Geometry.Angle(centerPos, position);
            SourceTweenState tween = SourceTweenState.Create(process.TweenPool, SourceTweenState.TweenMode.Oneshot,
                process.EasingArithmetic.BigBackIn, time, start: true);

            // the tween should pull the silver towards the center and rotate it at the same time.
            tween.OnUpdate = t =>
            {
                float angleRadians = process.ScalarArithmetic.Lerp(startAngle, startAngle - (float)Math.PI * 2f,
                    process.EasingArithmetic.CubeIn(t.Percent));
                float length = process.ScalarArithmetic.Lerp(25f, 0f, t.Eased);
                silverBerry.Position = centerPos + Vector2Geometry.AngleToVector(angleRadians, length);
            };

            // when done, emit particles and make the seed disappear.
            tween.OnComplete = delegate
            {
                silverBerry.Visible = false;
                for (int i = 0; i < 6; i++)
                {
                    float particleDirection = SourceRandomMath.NextFloat(((ISourceRandomEnvironment)process).Random,
                        (float)Math.PI * 2f);
                    particleSystem.Emit(process.ParticleTypes.StrawberrySeed.P_Burst, 1,
                        silverBerry.Position + Vector2Geometry.AngleToVector(particleDirection, 4f), Vector2.Zero,
                        particleDirection);
                }

                silverBerry.RemoveSelf();
            };

            Add(tween);
        }

        public override void Removed(object scene)
        {
            endSfx();
            base.Removed(scene);
        }

        public override void SceneEnd(object scene)
        {
            endSfx();
            base.SceneEnd(scene);
        }
    }
}
