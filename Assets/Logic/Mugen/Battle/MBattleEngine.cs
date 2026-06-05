// 战斗引擎装配器：把已移植的子系统按每帧顺序串成 live pipeline（M9 整合的核心）。
// 顺序对齐 CLAUDE §8.3：命令 → 状态机 → 物理 → 动画(M8) → 命中。无静态可变态：
// 全部运行态在 MChar 上，ComputeHash 覆盖全角色 → 接快照/回滚(M10)。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.Battle
{
    /// <summary>
    /// 一场对局的运行期：N 个角色 + 各自配置，每 tick 推进一帧。
    /// 物理目前为最小积分（pos += vel，X 乘朝向）——重力/摩擦/地面夹取属 M9 物理完善项，已标注。
    /// </summary>
    public sealed class MBattleEngine
    {
        public readonly List<MChar> Chars = new List<MChar>();
        public readonly List<MCharData> Data = new List<MCharData>();

        readonly MStateMachine _stateMachine = new MStateMachine();

        // 确定性随机源（= Ikemen 单一全局 sys.randseed）：全场角色共享，random trigger 推进它。
        // 固定默认种子保证回放/双端逐位一致；联机时由对局配置同步初始种子。模拟状态 → 纳入 ComputeHash。
        public readonly MRandom Random = new MRandom(DefaultRandomSeed);
        const int DefaultRandomSeed = 1;

        public void Add(MChar c, MCharData data)
        {
            c.Rng = Random;   // 接入共享随机源（对齐 Ikemen 全局种子）
            Chars.Add(c);
            Data.Add(data);
        }

        /// <summary>1v1：互设 P2/Root，便于 redirect(p2) 与命中。</summary>
        public void LinkPair()
        {
            for (int i = 0; i < Chars.Count; i++)
            {
                Chars[i].Root = Chars[i];
                Chars[i].P2 = Chars.Count >= 2 ? Chars[1 - i] : null;
            }
        }

        /// <summary>
        /// 回合开始：给玩家控制角色授予按键控制权 + ctrl（faithful shim，对应 Ikemen RoundState 进入战斗活动期）。
        /// 完整回合状态机（intro/5900/fight/KO）后置；此处只把"该读键且有控制权"的初始态摆正，
        /// 否则直接 spawn 进 state 0 的角色 ctrl=false → 引擎硬编码基础动作（走/跳/蹲）全被挡。
        /// </summary>
        public void StartRound()
        {
            for (int i = 0; i < Chars.Count; i++)
            {
                Chars[i].KeyCtrl = true;
                Chars[i].Ctrl = true;
            }
        }

        /// <summary>推进一帧。inputs[i] 对应 Chars[i] 本帧输入。
        /// 顺序对齐 Ikemen 每帧：输入缓冲 → actionPrepare(硬编码基础动作) → actionRun(状态机) → update(物理/动画) → 命中。</summary>
        public void Tick(IReadOnlyList<MInput> inputs)
        {
            // 1) 输入缓冲：命令匹配环形缓冲（搓招）+ 边沿计数缓冲（引擎硬编码键读 Fb/Bb/Ub/Db）。
            for (int i = 0; i < Chars.Count; i++)
            {
                MChar c = Chars[i];
                MInput input = inputs != null && i < inputs.Count ? inputs[i] : MInput.None;
                bool facingRight = c.Facing.Raw >= 0;
                if (c.CommandList != null)
                {
                    c.CommandList.Update(input, facingRight);
                }
                if (c.Input != null)
                {
                    c.Input.Update(input, facingRight);
                }
            }

            // 2) actionPrepare：引擎硬编码基础动作（站/走/蹲/跳/刹车的状态转移，缓冲到 PendingStateNo）。
            for (int i = 0; i < Chars.Count; i++)
            {
                MActionSystem.Prepare(Chars[i]);
            }

            // 3) 状态机（应用 Pending 切换、负状态/当前状态控制器、ChangeState 同帧重入）
            for (int i = 0; i < Chars.Count; i++)
            {
                _stateMachine.RunFrame(Chars[i], Data[i].States, Data[i].CommonStates);
            }

            // 4) 物理（位置积分 + 摩擦/重力，移植 Ikemen posUpdate）+ 空中落地检测（硬编码 → 状态 52）
            for (int i = 0; i < Chars.Count; i++)
            {
                MPhysics.Step(Chars[i]);
                MActionSystem.LandCheck(Chars[i]);
            }

            // 5) 动画推进（M8）+ 派生 Clsn
            for (int i = 0; i < Chars.Count; i++)
            {
                MAnimSystem.Action(Chars[i], Data[i].Anims);
            }

            // 6) 命中（1v1：双向尝试，TryHit 内部做 hitflag/重叠/同招一次判定）
            if (Chars.Count == 2)
            {
                MHitSystem.TryHit(Chars[0], Chars[1]);
                MHitSystem.TryHit(Chars[1], Chars[0]);
            }
        }

        /// <summary>全角色哈希（确定性对账/黄金哈希用）。</summary>
        public ulong ComputeHash()
        {
            Hash64 hash = new Hash64();
            hash.AddInt32(Chars.Count);
            for (int i = 0; i < Chars.Count; i++)
            {
                Chars[i].WriteHash(ref hash);
            }
            hash.AddInt32(Random.Seed);   // 共享随机源种子：模拟状态，全场混入一次（不在 per-char 哈希以免重复计数）
            return hash.Value;
        }
    }
}
