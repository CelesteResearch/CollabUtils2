using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Doors.HeartGems;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Collision;

namespace Celeste.Mods.CollabUtils2.Entities
{
    [SceneTracked]
    [CustomEntity("CollabUtils2/MiniHeartDoor=Load")]
    public sealed class MiniHeartDoor : OriginalHeartGemDoorState
    {
        private readonly ISourceEngineContext engine;
        private readonly CollabModule module;
        private readonly string levelSet;
        private readonly string entityID;
        private readonly float height;
        private readonly PackedColor color;
        public bool ForceTrigger;
        public bool ForceAllHearts;
        public readonly string DoorID;
        public float OpenPercent => openPercent;

        public static MiniHeartDoor Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, EntityData data) => new(level, data, offset);

        public MiniHeartDoor(OriginalLevelSceneRoot level, EntityData data, Vector2 offset)
            : base(data, offset, (IOriginalHeartGemDoorProcess)level.EngineContext,
                ((ISourcePlatformCollisionScene)level).SolidWorldFactory,
                ((IOriginalSaveCounterEnvironment)level.EngineContext).SaveCounters)
        {
            engine = level.EngineContext;
            module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            levelSet = data.Attr("levelSet");
            height = data.Height;
            DoorID = data.Attr("doorID");
            entityID = level.Session.Level + ":" + data.ID;
            string hex = data.Attr("color");
            hex = hex switch
            {
                "beginner" => "18668F", "intermediate" => "E0233D", "advanced" => "896900",
                "expert" => "824207", "grandmaster" => "650091", _ => hex
            };
            color = Celeste.Graphics.Display.Colors.SourceHexColor.HexToColor(hex);
            icon = ((IOriginalHeartGemDoorProcess)engine).GameGraphics.GameAtlas
                .GetAtlasSubtextures(data.Attr("iconTexture", "objects/heartdoor/icon"));
        }

        public override int HeartGems => ForceAllHearts
            ? Requires
            : ((IOriginalSaveLevelSetEnvironment)engine).SaveLevelSets.GetLevelSetStatsFor(
                ((IOriginalHeartGemDoorProcess)engine).SaveContext.CurrentSave, levelSet).TotalHeartGems;

        protected override PackedColor InteriorColor => color;

        protected override bool CanApproach(RoomPlayerSession player) => player != null && (ForceTrigger ||
            Math.Abs(player.X - Center.X) < 80f && player.Center.Y > Y - height && player.Center.Y < Y + height);

        protected override void OnOpened(OriginalLevelSceneRoot level)
        {
            level.Session.SetFlag("opened_mini_heart_door_" + entityID);
            base.OnOpened(level);
        }

        public string GetDoorSaveDataID(OriginalLevelSceneRoot level) =>
            ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSID(
                ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSessionArea(level.Session)) +
            (string.IsNullOrEmpty(DoorID) ? "" : ":" + DoorID);

        public override void Added(object scene)
        {
            var level = (OriginalLevelSceneRoot)scene;
            level.Session.SetFlag("opened_heartgem_door_" + Requires,
                level.Session.GetFlag("opened_mini_heart_door_" + entityID) ||
                module.SaveData.OpenedMiniHeartDoors.Contains(GetDoorSaveDataID(level)));
            base.Added(scene);
            TopSolid.Collider.Height = BotSolid.Collider.Height = height;
            TopSolid.Top = Y - height;
            BotSolid.Bottom = Y + height;
            if (Opened)
            {
                TopSolid.Collider.Height -= openDistance;
                BotSolid.Collider.Height -= openDistance;
                BotSolid.Top += openDistance;
            }
        }

        public override void Update()
        {
            base.Update();
            if (TopSolid.Top != Y - height && Opened)
            {
                float displacement = Y - height - TopSolid.Top;
                TopSolid.Collider.Height = height - displacement;
                TopSolid.Top = Y - height;
            }

            if (BotSolid.Bottom != Y + height && Opened)
                BotSolid.Collider.Height = height - (BotSolid.Top - Y);
        }

        internal void SnapOpen()
        {
            openPercent = 1f;
            Opened = true;
            Counter = Requires;
            TopSolid.Bottom = Y - openDistance;
            BotSolid.Top = Y + openDistance;
            foreach (var component in OwnedComponents)
                if (component is SimulationCoroutine)
                {
                    component.RemoveSelf();
                    break;
                }
        }
    }
}
