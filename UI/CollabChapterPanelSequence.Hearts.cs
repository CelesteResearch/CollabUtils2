using Celeste.Game.Presentation.Hud.Collectibles;
using Celeste.Mods.CollabUtils2.UI.LobbyMaps;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed partial class CollabChapterPanelSequence
    {
        private bool customHeart;

        private void ResetCustomHeart()
        {
            if (!customHeart) return;
            Remove(panel.Get<HeartGemDisplayState>("heart"));
            var heart = new HeartGemDisplayState(engine, 0, false);
            panel.Set("heart", heart);
            Add(heart);
            customHeart = false;
        }

        private void CustomizeHeart()
        {
            var registry = CollabHeartSpriteRegistry.For(engine);
            var sprites = panel.Get<HeartGemDisplayState>("heart").Sprites;
            for (int side = 0; side < sprites.Length; side++)
            {
                string id = registry.GetGuiHeartSpriteId(SID, side);
                if (id == null) continue;
                var sprite = registry.Create(id);
                sprite.Visible = sprites[side].Visible;
                sprites[side] = sprite;
                sprite.Play("spin");
                customHeart = true;
            }
        }
    }
}
