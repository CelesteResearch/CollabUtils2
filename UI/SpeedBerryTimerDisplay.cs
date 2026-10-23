using Celeste.Mods.CollabUtils2.Entities;
using Celeste.Mods.CollabUtils2.Persistence;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Core.Components;
using Celeste.Core.Components.Animation;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Mathematics.Easing;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Settings;
using Celeste.Game.Presentation.Hud.Speedrun;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Display.Colors;
using Celeste.Game.Execution.Areas;
using Celeste.Graphics.Text.BitmapFonts;
using Celeste.Scene.Entities;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using System.Numerics;
using System;
using System.Collections.Generic;

namespace Celeste.Mods.CollabUtils2.UI
{
    [SceneTracked]
    public class SpeedBerryTimerDisplay : SceneEntityState
    {
        private const float targetTimeScale = 0.7f;
        private const float lineSeparationFactor = 0.8f;

        public SpeedBerry TrackedBerry;

        private long startChapterTimer = -1;
        private long endChapterTimer = -1;

        // measurements
        private float numberWidth;
        private float spacerWidth;
        private float numberHeight;

        private Dictionary<string, Vector2> rankMeasurements;

        // draw state
        private float fadeTime;
        private bool timerEnded;
        private float drawLerp;

        private Vector2 offscreenPosition;
        private Vector2 onscreenPosition;

        private WigglerComponent wiggler;
        private bool tweenActive;

        private readonly AtlasEntry bg;
        public ISourceEngineContext Engine => TrackedBerry.Engine;
        public CollabModule Module => TrackedBerry.Module;
        private OriginalGameTextProcess Text => ((IOriginalGameTextEnvironment)Engine).Text;
        public bool Started => startChapterTimer != -1;

        private static Dictionary<string, PackedColor> rankPackedColors = new Dictionary<string, PackedColor>()
        {
            { "Bronze", SourceHexColor.HexToColor("cd7f32") },
            { "Silver", PackedColor.Silver },
            { "Gold", PackedColor.Gold },
            { "None", PackedColor.OrangeRed }
        };

        public SpeedBerryTimerDisplay(SpeedBerry berry)
            : base(((ISourceEntitySourceDataEnvironment)berry.Engine).EntitySourceDataRegistration)
        {
            TrackedBerry = berry;
            bg = ((OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)Engine).GameGraphics).GuiAtlas[
                "CollabUtils2/extendedStrawberryCountBG"];
            drawLerp = 0f;
            var tags = ((IOriginalGameTagsEnvironment)Engine).Tags;
            Tags = tags.HUD.Value | tags.Global.Value | tags.PauseUpdate.Value | tags.TransitionUpdate.Value;
            calculateBaseSizes();
            wiggler = ((ISourceWigglerPoolScope)Engine).WigglerPool.Create(0.5f, 4f);
            wiggler.AddTo(OwnedComponents);
            TrackedBerry = berry;
            timerEnded = false;
            fadeTime = 3f;

            createTween(0.6f, t => { Position = Vector2.Lerp(offscreenPosition, onscreenPosition, t.Eased); });
        }

        private void createTween(float fadeTime, Action<SourceTweenState> onUpdate)
        {
            SourceTweenState tween = SourceTweenState.Create(Engine.TweenPool, SourceTweenState.TweenMode.Oneshot,
                ((ISourceEasingEnvironment)Engine).EasingArithmetic.CubeInOut, fadeTime, true);
            tween.OnUpdate = onUpdate;
            tween.OnComplete = _ => tweenActive = false;
            OwnedComponents.Add(tween);
            tweenActive = true;
        }

        public long GetSpentTime()
        {
            if (startChapterTimer == -1)
            {
                return 0;
            }
            else if (endChapterTimer == -1)
            {
                return ((OriginalLevelSceneRoot)Scene).Session.Time - startChapterTimer;
            }

            return endChapterTimer - startChapterTimer;
        }

        public void StartTimer()
        {
            if (startChapterTimer == -1)
            {
                startChapterTimer = ((OriginalLevelSceneRoot)Scene).Session.Time;

                if (Module.Settings.HideSpeedBerryTimerDuringGameplay)
                {
                    // hide speed berry timer.
                    createTween(0.6f, t => { Position = Vector2.Lerp(onscreenPosition, offscreenPosition, t.Eased); });
                }
            }
        }

        private void calculateBaseSizes()
        {
            // compute the max size of a digit and separators in the English font, for the timer part.
            SourcePixelFont font = Text.Fonts.Get(Text.Dialog.Languages["english"].FontFace);
            float fontFaceSize = Text.Dialog.Languages["english"].FontFaceSize;
            SourcePixelFontSize pixelFontSize = font.Get(fontFaceSize);
            for (int i = 0; i < 10; i++)
            {
                float digitWidth = pixelFontSize.Measure(i.ToString()).X;
                if (digitWidth > numberWidth)
                {
                    numberWidth = digitWidth;
                }
            }

            spacerWidth = pixelFontSize.Measure('.').X;
            numberHeight = pixelFontSize.Measure("0:.").Y;

            // measure the ranks in the font for the current language.
            rankMeasurements = new Dictionary<string, Vector2>()
            {
                {
                    "Gold",
                    Text.ActiveFont.Measure(Text.Dialog.Clean("collabutils2_speedberry_gold") + " ") * targetTimeScale
                },
                {
                    "Silver",
                    Text.ActiveFont.Measure(Text.Dialog.Clean("collabutils2_speedberry_silver") + " ") * targetTimeScale
                },
                {
                    "Bronze",
                    Text.ActiveFont.Measure(Text.Dialog.Clean("collabutils2_speedberry_bronze") + " ") * targetTimeScale
                }
            };
        }

        public override void Update()
        {
            drawLerp = ScalarMath.Approach(drawLerp, 1f, Engine.DeltaTime * 4f,
                ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);
            if (timerEnded)
            {
                fadeTime -= Engine.DeltaTime;
                if (fadeTime <= 0f)
                {
                    RemoveSelf();
                }
                else if (!tweenActive && fadeTime <= 1.5f)
                {
                    createTween(1.5f, t => { Position = Vector2.Lerp(onscreenPosition, offscreenPosition, t.Eased); });
                }
            }

            if (Module.Settings.SpeedBerryTimerPosition == SpeedBerryTimerPosition.TopCenter)
            {
                offscreenPosition = new Vector2(1920f / 2f, -81f);
                onscreenPosition = new Vector2(1920f / 2f, 89f);
            }
            else if (((ISettingsContext)Engine).Settings.SpeedrunClock == 0)
            {
                offscreenPosition = new Vector2(-400f, 100f);
                onscreenPosition = new Vector2(32f, 100f);
            }
            else if (((ISettingsContext)Engine).Settings.SpeedrunClock == 1)
            {
                offscreenPosition = new Vector2(-400f, 160f);
                onscreenPosition = new Vector2(32f, 160f);
            }
            else
            {
                offscreenPosition = new Vector2(-400f, 180f);
                onscreenPosition = new Vector2(32f, 180f);
            }

            if (!tweenActive)
            {
                // timer is currently on-screen if the hide option is disabled, if the timer didn't start yet, or if it ended.
                if (!Module.Settings.HideSpeedBerryTimerDuringGameplay || startChapterTimer == -1 ||
                    endChapterTimer != -1)
                {
                    Position = onscreenPosition;
                }
                else
                {
                    Position = offscreenPosition;
                }
            }

            Depth = startChapterTimer == -1 ? -10000 : -100;

            base.Update();
        }

        public void EndTimer()
        {
            if (!timerEnded)
            {
                timerEnded = true;
                fadeTime = 5f;
                endChapterTimer = ((OriginalLevelSceneRoot)Scene).Session.Time;
                wiggler.Start();

                // save the PB if this was beaten.
                long time = endChapterTimer - startChapterTimer;
                bool newPB = true;
                if (Module.SaveData.SpeedBerryPBs.TryGetValue(
                        ((IOriginalAreaKeyEnvironment)Engine).AreaKeys.GetSID(
                            ((IOriginalAreaKeyEnvironment)Engine).AreaKeys.GetSessionArea(
                                ((OriginalLevelSceneRoot)Scene).Session)), out long pb))
                {
                    if (time > pb)
                    {
                        newPB = false;
                    }
                }

                if (newPB)
                {
                    Module.SaveData.SpeedBerryPBs[
                        ((IOriginalAreaKeyEnvironment)Engine).AreaKeys.GetSID(
                            ((IOriginalAreaKeyEnvironment)Engine).AreaKeys.GetSessionArea(
                                ((OriginalLevelSceneRoot)Scene).Session))] = time;
                }

                if (Module.Settings.HideSpeedBerryTimerDuringGameplay)
                {
                    // display speed berry timer.
                    createTween(0.6f, t => { Position = Vector2.Lerp(offscreenPosition, onscreenPosition, t.Eased); });
                }
            }
        }

        public string GetNextRank(out float nextRankTime)
        {
            float currentTime = (float)TimeSpan.FromTicks(GetSpentTime()).TotalSeconds;
            string nextRankName;
            if (currentTime < TrackedBerry.GoldTime)
            {
                nextRankTime = TrackedBerry.GoldTime;
                nextRankName = "Gold";
            }
            else if (currentTime < TrackedBerry.SilverTime)
            {
                nextRankTime = TrackedBerry.SilverTime;
                nextRankName = "Silver";
            }
            else if (currentTime < TrackedBerry.BronzeTime)
            {
                nextRankTime = TrackedBerry.BronzeTime;
                nextRankName = "Bronze";
            }
            else
            {
                // time ran out
                nextRankTime = 0;
                nextRankName = "None";
            }

            return nextRankName;
        }

        public override void Render(ISourceRenderContext context)
        {
            if (!(drawLerp <= 0f) && fadeTime > 0f)
            {
                // get next best rank time
                string nextRankName = GetNextRank(out float nextRankTime);
                string currentTimeString = SpeedrunTimerState.ShortGameplayFormat(GetSpentTime());
                string rankTimeString =
                    SpeedrunTimerState.ShortGameplayFormat(TimeSpan.FromSeconds(nextRankTime).Ticks);

                float scale = 1f + wiggler.Value * 0.15f;

                Vector2 currentTimeSize = new Vector2(getTimeWidth(currentTimeString, scale), numberHeight);
                Vector2 rankTimeSize = new Vector2(getTimeWidth(rankTimeString), numberHeight) * targetTimeScale;

                if (Module.Settings.SpeedBerryTimerPosition == SpeedBerryTimerPosition.TopCenter)
                {
                    // Current time
                    drawTime(context,
                        new Vector2(Position.X - (currentTimeSize.X / 2), Y + currentTimeSize.Y * lineSeparationFactor),
                        currentTimeString, rankPackedColors[nextRankName], 1f + wiggler.Value * 0.15f, 1f);
                    float fontOffset = Text.Dialog.Language.FontFace != "Renogare" ? -2f * targetTimeScale : 0f;

                    if (nextRankName != "None")
                    {
                        if (startChapterTimer == -1)
                        {
                            string silverRankTimeString =
                                SpeedrunTimerState.ShortGameplayFormat(TimeSpan.FromSeconds(TrackedBerry.SilverTime)
                                    .Ticks);
                            string bronzeRankTimeString =
                                SpeedrunTimerState.ShortGameplayFormat(TimeSpan.FromSeconds(TrackedBerry.BronzeTime)
                                    .Ticks);
                            drawNextRankTimeTopCenter(context, nextRankName, new Vector2(-320, 0), rankTimeString,
                                rankTimeSize, fontOffset);
                            drawNextRankTimeTopCenter(context, "Silver", new Vector2(0, 0), silverRankTimeString,
                                new Vector2(getTimeWidth(silverRankTimeString), numberHeight) * targetTimeScale,
                                fontOffset);
                            drawNextRankTimeTopCenter(context, "Bronze", new Vector2(320, 0), bronzeRankTimeString,
                                new Vector2(getTimeWidth(bronzeRankTimeString), numberHeight) * targetTimeScale,
                                fontOffset);
                        }
                        else
                        {
                            drawNextRankTimeTopCenter(context, nextRankName, Vector2.Zero, rankTimeString, rankTimeSize,
                                fontOffset);
                        }
                    }
                    else
                    {
                        // draw "time ran out!" text
                        Text.ActiveFont.DrawOutline(context, this,
                            Text.Dialog.Clean($"collabutils2_speedberry_timeranout"),
                            new Vector2(Position.X, Y + fontOffset),
                            new Vector2(0.5f, 1f), Vector2.One * targetTimeScale, rankPackedColors[nextRankName], 2f,
                            PackedColor.Black);
                    }
                }
                else
                {
                    // Current time
                    Vector2 timerOffset = startChapterTimer == -1 ? new Vector2(0, 70) : Vector2.Zero;
                    DrawBackground(context,
                        new Vector2(Position.X - 568 + currentTimeSize.X,
                            Y + currentTimeSize.Y * lineSeparationFactor - 45) + timerOffset);
                    drawTime(context,
                        new Vector2(Position.X, Y + currentTimeSize.Y * lineSeparationFactor) + timerOffset,
                        currentTimeString, rankPackedColors[nextRankName], 1f + wiggler.Value * 0.15f, 1f);

                    float fontOffset = Text.Dialog.Language.FontFace != "Renogare" ? -2f : 0f;

                    if (nextRankName != "None")
                    {
                        // draw next best rank time
                        if (startChapterTimer == -1)
                        {
                            string silverRankTimeString =
                                SpeedrunTimerState.ShortGameplayFormat(TimeSpan.FromSeconds(TrackedBerry.SilverTime)
                                    .Ticks);
                            string bronzeRankTimeString =
                                SpeedrunTimerState.ShortGameplayFormat(TimeSpan.FromSeconds(TrackedBerry.BronzeTime)
                                    .Ticks);
                            drawNextRankTimeTopLeft(context, nextRankName, Vector2.Zero, rankTimeString, rankTimeSize,
                                fontOffset);
                            drawNextRankTimeTopLeft(context, "Silver", new Vector2(0, 35), silverRankTimeString,
                                new Vector2(getTimeWidth(silverRankTimeString), numberHeight) * targetTimeScale,
                                fontOffset);
                            drawNextRankTimeTopLeft(context, "Bronze", new Vector2(0, 70), bronzeRankTimeString,
                                new Vector2(getTimeWidth(bronzeRankTimeString), numberHeight) * targetTimeScale,
                                fontOffset);
                        }
                        else
                        {
                            drawNextRankTimeTopLeft(context, nextRankName, Vector2.Zero, rankTimeString, rankTimeSize,
                                fontOffset);
                        }
                    }
                    else
                    {
                        // draw "time ran out!" text
                        DrawBackground(context, new Vector2(
                            Position.X - 392 +
                            Text.ActiveFont.Measure(Text.Dialog.Clean($"collabutils2_speedberry_timeranout")).X *
                            targetTimeScale,
                            Y - 32), Vector2.Zero, PackedColor.White, 0.7f);

                        Text.ActiveFont.DrawOutline(context, this,
                            Text.Dialog.Clean($"collabutils2_speedberry_timeranout"),
                            new Vector2(Position.X + 3, Y + fontOffset),
                            new Vector2(0f, 1f), Vector2.One * targetTimeScale, rankPackedColors[nextRankName], 2f,
                            PackedColor.Black);
                    }
                }
            }
        }

        private void drawNextRankTimeTopLeft(ISourceRenderContext context, string nextRankName, Vector2 positionOffset,
            string rankTimeString, Vector2 rankTimeSize, float fontOffset)
        {
            DrawBackground(context,
                new Vector2(Position.X - 392 + rankTimeSize.X + rankMeasurements[nextRankName].X, Y - 32) +
                positionOffset, Vector2.Zero, PackedColor.White, 0.7f);
            drawTime(context, new Vector2(Position.X + rankMeasurements[nextRankName].X + 3, Y) + positionOffset,
                rankTimeString, rankPackedColors[nextRankName], targetTimeScale, 1f);
            Text.ActiveFont.DrawOutline(context, this, Text.Dialog.Clean($"collabutils2_speedberry_{nextRankName}"),
                new Vector2(Position.X + 3, Y + fontOffset) + positionOffset,
                new Vector2(0f, 1f), Vector2.One * targetTimeScale, rankPackedColors[nextRankName], 2f,
                PackedColor.Black);
        }

        private void drawNextRankTimeTopCenter(ISourceRenderContext context, string nextRankName,
            Vector2 positionOffset, string rankTimeString, Vector2 rankTimeSize, float fontOffset)
        {
            // draw next best rank time
            Vector2 totalSize = rankTimeSize + rankMeasurements[nextRankName].X * Vector2.UnitX;
            drawTime(context,
                new Vector2(Position.X - (totalSize.X / 2) + rankMeasurements[nextRankName].X, Y) + positionOffset,
                rankTimeString, rankPackedColors[nextRankName], targetTimeScale, 1f);
            Text.ActiveFont.DrawOutline(context, this, Text.Dialog.Clean($"collabutils2_speedberry_{nextRankName}"),
                new Vector2(Position.X - (totalSize.X / 2), Y + fontOffset) + positionOffset,
                new Vector2(0f, 1f), Vector2.One * targetTimeScale, rankPackedColors[nextRankName], 2f,
                PackedColor.Black);
        }

        private void DrawBackground(ISourceRenderContext context, Vector2 position)
            => SourceAtlasDrawing.Draw(context, this, bg, position, Vector2.Zero, PackedColor.White, Vector2.One);

        private void DrawBackground(ISourceRenderContext context, Vector2 position, Vector2 origin, PackedColor color,
            float scale)
            => SourceAtlasDrawing.Draw(context, this, bg, position, origin, color, Vector2.One * scale);

        private float getTimeWidth(string timeString, float scale = 1f)
            => SpeedrunTimerState.TimeWidth(numberWidth, spacerWidth, timeString, scale);

        private void drawTime(ISourceRenderContext context, Vector2 position, string timeString, PackedColor color,
            float scale = 1f, float alpha = 1f)
        {
            SourcePixelFont font = Text.Fonts.Get(Text.Dialog.Languages["english"].FontFace);
            float fontFaceSize = Text.Dialog.Languages["english"].FontFaceSize;
            float currentScale = scale;
            float currentX = position.X;
            float currentY = position.Y;
            color = color * alpha;
            PackedColor colorDoubleAlpha = color * alpha;

            foreach (char c in timeString)
            {
                bool flag2 = c == '.';
                if (flag2)
                {
                    currentScale = scale * 0.7f;
                    currentY -= 5f * scale;
                }

                PackedColor colorToUse = (c == ':' || c == '.' || currentScale < scale) ? colorDoubleAlpha : color;
                float advance = (((c == ':' || c == '.') ? spacerWidth : numberWidth) + 4f) * currentScale;
                font.DrawOutline(context, this, fontFaceSize, c.ToString(),
                    new Vector2(currentX + advance / 2, currentY), new Vector2(0.5f, 1f), Vector2.One * currentScale,
                    colorToUse, 2f, PackedColor.Black);
                currentX += advance;
            }
        }
    }
}
