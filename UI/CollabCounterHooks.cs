using System;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Maps.Processing;
using Celeste.Game.Content.Maps;
using Celeste.Game.Presentation.Hud.Counters;
using Celeste.Mods.CollabUtils2.Modules;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed class CollabCounterHooks : IDisposable
    {
        private static readonly FieldInfo ContentOwner = typeof(InstalledChapterMapLoadPolicy)
            .GetField("content", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly CollabModule module;
        private readonly ISourceEngineContext engine;
        private ILHook deaths;
        private ILHook berries;
        private Hook mapLoad;

        public CollabCounterHooks(CollabModule module, ISourceEngineContext engine)
        {
            this.module = module;
            this.engine = engine;
        }

        public void Load()
        {
            try
            {
                mapLoad = new Hook(
                    typeof(InstalledChapterMapLoadPolicy).GetMethod(nameof(InstalledChapterMapLoadPolicy.Load)),
                    (Action<Action<InstalledChapterMapLoadPolicy, OriginalChapterMapData>, InstalledChapterMapLoadPolicy
                        ,
                        OriginalChapterMapData>)((original, policy, map) =>
                    {
                        original(policy, map);
                        if (!ReferenceEquals(ContentOwner.GetValue(policy), engine)) return;
                        foreach (var level in map.Levels)
                        foreach (var entity in level.Entities)
                            if (entity.Name == "CollabUtils2/SilverBerry" || entity.Name == "CollabUtils2/RainbowBerry")
                                map.Goldenberries.Add(entity);
                    }));
                deaths = new ILHook(typeof(DeathsCounterState).GetMethod(nameof(DeathsCounterState.Render)),
                    PatchDeathSpacing);
                berries = new ILHook(
                    typeof(StrawberriesCounterState).GetMethod(nameof(StrawberriesCounterState.Render)),
                    PatchBerryIcon);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void PatchDeathSpacing(ILContext il)
        {
            var cursor = new ILCursor(il);
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(62f)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, DeathsCounterState, float>>((original, counter) =>
                    counter.Entity is CollabChapterPanelSequence panel && ReferenceEquals(panel.Owner, module.Panels)
                        ? panel.DeathIconSpacing
                        : original);
                matches++;
            }

            if (matches != 2)
                throw new InvalidOperationException("The two original death counter spacing reads are required.");
        }

        private void PatchBerryIcon(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdstr("collectables/goldberry")))
                throw new InvalidOperationException("The original golden berry icon read is required.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<string, StrawberriesCounterState, string>>((original, counter) =>
                counter.Entity is CollabChapterPanelSequence panel && ReferenceEquals(panel.Owner, module.Panels)
                    ? panel.GoldenBerryIcon
                    : original);
        }

        public void Dispose()
        {
            berries?.Dispose();
            berries = null;
            deaths?.Dispose();
            deaths = null;
            mapLoad?.Dispose();
            mapLoad = null;
        }
    }
}
