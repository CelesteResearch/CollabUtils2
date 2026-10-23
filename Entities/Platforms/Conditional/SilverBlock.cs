using System.Numerics;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Walls.Conditional;
using Celeste.Mods.CollabUtils2.Entities;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.CollabUtils2.Entities.Platforms.Conditional
{
    [CustomEntity("CollabUtils2/SilverBlock=Load")]
    public class SilverBlock : GoldenBlockState
    {
        public Celeste.Core.Scheduling.ISourceEngineContext EngineContext => SourceWorld.EngineContext;

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new SilverBlock(data, offset,
            ((ISourcePlatformCollisionScene)level).SolidWorldFactory);

        public SilverBlock(EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds)
            : base(data.Position + offset, data.Width, data.Height, worlds.Create(data.Position + offset, false))
        {
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            bool hasSilverBerry = false;
            foreach (var entity in ((ISourceScene)scene).SourceScene.Entities)
            {
                if (entity is SilverBerry item && item.Follower.Leader != null)
                {
                    hasSilverBerry = true;
                    break;
                }
            }

            if (!hasSilverBerry)
                RemoveSelf();
        }
    }
}
