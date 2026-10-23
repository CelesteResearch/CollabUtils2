using System;
using System.Linq;
using System.Numerics;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Save.Runtime;
using Celeste.Mods.CollabUtils2.Modules;

namespace Celeste.Mods.CollabUtils2.Maps.LobbyMaps
{
    public enum LobbyMapMarkerType
    {
        Warp,
        RainbowBerry,
        HeartGate,
        Gym,
        Map,
        Journal,
        HeartSide,
        Custom,
    }
}
