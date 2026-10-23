using System;
using System.Collections.Generic;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Content.Types.Maps;

namespace Celeste.Mods.CollabUtils2.UI.MapMenus
{
    internal sealed class HelperTestMapVisibility
    {
        private readonly HashSet<string> hiddenMaps = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> hiddenLevelSets = new HashSet<string>(StringComparer.Ordinal);

        public HelperTestMapVisibility(EverestContentRegistry content)
        {
            var visibleLevelSets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in content.Map)
            {
                var asset = pair.Value;
                if (asset.Type != typeof(AssetTypeMap) || pair.Key != asset.PathVirtual ||
                    !pair.Key.StartsWith("Maps/", StringComparison.Ordinal)) continue;
                string sid = pair.Key.Substring(5);
                int separator = sid.LastIndexOf('/');
                string levelSet = sid.Length == 0 ? "Celeste" : separator < 0 ? "" : sid.Substring(0, separator);
                if (IsHelperTestMap(asset.Source.Mod?.Name, sid))
                {
                    hiddenMaps.Add(sid);
                    hiddenLevelSets.Add(levelSet);
                }
                else
                {
                    visibleLevelSets.Add(levelSet);
                }
            }

            hiddenLevelSets.ExceptWith(visibleLevelSets);
        }

        private static bool IsHelperTestMap(string module, string sid)
        {
            switch (module)
            {
                case "BounceHelper":
                    return sid == "Bio/BounceHelperTestMap/0-BounceHelperTestMap" ||
                           sid == "Bio/BounceHelperTestMap/1-chapter1";
                case "JackalHelper":
                    return sid == "Jackal/CodeLab/CodingTestLab" || sid == "Susan/SusanHelper/SusansLair";
                case "AltSidesHelper":
                    return sid == "leppa/AltSidesHelper/AltSidesHelperTest" ||
                           sid == "leppa/AltSidesHelper/AltSidesHelperTest_B" ||
                           sid == "leppa/AltSidesHelper/AltSidesHelperTest_C" ||
                           sid == "leppa/AltSidesHelper/AltSidesHelperTest_D";
                case "bitsbolts":
                    return sid == "bitsbolts/demo";
                default:
                    return false;
            }
        }

        public bool IsHiddenMap(string sid) => hiddenMaps.Contains(sid);
        public bool IsHiddenLevelSet(string levelSet) => hiddenLevelSets.Contains(levelSet);
    }
}
