using Celeste.Mods.CollabUtils2.Runtime.Platforms.Conditional;

namespace Celeste.Mods.CollabUtils2.Modules
{
    public sealed partial class CollabModule
    {
        private SilverBlockConstructorHooks silverBlocks;

        private void LoadSilverBlocks()
        {
            silverBlocks ??= new SilverBlockConstructorHooks(SourceEngine);
            silverBlocks.Load();
        }

        private void UnloadSilverBlocks()
        {
            silverBlocks?.Dispose();
            silverBlocks = null;
        }
    }
}
