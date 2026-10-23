using Celeste.Game.Presentation.Hud.Counters;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed partial class CollabChapterPanelSequence
    {
        internal float DeathIconSpacing { get; private set; } = 62f;
        internal string GoldenBerryIcon { get; private set; } = "collectables/goldberry";

        private void CustomizeCounters()
        {
            DeathIconSpacing = 62f;
            string iconPath = "CollabUtils2/skulls/" + SID;
            if (!Graphics.GuiAtlas.Has(iconPath))
            {
                string lobbySet = module.Identities.GetLobbyLevelSet(SID);
                iconPath = lobbySet == null ? null : "CollabUtils2/skulls/" + lobbySet;
                if (iconPath == null || !Graphics.GuiAtlas.Has(iconPath))
                    iconPath = "CollabUtils2/skulls/" + Data.LevelSet;
            }

            if (Graphics.GuiAtlas.Has(iconPath))
            {
                var icon = Graphics.GuiAtlas[iconPath];
                var deaths = panel.Get<DeathsCounterState>("deaths");
                new MonoMod.Utils.DynamicData(typeof(DeathsCounterState), deaths).Set("icon", icon);
                DeathIconSpacing = icon.FrameWidth - 4f;
            }

            GoldenBerryIcon = module.Maps.MapsWithRainbowBerries.Contains(SID)
                ? "CollabUtils2/rainbowberry"
                : module.Maps.MapsWithSilverBerries.Contains(SID)
                    ? "CollabUtils2/silverberry"
                    : "collectables/goldberry";

            if (module.Panels.ForceArea != null || module.Identities.IsCollabLobby(SID))
            {
                var map = Data.Mode[0].MapData;
                if (map.DetectedStrawberriesIncludingUntracked == map.Goldenberries.Count)
                {
                    var berries = panel.Get<StrawberriesCounterState>("strawberries");
                    berries.Golden = true;
                    berries.ShowOutOf = false;
                }
            }
        }
    }
}
