using System.Collections.Generic;
using System.Text;
using Lockstep.Input;
using Lockstep.Math;

namespace Lockstep.Core
{
    public class World
    {
        public int Frame;
        public int RoomSeed;
        public FRandom Random;
        public List<Entity> Entities = new List<Entity>();
        public List<ISystem> Systems = new List<ISystem>();
        public FrameInput[] CurrentInputs;
        public GameConfigData Config;
        public IGameData GameData;

        int _nextId = 1;

        public Entity CreateEntity()
        {
            var e = new Entity(_nextId++);
            Entities.Add(e);
            return e;
        }

        public void RegisterSystem(ISystem s) => Systems.Add(s);

        public void Init(int seed)
        {
            Frame = 0;
            RoomSeed = seed;
            Random = new FRandom((ulong)seed);
        }

        public void Tick()
        {
            foreach (var s in Systems) s.Tick(this);
            Frame++;
        }

        // ───────────────────────── 快照 / 还原 ─────────────────────────
        //
        // 快照故意只备份"运行时可变态"：Frame / Random.Seed / NextId / Entities。
        // 故意 NOT 备份：
        //   - Systems    : 必须无状态（行为代码，不存可变数据）
        //   - CurrentInputs : 每帧外部喂入，回滚重算时重新喂
        //   - Config     : GameConfigData 开局后不变
        //   - GameData   : IGameData 持有只读招式表/buff 表等静态数据，开局后不变
        // 上面任一打破"只读"约定，就要把它加入快照——否则 desync。
        // ─────────────────────────────────────────────────────────────────

        /// <summary>把当前 World 状态深拷贝成一份快照 —— 回滚的"备份"。</summary>
        public WorldSnapshot Snapshot()
        {
            var snap = new WorldSnapshot
            {
                Frame = Frame,
                RandomSeed = Random.Seed,
                NextId = _nextId,
                Entities = new List<Entity>(Entities.Count),
            };
            for (int i = 0; i < Entities.Count; i++)
                snap.Entities.Add(Entities[i].Clone());
            return snap;
        }

        /// <summary>把 World 还原到某份快照 —— 回滚的"还原"。</summary>
        public void Restore(WorldSnapshot snap)
        {
            Frame = snap.Frame;
            Random.Seed = snap.RandomSeed;
            _nextId = snap.NextId;
            Entities.Clear();
            // 再深拷贝一次：让快照本身保持纯净，可被反复 Restore
            for (int i = 0; i < snap.Entities.Count; i++)
                Entities.Add(snap.Entities[i].Clone());
        }

        /// <summary>把 World 状态拼成可比对的字符串。实体按 Id 排序。</summary>
        public string Fingerprint()
        {
            var sb = new StringBuilder();
            sb.Append('F').Append(Frame).Append("|seed=").Append(Random.Seed).Append('|');
            var list = new List<Entity>(Entities);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < list.Count; i++)
                list[i].Fingerprint(sb);
            return sb.ToString();
        }

        /// <summary>
        /// 把整个 World 状态压成一个 64 位哈希 —— 不同步检测器逐帧比对的就是它。
        /// 覆盖范围必须和 Snapshot 一致：Frame + Random.Seed + NextId + 全部实体（按 Id 排序）。
        /// 故意不含 Systems / CurrentInputs / Config / GameData，理由见上方 Snapshot 注释。
        /// </summary>
        public ulong ComputeHash()
        {
            Hash64 hash = Hash64.Create();
            hash.AddInt32(Frame);
            hash.AddUInt64(Random.Seed);
            hash.AddInt32(_nextId);
            List<Entity> sorted = new List<Entity>(Entities);
            sorted.Sort((left, right) => left.Id.CompareTo(right.Id));
            for (int index = 0; index < sorted.Count; index++)
            {
                sorted[index].WriteHash(ref hash);
            }
            return hash.Value;
        }
    }
}
