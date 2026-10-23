using System.Collections;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Menus.Journal;
using Celeste.Game.Menus.Overworld;
using Celeste.Mods.CollabUtils2.Modules;

namespace Celeste.Mods.CollabUtils2.UI.Journal
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.CollabUtils2.UI.OuiHelper_EnterJournal")]
    public sealed class JournalEntrySequence : OuiState
    {
        private readonly ISourceEngineContext engine;
        private readonly InGameOverworldProcess panels;

        public JournalEntrySequence(ISourceEngineContext engine) : base(engine)
        {
            this.engine = engine;
            panels = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single().Panels;
        }

        public override bool IsStart(OverworldSequence overworld, OverworldStartMode start)
        {
            if (!panels.JournalStartRequested) return false;
            panels.JournalStartRequested = false;
            OwnedComponents.Add(new SimulationCoroutine(engine, Enter(null)));
            return true;
        }

        public override IEnumerator Enter(OuiState from)
        {
            ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/ui/world_map/journal/select");
            Overworld.Goto<OuiJournalSequence>();
            yield break;
        }

        public override IEnumerator Leave(OuiState next)
        {
            yield break;
        }
    }
}
