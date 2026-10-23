using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Everest;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Execution.Levels.Construction;
using Celeste.Game.Execution.Threading;
using Celeste.Game.Save.Runtime;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Simulation.Runtime.Session;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI.Journal.Stickers
{
    public sealed class CollabJournalStickerProcess : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, CollabJournalStickerProcess> Processes =
            new();

        private readonly ISourceEngineContext engine;
        private readonly Dictionary<string, AtlasEntry> textures = new();
        private Hook loaderHook;
        private EverestContentRegistry Content => ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry;
        private IOriginalAreaKeyPolicy Areas => ((IOriginalAreaKeyEnvironment)engine).AreaKeys;

        public static CollabJournalStickerProcess For(CollabModule module) =>
            Processes.GetValue(module.Panels.Engine, scope => new CollabJournalStickerProcess(scope));

        private CollabJournalStickerProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
        }

        private delegate void LevelLoaderConstructor(OriginalLevelLoaderScene self, ISourceEngineContext sourceEngine,
            ChapterCompletionSession session, OriginalLevelSceneConstruction construction,
            OriginalLevelLoadingResources resources, IOriginalAreaDataResolver areas, OriginalGameThreadSession threads,
            IInstalledLevelLoaderPolicy installedPolicy, Vector2? startPosition);

        public void Load()
        {
            var constructor = typeof(OriginalLevelLoaderScene).GetConstructor(new[]
            {
                typeof(ISourceEngineContext), typeof(ChapterCompletionSession), typeof(OriginalLevelSceneConstruction),
                typeof(OriginalLevelLoadingResources), typeof(IOriginalAreaDataResolver),
                typeof(OriginalGameThreadSession),
                typeof(IInstalledLevelLoaderPolicy), typeof(Vector2?)
            });
            loaderHook = new Hook(constructor, (Action<LevelLoaderConstructor, OriginalLevelLoaderScene,
                ISourceEngineContext, ChapterCompletionSession, OriginalLevelSceneConstruction,
                OriginalLevelLoadingResources, IOriginalAreaDataResolver, OriginalGameThreadSession,
                IInstalledLevelLoaderPolicy, Vector2?>)OnLevelLoad);
        }

        private void OnLevelLoad(LevelLoaderConstructor orig, OriginalLevelLoaderScene self,
            ISourceEngineContext sourceEngine,
            ChapterCompletionSession session, OriginalLevelSceneConstruction construction,
            OriginalLevelLoadingResources resources, IOriginalAreaDataResolver areas, OriginalGameThreadSession threads,
            IInstalledLevelLoaderPolicy installedPolicy, Vector2? startPosition)
        {
            if (ReferenceEquals(engine, sourceEngine)) Reload(session);
            orig(self, sourceEngine, session, construction, resources, areas, threads, installedPolicy, startPosition);
        }

        public void Reload(ChapterCompletionSession session)
        {
            UnloadTextures();
            if (Content.Map.TryGetValue("Maps/" + Areas.GetSID(Areas.GetSessionArea(session)), out var asset) &&
                asset.TryGetMeta(out JournalStickerMetadata meta) && meta != null)
            {
                foreach (var sticker in meta.Stickers)
                {
                    if (!textures.ContainsKey(sticker.Path) && IsUnlocked(sticker))
                    {
                        Log("Loading sticker " + sticker.Path);
                        textures[sticker.Path] = new AtlasEntry(((ISourceVirtualContentEnvironment)engine)
                            .VirtualContent
                            .CreateTexture("Graphics/Atlases/Stickers/" + sticker.Path));
                    }
                }
            }
        }

        public List<JournalSticker> SelectForSession(ChapterCompletionSession session)
        {
            var stickers = new List<JournalSticker>();
            if (Content.Map.TryGetValue("Maps/" + Areas.GetSID(Areas.GetSessionArea(session)), out var asset) &&
                asset.TryGetMeta(out JournalStickerMetadata meta))
                foreach (var sticker in meta.Stickers)
                    if (textures.ContainsKey(sticker.Path) && IsUnlocked(sticker))
                        stickers.Add(sticker);
            return stickers;
        }

        public AtlasEntry GetTexture(string path) => textures[path];

        private bool IsUnlocked(JournalSticker sticker)
        {
            var save = ((Celeste.Game.Execution.Processes.IOriginalGameplayProcess)engine).SaveContext.CurrentSave;
            var counters = ((IOriginalSaveCounterEnvironment)engine).SaveCounters;
            var areaStats = ((IOriginalAreaStatsEnvironment)engine).AreaStats;
            return sticker.FinishedMaps.All(map => Areas.GetAreaBySID(map) != null &&
                                                   counters.GetAreasSafe(save).First(stats =>
                                                           areaStats.GetAreaStatsId(stats) ==
                                                           Areas.GetAreaBySID(map).ID)
                                                       .Modes[0].Completed);
        }

        private void Log(string text) => ((IEverestLoggerProcessEnvironment)engine).Logger.Log(EverestLogLevel.Info,
            "CollabUtils2/OuiJournalCoverWithStickers", text);

        private void UnloadTextures()
        {
            foreach (var texture in textures)
            {
                Log("Unloading sticker " + texture.Key);
                texture.Value.Unload();
            }

            textures.Clear();
        }

        public void Dispose()
        {
            loaderHook?.Dispose();
            loaderHook = null;
            UnloadTextures();
            Processes.Remove(engine);
        }
    }
}
