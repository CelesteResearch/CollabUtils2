using System.Linq;
using System.Numerics;
using Celeste.Core.Geometry;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Components.Talking;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.CollabUtils2.Triggers.Journal
{
    [CustomEntity("CollabUtils2/JournalTrigger")]
    public sealed class JournalTrigger : SourceTriggerEntityState
    {
        private readonly CollabModule module;
        private readonly OriginalTalkComponent talkComponent;

        public JournalTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height,
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            string levelSet = data.Attr("levelset");
            bool vanilla = data.Bool("vanillaJournal", false);
            bool discovered = data.Bool("showOnlyDiscovered", false);
            OwnedComponents.Add(talkComponent = new OriginalTalkComponent((IOriginalTalkProcess)engine,
                new IntRect(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? data.Nodes[0] - data.Position : new Vector2(data.Width / 2f, data.Height / 2f),
                player =>
                {
                    module.Journals.PrepareEntry(vanilla, discovered);
                    module.Panels.OpenJournal(player, levelSet);
                }) { PlayerMustBeFacing = false });
        }

        public override void Update()
        {
            base.Update();
            talkComponent.Enabled = !module.Panels.IsOpen;
        }
    }
}
