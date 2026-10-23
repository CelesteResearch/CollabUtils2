using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Randomness;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.CollabUtils2.Maps;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed partial class CollabChapterPanelSequence
    {
        private static readonly string[] DifficultyOrder =
            { "beginner", "intermediate", "advanced", "expert", "grandmaster" };

        private static readonly string[] DifficultyColors = { "56b3ff", "ff6d81", "ffff89", "ff9e66", "dd87ff" };
        private static readonly string[] LearnedColors = { "a7e2f9", "faa7bc", "fbf8b8", "fbd0a6", "f3bafa" };

        private sealed class GymOption : Option
        {
            public GymOption(Celeste.Core.Scheduling.ISourceEngineContext engine) : base(engine)
            {
            }

            public string TechName;
            public string CategoryLabel;
            public bool LegacyRenderMode;
            public bool Learned;
        }

        public void ConfigureCheckpointOptions(bool selectCurrentGym)
        {
            if (IsGymSelected)
            {
                ConfigureGymOptions(selectCurrentGym);
                return;
            }

            bool hasContinue = module.SaveData.SessionsPerLevel.ContainsKey(SID);
            checkpoints.Clear();
            checkpoints.Add(CreateCheckpoint(hasContinue ? "collabutils2_chapterpanel_start" : "overworld_start",
                "areaselect/startpoint", "eabe26", null, hasContinue ? 2 : 1));
            if (hasContinue)
                checkpoints.Add(CreateCheckpoint("collabutils2_chapterpanel_continue", "areaselect/checkpoint",
                    "3c6180", ContinueCheckpoint, 2));
            option = hasContinue ? 1 : 0;
        }

        private Option CreateCheckpoint(string label, string icon, string color, string checkpoint, int siblings)
        {
            var result = new Option(engine, this)
            {
                Label = Text.Dialog.Clean(label),
                Icon = Graphics.GuiAtlas[GetAreaselectTexture(icon)],
                BgColor = SourceHexColor.HexToColor(color),
                CheckpointLevelName = checkpoint,
                Large = false,
                Siblings = siblings
            };
            RandomizeCheckpoint(result, 0.2f);
            ApplyOptionBackground(result);
            return result;
        }

        private void RandomizeCheckpoint(Option result, float rotationRange)
        {
            var random = ((ISourceRandomEnvironment)engine).Random;
            result.CheckpointRotation = RandomSelection.Choose<int>(random, -1, 1) *
                                        SourceRandomMath.Range(random, 0.05f, rotationRange);
            result.CheckpointOffset = new Vector2(SourceRandomMath.Range(random, -16, 16),
                SourceRandomMath.Range(random, -16, 16));
        }

        private void ConfigureGymOptions(bool selectCurrentGym)
        {
            string collab = module.Identities.GetCollabNameForSID(module.Panels.ForceArea.SID);
            var available = module.Maps.GymTech[collab];
            string[] tech = activeGymTech.Where(available.ContainsKey).OrderBy(name => available[name].Order ??
                Array.IndexOf(DifficultyOrder, available[name].Difficulty)).ToArray();

            bool Learned(string name) => module.SaveData.LearnedTech.TryGetValue(collab, out var learned) &&
                                         learned.Contains(name);

            tech = tech.Where(name => !Learned(name)).Concat(tech.Where(Learned)).ToArray();
            checkpoints.Clear();
            int selected = 0;
            var current = ((Celeste.Game.Save.Runtime.IOriginalSaveSessionSelectionEnvironment)engine)
                .SaveSessionSelection.GetCurrentSessionSafe(Process.SaveContext.CurrentSave);
            foreach (string name in tech)
            {
                GymTechInfo info = available[name];
                bool learned = Learned(name);
                int difficulty = Array.IndexOf(DifficultyOrder, info.Difficulty);
                string owner = module.Identities.GetCollabNameForSID(info.AreaSID);
                string category = $"{owner}_gym_{name}_category";
                var result = new GymOption(engine)
                {
                    TechName = name,
                    Label = Text.Dialog.Clean($"{owner}_gym_{name}_name"),
                    CategoryLabel = Text.Dialog.Has(category) ? Text.Dialog.Clean(category) :
                        info.Difficulty != null ? Text.Dialog.Clean($"collabutils2_difficulty_{info.Difficulty}") :
                        null,
                    BgColor = learned
                        ? info.LearnedColor ??
                          SourceHexColor.HexToColor(difficulty >= 0 ? LearnedColors[difficulty] : "abf797")
                        : info.Color ??
                          SourceHexColor.HexToColor(difficulty >= 0 ? DifficultyColors[difficulty] : "f2e0cb"),
                    Icon = Graphics.GuiAtlas[
                        learned ? "CollabUtils2/areaselect/gym_checkmark" : "CollabUtils2/areaselect/gym_startpoint"],
                    CheckpointLevelName = $"{info.AreaSID}|{info.Level}",
                    Large = false,
                    Siblings = tech.Length,
                    LegacyRenderMode = info.LegacyRenderMode,
                    Learned = learned
                };
                ApplyOptionBackground(result);
                RandomizeCheckpoint(result, 0.1f);
                if (selectCurrentGym && current != null &&
                    AreaKeys.GetSID(AreaKeys.GetSessionArea(current)) == info.AreaSID &&
                    current.Level == info.Level) selected = checkpoints.Count;
                checkpoints.Add(result);
            }

            option = selected;
            for (int i = 0; i < checkpoints.Count; i++) checkpoints[i].SlideTowards(i, checkpoints.Count, true);
        }
    }
}
