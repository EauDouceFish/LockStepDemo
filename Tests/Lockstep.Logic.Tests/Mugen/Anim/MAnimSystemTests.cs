using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using NUnit.Framework;
using GameData = Lockstep.Game.Data;

namespace Lockstep.Logic.Tests.Mugen.Anim
{
    /// <summary>M8：动画推进（移植 Ikemen Animation.Action）+ AIR 导入桥接 + 快照覆盖。</summary>
    [TestFixture]
    public sealed class MAnimSystemTests
    {
        // ───────── 构造辅助 ─────────
        static MAnimData Anim(int no, int loopStart, params int[] times)
        {
            MAnimFrame[] frames = new MAnimFrame[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                frames[i] = new MAnimFrame { SpriteGroup = no, SpriteImage = i, Time = times[i] };
            }
            MAnimData data = new MAnimData { No = no, LoopStart = loopStart, Frames = frames };
            data.ComputePacing();
            return data;
        }

        static Dictionary<int, MAnimData> Table(params MAnimData[] anims)
        {
            Dictionary<int, MAnimData> t = new Dictionary<int, MAnimData>();
            foreach (MAnimData a in anims)
            {
                t[a.No] = a;
            }
            return t;
        }

        // ───────── 节拍量 ─────────
        [Test]
        public void ComputePacing_SumsFiniteDurations()
        {
            MAnimData a = Anim(0, 0, 3, 2);
            Assert.That(a.TotalTime, Is.EqualTo(5));
            Assert.That(a.LoopTime, Is.EqualTo(5));
            Assert.That(a.PreLoopTime, Is.EqualTo(0));
        }

        [Test]
        public void ComputePacing_LoopStartSplitsPreAndLoop()
        {
            MAnimData a = Anim(0, 1, 3, 2, 4);   // loopstart=1：preloop=3，loop=2+4=6
            Assert.That(a.TotalTime, Is.EqualTo(9));
            Assert.That(a.PreLoopTime, Is.EqualTo(3));
            Assert.That(a.LoopTime, Is.EqualTo(6));
        }

        [Test]
        public void ComputePacing_InfiniteLastFrame_TotalTimeMinusOne()
        {
            MAnimData a = Anim(0, 0, 3, -1);
            Assert.That(a.TotalTime, Is.EqualTo(-1));
        }

        // ───────── 推进 ─────────
        [Test]
        public void Action_AdvancesToNextElementAfterDuration()
        {
            MAnimData a = Anim(0, 0, 2, 2);
            Dictionary<int, MAnimData> table = Table(a);
            MChar c = new MChar { AnimNo = 0 };

            MAnimSystem.Action(c, table);   // tick1：仍在元素0（totaltime=4）
            Assert.That(c.AnimElemNo, Is.EqualTo(1));
            Assert.That(c.AnimTime, Is.EqualTo(-3));

            MAnimSystem.Action(c, table);   // tick2：跨入元素1
            Assert.That(c.AnimElemNo, Is.EqualTo(2));
            Assert.That(c.AnimElem, Is.EqualTo(1));
            Assert.That(c.AnimTime, Is.EqualTo(-2));
        }

        [Test]
        public void Action_LoopsBackToLoopStart()
        {
            MAnimData a = Anim(0, 0, 2);   // 单帧 2 tick 循环
            Dictionary<int, MAnimData> table = Table(a);
            MChar c = new MChar { AnimNo = 0 };

            MAnimSystem.Action(c, table);   // tick1
            MAnimSystem.Action(c, table);   // tick2：到终点并回绕
            Assert.That(c.AnimElemNo, Is.EqualTo(1));
            Assert.That(c.AnimTime, Is.EqualTo(0), "末帧到时 animtime=0");
            Assert.That(c.AnimLoopEnd, Is.True);

            MAnimSystem.Action(c, table);   // tick3：循环重新计时
            Assert.That(c.AnimElemNo, Is.EqualTo(1));
            Assert.That(c.AnimTime, Is.EqualTo(-1), "回绕后 curtime 复位继续");
        }

        [Test]
        public void Action_InfiniteFrame_StaysOnLastElement()
        {
            MAnimData a = Anim(0, 0, 2, -1);
            Dictionary<int, MAnimData> table = Table(a);
            MChar c = new MChar { AnimNo = 0 };

            for (int i = 0; i < 6; i++)
            {
                MAnimSystem.Action(c, table);
            }
            Assert.That(c.AnimElem, Is.EqualTo(1), "永久帧停在末元素");
            Assert.That(c.AnimElemNo, Is.EqualTo(2));
            Assert.That(c.AnimLoopEnd, Is.False, "永久动画不置 loopend");
        }

        [Test]
        public void Action_SkipsZeroDurationFrame()
        {
            MAnimData a = Anim(0, 0, 0, 3);   // 首帧 0 tick：应被跳过
            Dictionary<int, MAnimData> table = Table(a);
            MChar c = new MChar { AnimNo = 0 };

            MAnimSystem.Action(c, table);
            Assert.That(c.AnimElemNo, Is.EqualTo(2), "0 时长帧首 tick 即跳过");
        }

        [Test]
        public void Action_ChangeAnimNo_ReinitsRuntime()
        {
            MAnimData a10 = Anim(10, 0, 2, 2);
            MAnimData a20 = Anim(20, 0, 5);
            Dictionary<int, MAnimData> table = Table(a10, a20);
            MChar c = new MChar { AnimNo = 10 };

            MAnimSystem.Action(c, table);
            MAnimSystem.Action(c, table);   // 已进元素1
            Assert.That(c.AnimElem, Is.EqualTo(1));

            c.AnimNo = 20;                  // 模拟 ChangeAnim
            MAnimSystem.Action(c, table);
            Assert.That(c.AnimRunningNo, Is.EqualTo(20));
            Assert.That(c.AnimElem, Is.EqualTo(0), "切动画后回到头部元素");
            Assert.That(c.AnimElemNo, Is.EqualTo(1));
        }

        [Test]
        public void Action_PopulatesClsnFromCurrentFrame()
        {
            MAnimData a = Anim(0, 0, 5);
            a.Frames[0].Clsn1 = new[] { new MClsnBox(FFloat.FromInt(1), FFloat.FromInt(2), FFloat.FromInt(3), FFloat.FromInt(4)) };
            a.Frames[0].Clsn2 = new[] { new MClsnBox(FFloat.FromInt(-1), FFloat.FromInt(-2), FFloat.FromInt(5), FFloat.FromInt(6)) };
            Dictionary<int, MAnimData> table = Table(a);
            MChar c = new MChar { AnimNo = 0 };

            MAnimSystem.Action(c, table);
            Assert.That(c.Clsn1, Is.Not.Null);
            Assert.That(c.Clsn1.Length, Is.EqualTo(1));
            Assert.That(c.Clsn1[0].X2, Is.EqualTo(FFloat.FromInt(3)));
            Assert.That(c.Clsn2[0].Y2, Is.EqualTo(FFloat.FromInt(6)));
        }

        [Test]
        public void Play_SetsInitialFrameWithoutAdvancing()
        {
            MAnimData a = Anim(7, 0, 4, 4);
            Dictionary<int, MAnimData> table = Table(a);
            MChar c = new MChar { AnimNo = 0 };

            MAnimSystem.Play(c, 7, table);
            Assert.That(c.AnimNo, Is.EqualTo(7));
            Assert.That(c.AnimRunningNo, Is.EqualTo(7));
            Assert.That(c.AnimElem, Is.EqualTo(0));
            Assert.That(c.AnimElemNo, Is.EqualTo(1));
            Assert.That(c.AnimCurTime, Is.EqualTo(0), "Play 不推进");
            Assert.That(c.AnimTime, Is.EqualTo(-8));
        }

        [Test]
        public void Action_NullOrMissingAnim_DoesNotThrow()
        {
            MChar c = new MChar { AnimNo = 999 };
            Assert.DoesNotThrow(() => MAnimSystem.Action(c, Table(Anim(0, 0, 2))));
            Assert.DoesNotThrow(() => MAnimSystem.Action(c, null));
        }

        // ───────── AIR 导入桥接 ─────────
        [Test]
        public void Import_FromAir_ConvertsFramesClsnAndPacing()
        {
            GameData.AnimData src = new GameData.AnimData
            {
                Id = 200,
                LoopStart = 1,
                Frames = new[]
                {
                    new GameData.AnimFrame { SpriteGroup = 0, SpriteImage = 0, Duration = 3 },
                    new GameData.AnimFrame
                    {
                        SpriteGroup = 0, SpriteImage = 1, Duration = 2,
                        Clsn2 = new[] { new GameData.ClsnBox(FFloat.FromInt(-10), FFloat.FromInt(-80), FFloat.FromInt(10), FFloat.Zero) },
                    },
                },
            };

            MAnimData dst = MAnimImport.FromAir(src);
            Assert.That(dst.No, Is.EqualTo(200));
            Assert.That(dst.LoopStart, Is.EqualTo(1));
            Assert.That(dst.Frames.Length, Is.EqualTo(2));
            Assert.That(dst.TotalTime, Is.EqualTo(5));
            Assert.That(dst.Frames[1].Time, Is.EqualTo(2));
            Assert.That(dst.Frames[1].Clsn2[0].Y1, Is.EqualTo(FFloat.FromInt(-80)));
        }

        [Test]
        public void Import_FromAirTable_KeysByAnimNo()
        {
            GameData.AnimData a = new GameData.AnimData { Id = 0, Frames = new[] { new GameData.AnimFrame { Duration = 1 } } };
            GameData.AnimData b = new GameData.AnimData { Id = 5, Frames = new[] { new GameData.AnimFrame { Duration = 1 } } };
            Dictionary<int, MAnimData> table = MAnimImport.FromAirTable(new[] { a, b });
            Assert.That(table.ContainsKey(0), Is.True);
            Assert.That(table.ContainsKey(5), Is.True);
        }

        // ───────── 快照/回滚覆盖 ─────────
        [Test]
        public void AnimRuntime_IsHashedAndCloned()
        {
            MAnimData a = Anim(0, 0, 2, 2, 2);
            Dictionary<int, MAnimData> table = Table(a);

            MChar c1 = new MChar { AnimNo = 0 };
            MChar c2 = new MChar { AnimNo = 0 };
            MAnimSystem.Action(c1, table);
            MAnimSystem.Action(c2, table);
            Assert.That(HashOf(c1), Is.EqualTo(HashOf(c2)), "同样推进哈希相同");

            MChar clone = c1.Clone();
            MAnimSystem.Action(c1, table);   // 仅推进原对象
            Assert.That(HashOf(clone), Is.Not.EqualTo(HashOf(c1)), "克隆独立：原对象推进不影响克隆");
            Assert.That(clone.AnimElem, Is.Not.EqualTo(c1.AnimElem).Or.EqualTo(clone.AnimElem));
        }

        static ulong HashOf(MChar c)
        {
            Hash64 h = new Hash64();
            c.WriteHash(ref h);
            return h.Value;
        }
    }
}
