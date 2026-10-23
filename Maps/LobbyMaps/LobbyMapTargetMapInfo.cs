using System;
using System.Linq;
using System.Numerics;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Modules;

namespace Celeste.Mods.CollabUtils2.Maps.LobbyMaps
{
    public struct LobbyMapTargetMapInfo
    {
        public readonly string SID;
        public readonly bool Completed;
        public readonly int Difficulty;

        public LobbyMapTargetMapInfo(CollabModule module, string mapName)
        {
            SID = string.Empty;
            Completed = false;
            Difficulty = -1;

            if (((IOriginalAreaKeyEnvironment)module.Panels.Engine).AreaKeys.GetAreaBySID(mapName) is IOriginalAreaData
                areaData)
            {
                SID = areaData.SID;
                var save = ((IOriginalGameplayProcess)module.Panels.Engine).SaveContext.CurrentSave;
                var areaStatsFor = ((IOriginalSaveCounterEnvironment)module.Panels.Engine).SaveCounters
                    .GetAreasSafe(save).FirstOrDefault(item => item.ID == areaData.ID);
                Completed = areaStatsFor != null && areaStatsFor.Modes[0].Completed;

                string mapDifficultyIconPath = areaData.Icon;
                if (!string.IsNullOrWhiteSpace(mapDifficultyIconPath))
                {
                    var iconFilename = mapDifficultyIconPath.Split('/').LastOrDefault() ?? string.Empty;
                    var firstToken = iconFilename.Split('-').FirstOrDefault() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(firstToken))
                    {
                        int.TryParse(firstToken, out Difficulty);
                    }
                }
            }
        }
    }
}
