using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Randomness;
using Celeste.Core.Scheduling;
using Celeste.Everest.Entities.Memorials;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Particles.Sources;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Particles;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.CollabUtils2.Entities
{
    public sealed class HoloRainbowBerry : SceneEntityState
    {
        private readonly IOriginalStrawberryProcess process;
        private readonly Sprite sprite;
        private readonly Sprite desaturatedSprite;
        private readonly List<ParticleTypePresentation> particleColors = new List<ParticleTypePresentation>();
        private readonly float particleDelay;
        private readonly CustomMemorialTextState counterText;
        private float wobble;
        private int particleColor;
        public ParticleSystemPresentation Particles { get; private set; }

        public HoloRainbowBerry(IOriginalStrawberryProcess process, Vector2 position, int current, int total)
            : base(((ISourceEntitySourceDataEnvironment)process).EntitySourceDataRegistration, position)
        {
            this.process = process;
            float progress = (float)current / total;
            float transparency = Math.Min(.9f, progress), saturation = progress * .5f;
            for (int i = 0; i < 360; i += 60)
            {
                particleColors.Add(new ParticleTypePresentation(process.ParticleTypes.Strawberry.P_Glow)
                {
                    Color = ColorMath.HsvToPackedColor(i / 360f, saturation, 1) * transparency,
                    Color2 = ColorMath.HsvToPackedColor(i / 360f, saturation * .5f, 1) * transparency
                });
            }

            sprite = process.GameGraphics.SpriteFactory.Create("CollabUtils2_holoRainbowBerry");
            desaturatedSprite = process.GameGraphics.SpriteFactory.Create("CollabUtils2_desaturatedHoloRainbowBerry");
            sprite.Color *= transparency * saturation;
            desaturatedSprite.Color *= transparency * (1 - saturation);
            Add(sprite, desaturatedSprite);
            particleDelay = process.ScalarArithmetic.Lerp(1f, .08f, progress);
            if (current != 0)
            {
                var memorial = new CustomMemorialState(Position + new Vector2(1.5f, 82f), null, "", 16f, process);
                counterText = new CustomMemorialTextState(memorial, false, $"{current}/{total}", 16f, process);
            }
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            var level = (OriginalLevelSceneRoot)scene;
            if (counterText != null) level.Scheduler.QueueSourceEntity(counterText);
            Particles = new ParticleSystemPresentation(-50000, 800, process, (ISourceParticleTextureEnvironment)process)
            {
                Tags = Tags
            };
            level.Scheduler.QueueSourceEntity(Particles);
        }

        public override void Update()
        {
            base.Update();
            wobble += process.DeltaTime * 4f;
            sprite.Y = (float)Math.Sin(wobble) * 2f;
            desaturatedSprite.Y = sprite.Y;
            var level = (OriginalLevelSceneRoot)Scene;
            if (level.Scheduler.Clock.OnInterval(particleDelay, process))
            {
                Particles.Emit(particleColors[particleColor % particleColors.Count], Position +
                    SourceRandomMath.Range(((ISourceRandomEnvironment)process).Random, -Vector2.One * 6f,
                        Vector2.One * 6f));
                particleColor++;
            }

            if (counterText != null)
            {
                var players = level.Scheduler.Entities.GetTrackedEntities(typeof(RoomPlayerSession));
                var player = players.Count == 0 ? null : (RoomPlayerSession)players[0];
                counterText.Show = player != null && (player.Position - Position).LengthSquared() < 2500f;
            }
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            Particles?.RemoveSelf();
        }

        public override void SceneEnd(object scene)
        {
            base.SceneEnd(scene);
            Particles?.RemoveSelf();
        }
    }
}
