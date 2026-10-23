using System.Collections;
using System.Numerics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Levels.Cutscenes;
using Celeste.Simulation;

namespace Celeste.Mods.CollabUtils2.Entities
{
    public sealed class MiniHeartDoorUnlockCutscene : OriginalCutsceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly MiniHeartDoor door;
        private readonly RoomPlayerSession player;

        public MiniHeartDoorUnlockCutscene(ISourceEngineContext engine, MiniHeartDoor door, RoomPlayerSession player)
            : base(engine)
        {
            this.engine = engine;
            this.door = door;
            this.player = player;
        }

        public override void OnBegin(OriginalLevelSceneRoot level) =>
            OwnedComponents.Add(new SimulationCoroutine(engine, Cutscene(level)));

        private IEnumerator Cutscene(OriginalLevelSceneRoot level)
        {
            while (player.Machine.State != 0) yield return null;
            player.Machine.State = 11;
            yield return 0.5f;
            yield return CameraTo(engine, door.Center - new Vector2(160f - door.Size / 2, 90f), 1f,
                ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeOut);
            door.ForceTrigger = true;
            while (door.OpenPercent < 1f) yield return null;
            yield return 1f;
            yield return CameraTo(engine, player.CameraTarget, 1f,
                ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeOut);
            EndCutscene(level);
        }

        public override void OnEnd(OriginalLevelSceneRoot level)
        {
            player.Machine.State = 0;
            player.ForceCameraUpdate = false;
            if (!WasSkipped) return;
            level.Camera.Position = player.CameraTarget;
            door.SnapOpen();
        }
    }
}
