using System;
using System.Collections.Generic;
using System.Text;

namespace Lockstep.Core
{
    /// <summary>
    /// 实体 = 一个 Id + 一组组件。用 Dictionary&lt;Type&gt; 存，2 实体规模够用；
    /// 将来实体多了再考虑换数组 / 原型。
    /// </summary>
    public class Entity
    {
        public readonly int Id;
        readonly Dictionary<Type, IComponent> _components = new Dictionary<Type, IComponent>();

        public Entity(int id) { Id = id; }

        public void Add<T>(T component) where T : class, IComponent
        {
            _components[typeof(T)] = component;
        }

        public T Get<T>() where T : class, IComponent
        {
            return _components.TryGetValue(typeof(T), out var c) ? (T)c : null;
        }

        public bool Has<T>() where T : class, IComponent
        {
            return _components.ContainsKey(typeof(T));
        }

        /// <summary>深拷贝整个实体 —— 快照用。</summary>
        public Entity Clone()
        {
            var e = new Entity(Id);
            foreach (var kv in _components)
                e._components[kv.Key] = kv.Value.Clone();
            return e;
        }

        /// <summary>
        /// 把实体状态拼进 sb。组件按类型名排序 ——
        /// Dictionary 遍历顺序不保证跨平台/跨运行时一致，排序后才能用于比对。
        /// </summary>
        public void Fingerprint(StringBuilder sb)
        {
            var keys = new List<Type>(_components.Keys);
            keys.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            sb.Append('E').Append(Id).Append('{');
            for (int i = 0; i < keys.Count; i++)
                sb.Append(_components[keys[i]]).Append(';');
            sb.Append('}');
        }

        /// <summary>
        /// 把实体（Id + 全部组件）混入哈希。组件按类型名排序，理由同 Fingerprint：
        /// Dictionary 遍历顺序不稳定，必须先排序才能跨端得到一致的哈希。
        /// 类型名也一并混入，避免"字段值相同但组件类型不同"撞哈希。
        /// </summary>
        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Id);
            List<Type> keys = new List<Type>(_components.Keys);
            keys.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            for (int index = 0; index < keys.Count; index++)
            {
                hash.AddString(keys[index].Name);
                _components[keys[index]].WriteHash(ref hash);
            }
        }
    }
}
