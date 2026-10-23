using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Presentation.Hud;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Text.RichText.Drawing;
using Celeste.Graphics.Text.RichText.Layout;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed partial class CollabChapterPanelSequence
    {
        private RichTextLayout credits;

        protected override void DrawCheckpoint(ISourceRenderContext context, Vector2 center, Option checkpointOption,
            int checkpointIndex)
        {
            if (module.Panels.ForceArea == null || !RequiresSubmenu)
            {
                base.DrawCheckpoint(context, center, checkpointOption, checkpointIndex);
                return;
            }

            if (checkpointOption is GymOption gym)
            {
                DrawGymCheckpoint(context, center, gym, checkpointIndex);
                return;
            }

            if (checkpointIndex != 0) return;
            string key = module.Panels.ForceArea.Name + "_collabcredits";
            if (!Text.Dialog.Has(key)) return;
            credits ??= OriginalFancyTextParser.Parse(Text.Dialog.Get(key).Replace("{break}", "{n}"), int.MaxValue,
                int.MaxValue, 1f, PackedColor.Black,
                OriginalFancyTextContext.Create((IOriginalFancyTextProcess)engine));
            float height = panel.Get<float>("height");
            float alpha = Math.Max(0f, Math.Min(1f, (height - 600f) / 130f));
            float scale = Math.Min(1f, Math.Min(410f / (credits.Font.Get(credits.BaseSize).LineHeight *
                                                        (credits.Lines + 1)),
                800f / Math.Max(1f, credits.WidestLine())));
            credits.DrawJustifyPerLine(context, this, (ISourceRichTextDrawProcess)engine,
                center + new Vector2(0f, 40f), Vector2.One * 0.5f, Vector2.One * scale, 0.8f * alpha);
        }

        private void DrawGymCheckpoint(ISourceRenderContext context, Vector2 center, GymOption gym, int checkpointIndex)
        {
            string collab = module.Identities.GetCollabNameForSID(module.Panels.ForceArea.SID);
            string imageName = $"{collab}/Gyms/{gym.TechName}";
            if (!MountainResources.Checkpoints.Has(imageName)) return;
            var preview = MountainResources.Checkpoints[imageName];
            if (gym.LegacyRenderMode)
            {
                SourceAtlasDrawing.DrawCentered(context, this, preview,
                    center + Vector2.UnitX * 800f * Easing.CubeIn(gym.CheckpointSlideOut),
                    PackedColor.White, Vector2.One * 0.5f);
                return;
            }

            float rotation = gym.CheckpointRotation;
            Vector2 position = center + gym.CheckpointOffset +
                               new Vector2(800f * Easing.CubeIn(gym.CheckpointSlideOut), 20f);
            SourceAtlasDrawing.DrawCentered(context, this, MountainResources.Checkpoints["CollabUtils2/polaroid"],
                position, PackedColor.White, 0.85f, rotation);
            OriginalHiresRendererBatch.EndRender(context);
            OriginalHiresRendererBatch.BeginRender(context, SourceBlendState.AlphaBlend, SourceSamplerState.PointClamp);
            SourceAtlasDrawing.DrawCentered(context, this, preview, position, PackedColor.White,
                Vector2.One * (0.85f * 720f / preview.FrameWidth), rotation);
            OriginalHiresRendererBatch.EndRender(context);
            OriginalHiresRendererBatch.BeginRender(context);
            if (!gym.Learned) return;
            var info = module.Maps.GymTech[collab][gym.TechName];
            int difficulty = Array.IndexOf(DifficultyOrder, info.Difficulty);
            PackedColor color = info.LearnedColor ?? SourceHexColor.HexToColor(
                difficulty >= 0 ? LearnedColors[difficulty] : "abf797");
            Vector2 offset = Vector2.Transform(new Vector2(280f, -160f), Matrix3x2.CreateRotation(rotation));
            var pulse = panel.Get<Celeste.Scene.Components.Oscillation.OscillationPulseState>("wiggler");
            float scale = 1f + (checkpointIndex == option ? pulse.Value * gym.Pop * 0.4f : 0f);
            SourceAtlasDrawing.DrawCentered(context, this, MountainResources.Checkpoints["CollabUtils2/learnedMarker"],
                position + offset, color, scale, -rotation * 0.2f);
        }
    }
}
