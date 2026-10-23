using System.Numerics;

namespace Celeste.Mods.CollabUtils2.Maps.LobbyMaps
{
    public class LobbyVisitedPoint
    {
        public Vector2 Point;
        public float DistanceSquared;

        public LobbyVisitedPoint(Vector2 point, float distanceSquared = float.MaxValue)
        {
            Point = point;
            DistanceSquared = distanceSquared;
        }
    }
}
