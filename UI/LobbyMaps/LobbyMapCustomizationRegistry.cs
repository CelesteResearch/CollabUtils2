using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Scene.Entities;

namespace Celeste.Mods.CollabUtils2.UI.LobbyMaps
{
    internal sealed class LobbyMapCustomizationRegistry
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, LobbyMapCustomizationRegistry> Registries =
            new();

        public Dictionary<string, Action<SceneEntityState, List<SourceComponentState>>> RenderActions { get; } = new();

        public static LobbyMapCustomizationRegistry For(ISourceEngineContext engine) =>
            Registries.GetValue(engine, _ => new LobbyMapCustomizationRegistry());

        public static void Clear(ISourceEngineContext engine)
        {
            if (!Registries.TryGetValue(engine, out var registry)) return;
            registry.RenderActions.Clear();
            Registries.Remove(engine);
        }
    }
}
