using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Menus.Journal;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;

namespace Celeste.Mods.CollabUtils2.UI.Journal
{
    public sealed class JournalGuiIconCell : OuiJournalPage.Cell
    {
        private readonly string icon;
        private readonly float width;
        private readonly float height;

        public JournalGuiIconCell(ISourceEngineContext engine, string icon, float width, float height) : base(engine)
        {
            this.icon = icon;
            this.width = width;
            this.height = height;
        }

        public override float Width() => width;

        public override void Render(ISourceRenderContext context, object owner, Vector2 center, float columnWidth)
        {
            var atlas = ((OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics).GuiAtlas;
            var texture = atlas[icon];
            SourceAtlasDrawing.DrawCentered(context, owner, texture, center, PackedColor.White,
                Math.Min(width / texture.FrameWidth, height / texture.FrameHeight));
        }
    }
}
