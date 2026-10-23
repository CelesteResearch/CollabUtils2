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
        #region Entity Overrides

        public override void Added(object scene)
        {
            base.Added(scene);

            openedWithRevealMap = module.SaveData.RevealMap;

            if (scene is Level level && level.SourceScene.Entities.GetTrackedEntity<Player>() is Player player)
            {
                initialPlayerCenter = player.Center;

                SetLocked(engine, true);

                var path = player.Inventory.Backpack ? "marker/runBackpack" : "marker/runNoBackpack";
                Add(maddyRunSprite = new Sprite(engine, Mountain.Mountain, path));
                maddyRunSprite.Justify = new Vector2(0.5f, 1f);
                maddyRunSprite.Scale = new Vector2(0.3f);
                maddyRunSprite.Visible = false;
                maddyRunSprite.AddLoop("idle", "", 1 / 8f);
                maddyRunSprite.Play("idle");

                // find all the lobby controllers for this collab
                getLobbyControllers(level);

                // if we successfully selected a lobby to view, open the screen
                if (updateSelectedLobby(true))
                {
                    openScreen();
                }
            }
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            lobbyMapJoystick.Deregister();
            lobbyMapUpButton.Deregister();
            lobbyMapDownButton.Deregister();
            lobbyMapLeftButton.Deregister();
            lobbyMapRightButton.Deregister();
            holdToPanButtonRenderInfo.Button1.Deregister();
            aimButtonRenderInfo.Button1.Deregister();
            aimButtonRenderInfo.Button2.Deregister();
            aimButtonRenderInfo.Button3.Deregister();
            aimButtonRenderInfo.Button4.Deregister();
            renderTarget?.Dispose();
            overlayTexture?.Dispose();

            renderTarget = null;
            overlayTexture = null;
            mapTexture = null;

            SetLocked(engine, false);
        }

        public override void Update()
        {
            base.Update();

            if (!CheckLocked())
            {
                // something exploity happened, so bail
                closeScreen();
                return;
            }

            // handle input
            if (focused)
            {
                var holdToPan = module.Settings.HoldToPan.Check;
                var mainAim = Input.Aim.Value;
                var lobbyAim = lobbyMapJoystick.Value;
                var mainAiming = ((ISourceVectorEnvironment)engine).VectorArithmetic.LengthSquared(mainAim) >
                                 float.Epsilon;

                // if holding pan and using the main aim method, use main aim, otherwise fall back to our custom aim
                var aim = holdToPan && mainAiming ? mainAim : lobbyAim;
                var aiming = ((ISourceVectorEnvironment)engine).VectorArithmetic.LengthSquared(aim) > float.Epsilon;

                if (!viewOnly && activeWarps.Count > 0 && !holdToPan)
                {
                    int moveDir = 0;
                    if (Input.MenuUp.Pressed)
                    {
                        if (!Input.MenuUp.Repeating && selectedWarpIndexes[selectedLobbyIndex] == 0)
                        {
                            selectedWarpIndexes[selectedLobbyIndex] = activeWarps.Count - 1;
                            moveDir = -1;
                        }
                        else if (selectedWarpIndexes[selectedLobbyIndex] > 0)
                        {
                            selectedWarpIndexes[selectedLobbyIndex]--;
                            moveDir = -1;
                        }
                    }
                    else if (Input.MenuDown.Pressed)
                    {
                        if (!Input.MenuDown.Repeating &&
                            selectedWarpIndexes[selectedLobbyIndex] == activeWarps.Count - 1)
                        {
                            selectedWarpIndexes[selectedLobbyIndex] = 0;
                            moveDir = 1;
                        }
                        else if (selectedWarpIndexes[selectedLobbyIndex] < activeWarps.Count - 1)
                        {
                            selectedWarpIndexes[selectedLobbyIndex]++;
                            moveDir = 1;
                        }
                    }

                    if (moveDir != 0)
                    {
                        ((IOriginalGameplayProcess)engine).AudioSession.Play(moveDir < 0
                            ? "event:/ui/main/rollover_up"
                            : "event:/ui/main/rollover_down");
                        selectWarpWiggler.Start();
                    }
                }

                if (!holdToPan && Input.MenuLeft.Pressed)
                {
                    if (selectedLobbyIndex > 0)
                    {
                        ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/ui/main/rollover_up");
                        selectLobbyWiggler.Start();
                        lastSelectedWarpIndex = -1;
                        selectedLobbyIndex--;
                        updateSelectedLobby();
                    }
                }
                else if (!holdToPan && Input.MenuRight.Pressed)
                {
                    if (selectedLobbyIndex < lobbySelections.Count - 1)
                    {
                        ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/ui/main/rollover_down");
                        selectLobbyWiggler.Start();
                        lastSelectedWarpIndex = -1;
                        selectedLobbyIndex++;
                        updateSelectedLobby();
                    }
                }
                else if (Input.MenuJournal.Pressed)
                {
                    zoomWiggler.Start();
                    zoomLevel--;
                    if (zoomLevel < 0)
                    {
                        zoomLevel = zoomLevels.Length - 1;
                        ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/ui/main/rollover_up");
                    }
                    else
                    {
                        ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/ui/main/rollover_down");
                    }

                    targetScale = zoomLevels[zoomLevel];
                    scaleTimeRemaining = scale_time_seconds;
                    shouldCentreOrigin = zoomLevel == 0;

                    if (shouldCentreOrigin || zoomLevel == zoomLevels.Length - 1)
                    {
                        targetOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
                        translateTimeRemaining = translate_time_seconds;
                    }
                }
                else if (!shouldCentreOrigin && translateTimeRemaining <= 0 && scaleTimeRemaining <= 0 && aiming &&
                         mapTexture != null)
                {
                    var aspectRatio = (float)mapTexture.Width / mapTexture.Height;
                    var offset = ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(aim) * 2f /
                                 actualScale;
                    if (aspectRatio > 0)
                    {
                        offset.X /= aspectRatio;
                    }
                    else
                    {
                        offset.Y *= aspectRatio;
                    }

                    var newOrigin = actualOrigin + offset * engine.DeltaTime;
                    actualOrigin = Vector2.Clamp(newOrigin, Vector2.Zero, Vector2.One);

                    // update the selected warp if we should
                    if (!viewOnly && !shouldCentreOrigin)
                    {
                        var nearestWarpIndex = nearestWarpIndexToActualOrigin();
                        if (nearestWarpIndex != selectedWarpIndexes[selectedLobbyIndex])
                        {
                            ((IOriginalGameplayProcess)engine).AudioSession.Play(
                                nearestWarpIndex < selectedWarpIndexes[selectedLobbyIndex]
                                    ? "event:/ui/main/rollover_up"
                                    : "event:/ui/main/rollover_down");
                            selectedWarpIndexes[selectedLobbyIndex] = lastSelectedWarpIndex = nearestWarpIndex;
                            selectedOrigin = originForPosition(activeWarps[nearestWarpIndex].Position);
                        }
                    }

                    // make sure the markers are in the right place
                    updateMarkers();
                }

                var close = false;
                var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
                if (!viewOnly && Input.MenuConfirm.Pressed && warpIndex >= 0 && warpIndex < activeWarps.Count)
                {
                    if (selectedLobbyIndex == initialLobbyIndex && warpIndex == initialWarpIndex)
                    {
                        // confirming on the initial warp just closes the screen
                        close = true;
                    }
                    else
                    {
                        var warp = activeWarps[warpIndex];
                        confirmWiggler.Start();
                        teleportToWarp(warp);
                    }
                }
                else if (Input.MenuCancel.Pressed)
                {
                    close = true;
                }
                else if (Input.ESC.Pressed)
                {
                    close = true;
                    Input.ESC.ConsumeBuffer();
                }
                else if (Input.Pause.Pressed)
                {
                    close = true;
                    Input.Pause.ConsumeBuffer();
                }

                if (close)
                {
                    closeWiggler.Start();
                    closeScreen();
                    return;
                }
            }

            // update map position for warp selection if not view only
            if (!viewOnly && activeWarps.Count > 0 && lastSelectedWarpIndex != selectedWarpIndexes[selectedLobbyIndex])
            {
                var warp = activeWarps[selectedWarpIndexes[selectedLobbyIndex]];
                selectedOrigin = originForPosition(warp.Position);

                if (lastSelectedWarpIndex < 0)
                {
                    actualOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
                }
                else if (!shouldCentreOrigin)
                {
                    targetOrigin = selectedOrigin;
                    translateTimeRemaining = translate_time_seconds;
                }

                lastSelectedWarpIndex = selectedWarpIndexes[selectedLobbyIndex];
            }
        }

        /// <summary>
        /// Finds the index into <see cref="activeWarps"/> that has the nearest warp to <see cref="actualOrigin"/>.
        /// </summary>
        private int nearestWarpIndexToActualOrigin()
        {
            int nearestIndex = -1;
            var nearestLengthSquared = float.MaxValue;
            for (int i = 0; i < activeWarps.Count; i++)
            {
                var warpOrigin = originForPosition(activeWarps[i].Position);
                var warpLengthSquared =
                    ((ISourceVectorEnvironment)engine).VectorArithmetic.LengthSquared(actualOrigin - warpOrigin);
                if (warpLengthSquared < nearestLengthSquared)
                {
                    nearestLengthSquared = warpLengthSquared;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        #endregion
    }
}
