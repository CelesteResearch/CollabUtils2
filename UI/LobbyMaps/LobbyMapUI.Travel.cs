using Celeste.Game.Chapters.Flow;
using Celeste.Framework.Diagnostics.Commands;
using Celeste.Everest;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Everest.Content.Maps.Metadata;
using Celeste.Mods.CollabUtils2.Entities.LobbyMaps;
using Celeste.Core.Mathematics.Easing;
using MonoMod.ModInterop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Core.Components;
using Celeste.Core.Components.Rendering;
using Celeste.Core.Randomness;
using Celeste.Core.Mathematics;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Execution.Engine;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Session;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Game.Save.Runtime;
using Celeste.Game.Controls.Source;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tagging;
using Celeste.Scene.Components.Oscillation;
using Celeste.Everest.Input;
using Celeste.Everest.Modules.Runtime;
using Celeste.Input.Runtime;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Maps.LobbyMaps;
using Celeste.Simulation.Gameplay.Player.Intro;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using Component = Celeste.Core.Components.SourceComponentState;
using Player = Celeste.Simulation.RoomPlayerSession;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Scene = Celeste.Scene.Scenes.SourceSceneState;
using EntityData = Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData;
using LevelData = Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData;
using MapData = Celeste.Game.Content.Maps.OriginalChapterMapData;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using Sprite = Celeste.Graphics.Animation.Sprite;
using Wiggler = Celeste.Scene.Components.Oscillation.WigglerComponent;
using Color = Celeste.Core.Display.PackedColor;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Texture2D = Celeste.Graphics.Rendering.Textures.SourceTexture2DResource;
using VirtualJoystick = Celeste.Everest.Input.SourceEverestVirtualJoystick;
using VirtualButton = Celeste.Everest.Input.SourceEverestVirtualButton;
using Coroutine = Celeste.Core.Scheduling.SimulationCoroutine;
using ScreenWipe = Celeste.Simulation.Presentation.Scene.Transitions.ScreenWipePresentation;
using FadeWipe = Celeste.Simulation.Presentation.Scene.Transitions.FadeWipePresentation;
using MountainWipe = Celeste.Simulation.Presentation.Scene.Transitions.MountainWipePresentation;
using EntityID = Celeste.Core.Maps.MapEntityId;
using Binding = Celeste.Input.Bindings.SourceInputBinding;
using AreaKey = Celeste.Game.Execution.Areas.OriginalAreaKey;
using Session = Celeste.Simulation.Runtime.Session.ChapterCompletionSession;
using BlendState = Celeste.Core.Rendering.States.SourceBlendState;
using Blend = Celeste.Core.Rendering.SourceBlendFactor;
using SpriteSortMode = Celeste.Core.Rendering.States.SourceSpriteSortMode;
using SurfaceFormat = Celeste.Graphics.Rendering.Textures.SourceSurfaceFormat;

namespace Celeste.Mods.CollabUtils2.UI.LobbyMaps
{
    public partial class LobbyMapUI
    {
        #region Lifetime

        /// <summary>
        /// Opens the screen.
        /// </summary>
        private void openScreen()
        {
            SetLocked(engine, true, Scene);

            ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/ui/game/pause");

            Add(new Coroutine(engine, transitionRoutine(onFadeOut: () => { Visible = true; })));
        }

        /// <summary>
        /// Closes the screen.
        /// </summary>
        private void closeScreen(bool force = false)
        {
            // don't try to close twice
            if (closing) return;
            closing = true;

            void DoClose()
            {
                SetLocked(engine, false, Scene);
                RemoveSelf();
            }

            if (!force)
            {
                ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/ui/game/unpause");
                Add(new Coroutine(engine, transitionRoutine(onFadeOut: () => Visible = false, onFadeIn: DoClose)));
            }
            else
            {
                Visible = false;
                DoClose();
            }
        }

        /// <summary>
        /// Teleports to the selected warp within the current map.
        /// </summary>
        private void teleportToWarp(LobbyMapMarkerInfo warp)
        {
            if (!(Scene is Level level)) return;

            focused = false;
            const float wipeDuration = 0.5f;

            ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/game/04_cliffside/snowball_spawn");

            void onComplete()
            {
                closeScreen(true);
                if (warp.SID != Areas.GetSID(Areas.GetSessionArea(level.Session)))
                {
                    level.Scheduler.OnEndOfFrame += () =>
                    {
                        var targetArea = Areas.GetAreaBySID(warp.SID) ??
                                         Areas.GetAreaById(Areas.GetSessionArea(level.Session).ID);
                        var levelData = targetArea.Mode[0].MapData.Get(warp.Room);
                        var session =
                            ((IOriginalSessionEnvironment)engine).Sessions.CreateSession(Areas.ToKey(targetArea));
                        session.Level = warp.Room;
                        session.FirstLevel = false;
                        session.RespawnPoint = Vector2Selection.ClosestTo(levelData.Spawns,
                            levelData.Position + warp.Position, ((ISourceVectorEnvironment)engine).VectorArithmetic);
                        OriginalLevelEntryRouting.Go(session, false, engine.Frame.Scenes,
                            ((IOriginalLevelEntryEnvironment)engine).LevelEntries);
                    };
                }
                else
                {
                    level.Scheduler.OnEndOfFrame += () =>
                    {
                        if (level.SourceScene.Entities.GetTrackedEntity<Player>() is Player oldPlayer)
                        {
                            OriginalStrawberryTransfer.StoreStrawberries(oldPlayer.Leader);
                            level.RemoveEntity(oldPlayer);
                        }

                        level.UnloadLevel();
                        level.Session.Level = warp.Room;
                        level.Session.FirstLevel = false;
                        level.Session.RespawnPoint =
                            level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Top) + warp.Position);
                        level.LoadLevel(PlayerIntroType.Respawn);
                        level.Wipe?.Cancel();

                        if (level.SourceScene.Entities.GetTrackedEntity<Player>() is Player newPlayer)
                        {
                            level.Camera.Position = newPlayer.CameraTarget;
                            OriginalStrawberryTransfer.RestoreStrawberries(newPlayer.Leader);
                        }

                        createWipe(warp.WipeType, wipeDuration, level, true);
                    };
                }
            }

            createWipe(warp.WipeType, wipeDuration, level, false, onComplete);
        }

        #endregion
    }
}
