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
        #region Routines

        /// <summary>
        /// Tweens the current scale and origin to expected values.
        /// </summary>
        private IEnumerator mapFocusRoutine()
        {
            float scaleFrom = actualScale;
            Vector2 translateFrom = actualOrigin;

            while (true)
            {
                if (scaleTimeRemaining == scale_time_seconds) scaleFrom = actualScale;
                if (translateTimeRemaining == translate_time_seconds) translateFrom = actualOrigin;

                if (scaleTimeRemaining > 0)
                {
                    actualScale = ScalarMath.LerpClamp(scaleFrom, targetScale,
                        ((ISourceEasingEnvironment)engine).EasingArithmetic.QuintOut(1 - scaleTimeRemaining /
                            scale_time_seconds), ((ISourceScalarEnvironment)engine).ScalarArithmetic);
                    scaleTimeRemaining -= engine.DeltaTime;
                    if (scaleTimeRemaining <= 0) actualScale = targetScale;
                }

                if (translateTimeRemaining > 0)
                {
                    actualOrigin = Vector2.Lerp(translateFrom, targetOrigin,
                        ((ISourceEasingEnvironment)engine).EasingArithmetic.QuintOut(1 - translateTimeRemaining /
                            translate_time_seconds));
                    translateTimeRemaining -= engine.DeltaTime;
                    if (translateTimeRemaining <= 0) actualOrigin = targetOrigin;
                }

                updateMarkers();

                yield return null;
            }
        }

        /// <summary>
        /// Transitions the screen in and out with a fade wipe.
        /// </summary>
        private IEnumerator transitionRoutine(float duration = 0.5f, Action onFadeOut = null, Action onFadeIn = null)
        {
            duration = Math.Max(0f, duration);

            focused = false;

            yield return new FadeWipe((Celeste.Scene.Rendering.ISourceRendererScene)Scene, false)
            {
                Duration = duration / 2f,
                OnComplete = onFadeOut,
            }.Wait();

            yield return new FadeWipe((Celeste.Scene.Rendering.ISourceRendererScene)Scene, true)
            {
                Duration = duration / 2f,
                OnComplete = onFadeIn,
            }.Wait();

            focused = true;
        }

        #endregion
    }
}
