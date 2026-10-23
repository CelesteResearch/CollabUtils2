using System;
using System.Collections;
using System.Linq;
using Celeste.Everest.Chapters.Flow;
using Celeste.Game.Settings;
using Celeste.Game.Menus.Journal;
using Celeste.Game.Menus.Overworld;
using MonoMod.Utils;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Celeste.Core.Scheduling;
using Celeste.Everest;
using Celeste.Scene.Lifecycle;
using Celeste.Everest.Content.Reloading;
using Celeste.Everest.Content.Serialization;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Everest.Modules.Persistence;
using Celeste.Everest.Modules.Persistence.GameSaves;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Runtime.Events;
using Celeste.Everest.UI.Settings;
using Celeste.Game.Execution.Session;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Environment;
using Celeste.Game.Save.Data;
using Celeste.Game.Save.Formats.Original;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Persistence;
using Celeste.Mods.CollabUtils2.Triggers;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Intro;
using Celeste.Simulation.Gameplay.Player.Runtime;
using Celeste.Simulation.Runtime.Session;
using Celeste.Simulation.UI.Menus.Text;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed class CollabReturnProcess : IDisposable
    {
        private readonly CollabModule module;
        private readonly ISourceEngineContext engine;
        private readonly List<Hook> hooks = new List<Hook>();
        private CollabTasLoadingHooks tasLoading;
        private CollabSession restartState;
        private bool pauseTimerUntilAction;
        private bool unpauseTimerOnNextAction;
        private OriginalLevelSceneRoot timerLevel;
        private EverestGameEventsProcess Events => ((IEverestGameEventsEnvironment)engine).GameEvents;
        private IOriginalGameplayProcess Gameplay => (IOriginalGameplayProcess)engine;
        private IOriginalAreaKeyPolicy Areas => ((IOriginalAreaKeyEnvironment)engine).AreaKeys;

        private IOriginalSaveSessionSelectionPolicy Selection =>
            ((IOriginalSaveSessionSelectionEnvironment)engine).SaveSessionSelection;

        private string Clean(string key) => ((IOriginalGameTextEnvironment)engine).Text.CleanDialog(key);

        public CollabReturnProcess(CollabModule module, ISourceEngineContext engine)
        {
            this.module = module;
            this.engine = engine;
        }

        public void Load()
        {
            tasLoading = new CollabTasLoadingHooks(engine);
            tasLoading.Load();
            Events.OnCreatePauseMenuButtons += CreatePauseMenuButtons;
            hooks.Add(new Hook(
                typeof(EverestSaveRuntimePolicy).GetMethod(nameof(EverestSaveRuntimePolicy.RegisterHeartGem)),
                (Action<Action<EverestSaveRuntimePolicy, OriginalSaveDataState, OriginalAreaKey>,
                    EverestSaveRuntimePolicy, OriginalSaveDataState, OriginalAreaKey>)((original, policy, save, area) =>
                {
                    original(policy, save, area);
                    if (!ReferenceEquals(policy, ((IOriginalSaveCounterEnvironment)engine).SaveCounters) ||
                        !module.Identities.IsHeartSide(Areas.GetSID(area))) return;
                    string lobbySID = module.Identities.GetLobbyForLevelSet(Areas.GetLevelSet(area));
                    if (lobbySID != null)
                        policy.RegisterHeartGem(save, Areas.ToKey(Areas.GetAreaBySID(lobbySID)));
                })));
            hooks.Add(new Hook(
                typeof(EverestAreaCompleteInfoState).GetMethod(nameof(EverestAreaCompleteInfoState
                    .InitializeInstalled)),
                (Action<Action<EverestAreaCompleteInfoState, bool, ChapterCompletionSession>,
                    EverestAreaCompleteInfoState, bool, ChapterCompletionSession>)
                ((original, info, pie, session) =>
                {
                    original(info, pie, session);
                    if (!ReferenceEquals(info, ((IOriginalAreaCompleteInfoEnvironment)engine).AreaCompleteInfo) ||
                        ((ISettingsContext)engine).Settings.SpeedrunClock <= 0 || session == null) return;
                    AppendCollabVersion(info, session);
                })));
            hooks.Add(new Hook(
                typeof(EverestSaveRuntimePolicy).GetMethod(nameof(EverestSaveRuntimePolicy.RegisterCompletion)),
                (Action<Action<EverestSaveRuntimePolicy, OriginalSaveDataState, ChapterCompletionSession>,
                    EverestSaveRuntimePolicy, OriginalSaveDataState, ChapterCompletionSession>)((original, policy, save,
                    session) =>
                {
                    original(policy, save, session);
                    var counters = ((IOriginalSaveCounterEnvironment)engine).SaveCounters;
                    if (!ReferenceEquals(policy, counters)) return;
                    var area = Areas.GetSessionArea(session);
                    if (!module.Identities.IsHeartSide(Areas.GetSID(area))) return;
                    string lobbySID = module.Identities.GetLobbyForLevelSet(Areas.GetLevelSet(area));
                    if (lobbySID == null) return;
                    counters.GetAreasSafe(Gameplay.SaveContext.CurrentSave)
                        [Areas.GetAreaBySID(lobbySID).ID].Modes[0].Completed = true;
                })));
            Events.OnExit += OnExit;
            Events.OnLoadLevel += OnLoadLevel;
            Events.OnJournalEnter += OnJournalEnter;
            ((IEverestAssetReloadEnvironment)engine).AssetReload.Events.OnReloadLevel += OnReloadLevel;
            hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Update)),
                (Action<Action<RoomPlayerSession>, RoomPlayerSession>)((original, player) =>
                {
                    original(player);
                    if (!unpauseTimerOnNextAction || !ReferenceEquals(player.Scene, timerLevel) ||
                        !PlayerFrameMotion.InControl(player.Machine.State)) return;
                    var input = ((IOriginalGameInputProcessEnvironment)engine).InputSource;
                    if (input.MoveX.Value != 0 || input.MoveY.Value != 0 || input.Grab.Check || input.Jump.Check ||
                        input.Dash.Check || input.CrouchDash.Check)
                    {
                        timerLevel.TimerStopped = false;
                        unpauseTimerOnNextAction = false;
                    }
                })));
            hooks.Add(new Hook(
                typeof(EverestSaveRuntimePolicy).GetMethod(nameof(EverestSaveRuntimePolicy.StartSession)),
                (Action<Action<EverestSaveRuntimePolicy, OriginalSaveDataState, ChapterCompletionSession>,
                    EverestSaveRuntimePolicy, OriginalSaveDataState, ChapterCompletionSession>)((original, policy, save,
                    session) =>
                {
                    original(policy, save, session);
                    if (ReferenceEquals(policy, ((IOriginalSaveSessionStartEnvironment)engine).SaveSessionStart))
                        module.Panels.RestoreModSessions(session);
                })));
            hooks.Add(new Hook(
                typeof(OriginalLevelLoaderFactory).GetMethod(nameof(OriginalLevelLoaderFactory.CreateLevelLoader)),
                (Func<Func<OriginalLevelLoaderFactory, ChapterCompletionSession, Vector2?, OriginalLevelLoaderScene>,
                    OriginalLevelLoaderFactory, ChapterCompletionSession, Vector2?, OriginalLevelLoaderScene>)((
                    original, factory, session, position) =>
                {
                    if (ReferenceEquals(factory, ((IOriginalLevelLoaderEnvironment)engine).LevelLoaders) &&
                        engine.Frame.Scenes.CurrentScene is OriginalLevelSceneRoot level &&
                        !ReferenceEquals(level.Session, session) && level.Session.AreaID == session.AreaID)
                        restartState = Copy(module.Session);
                    return original(factory, session, position);
                })));
        }

        public void OnSessionCreated(ChapterCompletionSession session)
        {
            if (restartState != null)
            {
                CopyInto(restartState, module.Session);
                restartState = null;
            }

            if (session == null) return;
            SetCompletedMapFlags(session);
            if (module.Session.LobbySID != null) return;
            string sid = Areas.GetSID(Areas.GetSessionArea(session));
            string lobbySID = module.Identities.GetLobbyForLevelSet(Areas.GetLevelSet(Areas.GetSessionArea(session)))
                              ?? module.Identities.GetLobbyForGym(sid);
            if (lobbySID == null) return;
            module.Session.LobbySID = lobbySID;
            foreach (var room in Areas.GetAreaBySID(lobbySID).GetOriginalMode(0).MapData.Levels)
            {
                Celeste.Formats.Map.Entities.Structure.EntityData found = null;
                foreach (var trigger in room.Triggers)
                    if (trigger.Name == "CollabUtils2/ChapterPanelTrigger" && trigger.Attr("map") == sid)
                    {
                        found = trigger;
                        break;
                    }

                if (found == null)
                    foreach (var entity in room.Entities)
                        if ((entity.Name == "SJ2021/StrawberryJamJar" || entity.Name == "CollabUtils2/WarpPedestal" ||
                             entity.Name == "FlushelineCollab/LevelEntrance") && entity.Attr("map") == sid)
                        {
                            found = entity;
                            break;
                        }

                if (found == null) continue;
                var target = found.Position + new Vector2(found.Width, found.Height) / 2f + room.Position;
                Vector2 closest = Vector2.Zero;
                float closestDistance = float.MaxValue;
                foreach (var spawn in room.Spawns)
                {
                    float distance = Vector2.DistanceSquared(spawn, target);
                    if (distance >= closestDistance) continue;
                    closest = spawn;
                    closestDistance = distance;
                }

                module.Session.LobbyRoom = room.Name;
                module.Session.LobbySpawnPointX = closest.X;
                module.Session.LobbySpawnPointY = closest.Y;
                module.Session.SaveAndReturnToLobbyAllowed = found.Bool("allowSaving", true);
                break;
            }
        }

        private void SetCompletedMapFlags(ChapterCompletionSession session)
        {
            string sid = Areas.GetSID(Areas.GetSessionArea(session));
            string levelSet = module.Identities.GetLobbyLevelSet(sid);
            if (levelSet == null) return;
            var stats = ((IOriginalSaveLevelSetEnvironment)engine).SaveLevelSets
                .GetLevelSetStatsFor(Gameplay.SaveContext.CurrentSave, levelSet);
            if (stats == null) return;
            foreach (var area in stats.Areas)
                if (area.Modes[0].HeartGem)
                    session.SetFlag("CollabUtils2_MapCompleted_" + area.SID.Substring(levelSet.Length + 1));
        }

        private static CollabSession Copy(CollabSession state)
        {
            var copy = new CollabSession();
            CopyInto(state, copy);
            return copy;
        }

        private static void CopyInto(CollabSession source, CollabSession target)
        {
            target.LobbySID = source.LobbySID;
            target.LobbyRoom = source.LobbyRoom;
            target.LobbySpawnPointX = source.LobbySpawnPointX;
            target.LobbySpawnPointY = source.LobbySpawnPointY;
            target.SaveAndReturnToLobbyAllowed = source.SaveAndReturnToLobbyAllowed;
            target.GymExitMapSID = source.GymExitMapSID;
            target.GymExitSaveAllowed = source.GymExitSaveAllowed;
        }

        private void OnExit(OriginalLevelSceneRoot level, LevelExitSequence exit, LevelExitMode mode,
            ChapterCompletionSession session, HiresSnowState snow)
        {
            if (mode == LevelExitMode.Restart || mode == LevelExitMode.GoldenBerryRestart)
                restartState = Copy(module.Session);
            if ((mode == LevelExitMode.GiveUp || mode == LevelExitMode.Completed) && module.Session.LobbySID != null)
            {
                var lobby = Areas.GetAreaBySID(module.Session.LobbySID);
                if (lobby != null) Selection.SetLastAreaSafe(Gameplay.SaveContext.CurrentSave, Areas.ToKey(lobby));
            }
        }

        public void PauseTimerOnReturn() => pauseTimerUntilAction = true;

        private void OnLoadLevel(OriginalLevelSceneRoot level, PlayerIntroType intro, bool fromLoader)
        {
            restartState = null;
            if (!pauseTimerUntilAction) return;
            pauseTimerUntilAction = false;
            level.TimerStopped = true;
            timerLevel = level;
            unpauseTimerOnNextAction = true;
        }

        private void OnJournalEnter(OuiJournalSequence journal, OuiState from)
        {
            string levelSet = ((IOriginalSaveLevelSetEnvironment)engine).SaveLevelSets
                .GetLevelSet(Gameplay.SaveContext.CurrentSave);
            string collabName = module.Identities.GetCollabNameForSID(levelSet);
            if (collabName == null || levelSet != collabName + "/0-Lobbies") return;
            for (int index = 0; index < journal.Pages.Count; index++)
            {
                var type = journal.Pages[index].GetType();
                if (type == typeof(OuiJournalCoverPage) || type == typeof(OuiJournalPoemPage)) continue;
                journal.Pages.RemoveAt(index--);
            }

            journal.Pages.Insert(1, new OuiJournalCollabProgressInOverworld(module, journal));
        }

        private void OnReloadLevel(OriginalLevelSceneRoot level)
        {
            if (level == null || !module.Panels.HasWrapper) return;
            var players = level.SourceScene.Entities.GetTrackedEntities(typeof(RoomPlayerSession));
            if (players.Count > 0 && ((RoomPlayerSession)players[0]).Machine.State == 11)
                ((RoomPlayerSession)players[0]).Machine.State = 0;
        }

        private void CreatePauseMenuButtons(OriginalLevelSceneRoot level, TextMenuPresentation menu, bool minimal)
        {
            if (module.Session.LobbySID == null) return;
            int index = menu.Items.FindIndex(item => item.GetType() == typeof(TextMenuPresentation.Button) &&
                                                     ((TextMenuPresentation.Button)item).Label ==
                                                     Clean("MENU_PAUSE_RETURN"));
            if (index == -1) index = menu.Items.Count - 1;
            var button = new TextMenuPresentation.Button(Clean("collabutils2_returntolobby"))
                { ConfirmSfx = "event:/ui/main/message_confirm" };
            button.Pressed(() =>
            {
                level.PauseMainMenuOpen = false;
                menu.RemoveSelf();
                OpenConfirmMenu(level, menu.Selection);
            });
            menu.Remove(menu.Items[index]);
            menu.Insert(index, button);
        }

        private void OpenConfirmMenu(OriginalLevelSceneRoot level, int returnIndex)
        {
            level.Paused = true;
            var menu = new TextMenuPresentation(engine) { AutoScroll = false, Position = new Vector2(960f, 440f) };
            bool saveAllowed = module.Session.SaveAndReturnToLobbyAllowed;
            menu.Add(new TextMenuPresentation.Header(Clean("collabutils2_returntolobby_confirm_title")));
            if (saveAllowed)
            {
                menu.Add(new TextMenuPresentation.SubHeader(Clean("collabutils2_returntolobby_confirm_note1")));
                menu.Add(new EverestMenuSubHeaderExt(Clean("collabutils2_returntolobby_confirm_note2"))
                    { HeightExtra = 0f });
                menu.Add(new TextMenuPresentation.SubHeader(""));
                menu.Add(new TextMenuPresentation.Button(Clean("collabutils2_returntolobby_confirm_save"))
                    .Pressed(() => ReturnToLobby(level, menu, true)));
            }

            menu.Add(new TextMenuPresentation.Button(Clean(saveAllowed
                    ? "collabutils2_returntolobby_confirm_donotsave"
                    : "menu_return_continue"))
                .Pressed(() => ReturnToLobby(level, menu, false)));
            menu.Add(new TextMenuPresentation.Button(Clean(saveAllowed
                    ? "collabutils2_returntolobby_confirm_cancel"
                    : "menu_return_cancel"))
                .Pressed(() => menu.OnCancel()));
            menu.OnPause = menu.OnESC = () =>
            {
                menu.RemoveSelf();
                level.Paused = false;
                engine.Effects.FreezeTimer = 0.15f;
                Gameplay.AudioSession.Play("event:/ui/game/unpause");
            };
            menu.OnCancel = () =>
            {
                Gameplay.AudioSession.Play("event:/ui/main/button_back");
                menu.RemoveSelf();
                level.Pause(returnIndex, false);
            };
            if (saveAllowed && !((CollabSettings)module._Settings).SaveByDefaultWhenReturningToLobby)
                menu.Selection = menu.FirstPossibleSelection + 1;
            level.SourceScene.QueueSourceEntity(menu);
        }

        private void ReturnToLobby(OriginalLevelSceneRoot level, TextMenuPresentation menu, bool save)
        {
            engine.Effects.TimeRate = 1f;
            menu.Focused = false;
            Gameplay.AudioSession.SetMusic(null);
            Gameplay.AudioSession.BusStopAll("bus:/gameplay_sfx", true);
            if (save)
            {
                level.Session.InArea = true;
                level.Session.Deaths++;
                level.Session.DeathsInCurrentLevel++;
                Gameplay.SaveContext.AddDeath(
                    new Celeste.Core.Campaigns.CampaignAreaKey(level.Session.AreaID, level.Session.Mode));
            }

            level.DoScreenWipe(false, () =>
            {
                if (save) SaveSessions(level.Session);
                engine.Frame.Scenes.NextScene = new LevelExitToLobbySequence(module, engine,
                    save ? LevelExitMode.SaveAndQuit : LevelExitMode.GiveUp, level.Session);
            });
            foreach (LevelEndingHookState component in level.SourceScene.Components.GetTrackedComponents(
                         "Celeste.LevelEndingHook"))
                component.OnEnd?.Invoke();
        }

        private void SaveSessions(ChapterCompletionSession session)
        {
            string sid = Areas.GetSID(Areas.GetSessionArea(session));
            using (var stream = new MemoryStream())
            {
                new XmlSerializer(typeof(Celeste.Formats.Save.Xml.Session)).Serialize(stream,
                    FileSaveCodec.ToSession(session, Areas));
                module.SaveData.SessionsPerLevel[sid] = Encoding.UTF8.GetString(stream.ToArray());
            }

            var yaml = new Dictionary<string, string>();
            var binary = new Dictionary<string, string>();
            foreach (var mod in ((ISourceEverestModulesEnvironment)engine).Modules)
            {
                if (ReferenceEquals(mod, module)) continue;
                if (mod.SaveDataAsync)
                {
                    byte[] bytes = mod.SerializeSession(Gameplay.SaveContext.CurrentSave.FileSlot);
                    if (bytes != null) binary[mod.Metadata.Name] = Convert.ToBase64String(bytes);
                }
                else if (mod._Session != null && !(mod._Session is EverestModuleBinarySession))
                {
                    try
                    {
                        yaml[mod.Metadata.Name] =
                            ((IEverestYamlEnvironment)engine).Yaml.Serializer.Serialize(mod._Session);
                    }
                    catch (Exception exception)
                    {
                        var logger = ((IEverestLoggerProcessEnvironment)engine).Logger;
                        logger.Log(EverestLogLevel.Warn, "CollabUtils2/ReturnToLobbyHelper",
                            "Failed to save the session of " + mod.Metadata.Name + "!");
                        logger.LogDetailed(exception);
                    }
                }
            }

            module.SaveData.ModSessionsPerLevel[sid] = yaml;
            module.SaveData.ModSessionsPerLevelBinary[sid] = binary;
        }

        internal void AppendCollabVersion(EverestAreaCompleteInfoState info, ChapterCompletionSession session)
        {
            string collabName = module.Identities.GetCollabNameForSID(Areas.GetSID(Areas.GetSessionArea(session)));
            if (collabName == null) return;
            var version = ((ISourceEverestModulesEnvironment)engine).Modules
                .FirstOrDefault(mod => mod.Metadata?.Name == collabName)?.Metadata.Version ?? new Version(0, 0);
            var data = DynamicData.For(info);
            data.Set("versionFull",
                data.Get<string>("versionFull") + "\n" + Clean("endscreen_collabname_" + collabName) + " " + version);
            data.Set("versionOffset", data.Get<float>("versionOffset") - 32f);
        }

        public IEnumerator DisplayCollabMapEndScreenIfEnabled()
        {
            var input = ((IOriginalGameInputProcessEnvironment)engine).InputSource;
            if (((CollabSettings)module._Settings).DisplayEndScreenForAllMaps &&
                ((ISettingsContext)engine).Settings.SpeedrunClock != 0)
            {
                ((OriginalLevelSceneRoot)engine.Frame.Scenes.CurrentScene).SourceScene
                    .QueueSourceEntity(new CollabMapCompletionInfo(module, engine));
                yield return 0.5f;
                while (!input.MenuConfirm.Pressed && !input.MenuCancel.Pressed) yield return null;
            }
            else
            {
                float timer = 0f;
                while (!input.MenuConfirm.Pressed && !input.MenuCancel.Pressed && timer <= 1f)
                {
                    yield return null;
                    timer += engine.DeltaTime;
                }
            }
        }

        public bool HasSavedState(string mapSID) => module.SaveData.SessionsPerLevel.ContainsKey(mapSID);

        public void TeleportToMapWithReturnToLobby(string mapSID, string checkpoint, ReturnToLobbyMode returnMode,
            bool saveAllowed, bool continueSession, string gymExitMapSID = null, bool gymExitSaveAllowed = false)
        {
            var level = (OriginalLevelSceneRoot)engine.Frame.Scenes.CurrentScene;
            var target = ((IOriginalSessionEnvironment)engine).Sessions.CreateSession(
                Areas.ToKey(Areas.GetAreaBySID(mapSID)), checkpoint);
            module.Panels.PrepareReturnSession(target, level, returnMode, saveAllowed, gymExitMapSID,
                gymExitSaveAllowed);
            if (!continueSession)
            {
                module.SaveData.SessionsPerLevel.Remove(mapSID);
                module.SaveData.ModSessionsPerLevel.Remove(mapSID);
                module.SaveData.ModSessionsPerLevelBinary.Remove(mapSID);
            }

            ((IOriginalLevelExitEnvironment)engine).LevelExits.CreateLevelExit(LevelExitMode.GiveUp, level.Session);
            OriginalLevelEntryRouting.Go(target, false, engine.Frame.Scenes,
                ((IOriginalLevelEntryEnvironment)engine).LevelEntries);
        }

        public void TriggerReturnToLobby()
        {
            var level = (OriginalLevelSceneRoot)engine.Frame.Scenes.CurrentScene;
            level.DoScreenWipe(false, () => engine.Frame.Scenes.NextScene =
                new LevelExitToLobbySequence(module, engine, LevelExitMode.Completed, level.Session));
        }

        public void Dispose()
        {
            Events.OnCreatePauseMenuButtons -= CreatePauseMenuButtons;
            Events.OnExit -= OnExit;
            Events.OnLoadLevel -= OnLoadLevel;
            Events.OnJournalEnter -= OnJournalEnter;
            ((IEverestAssetReloadEnvironment)engine).AssetReload.Events.OnReloadLevel -= OnReloadLevel;
            tasLoading?.Dispose();
            tasLoading = null;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            restartState = null;
            timerLevel = null;
            pauseTimerUntilAction = unpauseTimerOnNextAction = false;
        }
    }
}
