using Celeste.Everest.Input.Bindings;
using Celeste.Input.Devices.GamePads;
using Celeste.Input.Devices.Keyboard;
using Celeste.Everest.Modules.Settings;
using Celeste.Everest.Settings.Metadata;

namespace Celeste.Mods.CollabUtils2.Persistence
{
    public sealed class CollabSettings : EverestModuleSettings
    {
        [DefaultButtonBinding(SourceGamePadButtonFlags.RightStick, SourceKeys.Tab)]
        public EverestButtonBinding DisplayLobbyMap { get; set; }

        [DefaultButtonBinding(0, SourceKeys.LeftShift)]
        public EverestButtonBinding HoldToPan { get; set; }

        [DefaultButtonBinding(SourceGamePadButtonFlags.RightThumbstickUp, 0)]
        public EverestButtonBinding PanLobbyMapUp { get; set; }

        [DefaultButtonBinding(SourceGamePadButtonFlags.RightThumbstickDown, 0)]
        public EverestButtonBinding PanLobbyMapDown { get; set; }

        [DefaultButtonBinding(SourceGamePadButtonFlags.RightThumbstickLeft, 0)]
        public EverestButtonBinding PanLobbyMapLeft { get; set; }

        [DefaultButtonBinding(SourceGamePadButtonFlags.RightThumbstickRight, 0)]
        public EverestButtonBinding PanLobbyMapRight { get; set; }

        public SpeedBerryTimerPosition SpeedBerryTimerPosition { get; set; } = SpeedBerryTimerPosition.TopLeft;
        public bool HideSpeedBerryTimerDuringGameplay { get; set; }

        [SettingSubText("modoptions_collab_displayendscreenforallmaps_description")]
        public bool DisplayEndScreenForAllMaps { get; set; }

        public BestTimeInJournal BestTimeToDisplayInJournal { get; set; } = BestTimeInJournal.SpeedBerry;
        public bool SaveByDefaultWhenReturningToLobby { get; set; } = true;
    }
}
