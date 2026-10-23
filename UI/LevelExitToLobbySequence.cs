using System.Collections;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Everest;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Session;
using Celeste.Game.Menus.Overworld;
using Celeste.Game.Platform.Statistics;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Scene.Entities;
using Celeste.Scene.Scenes;
using Celeste.Scene.Scheduling;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Presentation.Hud.Saving;
using Celeste.Simulation.Runtime.Session;
using Celeste.Game.Presentation.Hud;

namespace Celeste.Mods.CollabUtils2.UI
{
    [Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.CollabUtils2.UI.LevelExitToLobby")]
    public sealed class LevelExitToLobbySequence : SourceSceneState
    {
        private readonly CollabModule module;
        private readonly LevelExitMode exitMode;
        private string targetSID;
        private string targetRoom;
        private Vector2 targetSpawnPoint;
        private IOriginalGameplayProcess Gameplay => (IOriginalGameplayProcess)EngineContext;
        private IOriginalAreaKeyPolicy Areas => ((IOriginalAreaKeyEnvironment)EngineContext).AreaKeys;

        private IOriginalSaveSessionSelectionPolicy Selection =>
            ((IOriginalSaveSessionSelectionEnvironment)EngineContext).SaveSessionSelection;

        public LevelExitToLobbySequence(CollabModule module, ISourceEngineContext engine, LevelExitMode exitMode,
            ChapterCompletionSession currentSession)
            : base(engine, ((ISourceSceneCatalogEnvironment)engine).SceneCatalog,
                ((ISourceSceneUpdatePolicyEnvironment)engine).PlainScenePolicy)
        {
            this.module = module;
            this.exitMode = exitMode;
            Renderers.Add(new HudRendererState());
            ((IOriginalLevelExitEnvironment)engine).LevelExits.CreateLevelExit(exitMode, currentSession);
        }

        public override void Begin()
        {
            base.Begin();
            targetSID = module.Session.LobbySID;
            targetRoom = module.Session.LobbyRoom;
            targetSpawnPoint = new Vector2(module.Session.LobbySpawnPointX, module.Session.LobbySpawnPointY);
            module.Session.LobbySID = null;
            module.Session.LobbyRoom = null;
            module.Session.LobbySpawnPointX = 0f;
            module.Session.LobbySpawnPointY = 0f;
            module.Session.SaveAndReturnToLobbyAllowed = false;
            ((ISaveLoadIconEnvironment)EngineContext).SaveLoadIconProcess.Show(this);
            var coroutineHolder =
                new SceneEntityState(((ISourceEntitySourceDataEnvironment)EngineContext).EntitySourceDataRegistration);
            coroutineHolder.OwnedComponents.Add(new SimulationCoroutine(EngineContext, Routine()));
            SourceScene.QueueSourceEntity(coroutineHolder);
            ((IOriginalPlatformStatisticsEnvironment)EngineContext).Statistics.Store();
            Renderers.UpdateLists();
        }

        private IEnumerator Routine()
        {
            var save = Gameplay.SaveContext.CurrentSave;
            ChapterCompletionSession oldSession = Selection.GetCurrentSessionSafe(save);
            if (targetSID != null && Areas.GetAreaBySID(targetSID) == null)
            {
                ((IEverestLoggerProcessEnvironment)EngineContext).Logger.Log(EverestLogLevel.Warn,
                    "CollabUtils2/LevelExitToLobby",
                    "We are trying to return to a nonexistent lobby: " + targetSID + "!");
                targetSID = module.Identities.GetLobbyForLevelSet(Areas.GetLevelSet(Areas.GetSessionArea(oldSession)))
                            ?? module.Identities.GetLobbyForGym(Areas.GetSID(Areas.GetSessionArea(oldSession)));
                targetRoom = null;
            }

            ChapterCompletionSession nextSession = null;
            if (targetSID != null)
            {
                nextSession =
                    ((IOriginalSessionEnvironment)EngineContext).Sessions.CreateSession(
                        Areas.ToKey(Areas.GetAreaBySID(targetSID)));
                nextSession.FirstLevel = false;
                nextSession.StartedFromBeginning = false;
                nextSession.Level = targetRoom ?? nextSession.MapData.StartLevel().Name;
                nextSession.RespawnPoint = targetRoom == null ? (Vector2?)null : targetSpawnPoint;
                ((IOriginalSaveSessionStartEnvironment)EngineContext).SaveSessionStart.StartSession(save, nextSession);
            }

            var userIO = ((IOriginalUserIOEnvironment)EngineContext).UserIO;
            userIO.SaveHandler(true, true);
            while (userIO.Saving) yield return null;
            while (((ISaveLoadIconEnvironment)EngineContext).SaveLoadIconProcess.OnScreen) yield return null;
            Selection.SetCurrentSessionSafe(save, oldSession);
            if (targetSID == null)
                EngineContext.Frame.Scenes.NextScene = ((IOriginalOverworldLoaderEnvironment)EngineContext)
                    .OverworldLoaders.CreateOverworldLoader(
                        exitMode == LevelExitMode.Completed
                            ? OverworldStartMode.AreaComplete
                            : OverworldStartMode.AreaQuit);
            else
            {
                module.Return.PauseTimerOnReturn();
                OriginalLevelEntryRouting.Go(nextSession, false, EngineContext.Frame.Scenes,
                    ((IOriginalLevelEntryEnvironment)EngineContext).LevelEntries);
            }
        }
    }
}
