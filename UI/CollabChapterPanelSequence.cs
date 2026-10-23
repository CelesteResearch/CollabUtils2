using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Everest.Menus.Chapters;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Session;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Simulation.Runtime.Session;
using MonoMod.Utils;

namespace Celeste.Mods.CollabUtils2.UI
{
    [Celeste.Everest.Menus.Overworld.OuiProperties(true)]
    public sealed partial class CollabChapterPanelSequence : EverestChapterPanelSequence
    {
        private const string ContinueCheckpoint = "collabutils_continue";
        private readonly CollabModule module;
        private readonly DynamicData panel;
        private int gymMode = -1;
        private int returnMode = -1;
        private string[] activeGymTech;
        private OriginalGameTextProcess Text => ((IOriginalGameTextEnvironment)engine).Text;
        private string SID => AreaKeys.GetSID(Area);
        public InGameOverworldProcess Owner => module.Panels;

        public bool IsGymSelected => module.Panels.ForceArea != null &&
                                     (module.Panels.GymExitMapSID != null || gymMode >= 0 && Area.Mode == gymMode);

        public bool IsReturnSelected => module.Panels.ForceArea != null && returnMode >= 0 && Area.Mode == returnMode;

        public bool RequiresSubmenu => module.Panels.ForceArea != null && (IsGymSelected || !IsReturnSelected &&
            (module.SaveData.SessionsPerLevel.ContainsKey(SID) ||
             Text.Dialog.Has(module.Panels.ForceArea.Name + "_collabcredits") ||
             Text.Dialog.Has(module.Panels.ForceArea.Name + "_collabcreditstags")));

        public CollabChapterPanelSequence(ISourceEngineContext engine) : base(engine)
        {
            module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CollabModule>().Single();
            panel = new DynamicData(typeof(Celeste.Game.Menus.Chapters.ChapterPanelSequence), this);
        }

        protected override void Reset()
        {
            ResetCustomHeart();
            credits = null;
            gymMode = returnMode = -1;
            activeGymTech = null;
            var forced = module.Panels.ForceArea;
            if (forced == null || Overworld == null)
            {
                base.Reset();
                CustomizeHeart();
                return;
            }

            var save = Process.SaveContext.CurrentSave;
            var selection = ((IOriginalSaveSessionSelectionEnvironment)engine).SaveSessionSelection;
            var current = selection.GetCurrentSessionSafe(save);
            module.Panels.RememberLastArea(selection.GetLastAreaSafe(save));
            selection.SetLastAreaSafe(save, AreaKeys.ToKey(forced));
            selection.SetCurrentSessionSafe(save, null);
            try
            {
                base.Reset();
            }
            finally
            {
                selection.SetCurrentSessionSafe(save, current);
            }

            CustomizeHeart();

            if (!module.Identities.IsCollabLobby(forced.SID))
            {
                string author = forced.Name + "_author";
                panel.Set("chapter", Text.Dialog.Has(author) ? Text.Dialog.Clean(author) : string.Empty);
                string collab = module.Identities.GetCollabNameForSID(forced.SID);
                if (collab != null && module.Maps.GymLevels.TryGetValue(forced.SID, out var level) &&
                    module.Maps.GymTech.TryGetValue(collab, out var tech) && level.Tech.Any(tech.ContainsKey))
                {
                    activeGymTech = level.Tech;
                    gymMode = modes.Count;
                    modes.Add(CreateMode("collabutils2_overworld_gym", "CollabUtils2/menu/ppt", "FFD07E"));
                }
            }

            if (module.Panels.ExitFromGym)
            {
                returnMode = modes.Count;
                modes.Add(CreateMode("collabutils2_overworld_exit", "CollabUtils2/menu/exit", "FA5139"));
                // The gym exit panel starts on its tech page, preserving the current room selection.
                activeGymTech = module.Maps.GymLevels[forced.SID].Tech;
                Area.Mode = gymMode;
                panel.Set("selectingMode", false);
                panel.Set("height", 730f);
                panel.Set("contentOffset", new System.Numerics.Vector2(440f, 120f));
                ConfigureCheckpointOptions(true);
                Overworld.ShowInputUI = true;
                Focused = true;
            }
        }

        private Option CreateMode(string label, string icon, string color)
        {
            var result = new Option(engine, this)
            {
                Label = Text.Dialog.Clean(label),
                Icon = Graphics.GuiAtlas[icon],
                BgColor = Celeste.Graphics.Display.Colors.SourceHexColor.HexToColor(color)
            };
            ApplyOptionBackground(result);
            return result;
        }

        public int GetCollabModeHeight(int originalHeight) => IsGymSelected || IsReturnSelected ? 300 : originalHeight;

        protected override int GetStatsMode() => IsGymSelected || IsReturnSelected ? 0 : base.GetStatsMode();

        protected override int GetModeHeight()
        {
            if (module.Panels.ForceArea != null && !Data.Interlude &&
                (RealStats.Modes[GetStatsMode()].Deaths > 0 || module.SaveData.SpeedBerryPBs.ContainsKey(SID)))
                return IsGymSelected || IsReturnSelected ? 300 : 540;
            return GetCollabModeHeight(base.GetModeHeight());
        }

        protected override int GetCheckpointPanelHeight() =>
            RequiresSubmenu ? GetCollabCheckpointHeight() : base.GetCheckpointPanelHeight();

        protected override bool ShouldUseCheckpointSubmenu() => !IsReturnSelected &&
                                                                (RequiresSubmenu || base.ShouldUseCheckpointSubmenu());

        protected override void OnCheckpointOptionsCreated()
        {
            if (RequiresSubmenu) ConfigureCheckpointOptions(false);
        }

        protected override HashSet<string> GetPanelCheckpoints() =>
            RequiresSubmenu ? new HashSet<string>() : base.GetPanelCheckpoints();

        protected override void UpdateStats(bool wiggle = true, bool? overrideStrawberryWiggle = null,
            bool? overrideDeathWiggle = null, bool? overrideHeartWiggle = null)
        {
            int mode = Area.Mode;
            Area.Mode = GetStatsMode();
            try
            {
                base.UpdateStats(wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);
            }
            finally
            {
                Area.Mode = mode;
            }

            CustomizeCounters();

            if (IsGymSelected || IsReturnSelected)
            {
                panel.Get<Celeste.Game.Presentation.Hud.Counters.StrawberriesCounterState>("strawberries").Visible =
                    false;
                panel.Get<Celeste.Game.Presentation.Hud.Counters.DeathsCounterState>("deaths").Visible = false;
                panel.Get<Celeste.Game.Presentation.Hud.Collectibles.HeartGemDisplayState>("heart").Visible = false;
            }
            else if (module.Panels.ForceArea != null && !Data.Interlude && DisplayedStats.Modes[Area.Mode].Deaths > 0)
            {
                panel.Get<Celeste.Game.Presentation.Hud.Counters.DeathsCounterState>("deaths").Visible = true;
            }
        }

        public int GetCollabCheckpointHeight()
        {
            if (IsGymSelected) return 730;
            string name = module.Panels.ForceArea.Name;
            return Text.Dialog.Has(name + "_collabcredits") ? 730 :
                Text.Dialog.Has(name + "_collabcreditstags") ? 450 : 300;
        }

        public bool BeforeStart(string checkpoint)
        {
            if (module.Panels.ForceArea == null) return true;
            if (IsReturnSelected)
            {
                module.Panels.ExitToLobby(this);
                return false;
            }

            if (IsGymSelected)
            {
                Area.Mode = 0;
                module.Panels.GymExitMapSID = module.Panels.ForceArea.SID;
                module.Panels.GymExitSaveAllowed = module.Panels.SaveAndReturnToLobbyAllowed;
                module.Panels.SaveAndReturnToLobbyAllowed = false;
            }
            else if (checkpoint != ContinueCheckpoint)
            {
                module.SaveData.SessionsPerLevel.Remove(SID);
                module.SaveData.ModSessionsPerLevel.Remove(SID);
                module.SaveData.ModSessionsPerLevelBinary.Remove(SID);
            }

            return true;
        }

        protected override ChapterCompletionSession CreateStartSession(string checkpoint)
        {
            if (module.Panels.ForceArea == null) return base.CreateStartSession(checkpoint);
            ChapterCompletionSession session;
            if (checkpoint == ContinueCheckpoint)
            {
                // The shared entry routing hook restores vanilla and mod sessions together.
                session = base.CreateStartSession(null);
            }
            else if (IsGymSelected && checkpoint != null)
            {
                int separator = checkpoint.IndexOf('|');
                var target = AreaKeys.GetAreaBySID(checkpoint.Substring(0, separator));
                session = ((IOriginalSessionEnvironment)engine).Sessions.CreateSession(AreaKeys.ToKey(target),
                    checkpoint.Substring(separator + 1));
            }
            else
            {
                session = base.CreateStartSession(checkpoint);
            }

            module.Panels.PrepareReturnSession(session);
            return session;
        }

        protected override string GetAreaselectTexture(string path)
        {
            string original = base.GetAreaselectTexture(path);
            if (original != path || !path.StartsWith("areaselect/card", StringComparison.Ordinal)) return original;
            string color = module.Maps.MapsWithRainbowBerries.Contains(SID) ? "rainbow" :
                module.Maps.MapsWithSilverBerries.Contains(SID) ? "silver" : null;
            if (color == null) return original;
            string texture = path.Contains("cardtop") ? "cardtop" : "card";
            string mapPath = $"CollabUtils2/chapterCard/{SID}_{texture}_{color}";
            if (Graphics.GuiAtlas.Has(mapPath)) return mapPath;
            string setPath = $"CollabUtils2/chapterCard/{Data.LevelSet}/{texture}_{color}";
            return Graphics.GuiAtlas.Has(setPath) ? setPath : $"CollabUtils2/chapterCard/{texture}_{color}";
        }
    }
}
