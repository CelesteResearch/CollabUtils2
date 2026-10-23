using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Runtime.Events;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Menus.Journal;
using Celeste.Game.Menus.Overworld;
using Celeste.Mods.CollabUtils2.Modules;
using MonoMod.ModInterop;
using Celeste.Mods.CollabUtils2.UI.Journal.Stickers;

namespace Celeste.Mods.CollabUtils2.UI.Journal
{
    public sealed class CollabJournalProcess : IDisposable
    {
        private readonly CollabModule module;
        private readonly ISourceEngineContext engine;
        private readonly CollabJournalStickerProcess stickers;
        private readonly Dictionary<string, Action<OuiJournalSequence, string, bool>> editors = new();
        private bool vanillaJournal = true;
        private bool showOnlyDiscovered;
        private EverestGameEventsProcess Events => ((IEverestGameEventsEnvironment)engine).GameEvents;

        public CollabJournalProcess(CollabModule module, ISourceEngineContext engine)
        {
            this.module = module;
            this.engine = engine;
            stickers = CollabJournalStickerProcess.For(module);
        }

        public void Load()
        {
            editors.Clear();
            stickers.Load();
            Events.OnJournalEnter += OnJournalEnter;
            typeof(CollabJournalExports).ModInterop();
        }

        public void PrepareEntry(bool vanilla, bool discovered)
        {
            vanillaJournal = vanilla;
            showOnlyDiscovered = discovered;
        }

        public void AddJournalEditor(string collabID, Action<OuiJournalSequence, string, bool> editor) =>
            editors[collabID] = editor;

        public void RemoveJournalEditor(string collabID) => editors.Remove(collabID);

        private void OnJournalEnter(OuiJournalSequence journal, OuiState from)
        {
            if (vanillaJournal) return;
            IOriginalAreaData forceArea = module.Panels.ForceArea;
            if (forceArea == null) return;
            journal.Pages.Clear();
            journal.Pages.Add(new OuiJournalCoverWithStickers(module, journal));
            journal.Pages.AddRange(OuiJournalCollabProgressInLobby.GeneratePages(module, journal, forceArea.LevelSet,
                showOnlyDiscovered));
            var atlas = ((IOriginalMountainEnvironment)engine).MountainResources.Journal;
            if (atlas.Has("collabLobbyMaps/" + forceArea.LevelSet))
                journal.Pages.Add(new OuiJournalLobbyMap(journal, atlas["collabLobbyMaps/" + forceArea.LevelSet]));
            string collabID = module.Identities.GetCollabNameForSID(forceArea.SID);
            if (module.Identities.IsCollabLevelSet(forceArea.LevelSet) && collabID != null &&
                editors.TryGetValue(collabID, out var editor))
                editor(journal, forceArea.LevelSet, showOnlyDiscovered);
            if (journal.Pages.ElementAtOrDefault(0) is OuiJournalCoverWithStickers cover)
                cover.Redraw(journal.CurrentPageBuffer);
            vanillaJournal = true;
            showOnlyDiscovered = false;
        }

        public void Dispose()
        {
            Events.OnJournalEnter -= OnJournalEnter;
            stickers.Dispose();
            editors.Clear();
            vanillaJournal = true;
            showOnlyDiscovered = false;
        }
    }
}
