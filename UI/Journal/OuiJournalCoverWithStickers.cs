using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Menus.Journal;
using Celeste.Game.Save.Runtime;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.UI.Journal.Stickers;

namespace Celeste.Mods.CollabUtils2.UI.Journal
{
    public class OuiJournalCoverWithStickers : OuiJournalCoverPage
    {
        private readonly CollabJournalStickerProcess stickers;
        private readonly List<JournalSticker> stickersToRender;

        public OuiJournalCoverWithStickers(CollabModule module, OuiJournalSequence journal) : base(journal)
        {
            stickers = CollabJournalStickerProcess.For(module);
            var save = ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave;
            var session =
                ((IOriginalSaveSessionSelectionEnvironment)engine).SaveSessionSelection.GetCurrentSessionSafe(save);
            stickersToRender = stickers.SelectForSession(session);
        }

        public override void Redraw(VirtualRenderTarget buffer)
        {
            base.Redraw(buffer);
            ((ISourceDrawScope)Context).Draw.SpriteBatch.Begin();
            foreach (var sticker in stickersToRender)
                SourceAtlasDrawing.DrawCentered(Context, this, stickers.GetTexture(sticker.Path),
                    new Vector2(sticker.X, sticker.Y), PackedColor.White, sticker.Scale,
                    (float)(sticker.Rotation * Math.PI / 180));
            ((ISourceDrawScope)Context).Draw.SpriteBatch.End();
        }
    }
}
