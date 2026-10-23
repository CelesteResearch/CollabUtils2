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
        #region Rendering

        /// <summary>
        /// BeforeRenderHook that draws the map onto a RenderTarget.
        /// This allows unexplored areas to be transparent rather than using a fake black overlay.
        /// </summary>
        private void beforeRender()
        {
            var context = ((ISourceRenderEnvironment)engine).CurrentContext;
            // bail if there's nothing to draw
            if (renderTarget?.IsDisposed != false || mapTexture?.IsDisposed != false)
            {
                return;
            }

            var position = new Vector2(mapBounds.Center.X - windowBounds.Left, mapBounds.Center.Y - windowBounds.Top);
            var scale = finalScale;
            var destWidth = mapTexture.Width * scale;
            var destHeight = mapTexture.Height * scale;
            var destRect = new Rectangle(
                (int)(position.X - actualOrigin.X * destWidth), (int)(position.Y - actualOrigin.Y * destHeight),
                (int)destWidth, (int)destHeight);

            // set the render target and clear it
            ((ISourceGraphicsDeviceEnvironment)engine).GraphicsDevice.SetRenderTarget(renderTarget.Target);
            ((ISourceGraphicsDeviceEnvironment)engine).GraphicsDevice.Clear(Color.Transparent);

            // only draw the overlay if reveal map is not enabled
            if (!openedWithRevealMap && !visitManager.VisitedAll && overlayTexture?.IsDisposed == false)
            {
                // draw the exploration as a direct alpha channel
                ((ISourceDrawScope)engine).Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, explorationAlphaBlend);
                ((ISourceDrawScope)engine).Draw.SpriteBatch.Draw(overlayTexture, destRect, Color.White.PackedValue);
                ((ISourceDrawScope)engine).Draw.SpriteBatch.End();

                // begin a sprite batch that maintains the target alpha
                ((ISourceDrawScope)engine).Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, exploredMapBlend);
            }
            else
            {
                // begin a sprite batch that overwrites the target alpha
                ((ISourceDrawScope)engine).Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            }

            // draw the map
            ((ISourceDrawScope)engine).Draw.SpriteBatch.Draw(mapTexture, destRect, Color.White.PackedValue);
            ((ISourceDrawScope)engine).Draw.SpriteBatch.End();

            // draw a rectangle around the map if the console is open
            if (((ISourceCommandsProcessEnvironment)engine).Commands.Open)
            {
                ((ISourceDrawScope)engine).Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                SourcePrimitiveDrawing.HollowRect(context, this, destRect.X, destRect.Y, destRect.Width,
                    destRect.Height, Color.Red);
                ((ISourceDrawScope)engine).Draw.SpriteBatch.End();
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            drawBackground(context);
            drawMap(context);

            base.Render(context);

            if (shouldShowMaddy)
            {
                maddyRunSprite?.Render(context);
            }

            if (module.SaveData.ShowVisitedPoints)
            {
                drawVisitedPoints(context);
            }

            string currentCollabName =
                module.Identities.GetCollabNameForSID(Areas.GetSID(Areas.GetSessionArea(((Level)Scene).Session)));
            if (customizations.RenderActions.TryGetValue(currentCollabName,
                    out Action<Entity, List<Component>> customRenderAction))
                customRenderAction(this, markerComponents);

            drawForeground(context);
        }

        /// <summary>
        /// Draws a tinted background.
        /// </summary>
        private void drawBackground(ISourceRenderContext context)
        {
            SourcePrimitiveDrawing.Rect(context, this, 0, 0, GraphicsState.Width, GraphicsState.Height,
                Color.Black * 0.9f);
        }

        /// <summary>
        /// Draws all foreground components such as black borders, titles, heart count, etc.
        /// </summary>
        private void drawForeground(ISourceRenderContext context)
        {
            const int margin = 8;
            const int thickness = 8;
            const int safety = 10;
            var safeBounds = new Rectangle(-safety, -safety, GraphicsState.Width + 2 * safety,
                GraphicsState.Height + 2 * safety);
            var border = windowBounds;
            border.Inflate(margin + thickness, margin + thickness);

            // draw borders
            SourcePrimitiveDrawing.Rect(context, this, safeBounds.Left, safeBounds.Top, safeBounds.Width,
                windowBounds.Top - safeBounds.Top, Color.Black);
            SourcePrimitiveDrawing.Rect(context, this, safeBounds.Left, windowBounds.Bottom, safeBounds.Width,
                safeBounds.Bottom - windowBounds.Bottom, Color.Black);
            SourcePrimitiveDrawing.Rect(context, this, safeBounds.Left, windowBounds.Top - safety,
                windowBounds.Left - safeBounds.Left, windowBounds.Height + 2 * safety, Color.Black);
            SourcePrimitiveDrawing.Rect(context, this, windowBounds.Right, windowBounds.Top - safety,
                safeBounds.Right - windowBounds.Right, windowBounds.Height + 2 * safety, Color.Black);
            SourcePrimitiveDrawing.Rect(context, this, border.Left, border.Top, border.Width, thickness, Color.White);
            SourcePrimitiveDrawing.Rect(context, this, border.Left, border.Bottom - thickness, border.Width, thickness,
                Color.White);
            SourcePrimitiveDrawing.Rect(context, this, border.Left, border.Top, thickness, border.Height, Color.White);
            SourcePrimitiveDrawing.Rect(context, this, border.Right - thickness, border.Top, thickness, border.Height,
                Color.White);

            var lobby = lobbySelections[selectedLobbyIndex];
            var title = Text.Dialog.Clean(lobby.SID);
            var colorAlpha = 1f;

            // draw lobby title and arrows
            const float titleScale = 1.75f;
            const float titleArrowOffset = 100f;
            const float titleArrowScale = 0.9f;
            var titleWidth = Text.ActiveFont.Measure(title).X * titleScale;
            var titleY = border.Top * 0.5f;
            Text.ActiveFont.DrawEdgeOutline(context, this, title, new Vector2(1920 / 2f, titleY),
                new Vector2(0.5f, 0.5f), Vector2.One * titleScale, Color.Gray * colorAlpha, 4f,
                Color.DarkSlateBlue * colorAlpha, 2f, Color.Black * colorAlpha);
            if (selectedLobbyIndex > 0)
            {
                SourceAtlasDrawing.DrawCentered(context, this, arrowTexture,
                    new Vector2(1920 / 2f - titleWidth / 2f - titleArrowOffset, titleY), Color.White * colorAlpha,
                    titleArrowScale);
            }

            if (selectedLobbyIndex < lobbySelections.Count - 1)
            {
                SourceAtlasDrawing.DrawCentered(context, this, arrowTexture,
                    new Vector2(1920 / 2f + titleWidth / 2f + titleArrowOffset, titleY), Color.White * colorAlpha,
                    titleArrowScale, (float)Math.PI);
            }

            // draw heart count
            if (lobbyMapInfo.ShowHeartCount && heartSprite != null)
            {
                var heartCountColor = heartCount >= lobbyMapInfo.TotalMaps ? Color.Gold : Color.White;
                var heartText = $"{heartCount} / {lobbyMapInfo.TotalMaps}";
                var measured = Text.ActiveFont.Measure(heartText);
                var position = new Vector2(heartSprite.Position.X + heartSprite.Width / 2f + 10f + measured.X / 2f,
                    heartSprite.Position.Y + heartSprite.Height / 4f);
                Text.ActiveFont.DrawOutline(context, this, heartText, position, new Vector2(0.5f), Vector2.One,
                    heartCountColor, 2f, Color.Black);
            }

            // draw selected warp title
            if (!viewOnly)
            {
                var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
                if (warpIndex >= 0 && warpIndex < activeWarps.Count &&
                    !string.IsNullOrWhiteSpace(activeWarps[warpIndex].DialogKey))
                {
                    const float warpTitleOffset = -18f;
                    const float warpTitleAlpha = 1f;
                    var clean = Text.Dialog.Clean(activeWarps[warpIndex].DialogKey);
                    Text.ActiveFont.DrawOutline(context, this, clean,
                        new Vector2(windowBounds.Center.X, windowBounds.Bottom + warpTitleOffset), new Vector2(0.5f),
                        new Vector2(0.8f), Color.White * warpTitleAlpha, 2f, Color.Black);
                }
            }

            // draw controls
            const float buttonScale = 0.5f;
            const float disabledButtonAlpha = 0.4f;
            const float xOffset = 32f, yOffset = 45f;
            const float wiggleAmount = 0.05f;

            var buttonPosition = new Vector2(windowBounds.Left, windowBounds.Bottom + yOffset);
            var holdToPan = module.Settings.HoldToPan.Check;

            // draw change destination button
            if (!viewOnly && activeWarps.Count > 1 && !holdToPan)
            {
                buttons.RenderMultiButton(context, this, ref buttonPosition, xOffset, changeDestinationButtonRenderInfo,
                    buttonScale, justifyX: 0f, wiggle: wiggleAmount);
            }

            // draw change lobby button
            if (lobbySelections.Count > 1 && !holdToPan)
            {
                changeLobbyButtonRenderInfo.Button1Alpha = selectedLobbyIndex > 0 ? 1f : disabledButtonAlpha;
                changeLobbyButtonRenderInfo.Button2Alpha =
                    selectedLobbyIndex < lobbySelections.Count - 1 ? 1f : disabledButtonAlpha;
                buttons.RenderMultiButton(context, this, ref buttonPosition, xOffset, changeLobbyButtonRenderInfo,
                    buttonScale, justifyX: 0f, wiggle: wiggleAmount);
            }

            buttonPosition.X = windowBounds.Right;

            // draw close button
            buttons.RenderMultiButton(context, this, ref buttonPosition, xOffset, closeButtonRenderInfo, buttonScale,
                justifyX: 1f, wiggle: wiggleAmount);

            // draw confirm button if not view only
            if (!viewOnly)
            {
                buttons.RenderMultiButton(context, this, ref buttonPosition, xOffset, confirmButtonRenderInfo,
                    buttonScale, justifyX: 1f, wiggle: wiggleAmount);
            }

            // draw zoom button
            buttons.RenderMultiButton(context, this, ref buttonPosition, xOffset, zoomButtonRenderInfo, buttonScale,
                justifyX: 1f, wiggle: wiggleAmount);

            var panAlpha = shouldCentreOrigin ? disabledButtonAlpha : 1f;

            if (holdToPan)
            {
                // if we're holding pan, render the aim buttons as controls
                buttons.RenderMultiButton(context, this, ref buttonPosition, xOffset, aimButtonRenderInfo, buttonScale,
                    panAlpha, justifyX: 1f, wiggle: wiggleAmount);
            }
            else if (hasLatestBinding(module.Settings.PanLobbyMapUp.Binding,
                         module.Settings.PanLobbyMapDown.Binding,
                         module.Settings.PanLobbyMapLeft.Binding,
                         module.Settings.PanLobbyMapRight.Binding))
            {
                // draw custom pan inputs if at least one is bound
                buttons.RenderMultiButton(context, this, ref buttonPosition, xOffset, panButtonRenderInfo, buttonScale,
                    panAlpha, justifyX: 1f, wiggle: wiggleAmount);
            }
            else if (hasLatestBinding(module.Settings.HoldToPan.Binding))
            {
                // draw the "hold to pan" input if it's bound
                buttons.RenderMultiButton(context, this, ref buttonPosition, xOffset, holdToPanButtonRenderInfo,
                    buttonScale, panAlpha, justifyX: 1f, wiggle: wiggleAmount);
            }
        }

        /// <summary>
        /// Draws the render target to the screen.
        /// </summary>
        private void drawMap(ISourceRenderContext context)
        {
            if (renderTarget?.IsDisposed != false) return;
            ((ISourceDrawScope)engine).Draw.SpriteBatch.Draw(renderTarget.Target,
                new Vector2(windowBounds.Left, windowBounds.Top), Color.White.PackedValue);
        }

        /// <summary>
        /// Draw every visited point.
        /// </summary>
        private void drawVisitedPoints(ISourceRenderContext context)
        {
            if (visitManager == null) return;

            var scale = finalScale;
            var actualWidth = mapTexture.Width * scale;
            var actualHeight = mapTexture.Height * scale;

            foreach (var visitedPoint in visitManager.VisitedPoints)
            {
                var origin = originForPosition(visitedPoint.Point * Vector2.One * 8);
                var originOffset = origin - actualOrigin;
                var pointX = mapBounds.Center.X + originOffset.X * actualWidth;
                var pointY = mapBounds.Center.Y + originOffset.Y * actualHeight;
                SourcePrimitiveDrawing.Rect(context, this, pointX - 1, pointY - 1, 3, 3, Color.Red);
            }
        }

        /// <summary>
        /// Ensures marker representations have the right position and scale.
        /// </summary>
        private void updateMarkers()
        {
            // for now we assume markers are all Images
            var scale = finalScale;
            var actualWidth = mapTexture.Width * scale;
            var actualHeight = mapTexture.Height * scale;
            var scaleOffset = zoomLevels[1] - actualScale;
            var imageScale = scaleOffset <= 0
                ? 1f
                : ScalarMath.LerpClamp(1f, 0.75f, scaleOffset / (zoomLevels[1] - zoomLevels[0]),
                    ((ISourceScalarEnvironment)engine).ScalarArithmetic);

            // move and scale markers
            foreach (LobbyMapMarkerImage image in markerComponents)
            {
                var origin = originForPosition(image.Info.Position);
                var originOffset = origin - actualOrigin;
                image.Position = new Vector2(mapBounds.Center.X + originOffset.X * actualWidth,
                    mapBounds.Center.Y + originOffset.Y * actualHeight);
                image.Scale = new Vector2(imageScale);
            }

            // move the player icon
            if (maddyRunSprite != null)
            {
                var selectedOriginOffset = selectedOrigin - actualOrigin;
                maddyRunSprite.Position = new Vector2(mapBounds.Center.X + selectedOriginOffset.X * actualWidth,
                    mapBounds.Center.Y + selectedOriginOffset.Y * actualHeight);
            }
        }

        #endregion
    }
}
