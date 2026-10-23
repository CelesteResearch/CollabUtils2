using System.Numerics;
using Celeste.Core.Display;
using Celeste.Game.Menus.Journal;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;

namespace Celeste.Mods.CollabUtils2.UI.Journal
{
    public class OuiJournalLobbyMap : OuiJournalPage
    {
        private readonly AtlasEntry mapImage;

        public OuiJournalLobbyMap(OuiJournalSequence journal, AtlasEntry mapImage) : base(journal)
        {
            PageTexture = "page";
            this.mapImage = mapImage;
        }

        public override void Redraw(VirtualRenderTarget buffer)
        {
            base.Redraw(buffer);
            ((ISourceDrawScope)Context).Draw.SpriteBatch.Begin();
            SourceAtlasDrawing.DrawCentered(Context, this, mapImage, new Vector2(PageWidth / 2, PageHeight / 2),
                PackedColor.White);
            ((ISourceDrawScope)Context).Draw.SpriteBatch.End();
        }
    }
}
