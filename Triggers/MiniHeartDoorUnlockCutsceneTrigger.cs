using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.CollabUtils2.Entities;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.CollabUtils2.Triggers
{
    [SceneTracked]
    [CustomEntity("CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger")]
    public sealed class MiniHeartDoorUnlockCutsceneTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly CollabModule module;
        private readonly string doorID;

        public MiniHeartDoorUnlockCutsceneTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            doorID = data.Attr("doorID");
        }

        public MiniHeartDoor FindHeartGate() => ((OriginalLevelSceneRoot)Scene).SourceScene.Entities
            .GetTrackedEntities(typeof(MiniHeartDoor)).OfType<MiniHeartDoor>()
            .FirstOrDefault(door => door.DoorID == doorID);

        public override void Awake(object scene)
        {
            base.Awake(scene);
            var door = FindHeartGate();
            if (door == null || door.Opened) RemoveSelf();
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            OpenHeartGate(player);
        }

        public void OpenHeartGate(RoomPlayerSession player)
        {
            var door = FindHeartGate();
            if (door == null || door.Opened || door.Requires > door.HeartGems || player == null) return;
            var level = (OriginalLevelSceneRoot)Scene;
            level.SourceScene.QueueSourceEntity(new MiniHeartDoorUnlockCutscene(engine, door, player));
            module.SaveData.OpenedMiniHeartDoors.Add(door.GetDoorSaveDataID(level));
            RemoveSelf();
        }
    }
}
