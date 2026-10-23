using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Maps;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Runtime.Events;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Hud.Strawberries;
using Celeste.Game.Save.Runtime;
using Celeste.Game.Settings;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Persistence;
using Celeste.Mods.CollabUtils2.UI;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Player.Death;
using Celeste.Simulation.UI.Menus.Text;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.Entities
{
    public sealed class CollabBerryHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<Hook> hooks = new List<Hook>();
        private CollabModule module;
        private SpeedBerry storedSpeedBerry;
        public bool TransitionJustTriggered { get; set; }
        private EverestGameEventsProcess Events => ((IEverestGameEventsEnvironment)engine).GameEvents;

        public CollabBerryHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
        }

        public void Load()
        {
            module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            using (new DetourConfigContext(new DetourConfig("CollabUtils2_BeforeAll")
                       .WithPriority(int.MinValue)).Use())
            {
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.DieSourceBody)),
                    (Func<Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation>, RoomPlayerSession,
                        Vector2, bool, bool, PlayerDeathPresentation>)OnPlayerDie));
            }

            hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.AddedSourceBody)),
                (Action<Action<RoomPlayerSession, object>, RoomPlayerSession, object>)OnPlayerAdded));
            hooks.Add(new Hook(
                typeof(OriginalLevelSceneRoot).GetMethod("NextLevel", BindingFlags.Instance | BindingFlags.NonPublic),
                (Action<Action<OriginalLevelSceneRoot, Vector2, Vector2>, OriginalLevelSceneRoot, Vector2, Vector2>)
                ((original, level, at, direction) =>
                {
                    original(level, at, direction);
                    if (ReferenceEquals(level.EngineContext, engine)) TransitionJustTriggered = true;
                })));
            hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.OnTransition)),
                (Action<Action<RoomPlayerSession>, RoomPlayerSession>)((original, player) =>
                {
                    original(player);
                    if (ReferenceEquals(player.EngineContext, engine))
                        GetSpeedBerry(player.Scene)?.TimerDisplay?.StartTimer();
                })));
            hooks.Add(new Hook(typeof(OriginalGameSaveContext).GetMethod(nameof(OriginalGameSaveContext.AddStrawberry)),
                (Action<Action<OriginalGameSaveContext, MapEntityId, bool>, OriginalGameSaveContext, MapEntityId, bool>)
                ((original, saveContext, id, golden) =>
                {
                    if (ReferenceEquals(saveContext, ((IOriginalGameplayProcess)engine).SaveContext))
                    {
                        var session = ((IOriginalSaveSessionSelectionEnvironment)engine).SaveSessionSelection
                            .GetCurrentSessionSafe(saveContext.CurrentSave);
                        string sid = ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSID(
                            ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSessionArea(session));
                        if (module.Maps.SpeedBerries.TryGetValue(sid, out var speed) &&
                            speed.ID.Level == id.Level && speed.ID.ID == id.ID) return;
                    }

                    original(saveContext, id, golden);
                })));
            hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.End)),
                (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)((original, level) =>
                {
                    original(level);
                    if (ReferenceEquals(level.EngineContext, engine)) storedSpeedBerry = null;
                })));
            hooks.Add(new Hook(
                typeof(TotalStrawberriesDisplayState).GetMethod(nameof(TotalStrawberriesDisplayState.Update)),
                (Action<Action<TotalStrawberriesDisplayState>, TotalStrawberriesDisplayState>)
                OnStrawberriesDisplayUpdate));
            Events.OnCreatePauseMenuButtons += CreatePauseMenuButtons;
        }

        private PlayerDeathPresentation OnPlayerDie(
            Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation> original,
            RoomPlayerSession player, Vector2 direction, bool evenIfInvincible, bool registerDeath)
        {
            if (!ReferenceEquals(player.EngineContext, engine))
                return original(player, direction, evenIfInvincible, registerDeath);
            bool hasSilver = false;
            SpeedBerry speedBerry = null;
            if (!player.Dead && (evenIfInvincible || !player.Settings.Invincible) && player.Machine.State != 18)
            {
                foreach (var follower in player.Leader.Followers)
                {
                    if (follower.Entity is SilverBerry) hasSilver = true;
                    if (speedBerry == null && follower.Entity is SpeedBerry speed) speedBerry = speed;
                }

                if (speedBerry != null && !speedBerry.TimeRanOut)
                    speedBerry.PrepareForDeath((OriginalLevelSceneRoot)player.Scene);
            }

            var body = original(player, direction, evenIfInvincible, registerDeath);
            if (body != null)
            {
                storedSpeedBerry = speedBerry;
                if (hasSilver && speedBerry != null) body.DeathSoundOverride = "event:/SC2020_silverTimedBerry_death";
                else if (hasSilver) body.DeathSoundOverride = "event:/SC2020_silverBerry_death";
                else if (speedBerry != null) body.DeathSoundOverride = "event:/SC2020_timedBerry_death";
            }

            return body;
        }

        private void OnPlayerAdded(Action<RoomPlayerSession, object> original, RoomPlayerSession player, object scene)
        {
            original(player, scene);
            if (!ReferenceEquals(player.EngineContext, engine) || storedSpeedBerry == null) return;
            if (GetSpeedBerry(scene) == null)
            {
                var data = storedSpeedBerry.EntityData;
                Vector2 lastPosition = data.Position;
                data.Position = player.Position + new Vector2(8, -16);
                var berry = new SpeedBerry(engine, data, Vector2.Zero, storedSpeedBerry.ID, true);
                ((ISourceScene)scene).SourceScene.AddEntity(berry);
                data.Position = lastPosition;
                berry.TimerDisplay = storedSpeedBerry.TimerDisplay;
                berry.TimerDisplay.TrackedBerry = berry;
                player.Leader.GainFollower(berry.Follower);
                storedSpeedBerry.RemoveSelf();
            }
            else
            {
                storedSpeedBerry.TimerDisplay?.RemoveSelf();
            }

            storedSpeedBerry = null;
        }

        private void CreatePauseMenuButtons(OriginalLevelSceneRoot level, TextMenuPresentation menu, bool minimal)
        {
            var berry = GetSpeedBerry(level);
            if (berry == null || !berry.Follower.HasLeader || minimal) return;
            menu.Add(new TextMenuPresentation.Button(((IOriginalGameTextEnvironment)engine).Text
                .CleanDialog("collabutils2_restartspeedberry")).Pressed(() =>
            {
                level.Paused = false;
                level.PauseMainMenuOpen = false;
                menu.RemoveSelf();
                berry.TimeRanOut = true;
            }));
        }

        private void OnStrawberriesDisplayUpdate(Action<TotalStrawberriesDisplayState> original,
            TotalStrawberriesDisplayState display)
        {
            var level = display.Scene as OriginalLevelSceneRoot;
            SpeedBerryTimerDisplay timer = null;
            if (level != null && ReferenceEquals(level.EngineContext, engine))
            {
                var timers =
                    ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(typeof(SpeedBerryTimerDisplay));
                if (timers.Count != 0) timer = (SpeedBerryTimerDisplay)timers[0];
            }

            original(display);
            if (timer == null || module.Settings.SpeedBerryTimerPosition != SpeedBerryTimerPosition.TopLeft ||
                !display.Visible) return;
            float y = 206f;
            if (!level.TimerHidden)
            {
                int clock = ((ISettingsContext)engine).Settings.SpeedrunClock;
                if (clock == 1) y += 58f;
                else if (clock == 2) y += 78f;
            }

            if (!timer.Started) y += 70f;
            display.Position = new Vector2(display.Position.X, y);
        }

        private static SpeedBerry GetSpeedBerry(object scene)
        {
            if (scene == null) return null;
            var berries = ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities(typeof(SpeedBerry));
            return berries.Count == 0 ? null : (SpeedBerry)berries[0];
        }

        public void Dispose()
        {
            Events.OnCreatePauseMenuButtons -= CreatePauseMenuButtons;
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            storedSpeedBerry = null;
            TransitionJustTriggered = false;
        }
    }
}
