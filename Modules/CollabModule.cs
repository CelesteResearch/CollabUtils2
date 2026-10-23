using System;
using System.IO;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Interop;
using Celeste.Everest.Content.Maps.Processing;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Execution.Areas;
using Celeste.Mods.CollabUtils2.Maps;
using Celeste.Mods.CollabUtils2.Persistence;
using Celeste.Mods.CollabUtils2.UI;
using Celeste.Mods.CollabUtils2.Entities;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Save.Runtime;

namespace Celeste.Mods.CollabUtils2.Modules
{
    // Partial port: metadata and the in-game chapter panel are implemented.
    // Other CollabUtils2 entities, menu integration, and visual extensions remain pending.
    public sealed partial class CollabModule : SourceEverestModule
    {
        private UI.MapMenus.CollabMapMenuHooks mapMenuHooks;
        private IDisposable lobbyInterop;
        private Persistence.CollabSaveInitializationHooks saveInitialization;
        private CustomCrystalHeartHooks crystalHearts;
        private CollabCounterHooks counterHooks;

        public CollabMapCatalog Maps { get; } = new CollabMapCatalog();
        public CollabIdentityRegistry Identities { get; }
        public InGameOverworldProcess Panels { get; }
        public UI.Journal.CollabJournalProcess Journals { get; }
        public CollabReturnProcess Return { get; }
        public CollabBerryHooks Berries { get; }
        public RainbowBerryHooks RainbowBerries { get; }
        public MiniHeartDoorAssistProcess DoorAssist { get; }
        public override Type SettingsType => typeof(CollabSettings);
        public CollabSettings Settings => (CollabSettings)_Settings;
        public override Type SessionType => typeof(CollabSession);
        public override Type SaveDataType => typeof(CollabSaveData);
        public CollabSession Session => (CollabSession)_Session;
        public CollabSaveData SaveData => (CollabSaveData)_SaveData;

        public CollabModule(ISourceEngineContext engine) : base(engine)
        {
            Identities = new CollabIdentityRegistry(((IOriginalAreaKeyEnvironment)engine).AreaKeys);
            Panels = new InGameOverworldProcess(this, engine);
            Journals = new UI.Journal.CollabJournalProcess(this, engine);
            Return = new CollabReturnProcess(this, engine);
            Berries = new CollabBerryHooks(engine);
            RainbowBerries = new RainbowBerryHooks(engine);
            DoorAssist = new MiniHeartDoorAssistProcess(engine);
        }

        public override void Load()
        {
            LoadLobbyMaps();
            ReadCollabNames();
            saveInitialization = new Persistence.CollabSaveInitializationHooks(SourceEngine, Identities);
            saveInitialization.Load();
            lobbyInterop = ((ISourceModInteropEnvironment)SourceEngine).ModInterop.RegisterOwned(
                new SourceModInteropActualTypeBinding(typeof(CollabLobbyExports), new CollabLobbyExports(Identities)));
            mapMenuHooks = new UI.MapMenus.CollabMapMenuHooks(SourceEngine, Identities);
            mapMenuHooks.Load();
            Panels.Load();
            Return.Load();
            crystalHearts = new CustomCrystalHeartHooks(SourceEngine, Identities);
            crystalHearts.Load();
            counterHooks = new CollabCounterHooks(this, SourceEngine);
            counterHooks.Load();
            Journals.Load();
            Berries.Load();
            RainbowBerries.Load();
            DoorAssist.Load();
            LoadGoldenRespawns();
            LoadSilverBlocks();
        }

        public override void Initialize()
        {
            ReadCollabNames();
        }

        public override void Unload()
        {
            counterHooks?.Dispose();
            counterHooks = null;
            crystalHearts?.Dispose();
            crystalHearts = null;
            saveInitialization?.Dispose();
            saveInitialization = null;
            lobbyInterop?.Dispose();
            lobbyInterop = null;
            mapMenuHooks?.Dispose();
            mapMenuHooks = null;
            UnloadLobbyMaps();
            UnloadGoldenRespawns();
            UnloadSilverBlocks();
            DoorAssist.Dispose();
            RainbowBerries.Dispose();
            Berries.Dispose();
            Journals.Dispose();
            Return.Dispose();
            Panels.Dispose();
            Maps.Clear();
            Identities.Clear();
        }

        public override void DeserializeSession(int index, byte[] data)
        {
            base.DeserializeSession(index, data);
            var currentSave = ((IOriginalGameplayProcess)SourceEngine).SaveContext.CurrentSave;
            if (data == null && currentSave != null)
                Return.OnSessionCreated(((IOriginalSaveSessionSelectionEnvironment)SourceEngine).SaveSessionSelection
                    .GetCurrentSessionSafe(currentSave));
            var save = ((IOriginalGameplayProcess)SourceEngine).SaveContext.CurrentSave;
            if (save != null)
                Panels.ApplyPendingReturnSession(((IOriginalSaveSessionSelectionEnvironment)SourceEngine)
                    .SaveSessionSelection.GetCurrentSessionSafe(save));
        }

        public override void PrepareMapDataProcessors(EverestMapDataFixup context)
        {
            context.Add(new CollabMapDataProcessor(Maps, Identities.GetCollabNameForSID));
        }

        private void ReadCollabNames()
        {
            foreach (var content in ((IEverestContentRegistryEnvironment)SourceEngine).SourceContentRegistry.Mods)
            {
                if (!content.Map.TryGetValue("CollabUtils2CollabID", out var asset)) continue;
                using (var reader = new StreamReader(asset.Stream))
                {
                    Identities.Register(reader.ReadToEnd());
                }
            }
        }
    }
}
