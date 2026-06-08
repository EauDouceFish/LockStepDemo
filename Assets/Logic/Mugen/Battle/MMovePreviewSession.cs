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
        public string Label { get; private set; }

        public MMovePreviewSession(IReadOnlyList<MCommandDef> commands, bool facingRight, int targetStateNo,
            string label = "", int maxSearchFrames = 90, int maxMoveFrames = 180)
        {
            _targetStateNo = targetStateNo;
            _maxSearchFrames = maxSearchFrames <= 0 ? 90 : maxSearchFrames;
            _maxMoveFrames = maxMoveFrames <= 0 ? 180 : maxMoveFrames;
            Label = label ?? "";

            List<MInput> sequence = MCommandInputSynthesizer.BuildCombinedSequence(commands, facingRight);
            for (int i = 0; i < sequence.Count; i++)
            {
                _inputs.Enqueue(sequence[i]);
            }
        }

        public MInput NextInput()
        {
            if (Done || EnteredTarget || _inputs.Count == 0)
            {
                return MInput.None;
            }
            return _inputs.Dequeue();
        }

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
                    return;
                }
                if (_inputs.Count == 0 && _frames >= _maxSearchFrames)
                {
                    actor.CommandList?.ResetRuntime();
                    Done = true;
                }
                return;
            }

            _moveFrames++;
            if (actor.StateNo != _targetStateNo)
            {
                Done = IsDefaultReady(actor);
                return;
            }

            // Museum preview fallback only. Real battle semantics stay inside MBattleEngine/state data.
            if (_moveFrames >= _maxMoveFrames && actor.Ctrl)
            {
                actor.QueueTransition(0, actor.PlayerNo);
            }
        }

        static bool IsDefaultReady(MChar actor)
        {
            return actor.StateNo == 0 && actor.Ctrl;
        }
    }
}
