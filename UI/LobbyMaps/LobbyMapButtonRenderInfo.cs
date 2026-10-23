using Celeste.Input.Bindings;
using Celeste.Scene.Components.Oscillation;

namespace Celeste.Mods.CollabUtils2.UI.LobbyMaps
{
    public struct LobbyMapButtonRenderInfo
    {
        public readonly string Label;
        public readonly SourceVirtualButton Button1;
        public readonly SourceVirtualButton Button2;
        public readonly SourceVirtualButton Button3;
        public readonly SourceVirtualButton Button4;
        public readonly int ButtonCount;
        public readonly bool ShowFallback;
        public readonly WigglerComponent Wiggler;
        public float Button1Alpha;
        public float Button2Alpha;
        public float Button3Alpha;
        public float Button4Alpha;

        public LobbyMapButtonRenderInfo(string label, SourceVirtualButton button1 = null,
            SourceVirtualButton button2 = null, SourceVirtualButton button3 = null,
            SourceVirtualButton button4 = null, WigglerComponent wiggler = null, bool showFallback = true)
        {
            Label = label;
            Button1 = button1;
            Button2 = button2;
            Button3 = button3;
            Button4 = button4;
            Wiggler = wiggler;
            ButtonCount = button1 == null ? 0 : button2 == null ? 1 : button3 == null ? 2 : button4 == null ? 3 : 4;
            ShowFallback = showFallback;
            Button1Alpha = Button2Alpha = Button3Alpha = Button4Alpha = 1f;
        }

        public float AlphaForButtonIndex(int index) => index switch
        {
            0 => Button1Alpha,
            1 => Button2Alpha,
            2 => Button3Alpha,
            3 => Button4Alpha,
            _ => 0f
        };
    }
}
