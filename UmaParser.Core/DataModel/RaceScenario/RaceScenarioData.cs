using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using static UmaParser.DataModel.RaceScenario.RaceScenarioData;

namespace UmaParser.DataModel.RaceScenario
{
#region Enums

    /// <summary>
    /// Running style
    /// </summary>
    public enum RunningStyle : byte
    {
        Unknown = 0,
        Front = 1,
        Pace = 2,
        Late = 3,
        End = 4
    }

    /// <summary>
    /// Temptation / positioning mode during the race.
    /// </summary>
    public enum TemptationMode : sbyte
    {
        Null = 0,
        PositionLate = 1,
        PositionPace = 2,
        PositionFront = 3,
        Boost = 4
    }

    /// <summary>
    /// Type of race event
    /// </summary>
    public enum SimulateEventType : byte
    {
        Score = 0,
        ChallengeMatchPoint = 1,
        NoUse = 2,
        Skill = 3,
        CompeteTop = 4,
        CompeteFight = 5,
        ReleaseConservePower = 6,
        StaminaLimitBreakBuff = 7,
        CompeteBeforeSpurt = 8,
        StaminaKeep = 9,
        SecureLead = 10
    }
#endregion

    public class RaceScenarioData
    {
        public RaceSimulateHeaderData Header { get; set; } = new();

        /// <summary>
        /// (maybe?) The maximum distance difference between the first and last place horse in any single frame this race
        /// </summary>
        public float DistanceDiffMax { get; set; }

        /// <summary>
        /// Count of horses in the race.  Important for parsing the size of other arrays.
        /// </summary>
        public int HorseNum { get; set; }

        /// <summary>
        /// Size in bytes of each HorseFrame entry in a frame.  Currently always 12
        /// </summary>
        public int HorseFrameSize { get; set; }

        /// <summary>
        /// Size in bytes of each HorseResult entry.  Currently always 31
        /// </summary>
        public int HorseResultSize { get; set; }

        /// <summary>
        /// Number of simulation frame objects
        /// </summary>
        public int FrameCount { get; set; }

        /// <summary>
        /// Size in bytes of each frame object.  Should be 4 (for time) + (HorseNum * HorseFrameSize)
        /// </summary>
        public int FrameSize { get; set; }

        /// <summary>
        /// Array of simulation frames
        /// </summary>
        public List<RaceSimulateFrameData> Frames { get; set; } = new();

        /// <summary>
        /// Final result objects for each horse.  The order of this list is the gate order of the horses, not the finish order.
        /// </summary>
        public List<RaceSimulateHorseResultData> HorseResults { get; set; } = new();

        /// <summary>
        /// Count of events in the Events array.
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        /// Array of events that occurred during the race.
        /// The concrete type of each element may be a more specific subclass (e.g. <see cref="SkillSimulateEvent"/>)
        /// depending on <see cref="RaceSimulateEventData.Type"/>.
        /// </summary>
        public List<RaceSimulateEventData> Events { get; set; } = new();

        /// <summary>
        /// Strongly-typed view over all <see cref="SimulateEventType.Skill"/> events in this race.
        /// Use this (or LINQ <c>OfType&lt;SkillSimulateEvent&gt;()</c>) instead of manually filtering by Type + indexing Params.
        /// </summary>
        public IEnumerable<SkillSimulateEvent> SkillEvents => Events.OfType<SkillSimulateEvent>();

        /// <summary>
        /// Strongly-typed view over all <see cref="SimulateEventType.Score"/> events in this race.
        /// </summary>
        public IEnumerable<ScoreSimulateEvent> ScoreEvents => Events.OfType<ScoreSimulateEvent>();

        // Unknown/padding regions
        public int PaddingSize1 { get; set; }
        public int PaddingSize2 { get; set; }
        public int PaddingSize3 { get; set; }

        // Optional raw bytes for any trailing/unknown data
        public byte[]? TrailingData { get; set; }

        /// <summary>
        /// General tool for quickly dumping result data, changes frequently.
        /// </summary>
        /// <returns></returns>
        public string GetSummary()
        {
            var sb = new System.Text.StringBuilder();
            
            // top level info
            sb.AppendLine($"Race Scenario - {HorseNum} horses, {FrameCount} frames, {EventCount} events");

            // results summary in gate order
            if (HorseResults.Count > 0)
            {
                sb.AppendLine("Horses by gate:");
                int gate = 1;
                foreach (var r in HorseResults)
                {
                    sb.AppendLine($"[{gate}]  #{r.FinishOrder + 1} - Time: {r.FinishTime:F3}s - Style: {r.RunningStyle}");
                    gate++;
                }
            }

            // Events
            sb.AppendLine($"{Events.Count()} total events:");
            foreach (var e in Events)
            {
                sb.AppendLine($"  T:{e.FrameTime:F3}s  Type:{e.Type}  Params:[{string.Join(", ", e.Params)}]");
            }

            //// All frames for 1 horse
            //sb.AppendLine("Frame Data:");
            //float time = 0f;
            //int count = 0;
            //float lastDistance = 0f;
            //float lastSpeed = 0f;
            //foreach (var f in Frames)
            //{
            //    float dt = f.Time - time;
            //    var distance = f.HorseFrames[0].Distance;
            //    var dd = distance - lastDistance;
            //    var lane = f.HorseFrames[0].LanePosition;
            //    var speed = f.HorseFrames[0].Speed;
            //    var ds = speed - lastSpeed;
            //    var hp = f.HorseFrames[0].Hp;
            //    var mode = f.HorseFrames[0].TemptationMode;
            //    var block = f.HorseFrames[0].BlockFrontHorseIndex;
            //    sb.AppendLine($"[{count}  T:{f.Time:F3}s (+{dt:F3}s) dist:{distance}m ({dd:F3}m) lane:{lane} sp:{speed} ({ds}) hp:{hp} mode:{mode} bl:{block}");
            //    count++;
            //    time = f.Time;
            //    lastDistance = distance;
            //    lastSpeed = speed;
            //}

            return sb.ToString();
        }
    }

    public class RaceSimulateHeaderData
    {
        public int MaxLength { get; set; }
        public int Version { get; set; }
    }

    /// <summary>
    /// Data for one single frame of the race simulation replay.
    /// </summary>
    public class RaceSimulateFrameData
    {
        /// <summary>
        /// Timestamp of this frame, assumed to be seconds.
        /// The first frame is 0.  Subsequent frames can have varying intervals.
        /// Unclear if it is based on distance or time, but the first 17-18 frames are 1 60hz tick (0.067s) apart.
        /// After that, tick rate goes up to 1 second + 1 frame (1.067s) until near the end, where the last ~40ish frames
        /// are back to 60hz again.
        /// There is sometimes a 1.067s frame within that last part, it is not clear why that happens.
        /// </summary>
        public float Time { get; set; }

        /// <summary>
        /// Data for individual horses for this frame.  The order is the gate order of the horses.
        /// </summary>
        public List<RaceSimulateHorseFrameData> HorseFrames { get; set; } = new();
    }

    /// <summary>
    /// Data for one horse in one simulation frame.
    /// </summary>
    public class RaceSimulateHorseFrameData
    {
        /// <summary>
        /// Total distance covered by the horse from the starting line.  Assumed to be meters.
        /// </summary>
        public float Distance { get; set; }

        /// <summary>
        /// Lane position of the horse.  Units are unknown.
        /// Each "lane" seems to be ~555.5 units apart, with gate 1 (most inside) being 0, then 555, 1111, 1666 etc.
        /// During the race, horses can move between lanes and seem to keep this same spacing except during transitions.
        /// </summary>
        public ushort LanePosition { get; set; }

        /// <summary>
        /// Speed of the horse in this frame.  Units are unknown but they are somewhat close to (but not exactly) cm/s.
        /// In the first frame of the race (and the second if they got a late start) speed will be 300, but distance travelled will stay 0 until the delay is over.
        /// </summary>
        public ushort Speed { get; set; }

        /// <summary>
        /// Remaining stamina of the horse.
        /// </summary>
        public ushort Hp { get; set; }

        /// <summary>
        /// (not clear yet) might be position-keeping or rushed status.
        /// </summary>
        public TemptationMode TemptationMode { get; set; }

        /// <summary>
        /// (probably) index (based on gate order) of the horse blocking this one in this frame, or -1 if none.
        /// </summary>
        public sbyte BlockFrontHorseIndex { get; set; }   // -1 = none
    }

    /// <summary>
    /// Data for the final results of one horse.
    /// </summary>
    public class RaceSimulateHorseResultData
    {
        /// <summary>
        /// Race finish order (0-based)
        /// </summary>
        public int FinishOrder { get; set; }

        /// <summary>
        /// Finish time (in seconds) for this horse.  This value is accurate, but the game may display a different value in UI.
        /// </summary>
        public float FinishTime { get; set; }

        /// <summary>
        /// Difference in seconds between this horse and the one who finished immediately before it.
        /// For the winner this will be 0.  This is likely used for calculating the "lengths" shown in UI.
        /// </summary>
        public float FinishDiffTime { get; set; }

        /// <summary>
        /// Delay time (in seconds) before the horse started moving at the beginning of the race.
        /// This starts as a totally random number between 0.0 and 0.100 seconds, not affected by wit.
        /// Skills like Focus, Concentration, and Gatekept are multiplied on this value.
        /// A delay of 0.080 seconds or more shows as a "Late Start" in-game, however any delay greater than
        /// one tick (0.067s) results in the horse losing an entire frame of accelleration, which has a huge effect.
        /// </summary>
        public float StartDelayTime { get; set; }

        /// <summary>
        /// Unknown
        /// </summary>
        public byte GutsOrder { get; set; }

        /// <summary>
        /// Unknown
        /// </summary>
        public byte WizOrder { get; set; }

        /// <summary>
        /// (probably) the distance at which the horse began its final spurt.
        /// The game likely uses this for changing animations, but it is useful for analysis too since a delayed spurt
        /// indicates lacking stamina.
        /// </summary>
        public float LastSpurtStartDistance { get; set; }

        /// <summary>
        /// Running style used by the horse.
        /// </summary>
        public RunningStyle RunningStyle { get; set; }

        /// <summary>
        /// (unsure) possibly an indication of which win/defeat animation to use?
        /// This is usually 1 for the winner, and 2 for 2nd place, but the rest tend to be 9, 11, or 14.
        /// </summary>
        public int Defeat { get; set; }

        /// <summary>
        /// Unclear what this is.  It is usually a few seconds lower than the finish time.
        /// </summary>
        public float FinishTimeRaw { get; set; }
    }

    /// <summary>
    /// Data for a single race event.  These are not sporadic so they are not stored in frame data.
    /// </summary>
    public class RaceSimulateEventData
    {
        /// <summary>
        /// The time in the race where this event occurs.  Not clear yet if this is an exact frame time, or if it
        /// can be "in between" frames.
        /// </summary>
        public float FrameTime { get; set; }

        /// <summary>
        /// Enumerated type of event.  These are things such as skill activations, scoring points, state changes, etc.
        /// </summary>
        public SimulateEventType Type { get; set; }

        /// <summary>
        /// Number of parameters (all int32) for this event.  The size depends on the event type.
        /// </summary>
        public byte ParamCount { get; set; }

        /// <summary>
        /// List of int32 parameters for this event.  Size and meaning depend on event type.
        /// Raw access is provided for debugging and for event types that do not yet have a dedicated subclass.
        /// </summary>
        public List<int> Params { get; set; } = new();
    }

    /// <summary>
    /// Skill activation event (<see cref="SimulateEventType.Skill"/>).
    /// <para>
    /// Params layout: <c>[horseIndex (0-based, gate order), skillId]</c>
    /// </para>
    /// </summary>
    public sealed class SkillSimulateEvent : RaceSimulateEventData
    {
        /// <summary>0-based horse index in gate order (matches <see cref="RaceHorseData.FrameOrder"/> - 1).</summary>
        public int HorseIndex => Params.Count > 0 ? Params[0] : -1;

        /// <summary>The activated skill's master ID.</summary>
        public int SkillId => Params.Count > 1 ? Params[1] : 0;

        /// <summary>
        /// Skill duration, in 10000ths of a second (0.1ms).  Modifers already included.
        /// Frame-0 skills like greens can be -1, but triggered skills like groundwork
        /// may also report -1 in frame 0.
        /// </summary>
        public int Duration => Params.Count > 2 ? Params[2] : -2;

        /// <summary>
        /// Version of skill effect, if there is more than 1.
        /// </summary>
        public int Version => Params.Count > 3 ? Params[3] : -1;

        /// <summary>
        /// Bitmask of targets for effect 1.  If skills have more than 1 effect, the targets
        /// are not included :(
        /// </summary>
        public int Targets => Params.Count > 4 ? Params[4] : -1;
    }

    /// <summary>
    /// Score / point award event (<see cref="SimulateEventType.Score"/>).
    /// Primarily used in Team Trials for attributing skill activation points (condition type 8)
    /// and other race performance scoring.
    /// <para>
    /// Params layout: <c>[horseIndex (0-based), rawScoreId (see team_stadium_raw_score.id), points]</c>
    /// </para>
    /// </summary>
    public sealed class ScoreSimulateEvent : RaceSimulateEventData
    {
        /// <summary>0-based horse index in gate order.</summary>
        public int HorseIndex => Params.Count > 0 ? Params[0] : -1;

        /// <summary>
        /// The raw score type ID (from team_stadium_raw_score). For skill activations this is the
        /// condition-8 bucket (historically 26-57, now loaded from master data).
        /// </summary>
        public int RawScoreId => Params.Count > 1 ? Params[1] : 0;

        /// <summary>Points awarded by this score event.</summary>
        public int Points => Params.Count > 2 ? Params[2] : 0;
    }
}
