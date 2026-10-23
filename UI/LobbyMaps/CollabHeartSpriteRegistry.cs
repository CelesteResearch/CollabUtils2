using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Content.Types.Text;
using Celeste.Everest.Modules.Updating;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Graphics;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;

namespace Celeste.Mods.CollabUtils2.UI.LobbyMaps
{
    public sealed class CollabHeartSpriteRegistry
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, CollabHeartSpriteRegistry>
            Registries = new();

        private readonly ISourceEngineContext engine;
        private readonly Dictionary<string, Sprite> templates = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> overrides = new();

        public static CollabHeartSpriteRegistry For(ISourceEngineContext engine) =>
            Registries.GetValue(engine, scope => new CollabHeartSpriteRegistry(scope));

        public static void Clear(ISourceEngineContext engine)
        {
            if (!Registries.TryGetValue(engine, out var registry)) return;
            registry.templates.Clear();
            registry.overrides.Clear();
            Registries.Remove(engine);
        }

        private CollabHeartSpriteRegistry(ISourceEngineContext engine)
        {
            this.engine = engine;
            Reload();
        }

        public void Reload()
        {
            templates.Clear();
            var registry = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry;
            var graphics = (OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics;
            foreach (var pair in registry.Map)
            {
                if (pair.Value.Type != typeof(AssetTypeXml) ||
                    !pair.Key.StartsWith("Graphics/CollabUtils2/CrystalHeartSwaps_", StringComparison.Ordinal))
                    continue;
                var xml = new XmlDocument();
                using (var stream = pair.Value.Stream) xml.Load(stream);
                var bank = new SpriteBank(engine, graphics.GuiAtlas, xml);
                foreach (var sprite in bank.SpriteData)
                    templates[sprite.Key] = sprite.Value.Sprite;
            }
        }

        public string GetGuiHeartSpriteId(string mapSID, int side)
        {
            string key = SideName(mapSID, side);
            string levelSet = ((IOriginalAreaKeyEnvironment)engine).AreaKeys.GetAreaBySID(mapSID)?.LevelSet;
            if (overrides.TryGetValue(key, out string id) && templates.ContainsKey(id)) return id;
            id = "crystalHeart_" + key;
            if (templates.ContainsKey(id)) return id;
            id = "crystalHeart_" + (levelSet == null ? null : EverestStringSource.DialogKeyify(levelSet));
            return templates.ContainsKey(id) ? id : null;
        }

        public Sprite Create(string id) => templates[id].CreateClone();

        public bool Has(string id) => templates.ContainsKey(id);

        public void CreateOn(Sprite sprite, string id) => templates[id].CloneInto(sprite);

        public void AddOverrideHeartSpriteID(string mapSID, int side, string spriteID) =>
            overrides[SideName(mapSID, side)] = spriteID;

        public void RemoveOverrideHeartSpriteID(string mapSID, int side) => overrides.Remove(SideName(mapSID, side));

        private static string SideName(string sid, int side)
        {
            string name = EverestStringSource.DialogKeyify(sid);
            return side == 1 ? name + "_B" : side == 2 ? name + "_C" : name;
        }
    }
}
