using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Settings;
using Celeste.Graphics.Rendering;
using Celeste.Input.Bindings;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.UI.Menus.Text;

namespace Celeste.Mods.CollabUtils2.UI
{
    // heavily based on the parts of OuiFileSelectSlot that make up the "are you sure you want to delete this file" message.
    public class AssistSkipConfirmUI : SceneEntityState
    {
        private float openingEase;
        private bool opened;
        private int currentlySelectedOption; // 0 is yes, 1 is no

        private Action onConfirm;
        private Action onCancel;

        private WigglerComponent wiggler;
        private readonly ISourceEngineContext engine;

        public AssistSkipConfirmUI(ISourceEngineContext engine, Action onConfirm, Action onCancel) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            Tags = ((IOriginalGameTagsEnvironment)engine).Tags.HUD.Value |
                   ((IOriginalGameTagsEnvironment)engine).Tags.PauseUpdate.Value;

            OwnedComponents.Add(wiggler =
                WigglerComponent.Create(((ISourceWigglerPoolScope)engine).WigglerPool, 0.4f, 4f));
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            opened = true;
        }

        public override void Update()
        {
            base.Update();

            // fade the menu in or out
            openingEase = ScalarMath.Approach(openingEase, opened ? 1f : 0f, engine.DeltaTime * 4f,
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);

            if (opened)
            {
                if (((ISourceMenuInputEnvironment)engine).MenuInput.MenuCancel.Pressed)
                {
                    // cancelled out
                    opened = false;
                    wiggler.Start();
                    ((ITextMenuProcessEnvironment)engine).PlayMenuSound("event:/ui/main/button_back");
                    onCancel();
                }
                else if (((ISourceMenuInputEnvironment)engine).MenuInput.MenuUp.Pressed && currentlySelectedOption > 0)
                {
                    // move up to "yes"
                    currentlySelectedOption = 0;
                    wiggler.Start();
                    ((ITextMenuProcessEnvironment)engine).PlayMenuSound("event:/ui/main/rollover_up");
                }
                else if (((ISourceMenuInputEnvironment)engine).MenuInput.MenuDown.Pressed &&
                         currentlySelectedOption < 1)
                {
                    // move down to "no"
                    currentlySelectedOption = 1;
                    wiggler.Start();
                    ((ITextMenuProcessEnvironment)engine).PlayMenuSound("event:/ui/main/rollover_down");
                }
                else if (((ISourceMenuInputEnvironment)engine).MenuInput.MenuConfirm.Pressed)
                {
                    if (currentlySelectedOption == 1)
                    {
                        // pressed "no"
                        opened = false;
                        wiggler.Start();
                        ((ITextMenuProcessEnvironment)engine).PlayMenuSound("event:/ui/main/button_back");
                        onCancel();
                    }
                    else
                    {
                        // pressed "yes"
                        RemoveSelf();
                        ((ITextMenuProcessEnvironment)engine).PlayMenuSound("event:/ui/main/button_select");
                        onConfirm();
                    }
                }
            }
            else if (openingEase <= 0f)
            {
                // finished fading out
                RemoveSelf();
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);

            float currentWiggle = wiggler.Value * 8f;

            if (openingEase > 0f)
            {
                float openingEaseEased = ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeOut(openingEase);
                Vector2 anchor = new Vector2(960f, 540f);
                float lineHeight = ((IOriginalGameTextEnvironment)engine).Text.ActiveFont.LineHeight;

                // draw background
                SourcePrimitiveDrawing.Rect(context, this, -10f, -10f, 1940f, 1100f,
                    PackedColor.Black * openingEaseEased * 0.9f);

                // draw text
                ((IOriginalGameTextEnvironment)engine).Text.ActiveFont.Draw(context, this,
                    ((IOriginalGameTextEnvironment)engine).Text.Dialog.Clean("collabutils2_assist_skip_confirm"),
                    anchor + new Vector2(
                        x: 0f,
                        y: -16f - 64f * (1f - openingEaseEased)),
                    new Vector2(0.5f, 1f),
                    Vector2.One,
                    PackedColor.White * openingEaseEased);

                ((IOriginalGameTextEnvironment)engine).Text.ActiveFont.DrawOutline(context, this,
                    ((IOriginalGameTextEnvironment)engine).Text.Dialog.Clean("collabutils2_assist_skip_confirm_yes"),
                    anchor + new Vector2(
                        x: ((opened && currentlySelectedOption == 0) ? currentWiggle : 0f) * 1.2f * openingEaseEased,
                        y: 16f + 64f * (1f - openingEaseEased)),
                    new Vector2(0.5f, 0f),
                    Vector2.One * 0.8f,
                    opened ? selectionColor(currentlySelectedOption == 0) : PackedColor.Gray,
                    2f,
                    PackedColor.Black * openingEaseEased);

                ((IOriginalGameTextEnvironment)engine).Text.ActiveFont.DrawOutline(context, this,
                    ((IOriginalGameTextEnvironment)engine).Text.Dialog.Clean("collabutils2_assist_skip_confirm_no"),
                    anchor + new Vector2(
                        x: ((opened && currentlySelectedOption == 1) ? currentWiggle : 0f) * 1.2f * openingEaseEased,
                        y: 16f + lineHeight + 64f * (1f - openingEaseEased)),
                    new Vector2(0.5f, 0f),
                    Vector2.One * 0.8f,
                    opened ? selectionColor(currentlySelectedOption == 1) : PackedColor.Gray,
                    2f,
                    PackedColor.Black * openingEaseEased);
            }
        }

        private PackedColor selectionColor(bool selected)
        {
            if (selected)
            {
                if (!((ISettingsContext)engine).Settings.DisableFlashes &&
                    !((ISourceScene)Scene).SourceScene.Clock.BetweenInterval(0.1f))
                {
                    return TextMenuPresentation.HighlightColorB;
                }

                return TextMenuPresentation.HighlightColorA;
            }

            return PackedColor.White;
        }
    }
}
