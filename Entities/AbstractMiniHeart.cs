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
    // This is all code in common between mini hearts and fake mini hearts.
    public abstract class AbstractMiniHeart : SceneEntityState
    {
        protected static readonly int[] animationFrames = new int[]
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

        protected readonly ISourceEngineContext engine;
        protected IOriginalGameplayProcess Process => (IOriginalGameplayProcess)engine;

        protected CollabModule Module =>
            ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();

        protected Player FirstPlayer()
        {
            var players = ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(typeof(Player));
            return players.Count == 0 ? null : (Player)players[0];
        }

        protected Sprite sprite;
        private string spriteName;
        private bool refillDash;
        private bool requireDashToBreak;
        private bool noGhostSprite;
        private string particleColor;
        private bool playPulseSound;

        protected Wiggler scaleWiggler;

        private Vector2 moveWiggleDir;
        private Wiggler moveWiggler;

        private float bounceSfxDelay;

        protected VertexLight light;
        protected BloomPoint bloom;
        private ParticleType shineParticle;

        private HoldableCollider holdableCollider;

        public AbstractMiniHeart(ISourceEngineContext engine, EntityData data, Vector2 position, EntityID gid)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + position)
        {
            this.engine = engine;
            spriteName = data.Attr("sprite");
            refillDash = data.Bool("refillDash", defaultValue: true);
            requireDashToBreak = data.Bool("requireDashToBreak", defaultValue: true);
            noGhostSprite = data.Bool("noGhostSprite", defaultValue: false);
            particleColor = data.Attr("particleColor");
            playPulseSound = data.Bool("playPulseSound", defaultValue: true);

            Collider = new Celeste.Physics.Collision.Shapes.HitboxColliderGeometry(12f, 12f, -6f, -6f);

            Add(scaleWiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(0.5f, 4f,
                f => { sprite.Scale = Vector2.One * (1f + f * 0.3f); }));
            moveWiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(0.8f, 2f);
            moveWiggler.StartZero = true;
            Add(moveWiggler);

            Add(new PlayerColliderComponentState(OnPlayer));
            Add(holdableCollider = new HoldableCollider(OnHoldable));
        }

        public override void Added(object scene)
        {
            base.Added(scene);

            var area = ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSessionArea((scene as Level).Session);

            string spritePath = "CollabUtils2/miniheart/" + spriteName + "/";
            bool alreadyCollectedInSave =
                ((IOriginalSaveCounterEnvironment)engine).SaveCounters.GetAreasSafe(Process.SaveContext.CurrentSave)
                [area.ID].Modes[(int)area.Mode].HeartGem;
            if (alreadyCollectedInSave && !noGhostSprite)
            {
                // use the ghost sprite specific to the heart: instead of reading 00.png, read ghost00.png
                spritePath += "ghost";
                if (!((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas.Has(spritePath + "00"))
                {
                    // if those sprites are missing, use the default ghost heart instead
                    spritePath = "CollabUtils2/miniheart/ghost/ghost";
                }
            }

            Add(sprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                spritePath));
            sprite.AddLoop("idle", "", 0.1f, animationFrames);
            sprite.Play("idle");
            sprite.CenterOrigin();
            sprite.OnLoop = anim =>
            {
                if (Visible)
                {
                    if (playPulseSound)
                    {
                        ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/SC2020_heartShard_pulse",
                            Position);
                    }

                    scaleWiggler.Start();
                    (Scene as Level).Displacement.AddBurst(Position + sprite.Position, 0.35f, 4f, 24f, 0.25f);
                }
            };


            Color heartColor;
            switch (spriteName)
            {
                case "beginner":
                default:
                    heartColor = Color.Aqua;
                    shineParticle = Process.ParticleTypes.HeartGem.P_BlueShine;
                    break;
                case "intermediate":
                    heartColor = Color.Red;
                    shineParticle = Process.ParticleTypes.HeartGem.P_RedShine;
                    break;
                case "advanced":
                    heartColor = Color.Gold;
                    shineParticle = Process.ParticleTypes.HeartGem.P_GoldShine;
                    break;
                case "expert":
                    heartColor = Color.Orange;
                    shineParticle = new ParticleType(Process.ParticleTypes.HeartGem.P_BlueShine)
                    {
                        Color = Color.Orange
                    };
                    break;
                case "grandmaster":
                    heartColor = Color.DarkViolet;
                    shineParticle = new ParticleType(Process.ParticleTypes.HeartGem.P_BlueShine)
                    {
                        Color = Color.DarkViolet
                    };
                    break;
            }

            if (!string.IsNullOrEmpty(particleColor))
            {
                shineParticle = new ParticleType(Process.ParticleTypes.HeartGem.P_BlueShine)
                {
                    Color = SourceHexColor.HexToColor(particleColor)
                };
            }

            if (alreadyCollectedInSave && !noGhostSprite)
            {
                heartColor = Color.White * 0.8f;
                shineParticle = new ParticleType(Process.ParticleTypes.HeartGem.P_BlueShine)
                {
                    Color = SourceHexColor.HexToColor("7589FF")
                };
            }

            heartColor = Color.Lerp(heartColor, Color.White, 0.5f);
            Add(light = new VertexLight(engine, heartColor, 1f, 32, 64));
            Add(bloom = new BloomPoint(0.75f, 16f));
        }

        public override void Update()
        {
            base.Update();
            bounceSfxDelay -= engine.DeltaTime;
            sprite.Position = moveWiggleDir * moveWiggler.Value * -8f;

            if (Visible && ((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.1f, engine))
            {
                ((Level)Scene).Particles.Emit(shineParticle, 1, Center + sprite.Position, Vector2.One * 4f);
            }
        }

        private void OnPlayer(Player player)
        {
            Level level = Scene as Level;
            if (player.DashAttacking || !requireDashToBreak)
            {
                // player broke the heart
                HeartBroken(player, null, level);
            }
            else
            {
                // player bounces on the heart
                int dashCount = player.Data.Dashes;
                player.PointBounce(Center);
                if (!refillDash)
                {
                    player.Data.Dashes = dashCount;
                }

                moveWiggler.Start();
                scaleWiggler.Start();
                moveWiggleDir =
                    ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(Center - player.Center,
                        Vector2.UnitY);
                ((ISourceRumbleEnvironment)engine).RumbleSession.Rumble(1, 1);
                if (bounceSfxDelay <= 0f)
                {
                    ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                        "event:/game/general/crystalheart_bounce", Position);
                    bounceSfxDelay = 0.1f;
                }
            }
        }

        public void OnHoldable(Holdable holdable)
        {
            Player player = FirstPlayer();
            if (player != null && holdable.Dangerous(holdableCollider))
            {
                HeartBroken(player, holdable, ((Level)Scene));
            }
        }

        protected abstract void HeartBroken(Player player, Holdable holdable, Level level);
    }
}
