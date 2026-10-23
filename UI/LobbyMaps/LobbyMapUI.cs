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
    /// <summary>
    /// Displays a map allowing the player to warp between certain locations in lobbies.
    /// </summary>
    [Celeste.Scene.Tracking.Attributes.SceneTracked]
    public partial class LobbyMapUI : Entity
    {
        private readonly ISourceEngineContext engine;
        private readonly CollabModule module;
        private readonly LobbyMapButtonPresentation buttons;

        private readonly BlendState explorationAlphaBlend = new BlendState
        {
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.Zero,
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.Zero
        };

        private readonly BlendState exploredMapBlend = new BlendState
        {
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
            ColorSourceBlend = Blend.DestinationAlpha,
            ColorDestinationBlend = Blend.Zero
        };

        private OriginalGameInputProcess Input => ((IOriginalGameInputProcessEnvironment)engine).InputSource;
        private SourceMInputProcess MInput => ((ISourceMInputEnvironment)engine).MInput;
        private OriginalGameTextProcess Text => ((IOriginalGameTextEnvironment)engine).Text;

        private OriginalGameGraphicsResources Graphics =>
            (OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics;

        private OriginalMountainResources Mountain => ((IOriginalMountainEnvironment)engine).MountainResources;

        private IOriginalEngineGraphicsState GraphicsState =>
            ((IOriginalEngineGraphicsEnvironment)engine).EngineGraphics;

        private IOriginalAreaKeyPolicy Areas => ((IOriginalAreaKeyEnvironment)engine).AreaKeys;

        #region Fields

        // input handling
        private readonly VirtualJoystick lobbyMapJoystick;
        private readonly VirtualButton lobbyMapUpButton;
        private readonly VirtualButton lobbyMapDownButton;
        private readonly VirtualButton lobbyMapLeftButton;
        private readonly VirtualButton lobbyMapRightButton;

        // cached button render info
        private LobbyMapButtonRenderInfo changeDestinationButtonRenderInfo;
        private LobbyMapButtonRenderInfo changeLobbyButtonRenderInfo;
        private LobbyMapButtonRenderInfo closeButtonRenderInfo;
        private LobbyMapButtonRenderInfo confirmButtonRenderInfo;
        private LobbyMapButtonRenderInfo zoomButtonRenderInfo;
        private LobbyMapButtonRenderInfo holdToPanButtonRenderInfo;
        private LobbyMapButtonRenderInfo panButtonRenderInfo;
        private LobbyMapButtonRenderInfo aimButtonRenderInfo;

        // all lobbies in the collab
        private readonly List<LobbyMapSelection> lobbySelections = new List<LobbyMapSelection>();

        // all warps for the selected lobby
        private readonly List<LobbyMapMarkerInfo> activeWarps = new List<LobbyMapMarkerInfo>();

        // current lobby setup
        private int selectedLobbyIndex;
        private int[] selectedWarpIndexes;
        private LobbyMapControllerInfo lobbyMapInfo;
        private LobbyVisitedByteGrid visitedTiles;
        private LobbyVisitManager visitManager;
        private int heartCount;
        private int initialLobbyIndex;
        private int initialWarpIndex;

        // resources
        private Texture2D mapTexture;
        private Texture2D overlayTexture;
        private VirtualRenderTarget renderTarget;
        private readonly List<Component> markerComponents = new List<Component>();
        private readonly MTexture arrowTexture;
        private Sprite heartSprite;
        private Sprite maddyRunSprite;
        private readonly Wiggler selectWarpWiggler;
        private readonly Wiggler selectLobbyWiggler;
        private readonly Wiggler closeWiggler;
        private readonly Wiggler confirmWiggler;
        private readonly Wiggler zoomWiggler;

        // current view
        private readonly float[] zoomLevels = { 1f, 2f, 3f };
        private const int defaultZoomLevel = 1;
        private int zoomLevel = -1;
        private float actualScale;
        private Vector2 actualOrigin;
        private float targetScale = 1f;
        private Vector2 targetOrigin = Vector2.Zero;
        private Vector2 selectedOrigin = Vector2.Zero;
        private bool shouldShowMaddy;
        private bool shouldCentreOrigin;
        private float scaleTimeRemaining;
        private float translateTimeRemaining;
        private const float scale_time_seconds = 0.3f;
        private const float translate_time_seconds = 0.3f;
        private int lastSelectedWarpIndex = -1;
        private float scaleMultiplier = 1f;
        private float finalScale => actualScale * scaleMultiplier;

        private readonly LobbyMapCustomizationRegistry customizations;

        public static void AddCustomRenderAction(ISourceEngineContext engine, string collabID,
            Action<Entity, List<Component>> action) =>
            LobbyMapCustomizationRegistry.For(engine).RenderActions[collabID] = action;

        public static void RemoveCustomRenderAction(ISourceEngineContext engine, string collabID) =>
            LobbyMapCustomizationRegistry.For(engine).RenderActions.Remove(collabID);

        private Rectangle windowBounds;
        private Rectangle mapBounds;

        private bool focused;
        private bool closing;

        private bool openedWithRevealMap;
        private readonly bool viewOnly;
        private Vector2 initialPlayerCenter;


        public LobbyMapUI(ISourceEngineContext engine, bool viewOnly = false) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            customizations = LobbyMapCustomizationRegistry.For(engine);
            module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            buttons = new LobbyMapButtonPresentation(Input, Text);
            arrowTexture = Graphics.GuiAtlas["towerarrow"];
            Tags = ((ISourceBitTagEnvironment)engine).BitTags.Get("pauseUpdate") |
                   ((ISourceHudTagEnvironment)engine).HudTag;
            Depth = -10502;
            Visible = false;

            lobbyMapJoystick = new VirtualJoystick(MInput,
                module.Settings.PanLobbyMapUp.Binding,
                module.Settings.PanLobbyMapDown.Binding,
                module.Settings.PanLobbyMapLeft.Binding,
                module.Settings.PanLobbyMapRight.Binding, Input.Gamepad, 0.1f);
            lobbyMapUpButton =
                new VirtualButton(MInput, module.Settings.PanLobbyMapUp.Binding, Input.Gamepad, 0f, 0.4f);
            lobbyMapDownButton =
                new VirtualButton(MInput, module.Settings.PanLobbyMapDown.Binding, Input.Gamepad, 0f, 0.4f);
            lobbyMapLeftButton =
                new VirtualButton(MInput, module.Settings.PanLobbyMapLeft.Binding, Input.Gamepad, 0f, 0.4f);
            lobbyMapRightButton =
                new VirtualButton(MInput, module.Settings.PanLobbyMapRight.Binding, Input.Gamepad, 0f, 0.4f);

            this.viewOnly = viewOnly;

            const int top = 140, bottom = 80, left = 100, right = 100;
            const int topPadding = 10, bottomPadding = 40, leftPadding = 10, rightPadding = 10;
            windowBounds = new Rectangle(left, top, GraphicsState.Width - left - right,
                GraphicsState.Height - top - bottom);
            mapBounds = new Rectangle(windowBounds.Left + leftPadding, windowBounds.Top + topPadding,
                windowBounds.Width - leftPadding - rightPadding, windowBounds.Height - topPadding - bottomPadding);

            renderTarget =
                ((ISourceVirtualContentEnvironment)engine).VirtualContent.CreateRenderTarget("CU2_LobbyMapUI",
                    windowBounds.Width, windowBounds.Height);
            Add(selectWarpWiggler = Wiggler.Create(((ISourceWigglerPoolScope)engine).WigglerPool, 0.4f, 4f));
            Add(selectLobbyWiggler = Wiggler.Create(((ISourceWigglerPoolScope)engine).WigglerPool, 0.4f, 4f));
            Add(closeWiggler = Wiggler.Create(((ISourceWigglerPoolScope)engine).WigglerPool, 0.4f, 4f));
            Add(confirmWiggler = Wiggler.Create(((ISourceWigglerPoolScope)engine).WigglerPool, 0.4f, 4f));
            Add(zoomWiggler = Wiggler.Create(((ISourceWigglerPoolScope)engine).WigglerPool, 0.4f, 4f));

            Add(new BeforeRenderCallbackComponentState(beforeRender));
            Add(new Coroutine(engine, mapFocusRoutine()));

            // cache render info
            changeDestinationButtonRenderInfo = new LobbyMapButtonRenderInfo(
                Text.Dialog.Clean("collabutils2_lobbymap_change_destination"), Input.MenuUp, Input.MenuDown,
                wiggler: selectWarpWiggler);
            changeLobbyButtonRenderInfo = new LobbyMapButtonRenderInfo(
                Text.Dialog.Clean("collabutils2_lobbymap_change_lobby"), Input.MenuLeft, Input.MenuRight,
                wiggler: selectLobbyWiggler);
            closeButtonRenderInfo = new LobbyMapButtonRenderInfo(Text.Dialog.Clean("collabutils2_lobbymap_close"),
                Input.MenuCancel, wiggler: closeWiggler);
            confirmButtonRenderInfo = new LobbyMapButtonRenderInfo(Text.Dialog.Clean("collabutils2_lobbymap_confirm"),
                Input.MenuConfirm, wiggler: confirmWiggler);
            zoomButtonRenderInfo = new LobbyMapButtonRenderInfo(Text.Dialog.Clean("collabutils2_lobbymap_zoom"),
                Input.MenuJournal, wiggler: zoomWiggler);
            holdToPanButtonRenderInfo = new LobbyMapButtonRenderInfo(
                Text.Dialog.Clean("collabutils2_lobbymap_hold_to_pan"),
                new VirtualButton(MInput, null, Input.Gamepad, 0f, 0.4f)
                    { Binding = module.Settings.HoldToPan.Binding });

            // pan can be custom or aim
            panButtonRenderInfo = new LobbyMapButtonRenderInfo(Text.Dialog.Clean("collabutils2_lobbymap_pan"),
                lobbyMapUpButton, lobbyMapDownButton, lobbyMapLeftButton, lobbyMapRightButton);
            aimButtonRenderInfo = new LobbyMapButtonRenderInfo(Text.Dialog.Clean("collabutils2_lobbymap_pan"),
                new VirtualButton(MInput, null, Input.Gamepad, 0f, 0.4f)
                    { Binding = ((IOriginalGameInputSourceEnvironment)engine).InputSourceSettings.Up },
                new VirtualButton(MInput, null, Input.Gamepad, 0f, 0.4f)
                    { Binding = ((IOriginalGameInputSourceEnvironment)engine).InputSourceSettings.Down },
                new VirtualButton(MInput, null, Input.Gamepad, 0f, 0.4f)
                    { Binding = ((IOriginalGameInputSourceEnvironment)engine).InputSourceSettings.Left },
                new VirtualButton(MInput, null, Input.Gamepad, 0f, 0.4f)
                    { Binding = ((IOriginalGameInputSourceEnvironment)engine).InputSourceSettings.Right });
        }

        #endregion
    }
}
