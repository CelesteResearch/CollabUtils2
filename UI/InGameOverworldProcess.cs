using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Everest;
using Celeste.Everest.Content.Serialization;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Everest.Menus.Overworld;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Presentation.Scenes;
using Celeste.Game.Audio.Runtime;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Session;
using Celeste.Game.Menus.Chapters;
using Celeste.Game.Menus.Overworld;
using Celeste.Game.Presentation.Environment;
using Celeste.Game.Presentation.Hud.Strawberries;
using Celeste.Game.Presentation.Mountain.Rendering;
using Celeste.Game.Save.Formats.Original;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Persistence;
using Celeste.Mods.CollabUtils2.Triggers;
using Celeste.Scene.Lifecycle;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Player.Death;
using Celeste.Simulation.Presentation.Scene.Transitions;
using Celeste.Simulation.Runtime.Session;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed partial class InGameOverworldProcess : IDisposable
    {
        private readonly List<IDisposable> hooks = new List<IDisposable>();

        private readonly Dictionary<string, Action<IOriginalAreaData>> openCallbacks =
            new Dictionary<string, Action<IOriginalAreaData>>();

        private SceneWrappingEntity<EverestOverworldSequence> wrapper;
        private OriginalAreaKey? lastArea;
        private CollabSession pendingReturn;
        private ChapterCompletionSession pendingTargetSession;

        public ISourceEngineContext Engine { get; }
        public CollabModule Module { get; }
        public IOriginalAreaData ForceArea { get; private set; }
        public ReturnToLobbyMode ReturnMode { get; private set; }
        public bool SaveAndReturnToLobbyAllowed { get; set; }
        public bool ExitFromGym { get; private set; }
        public string GymExitMapSID { get; set; }
        public bool GymExitSaveAllowed { get; set; }
        internal bool StartRequested { get; set; }

        internal bool HasWrapper => wrapper != null;

        public bool IsOpen =>
            wrapper?.Scene != null && ReferenceEquals(wrapper.Scene, Engine.Frame.Scenes.CurrentScene);

        private IOriginalGameplayProcess Gameplay => (IOriginalGameplayProcess)Engine;
        private IOriginalAreaKeyPolicy Areas => ((IOriginalAreaKeyEnvironment)Engine).AreaKeys;

        private IOriginalSaveSessionSelectionPolicy Selection =>
            ((IOriginalSaveSessionSelectionEnvironment)Engine).SaveSessionSelection;

        public InGameOverworldProcess(CollabModule module, ISourceEngineContext engine)
        {
            Module = module;
            Engine = engine;
        }

        public void Load()
        {
            hooks.Add(new CollabChapterPanelMenuRegistration(Engine));
            LoadJournalEntry();
            hooks.Add(new Hook(typeof(OriginalGameAudioSession).GetMethod(nameof(OriginalGameAudioSession.SetMusic)),
                (Func<Func<OriginalGameAudioSession, string, bool, bool, bool>, OriginalGameAudioSession, string, bool,
                    bool, bool>)
                ((original, audio, path, startPlaying, allowFadeOut) =>
                    ReferenceEquals(audio, Gameplay.AudioSession) && path != null && IsOpen
                        ? false
                        : original(audio, path, startPlaying, allowFadeOut))));
            hooks.Add(new Hook(typeof(OriginalGameAudioSession).GetMethod(nameof(OriginalGameAudioSession.SetAmbience)),
                (Func<Func<OriginalGameAudioSession, string, bool, bool>, OriginalGameAudioSession, string, bool, bool>)
                ((original, audio, path, startPlaying) =>
                    ReferenceEquals(audio, Gameplay.AudioSession) && path != null && IsOpen
                        ? false
                        : original(audio, path, startPlaying))));
            hooks.Add(new Hook(typeof(ChapterPanelSequence).GetMethod(nameof(ChapterPanelSequence.Start)),
                (Action<Action<ChapterPanelSequence, string>, ChapterPanelSequence, string>)((original, panel,
                    checkpoint) =>
                {
                    if (panel is CollabChapterPanelSequence collab && ReferenceEquals(collab.Owner, this) && IsOpen)
                    {
                        ((OriginalLevelSceneRoot)wrapper.Scene).PauseLock = true;
                        if (!collab.BeforeStart(checkpoint)) return;
                    }

                    original(panel, checkpoint);
                })));
            hooks.Add(new Hook(
                typeof(ChapterPanelSequence).GetMethod("StartRoutine", BindingFlags.Instance | BindingFlags.NonPublic),
                (Func<Func<ChapterPanelSequence, string, IEnumerator>, ChapterPanelSequence, string, IEnumerator>)
                ((original, panel, checkpoint) =>
                    panel is CollabChapterPanelSequence collab && ReferenceEquals(collab.Owner, this)
                        ? NotifyLevelExitRoutine(original(panel, checkpoint))
                        : original(panel, checkpoint))));
            hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Pause)),
                (Action<Action<OriginalLevelSceneRoot, int, bool, bool>, OriginalLevelSceneRoot, int, bool, bool>)
                ((original, level, startIndex, minimal, quickReset) =>
                {
                    if (ReferenceEquals(wrapper?.Scene, level)) Close(level, true, true);
                    original(level, startIndex, minimal, quickReset);
                })));
            hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.DieSourceBody)),
                (Func<Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation>, RoomPlayerSession, Vector2,
                    bool, bool, PlayerDeathPresentation>)
                ((original, player, direction, evenIfInvincible, registerDeath) =>
                {
                    var result = original(player, direction, evenIfInvincible, registerDeath);
                    if (result != null && ReferenceEquals(wrapper?.Scene, player.Scene))
                        Close((OriginalLevelSceneRoot)player.Scene, true, true);
                    return result;
                })));
            using (new DetourConfigContext(new DetourConfig("CollabUtils2_BeforeAll", priority: int.MinValue)).Use())
            {
                hooks.Add(new Hook(typeof(OriginalLevelEntryRouting).GetMethod(nameof(OriginalLevelEntryRouting.Go)),
                    (Action<Action<ChapterCompletionSession, bool, SourceEngineSceneState, ILevelEntrySceneFactory>,
                        ChapterCompletionSession, bool, SourceEngineSceneState, ILevelEntrySceneFactory>)
                    ((original, session, fromSave, scenes, factory) =>
                    {
                        if (!ReferenceEquals(scenes, Engine.Frame.Scenes))
                        {
                            original(session, fromSave, scenes, factory);
                            return;
                        }

                        string sid = Areas.GetSID(Areas.GetSessionArea(session));
                        if (Module.SaveData.SessionsPerLevel.TryGetValue(sid, out string xml))
                        {
                            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
                            {
                                var wire = (Celeste.Formats.Save.Xml.Session)new XmlSerializer(
                                    typeof(Celeste.Formats.Save.Xml.Session)).Deserialize(stream);
                                var restored = FileSaveCodec.FromSession(wire, Areas,
                                    ((IOriginalSessionEnvironment)Engine).Sessions);
                                if (ReferenceEquals(pendingTargetSession, session)) pendingTargetSession = restored;
                                session = restored;
                                Selection.SetCurrentSessionSafe(Gameplay.SaveContext.CurrentSave, session);
                                fromSave = true;
                            }

                            Module.SaveData.SessionsPerLevel.Remove(sid);
                        }

                        RestoreModSessions(session);
                        ApplyPendingReturnSession(session);
                        original(session, fromSave, scenes, factory);
                    })));
            }
        }

        public void OpenChapterPanel(RoomPlayerSession player, string sid, ReturnToLobbyMode returnMode,
            bool saveAllowed, bool exitFromGym)
        {
            var area = Areas.GetAreaBySID(sid) ?? Areas.GetAreaById(0);
            var dialog = ((Celeste.Game.Content.Dialogs.IOriginalGameTextEnvironment)Engine).Text.Dialog;
            if (!dialog.Has(area.Name + "_collabcredits") && area.GetOriginalMode(0).Checkpoints?.Length > 0)
                saveAllowed = false;
            player.Drop();
            Open(player, area, returnMode, saveAllowed, exitFromGym);
        }

        private void Open(RoomPlayerSession player, IOriginalAreaData area, ReturnToLobbyMode returnMode,
            bool saveAllowed, bool exitFromGym, bool journal = false)
        {
            if (IsOpen || player.Machine.State == 11) return;
            player.Machine.State = 11;
            var level = (OriginalLevelSceneRoot)player.Scene;
            level.SourceScene.Entities.FindFirst<TotalStrawberriesDisplayState>().Active = false;
            ForceArea = area;
            ReturnMode = returnMode;
            SaveAndReturnToLobbyAllowed = saveAllowed;
            ExitFromGym = exitFromGym;
            StartRequested = !journal;
            JournalStartRequested = journal;
            var snow = new HiresSnowState(Engine) { Alpha = 0f, ParticleAlpha = 0.25f };
            wrapper = new SceneWrappingEntity<EverestOverworldSequence>(new EverestOverworldSequence(Engine,
                new OverworldLoaderSequence(Engine, (OverworldStartMode)(-1), snow)));
            wrapper.OnBegin += overworld =>
            {
                overworld.Renderers.Remove(
                    overworld.Renderers.Renderers.Find(renderer => renderer is MountainRendererState));
                overworld.Renderers.Remove(
                    overworld.Renderers.Renderers.Find(renderer => renderer is ScreenWipePresentation));
                overworld.Renderers.UpdateLists();
            };
            wrapper.OnEnd += overworld =>
            {
                if (!ReferenceEquals(wrapper?.WrappedScene, overworld)) return;
                wrapper = null;
                ForceArea = null;
                SaveAndReturnToLobbyAllowed = false;
                GymExitMapSID = null;
                GymExitSaveAllowed = false;
                ReturnMode = default;
                ExitFromGym = false;
            };
            level.SourceScene.QueueSourceEntity(wrapper);
            string collabID = Module.Identities.GetCollabNameForSID(Areas.GetSID(Areas.ToKey(area)));
            if (!journal && collabID != null && openCallbacks.TryGetValue(collabID, out var callback)) callback(area);
            wrapper.OwnedComponents.Add(new SimulationCoroutine(Engine, UpdateRoutine(wrapper)));
        }

        public void AddOpenChapterPanelCallback(string collabID, Action<IOriginalAreaData> callback) =>
            openCallbacks[collabID] = callback;

        public void RemoveOpenChapterPanelCallback(string collabID) => openCallbacks.Remove(collabID);
        public void RememberLastArea(OriginalAreaKey area) => lastArea = area;

        private IEnumerator NotifyLevelExitRoutine(IEnumerator original)
        {
            while (original.MoveNext())
            {
                yield return original.Current;
                if (original.Current is float wait && wait == 0.5f && IsOpen &&
                    Engine.Frame.Scenes.CurrentScene is OriginalLevelSceneRoot level)
                    ((IOriginalLevelExitEnvironment)Engine).LevelExits.CreateLevelExit(LevelExitMode.GiveUp,
                        level.Session);
            }
        }

        public void Close(OriginalLevelSceneRoot level, bool removeScene, bool resetPlayer)
        {
            if (removeScene)
            {
                wrapper?.WrappedScene.GetUI<ChapterPanelSequence>().RemoveSelf();
                wrapper?.WrappedScene.SourceScene.CommitQueuedEntityLists();
                wrapper?.RemoveSelf();
                if (lastArea != null && Gameplay.SaveContext.CurrentSave != null)
                {
                    Selection.SetLastAreaSafe(Gameplay.SaveContext.CurrentSave, lastArea.Value);
                    lastArea = null;
                    level.SourceScene.Entities.FindFirst<TotalStrawberriesDisplayState>().Active = true;
                }
            }

            if (!resetPlayer) return;
            var players = level.SourceScene.Entities.GetTrackedEntities(typeof(RoomPlayerSession));
            var player = players.Count == 0 ? null : (RoomPlayerSession)players[0];
            if (player?.Machine.State == 11) level.OnEndOfFrame += () => player.Machine.State = 0;
        }

        private IEnumerator DelayedCloseRoutine(OriginalLevelSceneRoot level)
        {
            yield return null;
            Close(level, false, true);
        }

        private IEnumerator UpdateRoutine(SceneWrappingEntity<EverestOverworldSequence> current)
        {
            while (current.Scene == null) yield return null;
            var level = (OriginalLevelSceneRoot)current.Scene;
            var overworld = current.WrappedScene;
            while (ReferenceEquals(wrapper?.Scene, Engine.Frame.Scenes.CurrentScene))
            {
                if (overworld.Next is Celeste.Game.Menus.Chapters.ChapterSelectSequence)
                {
                    overworld.Next.RemoveSelf();
                    current.OwnedComponents.Add(new SimulationCoroutine(Engine, DelayedCloseRoutine(level)));
                }

                overworld.Snow.ParticleAlpha = 0.25f;
                if (overworld.Current != null || overworld.Next?.Scene != null)
                    overworld.Snow.Alpha = ScalarMath.Approach(overworld.Snow.Alpha, 1f, Engine.DeltaTime * 2f);
                else
                {
                    overworld.Snow.Alpha = ScalarMath.Approach(overworld.Snow.Alpha, 0f, Engine.DeltaTime * 2f);
                    if (overworld.Snow.Alpha <= 0.01f) Close(level, true, true);
                }

                yield return null;
            }

            if (current.Scene != null) current.RemoveSelf();
        }

        public void PrepareReturnSession(ChapterCompletionSession target) => PrepareReturnSession(target,
            (OriginalLevelSceneRoot)wrapper.Scene, ReturnMode, SaveAndReturnToLobbyAllowed, GymExitMapSID,
            GymExitSaveAllowed);

        public void PrepareReturnSession(ChapterCompletionSession target, OriginalLevelSceneRoot level,
            ReturnToLobbyMode returnMode, bool saveAllowed, string gymExitMapSID, bool gymExitSaveAllowed)
        {
            var state = Module.Session;
            var next = new CollabSession
            {
                SaveAndReturnToLobbyAllowed = saveAllowed,
                GymExitMapSID = Module.Identities.IsCollabLobby(Areas.GetSID(Areas.GetSessionArea(level.Session)))
                    ? gymExitMapSID
                    : state.GymExitMapSID,
                GymExitSaveAllowed = Module.Identities.IsCollabLobby(Areas.GetSID(Areas.GetSessionArea(level.Session)))
                    ? gymExitSaveAllowed
                    : state.GymExitSaveAllowed
            };
            if (returnMode == ReturnToLobbyMode.DoNotChangeReturn)
            {
                next.LobbySID = state.LobbySID;
                next.LobbyRoom = state.LobbyRoom;
                next.LobbySpawnPointX = state.LobbySpawnPointX;
                next.LobbySpawnPointY = state.LobbySpawnPointY;
            }
            else if (returnMode == ReturnToLobbyMode.SetReturnToHere)
            {
                next.LobbySID = Areas.GetSID(Areas.GetSessionArea(level.Session));
                next.LobbyRoom = level.Session.LevelData.Name;
                var player = level.SourceScene.Entities.GetTrackedEntities(typeof(RoomPlayerSession));
                Vector2 spawn = level.GetSpawnPoint(player.Count > 0
                    ? ((RoomPlayerSession)player[0]).Position
                    : level.Camera.Position + new Vector2(160f, 90f));
                next.LobbySpawnPointX = spawn.X;
                next.LobbySpawnPointY = spawn.Y;
            }

            pendingTargetSession = target;
            pendingReturn = next;
        }

        public void ApplyPendingReturnSession(ChapterCompletionSession target)
        {
            if (pendingReturn == null || !ReferenceEquals(target, pendingTargetSession)) return;
            var state = Module.Session;
            state.LobbySID = pendingReturn.LobbySID;
            state.LobbyRoom = pendingReturn.LobbyRoom;
            state.LobbySpawnPointX = pendingReturn.LobbySpawnPointX;
            state.LobbySpawnPointY = pendingReturn.LobbySpawnPointY;
            state.SaveAndReturnToLobbyAllowed = pendingReturn.SaveAndReturnToLobbyAllowed;
            state.GymExitMapSID = pendingReturn.GymExitMapSID;
            state.GymExitSaveAllowed = pendingReturn.GymExitSaveAllowed;
        }

        internal void RestoreModSessions(ChapterCompletionSession session)
        {
            string sid = Areas.GetSID(Areas.GetSessionArea(session));
            if (!Module.SaveData.ModSessionsPerLevel.TryGetValue(sid, out var saved)) return;
            Module.SaveData.ModSessionsPerLevelBinary.TryGetValue(sid, out var binary);
            foreach (var mod in ((ISourceEverestModulesEnvironment)Engine).Modules)
            {
                if (ReferenceEquals(mod, Module)) continue;
                if (mod.SaveDataAsync && binary != null && binary.TryGetValue(mod.Metadata.Name, out string bytes))
                    mod.DeserializeSession(Gameplay.SaveContext.CurrentSave.FileSlot, Convert.FromBase64String(bytes));
                if (mod._Session == null || !saved.TryGetValue(mod.Metadata.Name, out string yaml)) continue;
                try
                {
                    ((IEverestYamlEnvironment)Engine).Yaml.DeserializerUsing(mod._Session)
                        .Deserialize(yaml, mod.SessionType);
                }
                catch (Exception exception)
                {
                    ((IEverestLoggerProcessEnvironment)Engine).Logger.Log(EverestLogLevel.Warn,
                        "CollabUtils2/ReturnToLobbyHelper", "Failed to load the session of " + mod.Metadata.Name + "!");
                    ((IEverestLoggerProcessEnvironment)Engine).Logger.LogDetailed(exception);
                }
            }

            Module.SaveData.ModSessionsPerLevel.Remove(sid);
            Module.SaveData.ModSessionsPerLevelBinary.Remove(sid);
        }

        public void ExitToLobby(ChapterPanelSequence panel)
        {
            panel.Focused = false;
            Gameplay.AudioSession.Play("event:/ui/world_map/chapter/back");
            panel.OwnedComponents.Add(new SimulationCoroutine(Engine, ExitToLobbyRoutine(panel)));
        }

        private IEnumerator ExitToLobbyRoutine(ChapterPanelSequence panel)
        {
            panel.EnteringChapter = true;
            panel.OwnedComponents.Add(new SimulationCoroutine(Engine, panel.EaseOut(false)));
            yield return 0.2f;
            ((ISourceScreenWipeEnvironment)Engine).ProcessWipe.WipeColor = Celeste.Core.Display.PackedColor.Black;
            Areas.GetArea(panel.Area).Wipe(panel.Overworld, false, null);
            Gameplay.AudioSession.SetMusic(null);
            Gameplay.AudioSession.SetAmbience(null);
            yield return 0.5f;
            var level = (OriginalLevelSceneRoot)wrapper.Scene;
            foreach (LevelEndingHookState component in level.SourceScene.Components.GetTrackedComponents(
                         "Celeste.LevelEndingHook"))
                component.OnEnd?.Invoke();
            Engine.Frame.Scenes.NextScene =
                new LevelExitToLobbySequence(Module, Engine, LevelExitMode.GiveUp, level.Session);
        }

        public void Dispose()
        {
            if (wrapper?.Scene is OriginalLevelSceneRoot level) Close(level, true, true);
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            openCallbacks.Clear();
            pendingReturn = null;
            pendingTargetSession = null;
        }
    }
}
