using Celeste.Core.Display;
using Celeste.Core.Maps;
using Celeste.Everest.Content.Maps.Binary;
using Celeste.Formats.Map.Binary.Source;
using Celeste.Everest.Content.Maps.Processing;
using Celeste.Graphics.Display.Colors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mods.CollabUtils2.Maps
{
    public class CollabMapDataProcessor : EverestMapDataProcessor
    {
        private readonly Func<string, string> getCollabName;
        private readonly CollabMapCatalog catalog;

        public CollabMapDataProcessor(CollabMapCatalog catalog, Func<string, string> getCollabName)
        {
            this.catalog = catalog;
            this.getCollabName = getCollabName;
        }

        private string levelName;

        public override Dictionary<string, Action<BinaryPackerElement>> Init()
        {
            return new Dictionary<string, Action<BinaryPackerElement>>
            {
                {
                    "level", level =>
                    {
                        // be sure to write the level name down.
                        levelName = level.Attr("name").Split(':')[0];
                        if (levelName.StartsWith("lvl_"))
                        {
                            levelName = levelName.Substring(4);
                        }
                    }
                },
                {
                    "entity:CollabUtils2/SilverBerry", silverBerry =>
                    {
                        if (!catalog.SilverBerries.TryGetValue(AreaData.LevelSet,
                                out Dictionary<string, MapEntityId> allSilversInLevelSet))
                        {
                            allSilversInLevelSet = new Dictionary<string, MapEntityId>();
                            catalog.SilverBerries.Add(AreaData.LevelSet, allSilversInLevelSet);
                        }

                        allSilversInLevelSet[AreaData.SID] = new MapEntityId(levelName, silverBerry.AttrInt("id"));

                        catalog.MapsWithSilverBerries.Add(AreaData.SID);
                    }
                },
                {
                    "entity:CollabUtils2/RainbowBerry", berry => catalog.MapsWithRainbowBerries.Add(AreaData.SID)
                },
                {
                    "entity:CollabUtils2/SpeedBerry", speedBerry =>
                    {
                        catalog.SpeedBerries[AreaData.SID] = new SpeedBerryInfo()
                        {
                            ID = new MapEntityId(levelName, speedBerry.AttrInt("id")),
                            Gold = speedBerry.AttrFloat("goldTime"),
                            Silver = speedBerry.AttrFloat("silverTime"),
                            Bronze = speedBerry.AttrFloat("bronzeTime")
                        };
                    }
                },
                {
                    "triggers", triggerList =>
                    {
                        foreach (BinaryPackerElement trigger in triggerList.Children)
                        {
                            if (trigger.Name == "CollabUtils2/ChapterPanelTrigger")
                            {
                                addGymInfoFromChapterPanelTrigger(trigger);
                            }
                        }
                    }
                },
                {
                    "entity:FlushelineCollab/LevelEntrance",
                    levelEntrance => { addGymInfoFromChapterPanelTrigger(levelEntrance); }
                },
                {
                    "entity:CollabUtils2/GymMarker", gymMarker =>
                    {
                        string techName = gymMarker.Attr("name");
                        if (string.IsNullOrEmpty(techName))
                            return;

                        string difficulty =
                            gymMarker.Attr("difficulty"); // for legacy placements, should no longer be used
                        int order = gymMarker.AttrInt("order", -1);
                        string color = gymMarker.Attr("color");
                        string learnedColor = gymMarker.Attr("learnedColor");
                        bool legacyRenderMode =
                            gymMarker.AttrBool("legacyRenderMode",
                                true); // for legacy placements, should no longer be used
                        GymTechInfo techInfo = new()
                        {
                            Difficulty = !string.IsNullOrEmpty(difficulty) ? difficulty : null,
                            Order = order >= 0 ? order : null,
                            Color = !string.IsNullOrEmpty(color) ? SourceHexColor.HexToColor(color) : null,
                            LearnedColor = !string.IsNullOrEmpty(learnedColor)
                                ? SourceHexColor.HexToColor(learnedColor)
                                : null,
                            AreaSID = AreaData.SID,
                            Level = levelName,
                            LegacyRenderMode = legacyRenderMode
                        };

                        string collabID = getCollabName(AreaData.SID);
                        if (catalog.GymTech.TryGetValue(collabID, out Dictionary<string, GymTechInfo> tech))
                            tech[techName] = techInfo;
                        else
                            catalog.GymTech.Add(collabID,
                                new Dictionary<string, GymTechInfo> { { techName, techInfo } });
                    }
                }
            };
        }

        private void addGymInfoFromChapterPanelTrigger(BinaryPackerElement trigger)
        {
            string map = trigger.Attr("map");
            string tech = trigger.Attr("tech");
            if (!string.IsNullOrEmpty(map) && !string.IsNullOrEmpty(tech))
            {
                catalog.GymLevels[map] = new GymLevelInfo
                {
                    Tech = trigger.Attr("tech").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                    Flag = trigger.Attr("flag")
                };
            }
        }

        public override void Reset()
        {
            if (catalog.SilverBerries.TryGetValue(AreaData.LevelSet, out Dictionary<string, MapEntityId> silverBerries))
                silverBerries.Remove(AreaData.SID);
            catalog.SpeedBerries.Remove(AreaData.SID);

            catalog.MapsWithSilverBerries.Remove(AreaData.SID);
            catalog.MapsWithRainbowBerries.Remove(AreaData.SID);

            catalog.GymLevels.Remove(AreaData.SID);
            foreach ((string _, Dictionary<string, GymTechInfo> techForCollab) in catalog.GymTech)
            {
                string[] affectedTech = techForCollab.Where(kvp => kvp.Value.AreaSID == AreaData.SID)
                    .Select(kvp => kvp.Key).ToArray();
                foreach (string tech in affectedTech)
                    techForCollab.Remove(tech);
            }
        }

        public override void End()
        {
            // nothing to do here
        }
    }
}
