using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Walls.Conditional;
using Celeste.Mods.CollabUtils2.Entities.Platforms.Conditional;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.Runtime.Platforms.Conditional
{
    public sealed class SilverBlockConstructorHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private ILHook constructorHook;

        public SilverBlockConstructorHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
        }

        public void Load()
        {
            constructorHook =
                new ILHook(
                    typeof(GoldenBlockState).GetConstructor(new[]
                        { typeof(Vector2), typeof(float), typeof(float), typeof(ISourceSolidWorld) }),
                    PatchConstructor);
        }

        private void PatchConstructor(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdstr("collectables/goldberry/idle00"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<string, GoldenBlockState, string>>(SelectBerryTexture);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdstr("objects/goldblock"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<string, GoldenBlockState, string>>(SelectBlockTexture);
        }

        private string SelectBerryTexture(string original, GoldenBlockState block)
            => block is SilverBlock silver && ReferenceEquals(silver.EngineContext, engine)
                ? "CollabUtils2/silverBerry/idle00"
                : original;

        private string SelectBlockTexture(string original, GoldenBlockState block)
            => block is SilverBlock silver && ReferenceEquals(silver.EngineContext, engine)
                ? "CollabUtils2/silverblock"
                : original;

        public void Dispose()
        {
            constructorHook?.Dispose();
            constructorHook = null;
        }
    }
}
