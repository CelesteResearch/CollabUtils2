using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Scene.Entities;

namespace Celeste.Mods.CollabUtils2.Entities.LobbyMaps
{
    // Intentionally passive upstream marker. The real map UI reads its original map EntityData.
    [CustomEntity("CollabUtils2/LobbyMapMarker")]
    public class LobbyMapMarker : SceneEntityState
    {
        public LobbyMapMarker(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            Active = Visible = false;
        }
    }
}
