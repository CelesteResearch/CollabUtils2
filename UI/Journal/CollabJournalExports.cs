using System;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Menus.Journal;
using Celeste.Mods.CollabUtils2.Modules;
using MonoMod.ModInterop;

namespace Celeste.Mods.CollabUtils2.UI.Journal
{
    [ModExportName("CollabUtils2.JournalHelper")]
    public static class CollabJournalExports
    {
        private static CollabJournalProcess Get(ISourceEngineContext engine) =>
            ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single().Journals;

        public static void AddJournalEditor(ISourceEngineContext engine, string collabID,
            Action<OuiJournalSequence, string, bool> editor) =>
            Get(engine).AddJournalEditor(collabID, editor);

        public static void RemoveJournalEditor(ISourceEngineContext engine, string collabID) =>
            Get(engine).RemoveJournalEditor(collabID);
    }
}
