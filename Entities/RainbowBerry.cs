using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Maps;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Game.Save.Runtime;
using Celeste.Graphics.Animation;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;

namespace Celeste.Mods.CollabUtils2.Entities
{
    [CustomEntity("CollabUtils2/RainbowBerry")]
    [RegisterStrawberry(tracked: false, blocksCollection: false)]
    public sealed class RainbowBerry : OriginalStrawberryState
    {
        private readonly string levelSet;
        private readonly string mapsRaw;
        private readonly string[] maps;
        private readonly int? requiredBerries;
        private readonly IOriginalStrawberryProcess process;
        private Sprite rainbowSprite;
        internal HoloRainbowBerry HologramForCutscene;
        internal int CutsceneTotalBerries;
        internal CollabModule Module { get; }
        internal ISourceEngineContext Engine => process;

        public RainbowBerry(ISourceEngineContext engine, EntityData data, Vector2 offset, MapEntityId id)
            : base(data, offset, id, (IOriginalStrawberryProcess)engine)
        {
            process = (IOriginalStrawberryProcess)engine;
            Module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            levelSet = data.Attr("levelSet");
            mapsRaw = data.Attr("maps");
            if (!string.IsNullOrEmpty(mapsRaw)) maps = mapsRaw.Split(',').Select(map => levelSet + "/" + map).ToArray();
            if (!string.IsNullOrEmpty(data.Attr("requires"))) requiredBerries = int.Parse(data.Attr("requires"));
        }

        protected override Sprite CreateSprite(bool ghost)
            => rainbowSprite = process.GameGraphics.SpriteFactory.Create(ghost
                ? "CollabUtils2_ghostRainbowBerry"
                : "CollabUtils2_rainbowBerry");

        protected override string CollectSound => "event:/SC2020_rainbowBerry_get";

        public override void Added(object scene)
        {
            base.Added(scene);
            rainbowSprite.Color = Celeste.Core.Display.PackedColor.White;
            var bloom = Get<BloomPointState>();
            bloom.Alpha = .5f;
            if (!Module.Maps.SilverBerries.TryGetValue(levelSet, out var silvers)) return;
            int missing = 0, total = 0;
            var areas = ((IOriginalAreaKeyEnvironment)process).AreaKeys;
            var stats =
                ((IOriginalSaveCounterEnvironment)process).SaveCounters.GetAreasSafe(process.SaveContext.CurrentSave);
            foreach (var silver in silvers)
            {
                if (maps != null && !maps.Contains(silver.Key)) continue;
                total++;
                if (!stats[areas.GetAreaBySID(silver.Key).ID].Modes[0].Strawberries.Contains(silver.Value)) missing++;
            }

            if (requiredBerries.HasValue)
            {
                int collected = total - missing;
                missing = Math.Max(0, requiredBerries.Value - collected);
                total = requiredBerries.Value;
            }

            var level = (OriginalLevelSceneRoot)scene;
            if (missing != 0)
            {
                level.Scheduler.QueueSourceEntity(new HoloRainbowBerry(process, Position, total - missing, total));
                RemoveSelf();
            }
            else if (!Module.SaveData.CombinedRainbowBerries.Contains(GetCombinedRainbowId(level)))
            {
                var hologram = new HoloRainbowBerry(process, Position, total, total)
                {
                    Tags = ((Celeste.Game.Execution.Startup.IOriginalGameTagsEnvironment)process).Tags.FrozenUpdate
                        .Value
                };
                level.Scheduler.QueueSourceEntity(hologram);
                Visible = Collidable = false;
                bloom.Visible = Get<VertexLightState>().Visible = false;
                HologramForCutscene = hologram;
                CutsceneTotalBerries = total;
            }
        }

        public string GetCombinedRainbowId(OriginalLevelSceneRoot level)
            => maps != null
                ? string.Join(",", maps)
                : ((IOriginalAreaKeyEnvironment)process).AreaKeys.GetSID(
                    ((IOriginalAreaKeyEnvironment)process).AreaKeys.GetSessionArea(level.Session));

        public bool MatchesRainbowBerryTriggerWithSettings(string requiredLevelSet, string requiredMaps)
            => (string.IsNullOrEmpty(requiredLevelSet) || levelSet == requiredLevelSet)
               && (string.IsNullOrEmpty(requiredMaps) || mapsRaw == requiredMaps);
    }
}
