using System;
using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Everest.Menus.Overworld;
using Celeste.Game.Menus.Overworld;
using MonoMod.RuntimeDetour;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed class CollabChapterPanelMenuRegistration : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly Hook hook;
        private readonly IDisposable actualTypeLease;
        private readonly Type standard;

        public CollabChapterPanelMenuRegistration(ISourceEngineContext engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            var catalog = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog;
            standard = ((IInstalledOverworldUITypeEnvironment)engine).ChapterPanelUIType;
            var map = engine.GetType().GetInterfaceMap(typeof(IOriginalMenuAssemblyEnvironment));
            var contract =
                typeof(IOriginalMenuAssemblyEnvironment).GetMethod(
                    nameof(IOriginalMenuAssemblyEnvironment.GetMenuTypes));
            int index = Array.IndexOf(map.InterfaceMethods, contract);
            if (index < 0) throw new MissingMethodException("The original menu assembly contract is not implemented.");
            hook = new Hook(map.TargetMethods[index],
                (Func<Func<ISourceEngineContext, Type[]>, ISourceEngineContext, Type[]>)SelectChapterPanel);
            try
            {
                var definition = catalog.Find(standard);
                if (definition.Identity != "Celeste.OuiChapterPanel" ||
                    !standard.IsAssignableFrom(typeof(CollabChapterPanelSequence)))
                    throw new InvalidOperationException(
                        "The Collab panel must extend the actual original chapter panel.");
                actualTypeLease =
                    catalog.RegisterScopedLogicalActualType(typeof(CollabChapterPanelSequence), definition.Identity);
            }
            catch
            {
                hook.Dispose();
                throw;
            }
        }

        private Type[] SelectChapterPanel(Func<ISourceEngineContext, Type[]> original, ISourceEngineContext owner)
        {
            Type[] types = original(owner);
            if (!ReferenceEquals(owner, engine)) return types;
            var selected = new List<Type>(types.Length);
            bool added = false;
            foreach (Type type in types)
            {
                if (type == standard || type == typeof(CollabChapterPanelSequence))
                {
                    if (!added)
                    {
                        selected.Add(typeof(CollabChapterPanelSequence));
                        added = true;
                    }
                }
                else selected.Add(type);
            }

            return selected.ToArray();
        }

        public void Dispose()
        {
            try
            {
                actualTypeLease.Dispose();
            }
            finally
            {
                hook.Dispose();
            }
        }
    }
}
