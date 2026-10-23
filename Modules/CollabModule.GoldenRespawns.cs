using Celeste.Mods.CollabUtils2.Runtime.GoldenRespawns;

namespace Celeste.Mods.CollabUtils2.Modules
{
    public sealed partial class CollabModule
    {
        private GoldenBerryRespawnProcess goldenRespawns;

        private void LoadGoldenRespawns()
        {
            goldenRespawns ??= new GoldenBerryRespawnProcess(SourceEngine);
            goldenRespawns.Load();
        }

        private void UnloadGoldenRespawns() => goldenRespawns?.Dispose();
    }
}
