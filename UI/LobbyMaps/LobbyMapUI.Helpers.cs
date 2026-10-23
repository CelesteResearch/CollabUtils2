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
        #region Static Helper Methods

        /// <summary>
        /// Generates a 2d array of bytes representing all visited tiles in the map.
        /// </summary>
        private static LobbyVisitedByteGrid generateVisitedTiles(LobbyMapControllerInfo config,
            LobbyVisitManager visitManager)
        {
            var circle = createCircleData(LobbyVisitManager.EXPLORATION_RADIUS - 1,
                LobbyVisitManager.EXPLORATION_RADIUS + 1);

            var visitedTiles = new LobbyVisitedByteGrid(config.RoomWidth, config.RoomHeight);

            // apply a circle for each visited point
            foreach (var pos in visitManager.VisitedPoints)
            {
                visitedTiles.Max(circle, (int)pos.Point.X - circle.Width / 2, (int)pos.Point.Y - circle.Height / 2);
            }

            // fuzzy edges to remove the hard map boundary
            const byte quarter = byte.MaxValue / 4;
            for (int x = 0; x < config.RoomWidth; x++)
            {
                visitedTiles[x, 0] = 0;
                visitedTiles[x, 1] = Math.Min(visitedTiles[x, 1], quarter);
                visitedTiles[x, config.RoomHeight - 1] = 0;
                visitedTiles[x, config.RoomHeight - 2] = Math.Min(visitedTiles[x, config.RoomHeight - 2], quarter);
            }

            for (int y = 1; y < config.RoomHeight - 1; y++)
            {
                visitedTiles[0, y] = 0;
                visitedTiles[1, y] = Math.Min(visitedTiles[1, y], quarter);
                visitedTiles[config.RoomWidth - 1, y] = 0;
                visitedTiles[config.RoomWidth - 2, y] = Math.Min(visitedTiles[config.RoomWidth - 2, y], quarter);
            }

            return visitedTiles;
        }

        /// <summary>
        /// Generates a 2D array of bytes containing an antialiased circle.
        /// </summary>
        private static LobbyVisitedByteGrid createCircleData(int hardRadius, int softRadius)
        {
            var diameter = 2 * softRadius;
            var array = new LobbyVisitedByteGrid(diameter, diameter);

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    var lenSq = (softRadius - x) * (softRadius - x) + (softRadius - y) * (softRadius - y);
                    float alpha =
                        lenSq < hardRadius * hardRadius ? 0f :
                        lenSq > softRadius * softRadius ? 1f :
                        ((float)lenSq - hardRadius * hardRadius) / (softRadius * softRadius - hardRadius * hardRadius);
                    array[x, y] = (byte)((1 - alpha) * byte.MaxValue);
                }
            }

            return array;
        }

        /// <summary>
        /// Creates a screen wipe animation.
        /// </summary>
        private ScreenWipe createWipe(string wipeTypeName, float wipeDuration, Level level, bool wipeIn,
            Action onComplete = null)
        {
            Type wipeType = ((IEverestFakeEntryAssemblyEnvironment)engine).FakeEntryAssembly.GetType(wipeTypeName);
            if (wipeType == null)
            {
                ((IEverestLoggerProcessEnvironment)engine).Logger.Log(EverestLogLevel.Warn, "CollabUtils2/LobbyMapUI",
                    $"Couldn't find wipe \"{wipeTypeName}\", falling back to Celeste.Mountain");
                wipeType = typeof(MountainWipe);
            }

            var screenWipe = (ScreenWipe)Activator.CreateInstance(wipeType, level, wipeIn, onComplete);
            if (screenWipe != null) screenWipe.Duration = wipeDuration;
            return screenWipe;
        }

        /// <summary>
        /// Checks whether any of the passed bindings have a valid button for the latest control scheme.
        /// </summary>
        private bool hasLatestBinding(Binding binding1, Binding binding2 = null, Binding binding3 = null,
            Binding binding4 = null)
        {
            if (Input.GuiInputPrefix() == "keyboard")
            {
                return binding1?.Keyboard.Any() == true || binding2?.Keyboard.Any() == true ||
                       binding3?.Keyboard.Any() == true || binding4?.Keyboard.Any() == true;
            }
            else
            {
                return binding1?.Controller.Any() == true || binding2?.Controller.Any() == true ||
                       binding3?.Controller.Any() == true || binding4?.Controller.Any() == true;
            }
        }

        /// <summary>
        /// Find the first entity in the level data with the specified name.
        /// </summary>
        private static EntityData findEntityData(LevelData levelData, string entityName) =>
            levelData.Entities.FirstOrDefault(e => e.Name == entityName);

        /// <summary>
        /// Returns true if at least one silver berry has been collected for this lobby.
        /// </summary>
        private bool isRainbowBerryUnlocked(string levelSet)
        {
            if (!module.Maps.SilverBerries.ContainsKey(levelSet)) return false;

            foreach (KeyValuePair<string, EntityID> requiredSilver in module.Maps.SilverBerries[levelSet])
            {
                // check if the silver was collected.
                var stats =
                    ((IOriginalSaveCounterEnvironment)engine).SaveCounters.GetAreasSafe(
                        ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave)[
                        Areas.GetAreaBySID(requiredSilver.Key).ID];
                if (stats.Modes[0].Strawberries.Contains(requiredSilver.Value)) return true;
            }

            return false;
        }

        /// <summary>
        /// Enforces removal of player control as much as possible.
        /// </summary>
        public static void SetLocked(ISourceEngineContext engine, bool locked, object scene = null,
            Player player = null)
        {
            // if we didn't pass in a scene and/or player, grab them ourselves
            Level level = (scene ?? engine.Frame.Scenes.CurrentScene) as Level;
            player = player ?? level?.SourceScene.Entities.GetTrackedEntity<Player>();
            if (level == null || player == null) return;

            level.CanRetry = !locked;
            level.PauseLock = locked;
            player.Speed = Vector2.Zero;
            player.Dummy.Gravity = !locked;
            player.Machine.State = locked ? 11 : 0;

            // disable auto animate if we're locking while not on the ground (happens while swimming)
            player.Dummy.AutoAnimate = !locked || player.OnGround();
        }

        /// <summary>
        /// Verifies that player control is still removed and there's no interaction storage we know of.
        /// </summary>
        public bool CheckLocked()
        {
            if (Scene is Level level && level.SourceScene.Entities.GetTrackedEntity<Player>() is Player player)
            {
                // we really want to stop interaction storage, so if any of these checks fail, it's no longer valid to show the map
                return ((ISourceVectorEnvironment)engine).VectorArithmetic.LengthSquared(initialPlayerCenter -
                           player.Center) < 16 * 16 &&
                       !player.Dead && !level.CanRetry && level.PauseLock && !player.Dummy.Gravity &&
                       player.Speed == Vector2.Zero && player.Machine.State == 11;
            }

            return false;
        }

        #endregion
    }
}
