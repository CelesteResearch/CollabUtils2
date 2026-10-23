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
    [CustomEntity("CollabUtils2/FakeMiniHeart")]
    public class FakeMiniHeart : AbstractMiniHeart
    {
        private float respawnTimer;
        private bool playBreakSound;
        private string flagOnBreak;

        public FakeMiniHeart(ISourceEngineContext engine, EntityData data, Vector2 position, EntityID gid)
            : base(engine, data, position, gid)
        {
            playBreakSound = data.Bool("playBreakSound", false);
            flagOnBreak = data.Attr("flagOnBreak");
        }

        public override void Update()
        {
            base.Update();

            if (respawnTimer > 0f)
            {
                respawnTimer -= engine.DeltaTime;
                if (respawnTimer <= 0f)
                {
                    Collidable = (Visible = true);
                    scaleWiggler.Start();
                }
            }
        }

        protected override void HeartBroken(Player player, Holdable holdable, Level level)
        {
            if (holdable != null)
            {
                MakeDisappear(player, Vector2Geometry.Angle(holdable.GetSpeed()));
            }
            else
            {
                MakeDisappear(player, Vector2Geometry.Angle(player.Speed));
            }
        }

        private void MakeDisappear(Player player, float angle)
        {
            if (Collidable)
            {
                Collidable = (Visible = false);
                respawnTimer = 3f;
                OriginalCelesteFreeze.Freeze(engine, 0.05f);
                ((Level)Scene).Shake();
                OriginalSlashFxState.Burst(engine, Position, angle);
                player?.RefillDash();

                if (playBreakSound)
                {
                    ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/game/general/diamond_touch",
                        Position);
                }

                if (!string.IsNullOrEmpty(flagOnBreak))
                {
                    ((Level)Scene).Session.SetFlag(flagOnBreak, true);
                }
            }
        }
    }
}
