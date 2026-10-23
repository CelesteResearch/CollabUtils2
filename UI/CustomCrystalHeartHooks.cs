using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Updating;
using Celeste.Game.Entities.Collectibles.Hearts;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Hud.Poems;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.CollabUtils2.Maps;
using Celeste.Mods.CollabUtils2.UI.LobbyMaps;
using Celeste.Simulation.Presentation.Particles.Definitions;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI
{
    public sealed class CustomCrystalHeartHooks : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo HeartEngine = typeof(OriginalHeartGemState).GetField("engine", Fields);
        private static readonly FieldInfo HeartSprite = typeof(OriginalHeartGemState).GetField("sprite", Fields);

        private static readonly FieldInfo ShineParticle =
            typeof(OriginalHeartGemState).GetField("shineParticle", Fields);

        private static readonly PropertyInfo PoemColor = typeof(PoemState).GetProperty(nameof(PoemState.Color));
        private readonly ISourceEngineContext engine;
        private readonly CollabIdentityRegistry identities;
        private Hook awakeHook;
        private Hook poemHook;

        public CustomCrystalHeartHooks(ISourceEngineContext engine, CollabIdentityRegistry identities)
        {
            this.engine = engine;
            this.identities = identities;
        }

        public void Load()
        {
            awakeHook = new Hook(typeof(OriginalHeartGemState).GetMethod(nameof(OriginalHeartGemState.Awake)),
                (Action<Action<OriginalHeartGemState, object>, OriginalHeartGemState, object>)CustomizeParticles);
            poemHook = new Hook(typeof(PoemState).GetConstructor(new[]
                    { typeof(ISourceEngineContext), typeof(string), typeof(int), typeof(float) }),
                (Action<Action<PoemState, ISourceEngineContext, string, int, float>, PoemState,
                    ISourceEngineContext, string, int, float>)CustomizePoem);
        }

        private void CustomizeParticles(Action<OriginalHeartGemState, object> original,
            OriginalHeartGemState heart, object scene)
        {
            original(heart, scene);
            if (!ReferenceEquals(HeartEngine.GetValue(heart), engine) || heart.IsGhost) return;
            var level = (OriginalLevelSceneRoot)scene;
            var areas = ((IOriginalAreaKeyEnvironment)engine).AreaKeys;
            if (!identities.IsHeartSide(areas.GetSID(areas.GetSessionArea(level.Session)))) return;
            string path = ((Sprite)HeartSprite.GetValue(heart)).Texture.AtlasPath;
            var particles = ((IOriginalGameplayProcess)engine).ParticleTypes.HeartGem;
            ParticleTypePresentation shine;
            switch (path)
            {
                case "collectables/heartGem/1/00": shine = particles.P_RedShine; break;
                case "collectables/heartGem/2/00": shine = particles.P_GoldShine; break;
                case "CollabUtils2/crystalHeart/expert/00":
                    shine = new ParticleTypePresentation(particles.P_BlueShine) { Color = PackedColor.Orange };
                    break;
                case "CollabUtils2/crystalHeart/grandmaster/00":
                    shine = new ParticleTypePresentation(particles.P_BlueShine) { Color = PackedColor.DarkViolet };
                    break;
                default: return;
            }

            ShineParticle.SetValue(heart, shine);
        }

        private void CustomizePoem(Action<PoemState, ISourceEngineContext, string, int, float> original,
            PoemState poem, ISourceEngineContext owner, string text, int heartIndex, float heartAlpha)
        {
            original(poem, owner, text, heartIndex, heartAlpha);
            if (!ReferenceEquals(owner, engine)) return;
            var level = engine.Frame.Scenes.CurrentScene as OriginalLevelSceneRoot;
            if (level == null) return;
            var areas = ((IOriginalAreaKeyEnvironment)engine).AreaKeys;
            string sid = areas.GetSID(areas.GetSessionArea(level.Session));
            var registry = CollabHeartSpriteRegistry.For(engine);
            string id = "crystalHeart_" + EverestStringSource.DialogKeyify(sid);
            if (!registry.Has(id)) return;
            registry.CreateOn(poem.Heart, id);
            poem.Heart.Play("spin");
            PackedColor color;
            switch (poem.Heart.Texture.AtlasPath)
            {
                case "collectables/heartgem/1/spin00": color = SourceHexColor.HexToColor("ff668a"); break;
                case "collectables/heartgem/2/spin00": color = SourceHexColor.HexToColor("D2AD01"); break;
                case "CollabUtils2/crystalHeart/expert/spin00": color = PackedColor.Orange; break;
                case "CollabUtils2/crystalHeart/grandmaster/spin00": color = SourceHexColor.HexToColor("d9a2ff"); break;
                default:
                    var match = Regex.Match(poem.Heart.Texture.AtlasPath, "poemtextcolor_([0-9a-fA-F]{6})");
                    if (!match.Success) return;
                    color = SourceHexColor.HexToColor(match.Groups[1].Value);
                    break;
            }

            PoemColor.SetValue(poem, color);
        }

        public void Dispose()
        {
            poemHook?.Dispose();
            poemHook = null;
            awakeHook?.Dispose();
            awakeHook = null;
        }
    }
}
