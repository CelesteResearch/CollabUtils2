using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.CollabUtils2.Triggers;
using Celeste.Simulation;
using Celeste.Simulation.UI.Menus.Text;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed class MiniHeartDoorAssistProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<Hook> hooks = new();

        private delegate void Orig(OriginalLevelSceneRoot level, int returnIndex, bool minimal);

        public MiniHeartDoorAssistProcess(ISourceEngineContext engine) => this.engine = engine;

        public void Load()
        {
            foreach (string method in new[] { "AssistMode", "VariantMode" })
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(method,
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Orig, OriginalLevelSceneRoot, int, bool>)AddOption));
        }

        private void AddOption(Orig orig, OriginalLevelSceneRoot level, int returnIndex, bool minimal)
        {
            orig(level, returnIndex, minimal);
            if (!ReferenceEquals(level.EngineContext, engine)) return;
            var player = level.SourceScene.Entities.GetTrackedEntities(typeof(RoomPlayerSession))
                .OfType<RoomPlayerSession>().FirstOrDefault();
            if (player == null) return;
            var trigger = level.SourceScene.Entities.GetTrackedEntities(typeof(MiniHeartDoorUnlockCutsceneTrigger))
                .OfType<MiniHeartDoorUnlockCutsceneTrigger>().FirstOrDefault(item =>
                    Celeste.Physics.Collision.SourceEntityCollision.Check(player, item));
            var gate = trigger?.FindHeartGate();
            var menu = level.SourceScene.GetToAdd().OfType<TextMenuPresentation>().FirstOrDefault();
            if (menu == null || gate == null || gate.Opened || gate.HeartGems >= gate.Requires) return;
            menu.Add(new TextMenuPresentation.Button(((IOriginalGameTextEnvironment)engine).Text.Dialog.Clean(
                "collabutils2_assist_skip")) { ConfirmSfx = "event:/ui/main/message_confirm" }.Pressed(() =>
            {
                menu.Focused = false;
                level.SourceScene.QueueSourceEntity(new AssistSkipConfirmUI(engine, () =>
                {
                    gate.ForceAllHearts = true;
                    trigger.OpenHeartGate(player);
                    level.Paused = false;
                    level.UnpauseTimer = 0.15f;
                    menu.Close();
                }, () => menu.Focused = true));
            }));
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
