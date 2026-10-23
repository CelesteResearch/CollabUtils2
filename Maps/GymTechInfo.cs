using Celeste.Core.Display;
using Celeste.Core.Maps;

namespace Celeste.Mods.CollabUtils2.Maps
{
    public struct GymTechInfo
    {
        public string Difficulty; // for legacy placements, should no longer be used
        public int? Order;
        public PackedColor? Color;
        public PackedColor? LearnedColor;
        public string AreaSID;
        public string Level;
        public bool LegacyRenderMode; // for legacy placements, should no longer be used
    }
}
