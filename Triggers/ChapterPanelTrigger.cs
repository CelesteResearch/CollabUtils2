using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Components.Talking;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.CollabUtils2.Triggers
{
    [CustomEntity("CollabUtils2/ChapterPanelTrigger", "CollabUtils2/ExitFromGymTrigger")]
    public sealed class ChapterPanelTrigger : SourceTriggerEntityState
    {
        private readonly CollabModule module;
        private readonly OriginalTalkComponent talkComponent;
        private readonly string interactFlag;

        public ChapterPanelTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height,
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            string map = data.Attr("map");
            var returnMode = data.Enum("returnToLobbyMode", ReturnToLobbyMode.SetReturnToHere);
            bool savingAllowed = data.Bool("allowSaving", true);
            bool exitFromGym = data.Name == "CollabUtils2/ExitFromGymTrigger";
            bool markTechAsLearned = exitFromGym && data.Bool("markTechAsLearned");
            interactFlag = data.Attr("interactFlag");
            talkComponent = new OriginalTalkComponent((IOriginalTalkProcess)engine,
                new IntRect(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? data.Nodes[0] - data.Position : new Vector2(data.Width / 2f, data.Height / 2f),
                player =>
                {
                    var session = ((OriginalLevelSceneRoot)Scene).Session;
                    string sid =
                        ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSID(
                            ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetSessionArea(session));
                    string collabID = module.Identities.GetCollabNameForSID(sid);
                    if (exitFromGym && module.Maps.GymTech.TryGetValue(collabID, out var techForCollab))
                    {
                        string currentGymTech = techForCollab.FirstOrDefault(pair =>
                            pair.Value.AreaSID == sid && pair.Value.Level == session.Level).Key;
                        if (currentGymTech != null && markTechAsLearned)
                        {
                            if (module.SaveData.LearnedTech.TryGetValue(collabID, out var learnedTech))
                                learnedTech.Add(currentGymTech);
                            else module.SaveData.LearnedTech.Add(collabID, new HashSet<string> { currentGymTech });
                        }
                    }

                    module.Panels.OpenChapterPanel(player,
                        exitFromGym ? module.Session.GymExitMapSID : map,
                        exitFromGym ? ReturnToLobbyMode.DoNotChangeReturn : returnMode,
                        exitFromGym ? module.Session.GymExitSaveAllowed : savingAllowed,
                        exitFromGym);
                }) { PlayerMustBeFacing = false };
            OwnedComponents.Add(talkComponent);
        }

        public override void Update()
        {
            base.Update();
            talkComponent.Enabled = !module.Panels.IsOpen && (string.IsNullOrWhiteSpace(interactFlag)
                                                              || (((OriginalLevelSceneRoot)Scene)?.Session.GetFlag(
                                                                  interactFlag) ?? true));
        }
    }
}
