using System.Collections;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Menus.Overworld;
using Celeste.Mods.CollabUtils2.Modules;

namespace Celeste.Mods.CollabUtils2.UI
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.CollabUtils2.UI.OuiHelper_EnterChapterPanel")]
    public sealed class ChapterPanelEntrySequence : OuiState
    {
        private readonly ISourceEngineContext engine;
        private readonly InGameOverworldProcess panels;

        public ChapterPanelEntrySequence(ISourceEngineContext engine) : base(engine)
        {
            this.engine = engine;
            panels = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single().Panels;
        }

        public override bool IsStart(OverworldSequence overworld, OverworldStartMode start)
        {
            if (!panels.StartRequested) return false;
            panels.StartRequested = false;
            OwnedComponents.Add(new SimulationCoroutine(engine, Enter(null)));
            return true;
        }

        public override IEnumerator Enter(OuiState from)
        {
            ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/ui/world_map/icon/select");
            Overworld.Goto<CollabChapterPanelSequence>();
            yield break;
        }

        public override IEnumerator Leave(OuiState next)
        {
            yield break;
        }
    }
}
