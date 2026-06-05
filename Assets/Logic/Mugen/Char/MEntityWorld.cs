// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go Helper/DestroySelf + system.go charList（实体注册/生命周期）。
// 去全局单例：把"谁能 spawn 实体"做成引擎持有、角色共享引用的注册表。控制器请求 spawn，引擎每帧 drain。
// Adapted to fixed-point lockstep. See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Mugen.Char
{
    /// <summary>helper spawn 请求（控制器入队，引擎 DrainSpawns 时据此造 helper 实体）。</summary>
    public struct MHelperRequest
    {
        public MChar Owner;     // 创建者（helper 的 parent）
        public int StateNo;     // helper 初始状态号
        public int HelperType;  // helper id（= Helper 控制器 id 参数；ishelper(id)/numhelper(id) 用）
        public int PosType;     // postype（0=p1 相对，1=p2，2=front，3=back，4=left，5=right；v1 仅 p1 相对）
        public FFloat PosX;     // 相对偏移
        public FFloat PosY;
        public int Facing;      // 1/-1（相对 owner 朝向）
        public bool KeyCtrl;    // 是否受键控（多数 helper 否）
    }

    /// <summary>
    /// 实体世界（共享）：helper/projectile/explod 的 spawn 队列 + 确定性实体 id 计数。引擎持单例、各角色共享引用。
    /// 实体列表本身存在引擎（MBattleEngine.Helpers）；本对象只承载"跨控制器→引擎"的请求通道 + id 分配。
    /// </summary>
    public sealed class MEntityWorld
    {
        public readonly List<MHelperRequest> SpawnQueue = new List<MHelperRequest>();

        // 当前存活 helper 实体（引擎维护；char 经共享 World 数 numhelper/ishelper）。与引擎 _helperData 平行。
        public readonly List<MChar> Helpers = new List<MChar>();

        // 实体 id 分配（确定性递增，模拟状态 → 入哈希）。玩家用 0/1，helper/proj/explod 从此起。
        public int NextEntityId = 1000;

        public int AllocId()
        {
            return NextEntityId++;
        }

        public void RequestHelper(MHelperRequest request)
        {
            SpawnQueue.Add(request);
        }

        /// <summary>helper 计数（numhelper trigger）。helperType&lt;0 表全部，否则按 type 计。</summary>
        public int CountHelpers(int helperType)
        {
            if (helperType < 0) { return Helpers.Count; }
            int n = 0;
            for (int i = 0; i < Helpers.Count; i++)
            {
                if (Helpers[i].HelperType == helperType) { n++; }
            }
            return n;
        }

        public MEntityWorld Clone()
        {
            MEntityWorld world = new MEntityWorld { NextEntityId = NextEntityId };
            // SpawnQueue 在每帧 DrainSpawns 后清空，帧边界通常为空；为安全仍浅拷请求（Owner 引用待引擎级重链）。
            world.SpawnQueue.AddRange(SpawnQueue);
            return world;
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(NextEntityId);
            hash.AddInt32(SpawnQueue.Count);
        }
    }
}
