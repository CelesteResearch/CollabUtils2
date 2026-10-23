using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Scene.Entities;

namespace Celeste.Mods.CollabUtils2.Entities
{
    public sealed class RainbowBerryPerfectEffect : SceneEntityState
    {
        public RainbowBerryPerfectEffect(ISourceEngineContext engine, Vector2 position)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            Depth = -10000000;
            var sprite =
                ((IOriginalStrawberryProcess)engine).GameGraphics.SpriteFactory.Create("CollabUtils2_perfectAnimation");
            Add(sprite);
            sprite.OnLastFrame = _ => RemoveSelf();
            sprite.Play("perfect");
        }
    }
}
