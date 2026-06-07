// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go Projectile struct + bytecode.go Projectile StateController + projectile update。
// 轻量弹幕实体（非 MChar，无完整状态机）：自带运动(vel/accel) + HitDef + 生命周期(removetime/越界/命中移除)。
// Adapted to fixed-point lockstep. See Docs/移植方案_Ikemen.md.
using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// 弹幕实体。引擎每帧推进：vel += accel；pos += vel(X 乘 facing)；Time++；RemoveTime 倒计时到 0 移除。
    /// 命中归切片 3b（HitDef + Clsn vs 敌方）。numproj/projcontact/projhit 触发器据此计数。
    /// </summary>
    public sealed class MProjectile
    {
        public int Id;              // 实体 id（World.AllocId）
        public int OwnerId;         // 发射者 id
        public int ProjId;          // 弹幕 id（numproj(projid)/projcontact(projid) 匹配）
        public FVector3 Pos;
        public FVector3 Vel;
        public FVector3 Accel;
        public FFloat Facing = FFloat.One;
        public int AnimNo;
        public int RemoveTime = -1; // -1=永久；>=0 倒计时，到 0 移除
        public int Time;            // 存活帧数
        public bool Removed;        // 待移除标记
        public int HitCount;        // 已命中次数（projhit）
        public int ContactCount;    // 已接触次数（projcontact）

        // 命中（切片 3b）：自带 HitDef + 攻击框（来自 projanim）。Owner 供 ApplyHit 取攻方倍率/能量（结构引用，不哈希）。
        public Hit.MHitDef HitDef;
        public Hit.MClsnBox[] Clsn1;
        public MChar Owner;
        public bool HitDone;        // 已命中（单段弹幕命中即移除）

        /// <summary>推进一帧（移植 char.go projectile update 运动部分）。</summary>
        public void Step()
        {
            Vel = new FVector3(Vel.X + Accel.X, Vel.Y + Accel.Y, FFloat.Zero);
            FFloat facingSign = Facing.Raw >= 0 ? FFloat.One : -FFloat.One;
            Pos = new FVector3(Pos.X + Vel.X * facingSign, Pos.Y + Vel.Y, FFloat.Zero);
            Time++;
            if (RemoveTime >= 0)
            {
                RemoveTime--;
                if (RemoveTime < 0) { Removed = true; }
            }
        }

        public MProjectile Clone()
        {
            return new MProjectile
            {
                Id = Id, OwnerId = OwnerId, ProjId = ProjId,
                Pos = Pos, Vel = Vel, Accel = Accel, Facing = Facing,
                AnimNo = AnimNo, RemoveTime = RemoveTime, Time = Time, Removed = Removed,
                HitCount = HitCount, ContactCount = ContactCount,
                HitDef = HitDef != null ? HitDef.Clone() : null,
                Clsn1 = Clsn1, Owner = Owner, HitDone = HitDone,   // Clsn1 帧派生浅引用；Owner 结构引用待重链
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Id); hash.AddInt32(OwnerId); hash.AddInt32(ProjId);
            hash.AddInt32(Owner != null ? Owner.Id : -1);
            hash.AddFixed(Pos); hash.AddFixed(Vel); hash.AddFixed(Accel); hash.AddFixed(Facing);
            hash.AddInt32(AnimNo); hash.AddInt32(RemoveTime); hash.AddInt32(Time);
            hash.AddBool(Removed); hash.AddInt32(HitCount); hash.AddInt32(ContactCount);
            hash.AddBool(HitDone);
            hash.AddBool(HitDef != null);
            if (HitDef != null)
            {
                HitDef.WriteHash(ref hash);
            }
            WriteClsn(ref hash, Clsn1);
        }

        static void WriteClsn(ref Hash64 hash, Hit.MClsnBox[] boxes)
        {
            hash.AddInt32(boxes != null ? boxes.Length : -1);
            if (boxes == null) { return; }
            for (int index = 0; index < boxes.Length; index++)
            {
                hash.AddFixed(boxes[index].X1);
                hash.AddFixed(boxes[index].Y1);
                hash.AddFixed(boxes[index].X2);
                hash.AddFixed(boxes[index].Y2);
            }
        }
    }
}
