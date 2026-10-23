using System.Numerics;
using Celeste.Core.Display;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Game.Menus.Journal;
using Celeste.Game.Save.Runtime;
using Celeste.Game.Save.Data;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Processes;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Persistence;
using Celeste.Mods.CollabUtils2.Maps;
using Celeste.Core.Maps;
using Celeste.Simulation.Save.Progress;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mods.CollabUtils2.UI
{
    public class OuiJournalCollabProgressInOverworld : OuiJournalPage
    {
        private readonly CollabModule module;
        private readonly Table table;
        private OriginalSaveDataState Save => ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave;
        private IOriginalSaveCounterQueries Counters => ((IOriginalSaveCounterEnvironment)engine).SaveCounters;
        private IOriginalSaveLevelSetQueries LevelSets => ((IOriginalSaveLevelSetEnvironment)engine).SaveLevelSets;
        private IOriginalAreaKeyPolicy Areas => ((IOriginalAreaKeyEnvironment)engine).AreaKeys;

        private IOriginalAreaData GetArea(ChapterAreaProgress stats)
            => Areas.GetAreaById(((IOriginalAreaStatsEnvironment)engine).AreaStats.GetAreaStatsId(stats));

        private static int getRankLevel(SpeedBerryInfo speedBerryInfo, long pb)
        {
            float pbSeconds = (float)TimeSpan.FromTicks(pb).TotalSeconds;
            if (pbSeconds < speedBerryInfo.Gold)
            {
                return 1;
            }
            else if (pbSeconds < speedBerryInfo.Silver)
            {
                return 2;
            }

            return 3;
        }

        private static PackedColor getRankPackedColor(int level)
        {
            if (level == 1)
            {
                return SourceHexColor.HexToColor("B07A00");
            }
            else if (level == 2)
            {
                return PackedColor.Gray;
            }

            return SourceHexColor.HexToColor("B96F11");
        }

        private static string getRankIcon(int level)
        {
            if (level == 1)
            {
                return "CollabUtils2/speed_berry_gold";
            }
            else if (level == 2)
            {
                return "CollabUtils2/speed_berry_silver";
            }

            return "CollabUtils2/speed_berry_bronze";
        }

        private bool shouldDisplaySpeedBerryColumn()
        {
            if (((CollabSettings)module._Settings).BestTimeToDisplayInJournal == BestTimeInJournal.SpeedBerry)
            {
                foreach (ChapterAreaProgress item in Counters.GetAreasSafe(Save))
                {
                    // start going through all maps belonging to the collab level set, or to a lobby level set belonging to this collab.
                    bool belongsToCollab = false;

                    if (GetArea(item).LevelSet == LevelSets.GetLevelSet(Save))
                    {
                        belongsToCollab = true;
                    }
                    else
                    {
                        string lobby = module.Identities.GetLobbyForLevelSet(GetArea(item).LevelSet);
                        if (lobby != null && Areas.GetAreaBySID(lobby).LevelSet == LevelSets.GetLevelSet(Save))
                        {
                            belongsToCollab = true;
                        }
                    }

                    if (belongsToCollab && module.Maps.SpeedBerries.ContainsKey(item.SID))
                    {
                        // this level has a speed berry!
                        return true;
                    }
                }
            }

            // either we disabled speed berries in the journal, or just went through all maps without finding any...
            return false;
        }

        public OuiJournalCollabProgressInOverworld(CollabModule module, OuiJournalSequence journal)
            : base(journal)
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            bool displaySpeedBerryColumn = shouldDisplaySpeedBerryColumn();

            PageTexture = "page";
            table = new Table()
                .AddColumn(new TextCell(engine, Text.Dialog.Clean("journal_progress"), new Vector2(0f, 0.5f), 1f,
                    PackedColor.Black * 0.7f, 420f))
                .AddColumn(new EmptyCell(engine, 20f))
                .AddColumn(new EmptyCell(engine, 64f))
                .AddColumn(new IconCell(engine, "strawberry", 150f))
                .AddColumn(new IconCell(engine, "skullblue", 100f))
                .AddColumn(new IconCell(engine, "CollabUtils2MinDeaths/SpringCollab2020/1-Beginner", 100f));

            if (OuiJournalCollabProgressDashCountMod.IsDashCountEnabled())
            {
                table.AddColumn(new IconCell(engine, "max480/DashCountMod/dashes", 80f));
            }

            table
                .AddColumn(new IconCell(engine, "time", 220f))
                .AddColumn(new IconCell(engine, "CollabUtils2/speed_berry_pbs_heading", 220f))
                .AddColumn(new EmptyCell(engine, 30f));

            int totalStrawberries = 0;
            int totalDeaths = 0;
            int sumOfBestDeaths = 0;
            int sumOfBestDashes = 0;
            long totalTime = 0;
            long sumOfBestTimes = 0;

            bool allLevelsDone = true;
            bool allSpeedBerriesDone = true;
            bool allMapsCompletedInSingleRun = true;

            foreach (ChapterAreaProgress item in Counters.GetAreasSafe(Save))
            {
                IOriginalAreaData areaData = GetArea(item);
                if (areaData.LevelSet == LevelSets.GetLevelSet(Save))
                {
                    string lobbyMapLevelSetName = module.Identities.GetLobbyLevelSet(areaData.SID);
                    OriginalLevelSetStats lobbyMapLevelSet = null;
                    if (lobbyMapLevelSetName != null)
                    {
                        lobbyMapLevelSet = LevelSets.GetLevelSetStatsFor(Save, lobbyMapLevelSetName);
                    }

                    int lobbyStrawberries = item.TotalStrawberries;
                    int lobbyTotalStrawberries = areaData.Mode[0].TotalStrawberries;
                    int lobbyDeaths = item.Modes[0].Deaths;
                    int lobbySumOfBestDeaths = 0;
                    int lobbySumOfBestDashes = OuiJournalCollabProgressDashCountMod.DisplaysTotalDashes()
                        ? OuiJournalCollabProgressDashCountMod.GetLevelDashesForJournalProgress(item)
                        : 0;
                    long lobbyTotalTime = item.TotalTimePlayed;
                    long lobbySumOfBestTimes = 0;
                    bool lobbyLevelsDone = true;
                    int lobbySpeedBerryLevel = 1;
                    bool lobbySilverBerriesObtained = true;
                    bool lobbyAllMapsCompletedInSingleRun = true;

                    foreach (ChapterAreaProgress lobbyMap in lobbyMapLevelSet?.Areas ?? new List<ChapterAreaProgress>())
                    {
                        IOriginalAreaData lobbyAreaData = GetArea(lobbyMap);
                        lobbyStrawberries += lobbyMap.TotalStrawberries;
                        lobbyTotalStrawberries += lobbyAreaData.Mode[0].TotalStrawberries;
                        lobbyDeaths += lobbyMap.Modes[0].Deaths;
                        lobbyTotalTime += lobbyMap.TotalTimePlayed;
                        lobbyAllMapsCompletedInSingleRun &= lobbyMap.Modes[0].SingleRunCompleted;

                        if (displaySpeedBerryColumn)
                        {
                            if (module.Maps.SpeedBerries.TryGetValue(lobbyMap.SID, out SpeedBerryInfo mapSpeedBerryInfo)
                                && module.SaveData.SpeedBerryPBs.TryGetValue(lobbyMap.SID, out long mapSpeedBerryPB))
                            {
                                lobbySpeedBerryLevel = Math.Max(getRankLevel(mapSpeedBerryInfo, mapSpeedBerryPB),
                                    lobbySpeedBerryLevel);
                                lobbySumOfBestTimes += mapSpeedBerryPB;
                            }
                            else
                            {
                                lobbySpeedBerryLevel = 4;
                            }
                        }
                        else
                        {
                            if (lobbyMap.Modes[0].BestTime > 0f)
                            {
                                lobbySumOfBestTimes += lobbyMap.Modes[0].BestTime;
                            }
                            else
                            {
                                lobbySpeedBerryLevel = 4;
                            }
                        }

                        bool goldenBerryNotObtained = !lobbyMap.Modes[0].Strawberries.Any(berry =>
                            lobbyAreaData.Mode[0].MapData.Goldenberries.Any(golden =>
                                golden.ID == berry.ID && golden.Level.Name == berry.Level));
                        bool silverBerryNotObtained = !module.Maps.SilverBerries.TryGetValue(GetArea(lobbyMap).LevelSet,
                                                          out Dictionary<string, MapEntityId> levelSetBerries)
                                                      || !levelSetBerries.TryGetValue(lobbyMap.SID,
                                                          out MapEntityId berryID)
                                                      || !lobbyMap.Modes[0].Strawberries.Contains(berryID);

                        if (goldenBerryNotObtained && silverBerryNotObtained)
                        {
                            lobbySilverBerriesObtained = false;
                            lobbySumOfBestDeaths += lobbyMap.Modes[0].BestDeaths;
                        }

                        lobbySumOfBestDashes +=
                            OuiJournalCollabProgressDashCountMod.GetLevelDashesForJournalProgress(lobbyMap);

                        if (!lobbyMap.Modes[0].HeartGem)
                        {
                            lobbyLevelsDone = false;
                        }
                    }

                    string strawberryText = null;
                    if (lobbyStrawberries > 0 || lobbyTotalStrawberries > 0)
                    {
                        strawberryText = lobbyStrawberries.ToString();
                        if (lobbyLevelsDone)
                        {
                            strawberryText = strawberryText + "/" + lobbyTotalStrawberries;
                        }
                    }
                    else
                    {
                        strawberryText = "-";
                    }

                    string heartTexturePath = lobbyMapLevelSetName ?? areaData.SID;
                    string heartTexture = MountainResources.Journal.Has("CollabUtils2Hearts/" + heartTexturePath)
                        ? "CollabUtils2Hearts/" + heartTexturePath
                        : "heartgem0";

                    string areaName = Text.Dialog.Clean(areaData.Name);
                    if (Text.Dialog.Has(areaData.Name + "_journal"))
                    {
                        areaName = Text.Dialog.Clean(areaData.Name + "_journal");
                    }

                    Row row = table.AddRow()
                        .Add(new TextCell(engine, areaName, new Vector2(1f, 0.5f), 0.6f, TextColor))
                        .Add(null)
                        .Add(new IconCell(engine, item.Modes[0].HeartGem ? heartTexture : "dot"))
                        .Add(new TextCell(engine, strawberryText, TextJustify, 0.5f, TextColor));

                    if (lobbyTotalTime > 0)
                    {
                        row.Add(new TextCell(engine, Text.Deaths(lobbyDeaths), TextJustify, 0.5f, TextColor));
                    }
                    else
                    {
                        row.Add(new IconCell(engine, "dot"));
                    }

                    if (lobbyLevelsDone)
                    {
                        if (lobbyMapLevelSet == null)
                        {
                            row.Add(new TextCell(engine, Text.Deaths(item.Modes[0].BestDeaths), TextJustify, 0.5f,
                                TextColor));
                            sumOfBestDeaths += item.Modes[0].BestDeaths;
                        }
                        else if (lobbySilverBerriesObtained)
                        {
                            row.Add(new IconCell(engine, "CollabUtils2/golden_strawberry"));
                        }
                        else if (lobbyAllMapsCompletedInSingleRun)
                        {
                            row.Add(new TextCell(engine, Text.Deaths(lobbySumOfBestDeaths), TextJustify, 0.5f,
                                TextColor));
                        }
                        else
                        {
                            row.Add(new IconCell(engine, "dot"));
                        }
                    }
                    else
                    {
                        row.Add(new IconCell(engine, "dot"));
                        allLevelsDone = false;
                    }


                    if (OuiJournalCollabProgressDashCountMod.IsDashCountEnabled())
                    {
                        if (lobbyMapLevelSet == null)
                        {
                            if ((OuiJournalCollabProgressDashCountMod.DisplaysTotalDashes() &&
                                 item.TotalTimePlayed > 0) || item.Modes[0].SingleRunCompleted)
                            {
                                row.Add(new TextCell(engine,
                                    Text.Deaths(
                                        OuiJournalCollabProgressDashCountMod.GetLevelDashesForJournalProgress(item)),
                                    TextJustify, 0.5f, TextColor));
                                sumOfBestDashes +=
                                    OuiJournalCollabProgressDashCountMod.GetLevelDashesForJournalProgress(item);
                            }
                            else
                            {
                                row.Add(new IconCell(engine, "dot"));
                            }
                        }
                        else if ((OuiJournalCollabProgressDashCountMod.DisplaysTotalDashes() &&
                                  item.TotalTimePlayed > 0) || lobbyAllMapsCompletedInSingleRun)
                        {
                            row.Add(new TextCell(engine, Text.Deaths(lobbySumOfBestDashes), TextJustify, 0.5f,
                                TextColor));
                        }
                        else
                        {
                            row.Add(new IconCell(engine, "dot"));
                        }
                    }

                    if (lobbyTotalTime > 0)
                    {
                        row.Add(new TextCell(engine, Text.Time(lobbyTotalTime), TextJustify, 0.5f, TextColor));
                    }
                    else
                    {
                        row.Add(new IconCell(engine, "dot"));
                    }

                    if (lobbyMapLevelSet == null)
                    {
                        row.Add(new TextCell(engine, "-", TextJustify, 0.5f, TextColor)).Add(null);
                    }
                    else if (lobbySpeedBerryLevel < 4)
                    {
                        if (displaySpeedBerryColumn)
                        {
                            row.Add(new TextCell(engine, Text.Time(lobbySumOfBestTimes), TextJustify, 0.5f,
                                getRankPackedColor(lobbySpeedBerryLevel)));
                            row.Add(new IconCell(engine, getRankIcon(lobbySpeedBerryLevel)));
                            sumOfBestTimes += lobbySumOfBestTimes;
                        }
                        else
                        {
                            row.Add(new TextCell(engine, Text.Time(lobbySumOfBestTimes), TextJustify, 0.5f, TextColor))
                                .Add(null);
                            sumOfBestTimes += lobbySumOfBestTimes;
                        }
                    }
                    else
                    {
                        row.Add(new IconCell(engine, "dot")).Add(null);
                        allSpeedBerriesDone = false;
                    }

                    totalStrawberries += lobbyStrawberries;
                    totalDeaths += lobbyDeaths;
                    sumOfBestDeaths += lobbySumOfBestDeaths;
                    sumOfBestDashes += lobbySumOfBestDashes;
                    totalTime += lobbyTotalTime;
                    allMapsCompletedInSingleRun &= lobbyAllMapsCompletedInSingleRun;

                    if (!lobbyLevelsDone)
                    {
                        allLevelsDone = false;
                    }
                }
            }

            table.AddRow();
            Row totalsRow = table.AddRow()
                .Add(new TextCell(engine, Text.Dialog.Clean("journal_totals"), new Vector2(1f, 0.5f), 0.7f, TextColor))
                .Add(null)
                .Add(null)
                .Add(new TextCell(engine, totalStrawberries.ToString(), TextJustify, 0.6f, TextColor))
                .Add(new TextCell(engine, Text.Deaths(totalDeaths), TextJustify, 0.6f, TextColor))
                .Add(new TextCell(engine,
                    allLevelsDone && allMapsCompletedInSingleRun ? Text.Deaths(sumOfBestDeaths) : "-", TextJustify,
                    0.6f, TextColor));

            if (OuiJournalCollabProgressDashCountMod.IsDashCountEnabled())
            {
                totalsRow.Add(new TextCell(engine,
                    OuiJournalCollabProgressDashCountMod.DisplaysTotalDashes() ||
                    (allLevelsDone && allMapsCompletedInSingleRun)
                        ? Text.Deaths(sumOfBestDashes)
                        : "-", TextJustify, 0.6f, TextColor));
            }

            totalsRow
                .Add(new TextCell(engine, Text.Time(totalTime), TextJustify, 0.6f, TextColor))
                .Add(new TextCell(engine, allSpeedBerriesDone ? Text.Time(sumOfBestTimes) : "-", TextJustify, 0.6f,
                    TextColor)).Add(null);

            for (int l = 1;
                 l < ((IOriginalSaveProgressionEnvironment)engine).SaveProgression.GetUnlockedModes(Save);
                 l++)
            {
                totalsRow.Add(null);
            }

            totalsRow.Add(new TextCell(engine, Text.Time(Save.Time), TextJustify, 0.6f, TextColor));
            table.AddRow();
        }

        public override void Redraw(VirtualRenderTarget buffer)
        {
            base.Redraw(buffer);
            ((ISourceDrawScope)Context).Draw.SpriteBatch.Begin();
            table.Render(Context, Journal, new Vector2(60f, 20f));
            ((ISourceDrawScope)Context).Draw.SpriteBatch.End();
        }
    }
}
