// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/compiler.go and src/bytecode.go entity/projectile trigger opcodes.
// Tests parameter-form entity triggers in the fixed-point lockstep VM.
using System.Collections.Generic;
using System.IO;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Battle;
using Lockstep.Tests;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Expr
{
    /// <summary>
    /// Parameter-form entity triggers: no argument emits -1, otherwise the provided id is consumed.
    /// </summary>
    [TestFixture]
    public sealed class EntityParamTriggerTests
    {
        static MBattleEngine TwoKfm()
        {
            string kfmDir = TestAssets.KfmDir();
            string common = TestAssets.Common1Cns();
            string cns = Path.Combine(kfmDir, "kfm.cns");
            string air = Path.Combine(kfmDir, "kfm.air");
            string cmd = Path.Combine(kfmDir, "kfm.cmd");
            if (!File.Exists(cns) || !File.Exists(air) || !File.Exists(common))
            {
                Assert.Ignore("KFM/common1 assets are missing.");
            }
            MCharData data = MCharLoader.Load(
                new[] { File.ReadAllText(cns) }, File.ReadAllText(cns),
                File.ReadAllText(common), File.ReadAllText(air),
                File.Exists(cmd) ? File.ReadAllText(cmd) : null, "kfm");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 1), data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static int Eval(string expr, MChar c)
        {
            return new MugenExprCompiler().Compile(expr).Run(c).ToI();
        }

        static ulong HashOf(MChar c)
        {
            Hash64 hash = new Hash64();
            c.WriteHash(ref hash);
            return hash.Value;
        }

        [Test]
        public void NumHelper_ById_CountsOnlyMatchingType()
        {
            MBattleEngine engine = TwoKfm();
            MChar owner = engine.Chars[0];
            MChar otherOwner = engine.Chars[1];
            owner.RequestHelper(0, helperType: 5, FFloat.Zero, FFloat.Zero, 1, false);
            owner.RequestHelper(0, helperType: 9, FFloat.Zero, FFloat.Zero, 1, false);
            otherOwner.RequestHelper(0, helperType: 5, FFloat.Zero, FFloat.Zero, 1, false);
            engine.Tick(new List<MInput> { MInput.None, MInput.None });

            Assert.That(Eval("numhelper", owner), Is.EqualTo(2), "numhelper without id counts all helpers");
            Assert.That(Eval("numhelper(5)", owner), Is.EqualTo(1), "numhelper(5) counts helper type 5");
            Assert.That(Eval("numhelper(9)", owner), Is.EqualTo(1));
            Assert.That(Eval("numhelper(3)", owner), Is.EqualTo(0), "helper type 3 is absent");
        }

        [Test]
        public void NumProj_ById_CountsOnlyMatchingProjectiles()
        {
            MBattleEngine engine = TwoKfm();
            MChar owner = engine.Chars[0];
            MChar otherOwner = engine.Chars[1];
            owner.RequestProjectile(3, FFloat.Zero, FFloat.Zero, FFloat.Zero, FFloat.Zero,
                FFloat.Zero, FFloat.Zero, 20, 0, null);
            owner.RequestProjectile(5, FFloat.Zero, FFloat.Zero, FFloat.Zero, FFloat.Zero,
                FFloat.Zero, FFloat.Zero, 20, 0, null);
            owner.RequestProjectile(5, FFloat.Zero, FFloat.Zero, FFloat.Zero, FFloat.Zero,
                FFloat.Zero, FFloat.Zero, 20, 0, null);
            otherOwner.RequestProjectile(5, FFloat.Zero, FFloat.Zero, FFloat.Zero, FFloat.Zero,
                FFloat.Zero, FFloat.Zero, 20, 0, null);
            engine.Tick(new List<MInput> { MInput.None, MInput.None });

            Assert.That(Eval("numproj", owner), Is.EqualTo(3));
            Assert.That(Eval("numproj(3)", owner), Is.EqualTo(1));
            Assert.That(Eval("numproj(5)", owner), Is.EqualTo(2));
            Assert.That(Eval("numproj(9)", owner), Is.EqualTo(0));
        }

        [Test]
        public void IsHelper_ById_MatchesType()
        {
            MBattleEngine engine = TwoKfm();
            engine.Chars[0].RequestHelper(0, helperType: 7, FFloat.Zero, FFloat.Zero, 1, false);
            engine.Tick(new List<MInput> { MInput.None, MInput.None });
            MChar helper = engine.Helpers[0];
            Assert.That(Eval("ishelper", helper), Is.EqualTo(1), "ishelper without id detects helpers");
            Assert.That(Eval("ishelper(7)", helper), Is.EqualTo(1), "ishelper(7) matches helper type 7");
            Assert.That(Eval("ishelper(3)", helper), Is.EqualTo(0), "helper type does not match");
            Assert.That(Eval("ishelper(7)", engine.Chars[0]), Is.EqualTo(0), "root player is not a helper");
        }

        [Test]
        public void ProjectileContactTime_ById_MatchesLastProjectileContact()
        {
            MChar owner = new MChar { ProjectileContactId = 3, ProjectileContactType = 0, ProjectileContactTime = 0 };

            Assert.That(Eval("projhittime(3)", owner), Is.EqualTo(0));
            Assert.That(Eval("projcontacttime(3)", owner), Is.EqualTo(0));
            Assert.That(Eval("projguardedtime(3)", owner), Is.EqualTo(-1));
            Assert.That(Eval("projhittime(7)", owner), Is.EqualTo(-1));
            Assert.That(Eval("projhit(3)", owner), Is.EqualTo(1));
            Assert.That(Eval("projcontact(3)", owner), Is.EqualTo(1));
            Assert.That(Eval("projguarded(3)", owner), Is.EqualTo(0));

            owner.ProjectileContactType = 1;
            owner.ProjectileContactTime = 4;

            Assert.That(Eval("projcontacttime(3)", owner), Is.EqualTo(4));
            Assert.That(Eval("projguardedtime(3)", owner), Is.EqualTo(4));
            Assert.That(Eval("projhittime(3)", owner), Is.EqualTo(-1));
            Assert.That(Eval("projguarded(3)", owner), Is.EqualTo(1));
        }

        [Test]
        public void ProjectileContactState_IsClonedHashedAndTicked()
        {
            MChar owner = new MChar { ProjectileContactId = 8, ProjectileContactType = 0, ProjectileContactTime = 2 };
            MChar clone = owner.Clone();
            Assert.That(clone.ProjectileContactId, Is.EqualTo(8));
            Assert.That(clone.ProjectileContactType, Is.EqualTo(0));
            Assert.That(clone.ProjectileContactTime, Is.EqualTo(2));

            MChar changed = owner.Clone();
            changed.ProjectileContactTime = 3;
            Assert.That(HashOf(changed), Is.Not.EqualTo(HashOf(owner)));

            MBattleEngine engine = TwoKfm();
            engine.Chars[0].RecordProjectileContact(8, false);
            Assert.That(Eval("projhittime(8)", engine.Chars[0]), Is.EqualTo(0));
            engine.Tick(new List<MInput> { MInput.None, MInput.None });
            Assert.That(Eval("projhittime(8)", engine.Chars[0]), Is.EqualTo(1));
        }
    }
}
