using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Stories.Dialogue;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Game.Save.Runtime;
using Celeste.Graphics.Animation;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Triggers;
using Celeste.Mods.CollabUtils2.UI;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.CollabUtils2.Entities
{
    [CustomEntity("CollabUtils2/SpeedBerry")]
    [SceneTracked]
    public sealed class SpeedBerry : OriginalStrawberryState
    {
        public ISourceEngineContext Engine { get; }
        public CollabModule Module { get; }
        public EntityData EntityData;
        public float BronzeTime;
        public float SilverTime;
        public float GoldTime;
        public bool TimeRanOut;
        public SpeedBerryTimerDisplay TimerDisplay;
        public bool restored;
        private readonly IOriginalStrawberryProcess process;
        private readonly ParticleTypePresentation silverGlow;
        private readonly ParticleTypePresentation bronzeGlow;
        private readonly bool isGhostBerry;
        private ParticleTypePresentation currentGlow;
        private Sprite speedSprite;
        private Vector2 transitionStart;
        private Vector2 transitionTarget;
        private bool forcePositionToTransitionTarget;

        public SpeedBerry(ISourceEngineContext engine, EntityData data, Vector2 offset, MapEntityId id)
            : this(engine, data, offset, id, false)
        {
        }

        public SpeedBerry(ISourceEngineContext engine, EntityData data, Vector2 offset, MapEntityId id, bool restored)
            : base(data, offset, id, (IOriginalStrawberryProcess)engine)
        {
            Engine = engine;
            process = (IOriginalStrawberryProcess)engine;
            Module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            EntityData = data;
            Golden = true;
            BronzeTime = data.Float("bronzeTime", 15f);
            SilverTime = data.Float("silverTime", 10f);
            GoldTime = data.Float("goldTime", 5f);
            this.restored = restored;
            isGhostBerry = process.CheckStrawberry(id);
            Follower.PersistentFollow = true;
            Add(new RoomTransitionListener
            {
                OnOutBegin = () =>
                {
                    var level = (OriginalLevelSceneRoot)Scene;
                    level.Session.DoNotLoad.Add(ID);
                    transitionStart = Position;
                    transitionTarget = new Vector2(
                        ScalarMath.Clamp(transitionStart.X, level.Bounds.Left + 8f, level.Bounds.Right - 8f,
                            ((ISourceExtremaEnvironment)engine).ExtremaArithmetic),
                        ScalarMath.Clamp(transitionStart.Y, level.Bounds.Top + 8f, level.Bounds.Bottom - 8f,
                            ((ISourceExtremaEnvironment)engine).ExtremaArithmetic));
                },
                OnOut = percent =>
                {
                    var player = GetPlayer(Scene);
                    if (player != null && PlayerInCollectTrigger(player))
                    {
                        float amount = process.EasingArithmetic.SineOut(percent);
                        Position = new Vector2(
                            process.ScalarArithmetic.Lerp(transitionStart.X, transitionTarget.X, amount),
                            process.ScalarArithmetic.Lerp(transitionStart.Y, transitionTarget.Y, amount));
                        forcePositionToTransitionTarget = true;
                    }
                }
            });
            silverGlow = new ParticleTypePresentation(process.ParticleTypes.Strawberry.P_Glow)
            {
                Color = new PackedColor(179, 175, 159), Color2 = new PackedColor(230, 220, 181)
            };
            bronzeGlow = new ParticleTypePresentation(process.ParticleTypes.Strawberry.P_Glow)
            {
                Color = new PackedColor(154, 91, 60), Color2 = new PackedColor(236, 175, 102)
            };
            var collider = Get<PlayerColliderComponentState>();
            var onPlayer = collider.OnCollide;
            collider.OnCollide = player =>
            {
                var session = ((OriginalLevelSceneRoot)Scene).Session;
                bool grabbedGolden = session.GrabbedGolden;
                onPlayer(player);
                session.GrabbedGolden = grabbedGolden;
            };
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            speedSprite.Color = PackedColor.White;
        }

        public override void Awake(object scene)
        {
            var session = ((OriginalLevelSceneRoot)scene).Session;
            var save = process.SaveContext.CurrentSave;
            if (!restored && (session.FurthestSeenLevel != session.Level ||
                              (!save.CheatMode && !((IOriginalSaveCounterEnvironment)Engine).SaveCounters
                                  .GetAreasSafe(save)[session.AreaID].Modes[session.Mode].Completed)))
            {
                RemoveSelf();
                return;
            }

            base.Awake(scene);
            if (TimerDisplay != null)
            {
                string rank = TimerDisplay.GetNextRank(out _).ToLowerInvariant();
                if (rank != "none") PlayAnimation("idle_" + rank);
            }
        }

        public override void Update()
        {
            currentGlow = null;
            if (Follower.HasLeader)
            {
                if (TimerDisplay == null)
                {
                    TimerDisplay = new SpeedBerryTimerDisplay(this);
                    ((ISourceScene)Scene).SourceScene.AddEntity(TimerDisplay);
                }

                var player = GetPlayer(Scene);
                if (player != null && !Module.SaveData.SpeedberryOptionMessageShown)
                {
                    Module.SaveData.SpeedberryOptionMessageShown = true;
                    ((ISourceScene)Scene).SourceScene.AddEntity(new EverestDialogCutsceneState(
                        "collabutils2_speedberry_optionmessage", player, false, Engine));
                }
            }

            if (TimerDisplay != null && !Collected)
            {
                if (BronzeTime < TimeSpan.FromTicks(TimerDisplay.GetSpentTime()).TotalSeconds)
                {
                    TimeRanOut = true;
                }
                else if (Follower.Leader?.Entity is RoomPlayerSession leader && !leader.Dead &&
                         PlayerInCollectTrigger(leader))
                {
                    TimerDisplay.EndTimer();
                    OnCollect();
                }
                else
                {
                    string rank = TimerDisplay.GetNextRank(out float nextTime).ToLowerInvariant();
                    if (rank == "bronze" && speedSprite.CurrentAnimationID != "explosion" &&
                        TimeSpan.FromTicks(TimerDisplay.GetSpentTime()).TotalMilliseconds + 1200 > nextTime * 1000)
                        PlayAnimation("explosion");
                    if (rank != "gold" && speedSprite.CurrentAnimationID != "explosion" &&
                        !speedSprite.CurrentAnimationID.Contains(rank))
                        PlayAnimation("transition_to_" + rank);
                    if (rank == "bronze") currentGlow = bronzeGlow;
                    else if (rank == "silver") currentGlow = silverGlow;
                }
            }

            if (TimeRanOut && !Module.Berries.TransitionJustTriggered) Dissolve();
            Module.Berries.TransitionJustTriggered = false;
            base.Update();
            if (forcePositionToTransitionTarget) Position = transitionTarget;
            if (speedSprite.CurrentAnimationID == "collect")
                PlayAnimation("collect_" + TimerDisplay.GetNextRank(out _).ToLowerInvariant());
        }

        protected override Sprite CreateSprite(bool ghost)
            => speedSprite = process.GameGraphics.SpriteFactory.Create(ghost
                ? "CollabUtils2_ghostSpeedBerry"
                : "CollabUtils2_speedBerry");

        protected override ParticleTypePresentation GetGlowParticle()
            => !isGhostBerry && currentGlow != null ? currentGlow : base.GetGlowParticle();

        protected override string CollectSound => "event:/SC2020_timedBerry_get";

        public void PrepareForDeath(OriginalLevelSceneRoot level)
        {
            Golden = false;
            StartPosition = level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Top)) + new Vector2(8, -16);
        }

        private void PlayAnimation(string animation)
        {
            if (speedSprite.Texture?.AtlasPath?.StartsWith("collectables/speedrun_tool_goldberry/") ?? false)
                process.GameGraphics.SpriteFactory.CreateOn(speedSprite, "CollabUtils2_speedBerry");
            speedSprite.Play(animation);
        }

        private void Dissolve()
        {
            var scene = ((ISourceScene)Scene).SourceScene;
            ((OriginalLevelSceneRoot)Scene).Session.DoNotLoad.Remove(ID);
            Collidable = false;
            foreach (var entity in scene.Entities.GetTrackedEntities(typeof(RoomPlayerSession)))
                ((RoomPlayerSession)entity).Die(Vector2.Zero, true, true);
            process.RumbleSession.Rumble(1, 1);
            Visible = false;
            RemoveSelf();
            scene.AddEntity(new SpeedBerryExplosionAnimation(Engine, Position));
        }

        public static RoomPlayerSession GetPlayer(object scene)
        {
            if (scene == null) return null;
            var players = ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities(typeof(RoomPlayerSession));
            return players.Count == 0 ? null : (RoomPlayerSession)players[0];
        }

        private static bool PlayerInCollectTrigger(RoomPlayerSession player)
        {
            foreach (var entity in ((ISourceScene)player.Scene).SourceScene.Entities
                     .GetTrackedEntities(typeof(SpeedBerryCollectTrigger)))
                if (SourceEntityCollision.Check(player, (ISourceEntityCollider)entity))
                    return true;
            return false;
        }

        private sealed class SpeedBerryExplosionAnimation : SceneEntityState
        {
            public SpeedBerryExplosionAnimation(ISourceEngineContext engine, Vector2 position)
                : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
            {
                Add(((IOriginalStrawberryProcess)engine).GameGraphics.SpriteFactory.Create(
                    "CollabUtils2_speedBerryExplosion"));
                Depth = -1000000;
            }
        }
    }
}
