using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using static UmaParser.DataModel.RaceScenario.RaceScenarioData;

namespace UmaParser.DataModel.RaceScenario
{
    public static class RaceScenarioParser
    {
        /// <summary>
        /// Helper for decoding the race_scenario string found in all race result responses, into a byte array.
        /// </summary>
        /// <param name="base64Gzip">race_scenario value from any race response.</param>
        /// <returns>Decoded byte array representing the race scenario.</returns>
        public static byte[] Decode(string base64Gzip)
        {
            if (string.IsNullOrEmpty(base64Gzip))
                return Array.Empty<byte>();

            // 1. Base64 → bytes
            byte[] compressed = Convert.FromBase64String(base64Gzip);

            // 2. GZip decompress
            using var ms = new MemoryStream(compressed);
            using var gzip = new GZipStream(ms, CompressionMode.Decompress);
            using var result = new MemoryStream();
            gzip.CopyTo(result);
            return result.ToArray();
        }

        public static RaceScenarioData Parse(byte[] raw)
        {
            if (raw == null || raw.Length == 0)
                return new RaceScenarioData();

            using var ms = new MemoryStream(raw);
            using var br = new BinaryReader(ms);

            var data = new RaceScenarioData();

            // Header
            data.Header.MaxLength = br.ReadInt32();
            data.Header.Version = br.ReadInt32();

            data.DistanceDiffMax = br.ReadSingle();
            data.HorseNum = br.ReadInt32();
            data.HorseFrameSize = br.ReadInt32();
            data.HorseResultSize = br.ReadInt32();

            data.PaddingSize1 = br.ReadInt32();   // __padding_size_1

            data.FrameCount = br.ReadInt32();
            data.FrameSize = br.ReadInt32();

            // Frames
            for (int f = 0; f < data.FrameCount; f++)
            {
                data.Frames.Add(ReadFrame(br, data.HorseNum));
            }

            data.PaddingSize2 = br.ReadInt32();   // __padding_size_2

            // Horse results            
            for (int r = 0; r < data.HorseNum; r++)
            {
                data.HorseResults.Add(ReadHorseResult(br));
            }

            data.PaddingSize3 = br.ReadInt32();   // __padding_size_3

            // Events
            data.EventCount = br.ReadInt32();
            for (int e = 0; e < data.EventCount; e++)
            {
                int eventSize = br.ReadInt16();           // event_size (int16)
                data.Events.Add(ReadEvent(br));
            }

            // Any remaining data (future-proofing?)
            if (ms.Position < ms.Length)
            {
                int remaining = (int)(ms.Length - ms.Position);
                data.TrailingData = br.ReadBytes(remaining);
            }

            return data;
        }

        public static RaceScenarioData Parse(string base64Gzip)
        {
            var raw = Decode(base64Gzip);
            return Parse(raw);
        }

        private static RaceSimulateFrameData ReadFrame(BinaryReader br, int horseNum)
        {
            var frame = new RaceSimulateFrameData
            {
                Time = br.ReadSingle()
            };

            // Horse frames inside this frame
            for (int h = 0; h < horseNum; h++) // adjust based on exact layout
            {
                frame.HorseFrames.Add(ReadHorseFrame(br));
            }

            return frame;
        }

        private static RaceSimulateHorseFrameData ReadHorseFrame(BinaryReader br)
        {
            return new RaceSimulateHorseFrameData
            {                                                       // offset
                Distance = br.ReadSingle(),                         // 00
                LanePosition = br.ReadUInt16(),                     // 04
                Speed = br.ReadUInt16(),                            // 08
                Hp = br.ReadUInt16(),                               // 10
                TemptationMode = (TemptationMode)br.ReadSByte(),    // 11
                BlockFrontHorseIndex = br.ReadSByte()               // 12
            };
        }

        private static RaceSimulateHorseResultData ReadHorseResult(BinaryReader br)
        {
            return new RaceSimulateHorseResultData
            {
                FinishOrder = br.ReadInt32(),
                FinishTime = br.ReadSingle(),
                FinishDiffTime = br.ReadSingle(),
                StartDelayTime = br.ReadSingle(),
                GutsOrder = br.ReadByte(),
                WizOrder = br.ReadByte(),
                LastSpurtStartDistance = br.ReadSingle(),
                RunningStyle = (RunningStyle)br.ReadByte(),
                Defeat = br.ReadInt32(),
                FinishTimeRaw = br.ReadSingle()
            };
        }

        private static RaceSimulateEventData ReadEvent(BinaryReader br)
        {
            float frameTime = br.ReadSingle();
            var type = (SimulateEventType)br.ReadByte();
            byte paramCount = br.ReadByte();

            var ev = type switch
            {
                SimulateEventType.Skill => new SkillSimulateEvent(),
                SimulateEventType.Score => new ScoreSimulateEvent(),
                // Add more specific subclasses here as their Params layouts are reverse-engineered.
                _ => new RaceSimulateEventData()
            };

            ev.FrameTime = frameTime;
            ev.Type = type;
            ev.ParamCount = paramCount;

            for (int p = 0; p < paramCount; p++)
            {
                ev.Params.Add(br.ReadInt32());
            }

            return ev;
        }
    }
}
