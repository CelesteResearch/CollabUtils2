using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Persistence.GameSaves;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Save.Data;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Maps;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.Persistence
{
    public sealed class CollabSaveInitializationHooks : IDisposable
    {
        private readonly IOriginalSaveInitializationPolicy initialization;
        private readonly IOriginalSaveSessionSelectionPolicy selections;
        private readonly IOriginalAreaKeyPolicy areas;
        private readonly CollabIdentityRegistry identities;
        private Hook afterInitialize;

        public CollabSaveInitializationHooks(ISourceEngineContext engine, CollabIdentityRegistry identities)
        {
            initialization = ((IOriginalSaveInitializationEnvironment)engine).SaveInitialization;
            selections = ((IOriginalSaveSessionSelectionEnvironment)engine).SaveSessionSelection;
            areas = ((IOriginalAreaKeyEnvironment)engine).AreaKeys;
            this.identities = identities;
        }

        public void Load()
        {
            afterInitialize = new Hook(
                typeof(EverestSaveRuntimePolicy).GetMethod(nameof(EverestSaveRuntimePolicy.AfterInitialize)),
                (Action<Action<EverestSaveRuntimePolicy, OriginalSaveDataState, IOriginalSaveInitializationServices>,
                    EverestSaveRuntimePolicy, OriginalSaveDataState, IOriginalSaveInitializationServices>)((original,
                    policy, save, services) =>
                {
                    original(policy, save, services);
                    if (!ReferenceEquals(policy, initialization)) return;
                    foreach (OriginalLevelSetStats stats in save.LevelSets)
                    {
                        if (!stats.Name.EndsWith("/0-Lobbies", StringComparison.Ordinal) ||
                            !identities.IsCollabLobby(stats.Name + "/")) continue;
                        string collab = stats.Name.Substring(0, stats.Name.Length - "/0-Lobbies".Length);
                        if (stats.UnlockedAreas > 0 || areas.GetAreaBySID(collab + "/0-Lobbies/0-Prologue") == null)
                            stats.UnlockedAreas = stats.Areas.Count - 1;
                    }

                    var session = selections.GetCurrentSessionSafe(save);
                    if (session != null && session.InArea) return;
                    string lobby = identities.GetLobbyForLevelSet(areas.GetLevelSet(selections.GetLastAreaSafe(save)));
                    if (lobby != null) selections.SetLastAreaSafe(save, areas.ToKey(areas.GetAreaBySID(lobby)));
                }));
        }

        public void Dispose()
        {
            afterInitialize?.Dispose();
            afterInitialize = null;
        }
    }
}
