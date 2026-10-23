// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Content.Maps;
using Celeste.Game.Execution.Session;
using Celeste.Simulation.Runtime.Session;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.Runtime.GoldenRespawns
{
    public sealed class GoldenBerryRespawnProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly IChapterSessionFactory factory;
        private Hook restartHook;

        public GoldenBerryRespawnProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            factory = ((IOriginalSessionEnvironment)engine).Sessions;
        }

        public void Load()
        {
            restartHook = new Hook(typeof(ChapterCompletionSession).GetMethod(nameof(ChapterCompletionSession.Restart)),
                (Func<Func<ChapterCompletionSession, string, ChapterCompletionSession>, ChapterCompletionSession, string
                    , ChapterCompletionSession>)Restart);
        }

        private ChapterCompletionSession Restart(Func<ChapterCompletionSession, string, ChapterCompletionSession> orig,
            ChapterCompletionSession session, string intoLevel)
        {
            var restarted = orig(session, intoLevel);
            if (session.IsCreatedBy(factory) && intoLevel != null
                                             && engine.Frame.Scenes.CurrentScene is LevelExitSequence
                                             {
                                                 Mode: LevelExitMode.GoldenBerryRestart
                                             })
            {
                var room = ((OriginalChapterMapData)restarted.MapData).Levels.Find(level => level.Name == intoLevel);
                var marker = room.Entities.FirstOrDefault(entity =>
                    entity.Name == "CollabUtils2/GoldenBerryPlayerRespawnPoint");
                if (marker != null)
                {
                    restarted.RespawnPoint = marker.Position + room.Position;
                    restarted.StartedFromBeginning = false;
                }
            }

            return restarted;
        }

        public void Dispose()
        {
            restartHook?.Dispose();
            restartHook = null;
        }
    }
}
