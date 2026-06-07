// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go Command.ReadCommandSymbols/Command.Step.
using System;
using System.Collections.Generic;
using Lockstep.Core;

namespace Lockstep.Mugen.Command
{
    /// <summary>
    /// Compatibility entry point for callers that still provide an input history. The production path uses
    /// <see cref="MCommandRuntime"/> incrementally through <see cref="MCommandList.Update"/>.
    /// </summary>
    public static class MCommandMatcher
    {
        public static bool Matches(MCommandDef cmd, MCommandBuffer buf, bool facingRight)
        {
            if (cmd == null || cmd.Steps.Count == 0 || buf == null || buf.Count == 0)
            {
                return false;
            }

            MInputBuffer input = new MInputBuffer();
            MCommandRuntime runtime = new MCommandRuntime(cmd);
            int count = System.Math.Min(buf.Count, buf.Capacity);
            bool completed = false;
            for (int age = count - 1; age >= 0; age--)
            {
                input.Update(buf.Ago(age), facingRight);
                completed = runtime.Step(input);
            }
            return completed;
        }
    }

    /// <summary>一条命令的逐帧状态。定义共享，完成位、计时器和缓冲均属于角色运行时。</summary>
    internal sealed class MCommandRuntime
    {
        static readonly MCommandKey[] RelativeDirections =
        {
            Dir(MInput.Up), Dir(MInput.Down), Back(), Forward(),
            Diagonal(MInput.Up, back: true), Diagonal(MInput.Up, back: false),
            Diagonal(MInput.Down, back: true), Diagonal(MInput.Down, back: false),
        };

        static readonly MCommandKey[] PhysicalDirections =
        {
            Dir(MInput.Up), Dir(MInput.Down), Dir(MInput.Left), Dir(MInput.Right),
            Dir(MInput.Up | MInput.Left), Dir(MInput.Up | MInput.Right),
            Dir(MInput.Down | MInput.Left), Dir(MInput.Down | MInput.Right),
        };

        static readonly MCommandKey[] Buttons =
        {
            Button(MInput.A), Button(MInput.B), Button(MInput.C), Button(MInput.X),
            Button(MInput.Y), Button(MInput.Z), Button(MInput.S),
        };

        readonly MCommandDef _definition;
        readonly bool[] _completed;
        readonly int[] _stepTimers;
        readonly int[] _loopOrder;
        int _currentTime;
        int _bufferTime;

        internal MCommandRuntime(MCommandDef definition)
        {
            _definition = definition;
            _completed = new bool[definition.Steps.Count];
            _stepTimers = new int[definition.Steps.Count];
            _loopOrder = BuildLoopOrder(definition.Steps);
        }

        MCommandRuntime(MCommandRuntime source)
        {
            _definition = source._definition;
            _completed = (bool[])source._completed.Clone();
            _stepTimers = (int[])source._stepTimers.Clone();
            _loopOrder = (int[])source._loopOrder.Clone();
            _currentTime = source._currentTime;
            _bufferTime = source._bufferTime;
        }

        internal MCommandDef Definition => _definition;
        internal bool Active => _bufferTime > 0;
        internal MCommandRuntime Clone() => new MCommandRuntime(this);

        internal bool Step(MInputBuffer input)
        {
            if (_bufferTime > 0)
            {
                _bufferTime--;
            }
            if (_definition.Steps.Count == 0)
            {
                return false;
            }

            bool anyDone = false;
            int maxStepTime = _definition.StepTime > 0 ? _definition.StepTime : _definition.Time;
            for (int i = 0; i < _completed.Length; i++)
            {
                if (!_completed[i])
                {
                    continue;
                }
                _stepTimers[i]++;
                if (maxStepTime > 0 && _stepTimers[i] > maxStepTime)
                {
                    _completed[i] = false;
                    _stepTimers[i] = 0;
                    continue;
                }
                anyDone = true;
            }

            if (anyDone)
            {
                _currentTime++;
            }
            else if (_currentTime > 0)
            {
                ClearProgress();
            }

            for (int order = 0; order < _loopOrder.Length; order++)
            {
                int i = _loopOrder[order];
                if (i > 0 && !_completed[i - 1])
                {
                    continue;
                }

                MCommandStep step = _definition.Steps[i];
                bool matched = StepMatches(step, input);
                if (step.Greater && i > 0 && _completed[i - 1] && !_completed[i] && GreaterCheckFail(step, input))
                {
                    matched = false;
                    _completed[i - 1] = false;
                    _stepTimers[i - 1] = 0;
                }
                if (!matched)
                {
                    continue;
                }

                _completed[i] = true;
                _stepTimers[i] = 0;
                if (i > 0)
                {
                    _completed[i - 1] = false;
                    _stepTimers[i - 1] = 0;
                }
                if (i == 0)
                {
                    _currentTime = 0;
                }
            }

            bool complete = _completed[_completed.Length - 1];
            if (!complete && _currentTime < _definition.Time)
            {
                return false;
            }

            ClearProgress();
            if (complete)
            {
                _bufferTime = System.Math.Max(_bufferTime, System.Math.Max(1, _definition.BufferTime));
            }
            return complete;
        }

        internal void ClearProgress()
        {
            _currentTime = 0;
            Array.Clear(_completed, 0, _completed.Length);
            Array.Clear(_stepTimers, 0, _stepTimers.Length);
        }

        internal void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(_currentTime);
            hash.AddInt32(_bufferTime);
            hash.AddInt32(_completed.Length);
            for (int i = 0; i < _completed.Length; i++)
            {
                hash.AddInt32(_completed[i] ? 1 : 0);
                hash.AddInt32(_stepTimers[i]);
            }
        }

        static bool StepMatches(MCommandStep step, MInputBuffer input)
        {
            if (step.Keys.Count == 0)
            {
                return false;
            }
            if (step.OrLogic)
            {
                for (int i = 0; i < step.Keys.Count; i++)
                {
                    if (KeyMatches(step.Keys[i], input)) return true;
                }
                return false;
            }
            for (int i = 0; i < step.Keys.Count; i++)
            {
                if (!KeyMatches(step.Keys[i], input)) return false;
            }
            return true;
        }

        static bool KeyMatches(MCommandKey key, MInputBuffer input)
        {
            int state = input.State(key);
            bool matched = key.Hold ? state > 0 : state == 1;
            return matched && (key.ChargeTime <= 1 || input.StateCharge(key) >= key.ChargeTime);
        }

        static bool GreaterCheckFail(MCommandStep step, MInputBuffer input)
        {
            bool usePhysical = false;
            for (int i = 0; i < step.Keys.Count; i++)
            {
                if (!step.Keys[i].IsButton &&
                    (step.Keys[i].Bits & (MInput.Left | MInput.Right)) != 0 &&
                    !step.Keys[i].IsBack && !step.Keys[i].IsFwd)
                {
                    usePhysical = true;
                    break;
                }
            }

            MCommandKey[] directions = usePhysical ? PhysicalDirections : RelativeDirections;
            for (int i = 0; i < directions.Length; i++)
            {
                if (ChangedKeyNotAllowed(directions[i], step, input)) return true;
            }
            for (int i = 0; i < Buttons.Length; i++)
            {
                if (ChangedKeyNotAllowed(Buttons[i], step, input)) return true;
            }
            return false;
        }

        static bool ChangedKeyNotAllowed(MCommandKey key, MCommandStep step, MInputBuffer input)
        {
            MCommandKey press = key;
            press.Release = false;
            if (input.State(press) == 1 && !Contains(step, press, release: false))
            {
                return true;
            }
            MCommandKey release = key;
            release.Release = true;
            if (input.State(release) == 1 && !Contains(step, release, release: true))
            {
                return true;
            }
            return false;
        }

        static bool Contains(MCommandStep step, MCommandKey key, bool release)
        {
            for (int i = 0; i < step.Keys.Count; i++)
            {
                if (step.Keys[i].Release == release && SameKey(step.Keys[i], key)) return true;
            }
            return false;
        }

        static int[] BuildLoopOrder(List<MCommandStep> steps)
        {
            List<int> order = new List<int>(steps.Count);
            for (int i = steps.Count - 1; i >= 0;)
            {
                if (i > 0 && IsDirToButton(steps[i - 1], steps[i]))
                {
                    int start = i - 1;
                    int end = i;
                    while (start > 0 && IsDirToButton(steps[start - 1], steps[start])) start--;
                    for (int j = start; j <= end; j++) order.Add(j);
                    i = start - 1;
                }
                else
                {
                    order.Add(i--);
                }
            }
            return order.ToArray();
        }

        static bool IsDirToButton(MCommandStep current, MCommandStep next)
        {
            for (int i = 0; i < next.Keys.Count; i++) if (next.Keys[i].Hold) return false;
            for (int i = 0; i < current.Keys.Count; i++) if (current.Keys[i].IsButton) return false;
            for (int i = 0; i < current.Keys.Count; i++)
            {
                for (int j = 0; j < next.Keys.Count; j++)
                {
                    if (SameKey(current.Keys[i], next.Keys[j])) return false;
                }
            }
            for (int i = 0; i < next.Keys.Count; i++)
            {
                if (next.Keys[i].IsButton && !next.Keys[i].Release) return true;
            }
            for (int i = 0; i < current.Keys.Count; i++)
            {
                if (!current.Keys[i].Release) continue;
                for (int j = 0; j < next.Keys.Count; j++)
                {
                    if (!next.Keys[j].Release) return true;
                }
            }
            return false;
        }

        static bool SameKey(MCommandKey a, MCommandKey b)
        {
            return a.Bits == b.Bits && a.IsBack == b.IsBack && a.IsFwd == b.IsFwd &&
                   a.IsButton == b.IsButton && a.IsNeutral == b.IsNeutral;
        }

        static MCommandKey Dir(MInput bits) => new MCommandKey { Bits = bits };
        static MCommandKey Back() => new MCommandKey { IsBack = true };
        static MCommandKey Forward() => new MCommandKey { IsFwd = true };
        static MCommandKey Diagonal(MInput vertical, bool back) => new MCommandKey
        {
            Bits = vertical,
            IsBack = back,
            IsFwd = !back,
        };
        static MCommandKey Button(MInput bit) => new MCommandKey { Bits = bit, IsButton = true };
    }
}
