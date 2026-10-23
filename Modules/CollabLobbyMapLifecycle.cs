using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Assets;
using Celeste.Everest.Content.Registry;
using Celeste.Mods.CollabUtils2.UI.LobbyMaps;
using MonoMod.ModInterop;

namespace Celeste.Mods.CollabUtils2.Modules
{
    internal sealed class CollabLobbyMapLifecycle : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly EverestContentRegistry content;

        public CollabLobbyMapLifecycle(ISourceEngineContext engine)
        {
            this.engine = engine;
            content = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry;
        }

        public void Load()
        {
            content.OnUpdate += OnModAssetUpdate;
            typeof(LobbyMapExports).ModInterop();
        }

        public void Dispose()
        {
            content.OnUpdate -= OnModAssetUpdate;
            LobbyMapCustomizationRegistry.Clear(engine);
            CollabHeartSpriteRegistry.Clear(engine);
        }

        private void OnModAssetUpdate(EverestContentAsset oldAsset, EverestContentAsset newAsset)
        {
            if (newAsset != null && newAsset.PathVirtual.StartsWith("Graphics/CollabUtils2/CrystalHeartSwaps_"))
                CollabHeartSpriteRegistry.For(engine).Reload();
        }
    }
}
