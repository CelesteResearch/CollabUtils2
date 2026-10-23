using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.CollabUtils2.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.CollabUtils2.Triggers
{
    [CustomEntity("CollabUtils2/RainbowBerryUnlockCutsceneTrigger")]
    public sealed class RainbowBerryUnlockCutsceneTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly string levelSet;
        private readonly string maps;
        private RainbowBerry berry;

        public RainbowBerryUnlockCutsceneTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            levelSet = data.Attr("levelSet");
            maps = data.Attr("maps");
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            berry = ((OriginalLevelSceneRoot)scene).Scheduler.Entities.Entities.OfType<RainbowBerry>()
                .FirstOrDefault(value => value.MatchesRainbowBerryTriggerWithSettings(levelSet, maps));
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            if (berry?.HologramForCutscene != null && player != null)
            {
                var level = (OriginalLevelSceneRoot)Scene;
                level.Scheduler.QueueSourceEntity(new RainbowBerryUnlockCutscene(engine, berry,
                    berry.HologramForCutscene, berry.CutsceneTotalBerries));
                berry.HologramForCutscene = null;
                berry.Module.SaveData.CombinedRainbowBerries.Add(berry.GetCombinedRainbowId(level));
            }

            RemoveSelf();
        }
    }
}
