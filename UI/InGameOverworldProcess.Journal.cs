using System;
using System.Collections;
using System.Linq;
using Celeste.Everest.Menus.Journal;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Session;
using Celeste.Game.Menus.Journal;
using Celeste.Game.Menus.Overworld;
using Celeste.Simulation.Runtime.Session;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed partial class InGameOverworldProcess
    {
        internal bool JournalStartRequested { get; set; }

        public void OpenJournal(RoomPlayerSession player, string levelSet)
        {
            player.Drop();
            var area = ((IOriginalSessionConstructionProcess)Engine).AreaTable.Areas
                .FirstOrDefault(candidate => candidate.LevelSet == levelSet) ?? Areas.GetAreaById(0);
            Open(player, area, default, false, false, journal: true);
        }

        private void LoadJournalEntry()
        {
            hooks.Add(new Hook(typeof(EverestJournalSequence).GetMethod(nameof(EverestJournalSequence.Enter)),
                (Func<Func<EverestJournalSequence, OuiState, IEnumerator>, EverestJournalSequence, OuiState,
                    IEnumerator>)
                ((original, journal, from) => ReferenceEquals(wrapper?.WrappedScene, journal.Overworld)
                    ? EnterForcedJournal(original(journal, from))
                    : original(journal, from))));
        }

        private IEnumerator EnterForcedJournal(IEnumerator original)
        {
            var save = Gameplay.SaveContext.CurrentSave;
            var forceArea = ForceArea;
            if (forceArea != null)
            {
                lastArea = Selection.GetLastAreaSafe(save);
                Selection.SetLastAreaSafe(save, Areas.ToKey(forceArea));
            }

            while (original.MoveNext()) yield return original.Current;
            if (forceArea != null && lastArea != null)
            {
                Selection.SetLastAreaSafe(save, lastArea.Value);
                lastArea = null;
            }
        }
    }
}
