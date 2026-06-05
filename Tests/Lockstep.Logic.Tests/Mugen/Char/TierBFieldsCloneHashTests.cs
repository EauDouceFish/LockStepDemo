using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Char
{
    /// <summary>
    /// Tier B 控制器运行态字段（Pause/SuperPause/PosFreeze/Width/PlayerPush/ScreenBound/MoveContactTime/HitOverride）
    /// 的 Clone 深拷 + WriteHash 覆盖回归。这些影响模拟 → 必须进快照/哈希，否则回滚串状态。
    /// </summary>
    [TestFixture]
    public sealed class TierBFieldsCloneHashTests
    {
        static MChar MakePopulated()
        {
            MChar c = new MChar
            {
                PauseMovetime = 2, SuperMovetime = 10, UnhittableTime = 7,
                PauseBool = true, Acttmp = -2,
                PosFreeze = true,
                WidthPlayerFront = FFloat.FromInt(20), WidthPlayerBack = FFloat.FromInt(18),
                WidthEdgeFront = FFloat.FromInt(25), WidthEdgeBack = FFloat.FromInt(22),
                PlayerPushEnabled = false, PushPriority = 3, PushAffectTeam = 1,
                ScreenBoundEnabled = true, ScreenBoundMoveCameraX = true,
                ScreenBoundMoveCameraY = false, ScreenBoundStageBound = true,
                MoveContactTime = 7, CounterHit = true,
            };
            c.HitOverrides[0] = new MHitOverride { Attr = 0x12, StateNo = 1300, Time = 60, ForceAir = true };
            c.HitOverrides[3] = new MHitOverride { Attr = 0x04, StateNo = 1310, Time = -1, KeepState = true };
            return c;
        }

        static ulong HashOf(MChar c)
        {
            Hash64 h = new Hash64();
            c.WriteHash(ref h);
            return h.Value;
        }

        [Test]
        public void Clone_CopiesAllTierBFields()
        {
            MChar c = MakePopulated();
            MChar clone = c.Clone();
            Assert.That(clone.PauseMovetime, Is.EqualTo(2));
            Assert.That(clone.SuperMovetime, Is.EqualTo(10));
            Assert.That(clone.UnhittableTime, Is.EqualTo(7));
            Assert.That(clone.PauseBool, Is.True);
            Assert.That(clone.PosFreeze, Is.True);
            Assert.That(clone.WidthPlayerFront.Raw, Is.EqualTo(FFloat.FromInt(20).Raw));
            Assert.That(clone.PlayerPushEnabled, Is.False);
            Assert.That(clone.PushPriority, Is.EqualTo(3));
            Assert.That(clone.ScreenBoundStageBound, Is.True);
            Assert.That(clone.MoveContactTime, Is.EqualTo(7));
            Assert.That(clone.CounterHit, Is.True);
            Assert.That(clone.HitOverrides[0].StateNo, Is.EqualTo(1300));
            Assert.That(clone.HitOverrides[3].Time, Is.EqualTo(-1));
        }

        [Test]
        public void Clone_HitOverridesAreDeepCopied()
        {
            MChar c = MakePopulated();
            MChar clone = c.Clone();
            clone.HitOverrides[0].StateNo = 9999;   // 改克隆不应影响原件（值类型数组深拷）
            Assert.That(c.HitOverrides[0].StateNo, Is.EqualTo(1300), "克隆的 HitOverride 修改不回写原件");
        }

        [Test]
        public void Hash_CloneEqualsOriginal()
        {
            MChar c = MakePopulated();
            Assert.That(HashOf(c.Clone()), Is.EqualTo(HashOf(c)), "Clone 后哈希一致");
        }

        [Test]
        public void Hash_ChangesWhenTierBFieldChanges()
        {
            MChar a = MakePopulated();
            MChar b = MakePopulated();
            b.SuperMovetime = 31;   // 改一个 sim 字段
            Assert.That(HashOf(b), Is.Not.EqualTo(HashOf(a)), "SuperMovetime 变化必须改变哈希");
        }

        [Test]
        public void Hash_ChangesWhenHitOverrideChanges()
        {
            MChar a = MakePopulated();
            MChar b = MakePopulated();
            b.HitOverrides[0].Time = 59;
            Assert.That(HashOf(b), Is.Not.EqualTo(HashOf(a)), "HitOverride 变化必须改变哈希");
        }
    }
}
