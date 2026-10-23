// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Scene.Entities;

namespace Celeste.Mods.CollabUtils2.Entities.GoldenRespawns
{
    [CustomEntity("CollabUtils2/GoldenBerryPlayerRespawnPoint")]
    [RuntimeTypeAlias("Celeste.Mod.CollabUtils2.Entities.GoldenBerryPlayerRespawnPoint")]
    public sealed class GoldenBerryPlayerRespawnPoint : SceneEntityState
    {
        public GoldenBerryPlayerRespawnPoint(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            RemoveSelf();
        }
    }
}
