using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Maps;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Game.Save.Runtime;
using Celeste.Graphics.Animation;
using Celeste.Mods.CollabUtils2.Triggers;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.CollabUtils2.Entities
{
    [CustomEntity("CollabUtils2/SilverBerry")]
    [RegisterStrawberry(tracked: false, blocksCollection: true)]
    public sealed class SilverBerry : OriginalStrawberryState
    {
        private readonly IOriginalStrawberryProcess process;
        private readonly bool alwaysSpawn;
        private readonly bool spawnedThroughGiveSilver;
        private readonly bool isGhostBerry;
        private readonly ParticleTypePresentation silverGlow;
        private readonly ParticleTypePresentation silverGhostGlow;
        private Sprite silverSprite;

        public SilverBerry(ISourceEngineContext engine, EntityData data, Vector2 offset, MapEntityId id)
            : this((IOriginalStrawberryProcess)engine, data, offset, id, false)
        {
        }

        public SilverBerry(IOriginalStrawberryProcess process, EntityData data, Vector2 offset, MapEntityId id,
            bool spawnedThroughGiveSilver) : base(data, offset, id, process)
        {
            this.process = process;
            this.spawnedThroughGiveSilver = spawnedThroughGiveSilver;
            isGhostBerry = process.CheckStrawberry(id);
            alwaysSpawn = data.Bool("alwaysSpawn");
            Golden = true;
            silverGlow = new ParticleTypePresentation(process.ParticleTypes.Strawberry.P_Glow)
            {
                Color = new PackedColor(186, 187, 192),
                Color2 = new PackedColor(106, 132, 151)
            };
            silverGhostGlow = new ParticleTypePresentation(process.ParticleTypes.Strawberry.P_Glow)
            {
                Color = new PackedColor(129, 142, 158),
                Color2 = new PackedColor(54, 88, 91)
            };
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            // Upstream replaces the vanilla sprite after vanilla's ghost tint;
            // the replacement silver sprite therefore keeps its default white tint.
            silverSprite.Color = PackedColor.White;
            var session = ((OriginalLevelSceneRoot)scene).Session;
            var save = process.SaveContext.CurrentSave;
            if (!spawnedThroughGiveSilver &&
                ((session.FurthestSeenLevel != session.Level && session.Deaths != 0) ||
                 (!alwaysSpawn && !save.CheatMode &&
                  !((IOriginalSaveCounterEnvironment)process).SaveCounters.GetAreasSafe(save)[session.AreaID]
                      .Modes[session.Mode].Completed)))
            {
                RemoveSelf();
            }
        }

        protected override Sprite CreateSprite(bool ghost)
            => silverSprite = process.GameGraphics.SpriteFactory.Create(ghost
                ? "CollabUtils2_ghostSilverBerry"
                : "CollabUtils2_silverBerry");

        protected override ParticleTypePresentation GetGlowParticle()
            => isGhostBerry ? silverGhostGlow : silverGlow;

        protected override string CollectSound => "event:/SC2020_silverBerry_get";

        protected override bool CanCollectGolden(RoomPlayerSession player)
        {
            if (base.CanCollectGolden(player)) return true;
            foreach (var entity in ((ISourceScene)player.Scene).SourceScene.Entities
                     .GetTrackedEntities(typeof(SilverBerryCollectTrigger)))
            {
                if (SourceEntityCollision.Check(player, (ISourceEntityCollider)entity)) return true;
            }

            return false;
        }
    }
}
