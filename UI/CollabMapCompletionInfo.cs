using System.Numerics;
using Celeste.Core.Display;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Core.Components;
using Celeste.Game.Execution.Areas;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Chapters.Flow;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Engine;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Presentation.Hud.Speedrun;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.CollabUtils2.UI
{
    [SceneTracked]
    public sealed class CollabMapCompletionInfo : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly CollabModule module;
        private float ease;
        private string chapterTime;
        private string fileTime;
        private string chapterText;
        private string version;
        private OriginalGameTextProcess Text => ((IOriginalGameTextEnvironment)engine).Text;

        private EverestAreaCompleteInfoState Info => (EverestAreaCompleteInfoState)
            ((IOriginalAreaCompleteInfoEnvironment)engine).AreaCompleteInfo;

        public CollabMapCompletionInfo(CollabModule module, ISourceEngineContext engine)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            this.module = module;
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.HUD.Value |
                   ((IOriginalGameTagsEnvironment)engine).Tags.FrozenUpdate.Value);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            var level = (OriginalLevelSceneRoot)scene;
            Info.InitializeInstalled(false, null);
            module.Return.AppendCollabVersion(Info, level.Session);
            chapterTime = SpeedrunTimerState.ShortGameplayFormat(level.Session.Time);
            fileTime = Text.FileTime(((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Time);
            chapterText = Text.Dialog.Get("OPTIONS_SPEEDRUN_CHAPTER") + ":";
            version = ((IOriginalEngineIdentityEnvironment)engine).EngineIdentity.Version.ToString();
            SpeedrunTimerState.CalculateBaseSizes(engine);
        }

        public override void Update()
        {
            base.Update();
            ease = ScalarMath.Approach(ease, 1f, engine.DeltaTime * 4f);
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);
            Info.Info(context, this, ease, chapterTime, fileTime, chapterText, version);
            var level = (OriginalLevelSceneRoot)Scene;
            Text.ActiveFont.DrawOutline(context, this,
                Text.CleanDialog(((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSID(
                    ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSessionArea(level.Session))),
                new Vector2(960f, 900f), new Vector2(.5f), new Vector2(.5f), PackedColor.White, 2f, PackedColor.Black);
        }

        public override void SceneEnd(object scene)
        {
            base.SceneEnd(scene);
            Info.DisposeInstalled();
        }
    }
}
