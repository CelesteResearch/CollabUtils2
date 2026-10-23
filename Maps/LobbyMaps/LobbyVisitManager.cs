using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Everest;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Mods.CollabUtils2.Modules;
using Celeste.Mods.CollabUtils2.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Celeste.Mods.CollabUtils2.Maps.LobbyMaps
{
    public class LobbyVisitManager
    {
        /// <summary>
        /// Each of the points the player has visited and thus generated circular snapshots.
        /// These points are in 8x8 pixel tile offsets from the top left of the map.
        /// </summary>
        public List<LobbyVisitedPoint> VisitedPoints { get; } = new List<LobbyVisitedPoint>();

        public List<string> ActivatedWarps { get; } = new List<string>();

        public bool VisitedAll { get; private set; }

        public const int EXPLORATION_RADIUS = 20;
        private const ushort currentVersion = 1;

        public string SID { get; }
        public string Room { get; }

        private static string GetKey(string sid, string room = null) =>
            string.IsNullOrWhiteSpace(room) ? sid : $"{sid}.{room}";

        public string Key => GetKey(SID, Room);

        public bool MatchesKey(string sid, string room = null) => Key == GetKey(sid, room);

        private readonly CollabSaveData saveData;
        private readonly ISourceVectorArithmetic arithmetic;
        private readonly Action<EverestLogLevel, string> log;

        public LobbyVisitManager(CollabModule module, string sid, string room = null)
            : this(module.SaveData, ((ISourceVectorEnvironment)module.Panels.Engine).VectorArithmetic,
                (level, message) =>
                    ((IEverestLoggerProcessEnvironment)module.Panels.Engine).Logger.Log(level,
                        "CollabUtils2/LobbyVisitManager", message), sid, room)
        {
        }

        public LobbyVisitManager(CollabSaveData saveData, ISourceVectorArithmetic arithmetic,
            Action<EverestLogLevel, string> log, string sid, string room = null)
        {
            this.saveData = saveData;
            this.arithmetic = arithmetic;
            this.log = log;
            SID = sid;
            Room = room;
            Load();
        }

        public void VisitAll(bool shouldSave = true)
        {
            VisitedPoints.Clear();
            VisitedAll = true;
            if (shouldSave)
            {
                Save();
            }
        }

        public void Reset(bool shouldSave = true)
        {
            VisitedPoints.Clear();
            ActivatedWarps.Clear();
            VisitedAll = false;

            if (shouldSave)
            {
                Save();
            }
        }

        public void Save()
        {
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    // write version number
                    writer.Write(currentVersion);

                    // write whether we've visited everything
                    writer.Write(VisitedAll);

                    // write points if we haven't visited them all
                    if (!VisitedAll)
                    {
                        writer.Write((uint)VisitedPoints.Count);
                        foreach (var v in VisitedPoints)
                        {
                            writer.Write((short)v.Point.X);
                            writer.Write((short)v.Point.Y);
                        }
                    }

                    // write activated warps
                    writer.Write((uint)ActivatedWarps.Count);
                    foreach (var w in ActivatedWarps)
                    {
                        writer.Write(w);
                    }

                    // store it
                    saveData.VisitedLobbyPositions[Key] = Convert.ToBase64String(stream.ToArray());
                }
            }
            catch (Exception)
            {
                log(EverestLogLevel.Error, "Save: Error trying to serialise visited points.");
            }
        }

        private void Load()
        {
            VisitedPoints.Clear();
            ActivatedWarps.Clear();
            VisitedAll = false;

            if (!saveData.VisitedLobbyPositions.TryGetValue(Key, out var value))
            {
                return;
            }

            try
            {
                var visitedAll = false;
                var visitedPoints = new List<LobbyVisitedPoint>();
                var activatedWarps = new List<string>();
                var bytes = Convert.FromBase64String(value);

                using (var stream = new MemoryStream(bytes))
                using (var reader = new BinaryReader(stream))
                {
                    // check a version number so we can clear out bad data later
                    var version = reader.ReadUInt16();
                    if (version != currentVersion)
                    {
                        log(EverestLogLevel.Warn, "Load: Wrong version found, clearing stored data instead.");
                        saveData.VisitedLobbyPositions.Remove(Key);
                        return;
                    }

                    // read whether we've visited everything
                    visitedAll = reader.ReadBoolean();

                    // if we haven't visited everything, read all the points
                    if (!visitedAll)
                    {
                        var visitedCount = reader.ReadUInt32();
                        for (int i = 0; i < visitedCount; i++)
                        {
                            var x = reader.ReadInt16();
                            var y = reader.ReadInt16();
                            visitedPoints.Add(new LobbyVisitedPoint(new Vector2(x, y)));
                        }
                    }

                    // read all the activated warps
                    var activatedCount = reader.ReadUInt32();
                    for (int i = 0; i < activatedCount; i++)
                    {
                        var warpId = reader.ReadString();
                        activatedWarps.Add(warpId);
                    }
                }

                VisitedAll = visitedAll;
                VisitedPoints.AddRange(visitedPoints);
                ActivatedWarps.AddRange(activatedWarps);
            }
            catch (Exception)
            {
                log(EverestLogLevel.Error,
                    "Load: Error trying to deserialise visited points, clearing stored data instead.");
                saveData.VisitedLobbyPositions.Remove(Key);
            }
        }

        public void ActivateWarp(string id, bool shouldSave = true)
        {
            if (ActivatedWarps.Contains(id)) return;

            ActivatedWarps.Add(id);

            if (shouldSave)
            {
                Save();
            }
        }

        public void VisitPoint(Vector2 point, bool shouldSave = true)
        {
            const float threshold = EXPLORATION_RADIUS / 2f;

            // don't need to do anything if we've visited everywhere
            if (VisitedAll) return;

            var shouldGenerate = !VisitedPoints.Any();
            var shouldSort = !shouldGenerate &&
                             (point - VisitedPoints.First().Point).LengthSquared() > threshold * threshold;

            if (shouldSort)
            {
                // update distances and sort
                foreach (var vp in VisitedPoints)
                {
                    vp.DistanceSquared = arithmetic.LengthSquared(vp.Point - point);
                }

                VisitedPoints.Sort((a, b) => Math.Sign(a.DistanceSquared - b.DistanceSquared));
                // check whether we should generate after the sort
                shouldGenerate = VisitedPoints.First().DistanceSquared > threshold * threshold;
            }

            if (shouldGenerate)
            {
                VisitedPoints.Insert(0, new LobbyVisitedPoint(point, 0f));
                if (shouldSave)
                {
                    Save();
                }
            }
        }
    }
}
