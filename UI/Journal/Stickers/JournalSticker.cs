using System.Collections.Generic;

namespace Celeste.Mods.CollabUtils2.UI.Journal.Stickers
{
    public sealed class JournalSticker
    {
        public string Path { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Rotation { get; set; }
        public float Scale { get; set; } = 1;
        public List<string> FinishedMaps { get; set; } = new();
    }
}
