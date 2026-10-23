using System;
using System.Reflection;
using Celeste.Core.Reflection.Members;
using Celeste.Core.Scheduling;
using Celeste.Mods.Tas;
using Celeste.Game.Save.Runtime;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed class CollabTasLoadingHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private TasRuntimeEnvironment tas;
        private Hook loading;
        private Hook actuallyLoading;

        public CollabTasLoadingHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
        }

        public void Load()
        {
            tas = ((ISourceRuntimeExtensionEnvironment)engine).GetRuntimeExtension<TasRuntimeEnvironment>();
            if (tas == null) return;
            loading = new Hook(typeof(TasGameHost).GetMethod(nameof(TasGameHost.Loading)),
                (Func<Func<TasGameHost, bool>, TasGameHost, bool>)((original, host) =>
                    IsOwnedLobbyExit(host) || original(host)));
            actuallyLoading = new Hook(typeof(TasGameHost).GetMethod("IsActuallyLoadingScene",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                (Func<Func<TasGameHost, SourceMemberCatalog, bool>, TasGameHost, SourceMemberCatalog, bool>)
                ((original, host, members) => IsOwnedLobbyExit(host)
                    ? ((IOriginalUserIOEnvironment)engine).UserIO.Saving
                    : original(host, members)));
        }

        private bool IsOwnedLobbyExit(TasGameHost host)
        {
            return engine.Frame.Scenes.CurrentScene is LevelExitToLobbySequence &&
                   ReferenceEquals(host, tas.ActualTasDriver.GameHost);
        }

        public void Dispose()
        {
            actuallyLoading?.Dispose();
            actuallyLoading = null;
            loading?.Dispose();
            loading = null;
        }
    }
}
