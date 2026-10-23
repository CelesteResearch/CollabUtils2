using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Game.Gameplay.Collectibles.Strawberries.Points;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.Entities
{
    public sealed class RainbowBerryHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private Hook collectHook;

        public RainbowBerryHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
        }

        public void Load()
        {
            collectHook = new Hook(
                typeof(OriginalStrawberryState).GetMethod("CollectRoutine",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                (Func<Func<OriginalStrawberryState, int, IEnumerator>, OriginalStrawberryState, int, IEnumerator>)
                CollectRoutine);
        }

        private IEnumerator CollectRoutine(Func<OriginalStrawberryState, int, IEnumerator> original,
            OriginalStrawberryState berry, int index)
        {
            var scene = berry.Scene as OriginalLevelSceneRoot;
            var routine = original(berry, index);
            while (routine.MoveNext()) yield return routine.Current;
            if (berry is RainbowBerry rainbow && ReferenceEquals(rainbow.Engine, engine))
            {
                var pending = scene.Scheduler.GetToAdd();
                var points = pending.OfType<OriginalStrawberryPointsState>().FirstOrDefault();
                if (points != null) pending.Remove(points);
                scene.Scheduler.QueueSourceEntity(new RainbowBerryPerfectEffect(engine, berry.Position));
            }
        }

        public void Dispose()
        {
            collectHook?.Dispose();
            collectHook = null;
        }
    }
}
