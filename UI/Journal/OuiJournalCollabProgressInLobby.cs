using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Celeste.Core.Display;
using Celeste.Core.Maps;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Maps.Registry;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Menus.Journal;
using Celeste.Game.Save.Runtime;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Mods.CollabUtils2.Maps;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Persistence;
using Celeste.Simulation.Save.Progress;

namespace Celeste.Mods.CollabUtils2.UI.Journal
{
    public class OuiJournalCollabProgressInLobby : OuiJournalPage
    {
        private readonly Table table;
        private readonly CollabModule module;

        private static PackedColor getRankPackedColor(SpeedBerryInfo speedBerryInfo, long pb)
        {
            float pbSeconds = (float)TimeSpan.FromTicks(pb).TotalSeconds;
            if (pbSeconds < speedBerryInfo.Gold)
            {
                return SourceHexColor.HexToColor("B07A00");
            }
            else if (pbSeconds < speedBerryInfo.Silver)
            {
                return PackedColor.Gray;
            }

            return SourceHexColor.HexToColor("B96F11");
        }

        private static string getRankIcon(SpeedBerryInfo speedBerryInfo, long pb)
        {
            float pbSeconds = (float)TimeSpan.FromTicks(pb).TotalSeconds;
            if (pbSeconds < speedBerryInfo.Gold)
            {
                return "CollabUtils2/speed_berry_gold";
            }
            else if (pbSeconds < speedBerryInfo.Silver)
            {
                return "CollabUtils2/speed_berry_silver";
            }

            return "CollabUtils2/speed_berry_bronze";
        }

        private static bool shouldDisplaySpeedBerryColumn(CollabModule module, string levelSet)
        {
            var engine = module.Panels.Engine;
            var save = ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave;
            var counters = ((IOriginalSaveCounterEnvironment)engine).SaveCounters;
            var areaKeys = ((IOriginalAreaKeyEnvironment)engine).AreaKeys;
            var areaStats = ((IOriginalAreaStatsEnvironment)engine).AreaStats;
            if (module.Settings.BestTimeToDisplayInJournal == BestTimeInJournal.SpeedBerry)
            {
                // start going through all maps belonging to our assigned level set.
                foreach (ChapterAreaProgress item in counters.GetAreasSafe(save))
                {
                    if (areaKeys.GetAreaById(areaStats.GetAreaStatsId(item)).LevelSet == levelSet &&
                        module.Maps.SpeedBerries.ContainsKey(item.SID))
                    {
                        // this level has a speed berry!
                        return true;
                    }
                }
            }

            // either we disabled speed berries in the journal, or just went through all maps without finding any...
            return false;
        }

        public static List<OuiJournalCollabProgressInLobby> GeneratePages(CollabModule module,
            OuiJournalSequence journal, string levelSet, bool showOnlyDiscovered)
        {
            var engine = module.Panels.Engine;
            var save = ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave;
            var counters = ((IOriginalSaveCounterEnvironment)engine).SaveCounters;
            var areaKeys = ((IOriginalAreaKeyEnvironment)engine).AreaKeys;
            var areaStats = ((IOriginalAreaStatsEnvironment)engine).AreaStats;
            var Text = ((IOriginalGameTextEnvironment)engine).Text;
            var MountainResources = ((IOriginalMountainEnvironment)engine).MountainResources;
            var Graphics = (OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics;

            IOriginalAreaData GetArea(ChapterAreaProgress stats) =>
                areaKeys.GetAreaById(areaStats.GetAreaStatsId(stats));

            bool displaySpeedBerryColumn = shouldDisplaySpeedBerryColumn(module, levelSet);

            List<OuiJournalCollabProgressInLobby> pages = new List<OuiJournalCollabProgressInLobby>();
            int rowCount = 0;

            int totalStrawberries = 0;
            int totalDeaths = 0;
            int sumOfBestDeaths = 0;
            int sumOfBestDashes = 0;
            long totalTime = 0;
            long sumOfBestTimes = 0;

            bool allMapsDone = true;

            bool allLevelsDone = true;
            bool allSpeedBerriesDone = true;

            string heartTexture = MountainResources.Journal.Has("CollabUtils2Hearts/" + levelSet)
                ? "CollabUtils2Hearts/" + levelSet
                : "heartgem0";

            int mapsPerPage = 12;
            int mapAmount = counters.GetAreasSafe(save).Where(item =>
                !EverestAreaSafeQueries.GetInterlude(engine, GetArea(item))
                && (!showOnlyDiscovered || item.TotalTimePlayed > 0)).Count();

            // we want to display the map icons if they're not actually all the same. ^^'
            bool displayIcons = ((IOriginalAreaSourceTable)engine).Areas
                .Where(area => !EverestAreaSafeQueries.GetInterlude(engine, area))
                .Select(area => area.Icon)
                .Distinct()
                .Count() > 1;

            OuiJournalCollabProgressInLobby currentPage =
                new OuiJournalCollabProgressInLobby(module, journal, levelSet, displayIcons);
            pages.Add(currentPage);

            if (mapAmount >= mapsPerPage)
            {
                // we want the last page to contain at least 2 maps.
                while (mapAmount % mapsPerPage < 2)
                {
                    mapsPerPage--;
                }
            }

            List<ChapterAreaProgress> sortedMaps = new List<ChapterAreaProgress>(counters.GetAreasSafe(save))
                .Where(map => !EverestAreaSafeQueries.GetInterlude(engine, GetArea(map)))
                .ToList();

            // sort maps by icon name if all of their icons start with [number]-...
            // because then we know the ordering is intentional (and not accidental like easy > hard > medium)
            Regex startsWithNumber = new Regex(".*/[0-9]+-.*");
            if (sortedMaps.Select(map => GetArea(map).Icon ?? "").All(icon => startsWithNumber.IsMatch(icon)))
            {
                sortedMaps.Sort((a, b) =>
                {
                    IOriginalAreaData adata = GetArea(a);
                    IOriginalAreaData bdata = GetArea(b);

                    bool aHeartSide = module.Identities.IsHeartSide(a.SID);
                    bool bHeartSide = module.Identities.IsHeartSide(b.SID);

                    // heart sides should appear last.
                    if (aHeartSide && !bHeartSide)
                        return 1;
                    if (!aHeartSide && bHeartSide)
                        return -1;

                    // sort by icon name, then by map bin name.
                    return adata.Icon == bdata.Icon
                        ? adata.Name.CompareTo(bdata.Name)
                        : adata.Icon.CompareTo(bdata.Icon);
                });
            }

            foreach (ChapterAreaProgress item in sortedMaps)
            {
                IOriginalAreaData areaData = GetArea(item);
                if (module.Identities.IsHeartSide(areaData.SID))
                {
                    if (allMapsDone || item.TotalTimePlayed > 0)
                    {
                        // add a separator, like the one between regular maps and Farewell
                        currentPage.table.AddRow();
                    }
                    else
                    {
                        // all maps weren't complete yet, and the heart side was never accessed: hide the heart side for now.
                        continue;
                    }
                }

                if (showOnlyDiscovered && item.TotalTimePlayed <= 0)
                {
                    // skip the map, because it was not discovered yet.
                    // since it wasn't discovered, we can already say all maps weren't done though.
                    allMapsDone = false;
                    allLevelsDone = false;
                    allSpeedBerriesDone = false;
                    continue;
                }

                string strawberryText = null;
                if (areaData.Mode[0].TotalStrawberries > 0 || item.TotalStrawberries > 0)
                {
                    strawberryText = item.TotalStrawberries.ToString();
                    if (item.Modes[0].Completed)
                    {
                        strawberryText = strawberryText + "/" + areaData.Mode[0].TotalStrawberries;
                    }
                }
                else
                {
                    strawberryText = "-";
                }

                Row row = currentPage.table.AddRow()
                    .Add(new TextCell(engine, Text.Dialog.Clean(areaData.Name), new Vector2(1f, 0.5f), 0.6f,
                        currentPage.TextColor));

                if (displayIcons)
                {
                    row.Add(null).Add(new JournalGuiIconCell(engine,
                        Graphics.GuiAtlas.Has(areaData.Icon) ? areaData.Icon : "areas/null", 60f, 50f));
                }

                string levelHeartTexture = MountainResources.Journal.Has("CollabUtils2LevelHearts/" + item.SID)
                    ? "CollabUtils2LevelHearts/" + item.SID
                    : heartTexture;

                row.Add(null)
                    .Add(new IconCell(engine, item.Modes[0].HeartGem ? levelHeartTexture : "dot"))
                    .Add(new TextCell(engine, strawberryText, currentPage.TextJustify, 0.5f, currentPage.TextColor));

                if (item.TotalTimePlayed > 0)
                {
                    row.Add(new TextCell(engine, Text.Deaths(item.Modes[0].Deaths), currentPage.TextJustify, 0.5f,
                        currentPage.TextColor));
                }
                else
                {
                    row.Add(new IconCell(engine, "dot"));
                }

                ChapterAreaProgress stats = counters.GetAreasSafe(save)
                    .First(statsForArea => areaStats.GetAreaStatsId(statsForArea) == areaData.ID);
                if (module.Maps.SilverBerries.TryGetValue(areaData.LevelSet,
                        out Dictionary<string, MapEntityId> levelSetBerries)
                    && levelSetBerries.TryGetValue(areaData.SID, out MapEntityId berryID)
                    && stats.Modes[0].Strawberries.Contains(berryID))
                {
                    // silver berry was obtained!
                    row.Add(new IconCell(engine, "CollabUtils2/silver_strawberry"));
                }
                else if (stats.Modes[0].Strawberries.Any(berry =>
                             areaData.Mode[0].MapData.Goldenberries.Any(golden =>
                                 golden.ID == berry.ID && golden.Level.Name == berry.Level)))
                {
                    // golden berry was obtained!
                    row.Add(new IconCell(engine, "CollabUtils2/golden_strawberry"));
                }
                else if (item.Modes[0].SingleRunCompleted)
                {
                    row.Add(new TextCell(engine, Text.Deaths(item.Modes[0].BestDeaths), currentPage.TextJustify, 0.5f,
                        currentPage.TextColor));
                    sumOfBestDeaths += item.Modes[0].BestDeaths;
                }
                else
                {
                    // the player didn't ever do a single run.
                    row.Add(new IconCell(engine, "dot"));
                    allLevelsDone = false;
                }

                if (OuiJournalCollabProgressDashCountMod.IsDashCountEnabled())
                {
                    if ((OuiJournalCollabProgressDashCountMod.DisplaysTotalDashes() && item.TotalTimePlayed > 0) ||
                        item.Modes[0].SingleRunCompleted)
                    {
                        row.Add(new TextCell(engine,
                            Text.Deaths(OuiJournalCollabProgressDashCountMod.GetLevelDashesForJournalProgress(item)),
                            currentPage.TextJustify, 0.5f, currentPage.TextColor));
                        sumOfBestDashes += OuiJournalCollabProgressDashCountMod.GetLevelDashesForJournalProgress(item);
                    }
                    else
                    {
                        row.Add(new IconCell(engine, "dot"));
                    }
                }

                if (item.TotalTimePlayed > 0)
                {
                    row.Add(new TextCell(engine, Text.Time(item.TotalTimePlayed), currentPage.TextJustify, 0.5f,
                        currentPage.TextColor));
                }
                else
                {
                    row.Add(new IconCell(engine, "dot"));
                }

                if (displaySpeedBerryColumn)
                {
                    if (module.Maps.SpeedBerries.TryGetValue(item.SID, out SpeedBerryInfo speedBerryInfo)
                        && module.SaveData.SpeedBerryPBs.TryGetValue(item.SID, out long speedBerryPB))
                    {
                        row.Add(new TextCell(engine, Text.Time(speedBerryPB), currentPage.TextJustify, 0.5f,
                            getRankPackedColor(speedBerryInfo, speedBerryPB)));
                        row.Add(new IconCell(engine, getRankIcon(speedBerryInfo, speedBerryPB)));
                        sumOfBestTimes += speedBerryPB;
                    }
                    else
                    {
                        row.Add(new IconCell(engine, "dot")).Add(null);
                        allSpeedBerriesDone = false;
                    }
                }
                else
                {
                    if (item.Modes[0].BestTime > 0f)
                    {
                        row.Add(new TextCell(engine, Text.Time(item.Modes[0].BestTime), currentPage.TextJustify, 0.5f,
                            currentPage.TextColor)).Add(null);
                        sumOfBestTimes += item.Modes[0].BestTime;
                    }
                    else
                    {
                        row.Add(new IconCell(engine, "dot")).Add(null);
                        allSpeedBerriesDone = false;
                    }
                }

                totalStrawberries += item.TotalStrawberries;
                totalDeaths += item.Modes[0].Deaths;
                totalTime += item.TotalTimePlayed;

                if (!item.Modes[0].HeartGem)
                {
                    allMapsDone = false;
                }

                rowCount++;
                if (rowCount >= mapsPerPage)
                {
                    // split the next zones into another page.
                    rowCount = 0;
                    currentPage = new OuiJournalCollabProgressInLobby(module, journal, levelSet, displayIcons);
                    pages.Add(currentPage);
                }
            }

            if (currentPage.table.Rows > 1)
            {
                currentPage.table.AddRow();
                Row totalsRow = currentPage.table.AddRow()
                    .Add(new TextCell(engine, Text.Dialog.Clean("journal_totals"), new Vector2(1f, 0.5f), 0.7f,
                        currentPage.TextColor)).Add(null);

                if (displayIcons)
                {
                    totalsRow.Add(null).Add(null);
                }

                totalsRow.Add(null)
                    .Add(new TextCell(engine, totalStrawberries.ToString(), currentPage.TextJustify, 0.6f,
                        currentPage.TextColor))
                    .Add(new TextCell(engine, Text.Deaths(totalDeaths), currentPage.TextJustify, 0.6f,
                        currentPage.TextColor))
                    .Add(new TextCell(engine, allLevelsDone ? Text.Deaths(sumOfBestDeaths) : "-",
                        currentPage.TextJustify, 0.6f, currentPage.TextColor));

                if (OuiJournalCollabProgressDashCountMod.IsDashCountEnabled())
                {
                    totalsRow.Add(new TextCell(engine,
                        OuiJournalCollabProgressDashCountMod.DisplaysTotalDashes() || allLevelsDone
                            ? Text.Deaths(sumOfBestDashes)
                            : "-",
                        currentPage.TextJustify, 0.6f, currentPage.TextColor));
                }

                totalsRow
                    .Add(new TextCell(engine, Text.Time(totalTime), currentPage.TextJustify, 0.6f,
                        currentPage.TextColor))
                    .Add(new TextCell(engine, allSpeedBerriesDone ? Text.Time(sumOfBestTimes) : "-",
                        currentPage.TextJustify, 0.6f, currentPage.TextColor)).Add(null);

                for (int l = 1;
                     l < ((IOriginalSaveProgressionEnvironment)engine).SaveProgression.GetUnlockedModes(save);
                     l++)
                {
                    totalsRow.Add(null);
                }

                totalsRow.Add(new TextCell(engine, Text.Time(save.Time), currentPage.TextJustify, 0.6f,
                    currentPage.TextColor));
                currentPage.table.AddRow();
            }

            return pages;
        }

        public OuiJournalCollabProgressInLobby(CollabModule module, OuiJournalSequence journal, string levelSet,
            bool displayIcons)
            : base(journal)
        {
            this.module = module;

            string skullTexture = MountainResources.Journal.Has("CollabUtils2Skulls/" + levelSet)
                ? "CollabUtils2Skulls/" + levelSet
                : "skullblue";
            string minDeathsTexture = MountainResources.Journal.Has("CollabUtils2MinDeaths/" + levelSet)
                ? "CollabUtils2MinDeaths/" + levelSet
                : "CollabUtils2MinDeaths/SpringCollab2020/1-Beginner";

            PageTexture = "page";
            table = new Table()
                .AddColumn(new TextCell(engine, Text.Dialog.Clean("journal_progress"), new Vector2(0f, 0.5f), 1f,
                    PackedColor.Black * 0.7f, displayIcons ? 360f : 420f));

            if (displayIcons)
            {
                table
                    .AddColumn(new EmptyCell(engine, 0f))
                    .AddColumn(new EmptyCell(engine, 64f));
            }

            table.AddColumn(new EmptyCell(engine, 0f))
                .AddColumn(new EmptyCell(engine, 64f))
                .AddColumn(new IconCell(engine, "strawberry", 150f))
                .AddColumn(new IconCell(engine, skullTexture, 100f))
                .AddColumn(new IconCell(engine, minDeathsTexture, 100f));

            if (OuiJournalCollabProgressDashCountMod.IsDashCountEnabled())
            {
                table.AddColumn(new IconCell(engine, "max480/DashCountMod/dashes", 80f));
            }

            table
                .AddColumn(new IconCell(engine, "time", 220f))
                .AddColumn(new IconCell(engine, "CollabUtils2/speed_berry_pbs_heading", 220f))
                .AddColumn(new EmptyCell(engine, 30f));
        }

        public override void Redraw(VirtualRenderTarget buffer)
        {
            base.Redraw(buffer);
            ((ISourceDrawScope)Context).Draw.SpriteBatch.Begin();
            table.Render(Context, this, new Vector2(60f, 20f));
            ((ISourceDrawScope)Context).Draw.SpriteBatch.End();
        }
    }
}
