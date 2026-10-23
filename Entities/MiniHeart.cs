using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Graphics.Content;
using Celeste.Core.Components;
using Celeste.Core.Maps;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Execution.Engine.Actions;
using Celeste.Game.Save.Runtime;
using Celeste.Game.Entities.Effects.AbsorbOrbs;
using Celeste.Game.Gameplay.Effects.Slashes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Components.Oscillation;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Gameplay.Collectibles.Strawberry;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Simulation.Audio.Emitters;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Display.Colors;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Content.Extensions.Collectibles;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.CollabUtils2.Modules;
using Color = Celeste.Core.Display.PackedColor;
using Player = Celeste.Simulation.RoomPlayerSession;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Holdable = Celeste.Simulation.Gameplay.Holdables.HoldableState;
using HoldableCollider = Celeste.Simulation.Gameplay.Holdables.HoldableColliderState;
using Wiggler = Celeste.Scene.Components.Oscillation.WigglerComponent;
using VertexLight = Celeste.Simulation.Presentation.Lighting.VertexLightState;
using BloomPoint = Celeste.Simulation.Presentation.PostProcessing.Bloom.BloomPointState;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using EntityID = Celeste.Core.Maps.MapEntityId;
using SoundEmitter = Celeste.Simulation.Audio.Emitters.SoundEmitterState;
using EventInstance = Celeste.Audio.Fmod.Events.FmodEventReference;

namespace Celeste.Mods.CollabUtils2.Entities
{
    [CustomEntity("CollabUtils2/MiniHeart")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.CollabUtils2.Entities.MiniHeart")]
    public class MiniHeart : AbstractMiniHeart
    {
        private Sprite white;
        private bool hasBeenBroken = false;
        private readonly bool flash;

        private SimulationCoroutine smashRoutine;
        private EventInstance pauseMusicSnapshot;
        private SoundEmitter collectSound;

        public MiniHeart(ISourceEngineContext engine, EntityData data, Vector2 position, EntityID gid)
            : base(engine, data, position, gid)
        {
            this.flash = data.Bool("flash", defaultValue: true);
        }

        protected override void HeartBroken(Player player, Holdable holdable, Level level)
        {
            if (hasBeenBroken) return;

            hasBeenBroken = true;
            Add(smashRoutine = new SimulationCoroutine(engine, SmashRoutine(player, level)));
        }

        private IEnumerator SmashRoutine(Player player, Level level)
        {
            level.CanRetry = false;

            Collidable = false;

            StopMusic();

            // collect all berries
            List<ISourceStrawberryCollectible> list = new List<ISourceStrawberryCollectible>();
            ReadOnlyCollection<Type> berryTypes =
                ((ISourceInstalledStrawberryRegistryEnvironment)engine).StrawberryRegistry.GetBerryTypes();
            foreach (var follower in player.Leader.Followers)
            {
                if (berryTypes.Contains(follower.Entity.GetType()) && follower.Entity is ISourceStrawberryCollectible)
                {
                    list.Add(follower.Entity as ISourceStrawberryCollectible);
                }
            }

            foreach (ISourceStrawberryCollectible item in list)
            {
                item.OnCollect();
            }

            // play the collect jingle
            collectSound = SoundEmitter.Play("event:/SC2020_heartShard_get", this, engine);

            // overlap a white sprite
            Add(white = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                "CollabUtils2/miniheart/white/white"));
            white.AddLoop("idle", "", 0.1f, animationFrames);
            white.Play("idle");
            white.CenterOrigin();

            // slow down time, visual effects
            Depth = -2000000;
            yield return null;
            OriginalCelesteFreeze.Freeze(engine, 0.2f);
            yield return null;
            SetTimeRate(0.5f);
            player.Depth = -2000000;
            for (int i = 0; i < 10; i++)
            {
                ((ISourceScene)Scene).SourceScene.QueueSourceEntity(new OriginalAbsorbOrbState(engine, Position));
            }

            level.Shake();
            ((ISourceRumbleEnvironment)engine).RumbleSession.Rumble(2, 1);
            if (flash) level.Flash(Color.White);
            light.Alpha = (bloom.Alpha = 0f);
            level.FormationBackdrop.Display = true;
            level.FormationBackdrop.Alpha = 1f;

            // slow down time further, to a freeze
            Visible = false;
            for (float time = 0f; time < 2f; time += engine.RawDeltaTime)
            {
                SetTimeRate(ScalarMath.Approach(GetTimeRate(), 0f, engine.RawDeltaTime * 0.25f));
                yield return null;
            }

            // make sure update order with the player is just right
            Depth = 0;
            Depth = -2000000;

            yield return null;
            if (player.Dead)
            {
                yield return 100f;
            }

            SetTimeRate(1f);
            Tags = ((IOriginalGameTagsEnvironment)engine).Tags.FrozenUpdate.Value;
            level.Frozen = true;

            // level is done! stop timer, save completion
            ((IOriginalSaveHeartEnvironment)engine).SaveHearts.RegisterHeartGem(Process.SaveContext.CurrentSave,
                ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSessionArea(level.Session));
            level.TimerStopped = true;
            level.PauseLock = true;
            level.RegisterAreaComplete();

            // music is definitively muted at this point. It shouldn't come back when the muted snapshot is released.
            Process.AudioSession.SetMusic(null);
            Process.AudioSession.SetAmbience(null);

            // display the end screen and get out of here!
            yield return new SourceSwapImmediately(Module.Return.DisplayCollabMapEndScreenIfEnabled());
            Module.Return.TriggerReturnToLobby();
        }

// Direct time-rate changes retain the upstream TAS timing.
        private void SetTimeRate(float timeRate) => engine.Effects.TimeRate = timeRate;
        private float GetTimeRate() => engine.Effects.TimeRate;


        public override void Update()
        {
            base.Update();

            if (white != null)
            {
                white.Position = sprite.Position;
                white.Scale = sprite.Scale;
                white.SetAnimationFrame(sprite.CurrentAnimationFrame);
            }

            if (hasBeenBroken && (FirstPlayer()?.Dead ?? true))
            {
                InterruptCollection();
            }
        }

        private void InterruptCollection()
        {
            Level level = Scene as Level;
            level.Frozen = false;
            level.CanRetry = true;
            level.FormationBackdrop.Display = false;
            SetTimeRate(1f);

            if (collectSound != null)
            {
                collectSound.RemoveSelf();
                collectSound = null;
            }

            if (smashRoutine != null)
            {
                smashRoutine.RemoveSelf();
                smashRoutine = null;
            }
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);

            // resume music when the player respawns.
            ResumeMusic();
        }

        public override void SceneEnd(object scene)
        {
            base.SceneEnd(scene);

            // we don't want the music to be muted forever!
            ResumeMusic();
        }

        private void StopMusic()
        {
            if (pauseMusicSnapshot == null)
            {
                pauseMusicSnapshot = Process.AudioSession.CreateSnapshot("snapshot:/music_mains_mute");
            }

            Process.AudioSession.BusStopAll("bus:/gameplay_sfx", immediate: true);
        }

        private void ResumeMusic()
        {
            if (pauseMusicSnapshot != null)
            {
                Process.AudioSession.ReleaseSnapshot(pauseMusicSnapshot);
                pauseMusicSnapshot = null;
            }
        }
    }
}
