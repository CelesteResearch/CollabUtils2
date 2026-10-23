using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Controls.Source;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Components.Oscillation;

namespace Celeste.Mods.CollabUtils2.UI.LobbyMaps
{
    public sealed class LobbyMapButtonPresentation
    {
        private readonly OriginalGameInputProcess input;
        private readonly OriginalGameTextProcess text;
        private readonly List<AtlasEntry> textures = new List<AtlasEntry>(4);

        public LobbyMapButtonPresentation(OriginalGameInputProcess input, OriginalGameTextProcess text)
        {
            this.input = input;
            this.text = text;
        }

        public void RenderMultiButton(ISourceRenderContext context, object entity, ref Vector2 position,
            float xAdvance, LobbyMapButtonRenderInfo info, float scale = 1f, float alpha = 1f,
            float justifyX = 0.5f, float wiggle = 1f, WigglerComponent wiggler = null)
        {
            float width = RenderMultiButton(context, entity, position, info, scale, alpha, justifyX, wiggle, wiggler);
            if (justifyX < 0.5f) position.X += width + xAdvance;
            else position.X -= width + xAdvance;
        }

        public float RenderMultiButton(ISourceRenderContext context, object entity, Vector2 position,
            LobbyMapButtonRenderInfo info, float scale = 1f, float alpha = 1f,
            float justifyX = 0.5f, float wiggle = 1f, WigglerComponent wiggler = null)
        {
            textures.Clear();
            string fallback = info.ShowFallback ? "controls/keyboard/oemquestion" : null;
            if (info.Button1 != null) textures.Add(input.GuiButton(info.Button1, fallback: fallback));
            if (info.Button2 != null) textures.Add(input.GuiButton(info.Button2, fallback: fallback));
            if (info.Button3 != null) textures.Add(input.GuiButton(info.Button3, fallback: fallback));
            if (info.Button4 != null) textures.Add(input.GuiButton(info.Button4, fallback: fallback));
            float buttonWidths = 0f;
            foreach (var texture in textures) buttonWidths += texture?.FrameWidth ?? 0;
            float labelWidth = text.ActiveFont.Measure(info.Label).X;
            float fullWidth = labelWidth + 8f + buttonWidths;
            float labelJustifyX = fullWidth / 2f / labelWidth;
            position.X += scale * fullWidth * (0.5f - justifyX);
            wiggle *= (wiggler ?? info.Wiggler)?.Value ?? 1f;
            text.ActiveFont.DrawOutline(context, entity, info.Label, position, new Vector2(labelJustifyX, 0.5f),
                Vector2.One * (scale + wiggle), PackedColor.White * alpha, 2f, PackedColor.Black * alpha);
            float buttonX = labelWidth + 8f - fullWidth / 2f;
            for (int i = 0; i < textures.Count; i++)
            {
                var texture = textures[i];
                if (texture == null) continue;
                Vector2 origin = new Vector2(-buttonX, texture.FrameHeight / 2f);
                buttonX += texture.FrameWidth;
                float buttonAlpha = info.AlphaForButtonIndex(i);
                if (buttonAlpha > 0f)
                    SourceAtlasDrawing.Draw(context, entity, texture, position, origin,
                        PackedColor.White * (alpha * buttonAlpha), scale + wiggle);
            }

            return fullWidth * scale;
        }
    }
}
