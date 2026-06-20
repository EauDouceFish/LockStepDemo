using System.Collections.Generic;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle
{
    /// <summary>
    /// One-shot scripted move playback for the Unity museum. It feeds synthesized inputs frame by frame,
    /// then clears command runtime state once the requested move state is reached so the same buffered
    /// command cannot retrigger after the move returns to state 0.
    /// </summary>
    public sealed class MMovePreviewSession
    {
        readonly Queue<MInput> _inputs = new Queue<MInput>();
        readonly int _targetStateNo;
        readonly int _maxSearchFrames;
        readonly int _maxMoveFrames;
        int _frames;
        int _moveFrames;

        public bool EnteredTarget { get; private set; }
        public bool Done { get; private set; }
        public bool TimedOutSearching { get; private set; }
        public bool TimedOutInMoveState { get; private set; }
        public int TimeoutStateNo { get; private set; } = -1;
        public int TimeoutAnimNo { get; private set; } = -1;
        public int TimeoutTime { get; private set; } = -1;
        public string Label { get; private set; }
        public string StatusText { get; private set; }

        // Project-specific: Unity move preview harness that feeds synthesized Ikemen-style command input to MBattleEngine.
        public MMovePreviewSession(IReadOnlyList<MCommandDef> commands, bool facingRight, int targetStateNo,
            string label = "", int maxSearchFrames = 90, int maxMoveFrames = 180)
        {
            _targetStateNo = targetStateNo;
            _maxSearchFrames = maxSearchFrames <= 0 ? 90 : maxSearchFrames;
            _maxMoveFrames = maxMoveFrames <= 0 ? 180 : maxMoveFrames;
            Label = label ?? "";
            StatusText = Label.Length == 0 ? "Ready" : Label;

            List<MInput> sequence = MCommandInputSynthesizer.BuildCombinedSequence(commands, facingRight);
            for (int i = 0; i < sequence.Count; i++)
            {
                _inputs.Enqueue(sequence[i]);
            }
        }

        // Project-specific: exposes the next synthesized preview input; Ikemen consumes real input buffers instead.
        public MInput NextInput()
        {
            if (Done || EnteredTarget || _inputs.Count == 0)
            {
                return MInput.None;
            }
            return _inputs.Dequeue();
        }

        // Project-specific: watches C# preview playback state and clears command runtime after the target state is reached.
        public void AfterTick(MBattleEngine engine)
        {
            if (Done || engine == null || engine.Chars.Count == 0)
            {
                return;
            }

            _frames++;
            MChar actor = engine.Chars[0];
            if (!EnteredTarget)
            {
                if (_targetStateNo >= 0 && actor.StateNo == _targetStateNo)
                {
                    EnteredTarget = true;
                    _moveFrames = 0;
                    _inputs.Clear();
                    actor.CommandList?.ResetRuntime();
                    StatusText = FormatStatus("playing", actor);
                    return;
                }
                if (_inputs.Count == 0 && _frames >= _maxSearchFrames)
                {
                    TimedOutSearching = true;
                    actor.CommandList?.ResetRuntime();
                    StatusText = FormatStatus("timeout before target", actor);
                    Done = true;
                }
                return;
            }

            _moveFrames++;
            if (actor.StateNo != _targetStateNo)
            {
                StatusText = FormatStatus(IsDefaultReady(actor) ? "recovered" : "recovering", actor);
                if (IsDefaultReady(actor))
                {
                    Done = true;
                }
                return;
            }

            if (_moveFrames >= _maxMoveFrames && !TimedOutInMoveState)
            {
                TimedOutInMoveState = true;
                TimeoutStateNo = actor.StateNo;
                TimeoutAnimNo = actor.AnimNo;
                TimeoutTime = actor.Time;
                actor.CommandList?.ResetRuntime();
                StatusText = FormatStatus("timeout in move", actor);
                Done = true;
                return;
            }

            StatusText = FormatStatus("playing", actor);
        }

        // Project-specific: preview completion predicate for returning to controllable standing state 0.
        static bool IsDefaultReady(MChar actor)
        {
            return actor.StateNo == 0 && actor.Ctrl;
        }

        // Project-specific: formats Unity preview diagnostics; no Ikemen runtime counterpart.
        string FormatStatus(string status, MChar actor)
        {
            string prefix = Label.Length == 0 ? "preview" : Label;
            return prefix + " [" + status + "] state=" + actor.StateNo + " anim=" + actor.AnimNo +
                   " time=" + actor.Time + " ctrl=" + actor.Ctrl;
        }
    }
}
