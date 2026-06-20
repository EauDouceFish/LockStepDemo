using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle
{
    public enum MBattleRunMode
    {
        LocalTest,
        LocalAi,
        NetKcp,
    }

    public sealed class MBattleRunLog
    {
        public string Schema = "mugen-battle-run-log/v1";
        public MBattleRunMode Mode;
        public bool IsLocalTest;
        public string ScenarioId = "";
        public readonly List<MBattleRunPlayer> Players = new List<MBattleRunPlayer>();
        public readonly List<MBattleRunFrame> Frames = new List<MBattleRunFrame>();
        public bool Completed;
        public string EndReason = "";
        public string InputChecksumHex = "";
        public string HashChecksumHex = "";
        public string FinalHashHex = "";
    }

    public sealed class MBattleRunPlayer
    {
        public int Slot;
        public string Uid = "";
        public string Agent = "";
        public string Character = "";
    }

    public sealed class MBattleRunFrame
    {
        public int Frame;
        public int[] Inputs = new int[0];
        public List<string>[] ActiveCommands = new List<string>[0];
        public string HashHex = "";
        public readonly List<MBattleRunEntity> Players = new List<MBattleRunEntity>();
    }

    public sealed class MBattleRunEntity
    {
        public int Slot;
        public int StateNo;
        public int AnimNo;
        public int AnimElemNo;
        public int AnimElem;
        public int AnimElemTime;
        public int AnimTime;
        public int Time;
        public int StateType;
        public int MoveType;
        public bool Ctrl;
        public bool KeyCtrl;
        public int Hitstop;
        public bool PauseBool;
        public int PauseMovetime;
        public int SuperMovetime;
        public bool PosFreeze;
        public int Acttmp;
        public int Life;
        public int Power;
        public long FacingRaw;
        public long PosXRaw;
        public long PosYRaw;
        public long VelXRaw;
        public long VelYRaw;
        public long WidthPlayerFrontRaw;
        public long WidthPlayerBackRaw;
        public bool PlayerPushEnabled;
        public int PushPriority;
        public int PushAffectTeam;
    }

    public sealed class MBattleRunLogRecorder
    {
        readonly MBattleRunLog _log;
        Hash64 _inputChecksum;
        Hash64 _hashChecksum;
        string _finalHashHex = "0000000000000000";

        // Project-specific: creates a compact C# battle-run audit log around Ikemen-style deterministic simulation.
        public MBattleRunLogRecorder(MBattleRunMode mode, string scenarioId, int playerCount)
        {
            _log = new MBattleRunLog
            {
                Mode = mode,
                IsLocalTest = mode != MBattleRunMode.NetKcp,
                ScenarioId = scenarioId ?? "",
            };
            for (int i = 0; i < playerCount; i++)
            {
                _log.Players.Add(new MBattleRunPlayer { Slot = i });
            }
            _inputChecksum = Hash64.Create();
            _hashChecksum = Hash64.Create();
        }

        // Project-specific: annotates log players with local/server-facing identity and input source.
        public void SetPlayer(int slot, string uid, string agent, string character)
        {
            if (slot < 0 || slot >= _log.Players.Count)
            {
                return;
            }
            MBattleRunPlayer player = _log.Players[slot];
            player.Uid = uid ?? "";
            player.Agent = agent ?? "";
            player.Character = character ?? "";
        }

        // Project-specific: records one post-tick frame for replay/hash validation and server diagnostics.
        public MBattleRunFrame CaptureFrame(int frame, MBattleEngine engine, IReadOnlyList<MInput> inputs)
        {
            if (engine == null) { throw new ArgumentNullException(nameof(engine)); }
            return CaptureFrame(frame, engine, engine.ComputeHash, inputs);
        }

        // Project-specific: records a frame with a higher-level match hash while still capturing engine entities.
        public MBattleRunFrame CaptureFrame(int frame, MBattleEngine engine, Func<ulong> computeHash, IReadOnlyList<MInput> inputs)
        {
            if (engine == null) { throw new ArgumentNullException(nameof(engine)); }
            if (computeHash == null) { throw new ArgumentNullException(nameof(computeHash)); }
            ulong hash = computeHash();
            int inputCount = System.Math.Max(inputs != null ? inputs.Count : 0, _log.Players.Count);
            MBattleRunFrame record = new MBattleRunFrame
            {
                Frame = frame,
                Inputs = new int[inputCount],
                ActiveCommands = new List<string>[inputCount],
                HashHex = ToHex(hash),
            };
            _finalHashHex = record.HashHex;

            _inputChecksum.AddInt32(frame);
            _hashChecksum.AddInt32(frame);
            for (int i = 0; i < inputCount; i++)
            {
                int bits = inputs != null && i < inputs.Count ? (int)inputs[i] : 0;
                record.Inputs[i] = bits;
                _inputChecksum.AddInt32(bits);
                record.ActiveCommands[i] = ActiveNames(engine, i);
            }

            _hashChecksum.AddUInt64(hash);
            for (int i = 0; i < engine.Chars.Count; i++)
            {
                MChar c = engine.Chars[i];
                record.Players.Add(new MBattleRunEntity
                {
                    Slot = i,
                    StateNo = c.StateNo,
                    AnimNo = c.AnimNo,
                    AnimElemNo = c.AnimElemNo,
                    AnimElem = c.AnimElem,
                    AnimElemTime = c.AnimElemTime,
                    AnimTime = c.AnimTime,
                    Time = c.Time,
                    StateType = c.StateType,
                    MoveType = c.MoveType,
                    Ctrl = c.Ctrl,
                    KeyCtrl = c.KeyCtrl,
                    Hitstop = c.Hitstop,
                    PauseBool = c.PauseBool,
                    PauseMovetime = c.PauseMovetime,
                    SuperMovetime = c.SuperMovetime,
                    PosFreeze = c.PosFreeze,
                    Acttmp = c.Acttmp,
                    Life = c.Life,
                    Power = c.Power,
                    FacingRaw = c.Facing.Raw,
                    PosXRaw = c.Pos.X.Raw,
                    PosYRaw = c.Pos.Y.Raw,
                    VelXRaw = c.Vel.X.Raw,
                    VelYRaw = c.Vel.Y.Raw,
                    WidthPlayerFrontRaw = c.WidthPlayerFront.Raw,
                    WidthPlayerBackRaw = c.WidthPlayerBack.Raw,
                    PlayerPushEnabled = c.PlayerPushEnabled,
                    PushPriority = c.PushPriority,
                    PushAffectTeam = c.PushAffectTeam,
                });
            }
            _log.Frames.Add(record);
            return record;
        }

        // Project-specific: closes the run log and emits deterministic checksums for later validation.
        public MBattleRunLog Complete(string reason)
        {
            _log.Completed = true;
            _log.EndReason = reason ?? "";
            _log.InputChecksumHex = ToHex(_inputChecksum.Value);
            _log.HashChecksumHex = ToHex(_hashChecksum.Value);
            _log.FinalHashHex = _finalHashHex;
            return _log;
        }

        static List<string> ActiveNames(MBattleEngine engine, int slot)
        {
            if (slot < 0 || slot >= engine.Chars.Count || engine.Chars[slot].CommandList == null)
            {
                return new List<string>();
            }
            return engine.Chars[slot].CommandList.ActiveNames();
        }

        static string ToHex(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);
    }

    public sealed class MBattleRunLogVerification
    {
        public bool Success;
        public string Message = "";
        public string ExpectedHashHex = "";
        public string ActualHashHex = "";
    }

    public static class MBattleRunLogVerifier
    {
        // Project-specific: replays recorded inputs against a fresh C# engine and checks per-frame native hashes.
        public static MBattleRunLogVerification Verify(MBattleEngine engine, MBattleRunLog log)
        {
            if (engine == null) { throw new ArgumentNullException(nameof(engine)); }
            return Verify(engine.Tick, engine.ComputeHash, log);
        }

        // Project-specific: verifies logs for higher-level deterministic drivers such as rounds or turns matches.
        public static MBattleRunLogVerification Verify(
            Action<IReadOnlyList<MInput>> simulate,
            Func<ulong> computeHash,
            MBattleRunLog log)
        {
            if (simulate == null) { throw new ArgumentNullException(nameof(simulate)); }
            if (computeHash == null) { throw new ArgumentNullException(nameof(computeHash)); }
            if (log == null) { throw new ArgumentNullException(nameof(log)); }
            if (!log.Completed)
            {
                return Failed("log is not completed", "", "");
            }
            Hash64 inputChecksum = Hash64.Create();
            Hash64 hashChecksum = Hash64.Create();
            string finalHashHex = "0000000000000000";
            for (int i = 0; i < log.Frames.Count; i++)
            {
                MBattleRunFrame frame = log.Frames[i];
                if (frame.Frame != i)
                {
                    return Failed(
                        "frame sequence mismatch at index " + i.ToString(CultureInfo.InvariantCulture),
                        i.ToString(CultureInfo.InvariantCulture),
                        frame.Frame.ToString(CultureInfo.InvariantCulture));
                }
                inputChecksum.AddInt32(frame.Frame);
                hashChecksum.AddInt32(frame.Frame);
                MInput[] inputs = new MInput[frame.Inputs != null ? frame.Inputs.Length : 0];
                for (int p = 0; p < inputs.Length; p++)
                {
                    inputs[p] = (MInput)frame.Inputs[p];
                    inputChecksum.AddInt32(frame.Inputs[p]);
                }
                simulate(inputs);
                ulong hash = computeHash();
                hashChecksum.AddUInt64(hash);
                string actual = hash.ToString("x16", CultureInfo.InvariantCulture);
                finalHashHex = actual;
                if (!string.Equals(frame.HashHex, actual, StringComparison.Ordinal))
                {
                    return Failed(
                        "frame " + frame.Frame.ToString(CultureInfo.InvariantCulture) + " hash mismatch",
                        frame.HashHex,
                        actual);
                }
            }
            string actualInputChecksum = inputChecksum.Value.ToString("x16", CultureInfo.InvariantCulture);
            if (!string.Equals(log.InputChecksumHex, actualInputChecksum, StringComparison.Ordinal))
            {
                return Failed("input checksum mismatch", log.InputChecksumHex, actualInputChecksum);
            }
            string actualHashChecksum = hashChecksum.Value.ToString("x16", CultureInfo.InvariantCulture);
            if (!string.Equals(log.HashChecksumHex, actualHashChecksum, StringComparison.Ordinal))
            {
                return Failed("hash checksum mismatch", log.HashChecksumHex, actualHashChecksum);
            }
            if (!string.Equals(log.FinalHashHex, finalHashHex, StringComparison.Ordinal))
            {
                return Failed("final hash mismatch", log.FinalHashHex, finalHashHex);
            }
            return new MBattleRunLogVerification
            {
                Success = true,
                Message = "ok",
                ExpectedHashHex = log.FinalHashHex,
                ActualHashHex = finalHashHex,
            };
        }

        static MBattleRunLogVerification Failed(string message, string expected, string actual)
        {
            return new MBattleRunLogVerification
            {
                Success = false,
                Message = message,
                ExpectedHashHex = expected,
                ActualHashHex = actual,
            };
        }
    }

    public static class MBattleRunLogJson
    {
        public static string ToJson(MBattleRunLog log)
        {
            StringBuilder sb = new StringBuilder(1024 + (log != null ? log.Frames.Count * 256 : 0));
            WriteLog(sb, log);
            return sb.ToString();
        }

        static void WriteLog(StringBuilder sb, MBattleRunLog log)
        {
            if (log == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            Prop(sb, "schema", log.Schema).Append(',');
            Prop(sb, "mode", log.Mode.ToString()).Append(',');
            Prop(sb, "isLocalTest", log.IsLocalTest).Append(',');
            Prop(sb, "scenarioId", log.ScenarioId).Append(',');
            Prop(sb, "completed", log.Completed).Append(',');
            Prop(sb, "endReason", log.EndReason).Append(',');
            Prop(sb, "inputChecksumHex", log.InputChecksumHex).Append(',');
            Prop(sb, "hashChecksumHex", log.HashChecksumHex).Append(',');
            Prop(sb, "finalHashHex", log.FinalHashHex).Append(',');

            sb.Append("\"players\":[");
            for (int i = 0; i < log.Players.Count; i++)
            {
                if (i > 0) { sb.Append(','); }
                MBattleRunPlayer p = log.Players[i];
                sb.Append('{');
                Prop(sb, "slot", p.Slot).Append(',');
                Prop(sb, "uid", p.Uid).Append(',');
                Prop(sb, "agent", p.Agent).Append(',');
                Prop(sb, "character", p.Character);
                sb.Append('}');
            }
            sb.Append("],");

            sb.Append("\"frames\":[");
            for (int i = 0; i < log.Frames.Count; i++)
            {
                if (i > 0) { sb.Append(','); }
                WriteFrame(sb, log.Frames[i]);
            }
            sb.Append(']');
            sb.Append('}');
        }

        static void WriteFrame(StringBuilder sb, MBattleRunFrame frame)
        {
            sb.Append('{');
            Prop(sb, "frame", frame.Frame).Append(',');
            sb.Append("\"inputs\":");
            WriteIntArray(sb, frame.Inputs);
            sb.Append(',');
            sb.Append("\"activeCommands\":[");
            for (int i = 0; i < frame.ActiveCommands.Length; i++)
            {
                if (i > 0) { sb.Append(','); }
                WriteStringList(sb, frame.ActiveCommands[i]);
            }
            sb.Append("],");
            Prop(sb, "hashHex", frame.HashHex).Append(',');
            sb.Append("\"entities\":[");
            for (int i = 0; i < frame.Players.Count; i++)
            {
                if (i > 0) { sb.Append(','); }
                WriteEntity(sb, frame.Players[i]);
            }
            sb.Append(']');
            sb.Append('}');
        }

        static void WriteEntity(StringBuilder sb, MBattleRunEntity entity)
        {
            sb.Append('{');
            Prop(sb, "slot", entity.Slot).Append(',');
            Prop(sb, "stateNo", entity.StateNo).Append(',');
            Prop(sb, "animNo", entity.AnimNo).Append(',');
            Prop(sb, "animElemNo", entity.AnimElemNo).Append(',');
            Prop(sb, "animElem", entity.AnimElem).Append(',');
            Prop(sb, "animElemTime", entity.AnimElemTime).Append(',');
            Prop(sb, "animTime", entity.AnimTime).Append(',');
            Prop(sb, "time", entity.Time).Append(',');
            Prop(sb, "stateType", entity.StateType).Append(',');
            Prop(sb, "moveType", entity.MoveType).Append(',');
            Prop(sb, "ctrl", entity.Ctrl).Append(',');
            Prop(sb, "keyCtrl", entity.KeyCtrl).Append(',');
            Prop(sb, "hitstop", entity.Hitstop).Append(',');
            Prop(sb, "pauseBool", entity.PauseBool).Append(',');
            Prop(sb, "pauseMovetime", entity.PauseMovetime).Append(',');
            Prop(sb, "superMovetime", entity.SuperMovetime).Append(',');
            Prop(sb, "posFreeze", entity.PosFreeze).Append(',');
            Prop(sb, "acttmp", entity.Acttmp).Append(',');
            Prop(sb, "life", entity.Life).Append(',');
            Prop(sb, "power", entity.Power).Append(',');
            Prop(sb, "facingRaw", entity.FacingRaw).Append(',');
            Prop(sb, "posXRaw", entity.PosXRaw).Append(',');
            Prop(sb, "posYRaw", entity.PosYRaw).Append(',');
            Prop(sb, "velXRaw", entity.VelXRaw).Append(',');
            Prop(sb, "velYRaw", entity.VelYRaw).Append(',');
            Prop(sb, "widthPlayerFrontRaw", entity.WidthPlayerFrontRaw).Append(',');
            Prop(sb, "widthPlayerBackRaw", entity.WidthPlayerBackRaw).Append(',');
            Prop(sb, "playerPushEnabled", entity.PlayerPushEnabled).Append(',');
            Prop(sb, "pushPriority", entity.PushPriority).Append(',');
            Prop(sb, "pushAffectTeam", entity.PushAffectTeam);
            sb.Append('}');
        }

        static void WriteIntArray(StringBuilder sb, int[] values)
        {
            sb.Append('[');
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) { sb.Append(','); }
                    sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
                }
            }
            sb.Append(']');
        }

        static void WriteStringList(StringBuilder sb, List<string> values)
        {
            sb.Append('[');
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    if (i > 0) { sb.Append(','); }
                    String(sb, values[i]);
                }
            }
            sb.Append(']');
        }

        static StringBuilder Prop(StringBuilder sb, string name, string value)
        {
            String(sb, name).Append(':');
            String(sb, value);
            return sb;
        }

        static StringBuilder Prop(StringBuilder sb, string name, bool value)
        {
            String(sb, name).Append(':').Append(value ? "true" : "false");
            return sb;
        }

        static StringBuilder Prop(StringBuilder sb, string name, int value)
        {
            String(sb, name).Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
            return sb;
        }

        static StringBuilder Prop(StringBuilder sb, string name, long value)
        {
            String(sb, name).Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
            return sb;
        }

        static StringBuilder String(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char ch = value[i];
                    switch (ch)
                    {
                        case '\\': sb.Append("\\\\"); break;
                        case '"': sb.Append("\\\""); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (ch < 32)
                            {
                                sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(ch);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
            return sb;
        }
    }
}
