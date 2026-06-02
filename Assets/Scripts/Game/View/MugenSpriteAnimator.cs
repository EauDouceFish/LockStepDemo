using System.Collections.Generic;
using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Game.Anim;

namespace Lockstep.View
{
    /// <summary>
    /// 表现层动画器：用已测的 <see cref="AnimAdvance"/> 推进，按 (group,image) 取 Sprite 显示。
    /// Phase 1 用固定计时驱动（还没逻辑帧）；Phase 2 接逻辑帧后改由 AnimC 驱动。
    /// 逻辑层禁 Unity，此类在表现层，可用 UnityEngine。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MugenSpriteAnimator : MonoBehaviour
    {
        public float TicksPerSecond = 60f;

        SpriteRenderer _renderer;
        AnimData _anim;
        Dictionary<long, Sprite> _sprites;
        int _frameIndex;
        int _elemTime;
        int _animTime;
        float _accumulator;

        /// <summary>当前显示帧（供 ClsnGizmo 读取画判定框）。</summary>
        public AnimFrame CurrentDisplayFrame { get; private set; }

        /// <summary>朝向：+1 右、-1 左。</summary>
        public int Facing = 1;

        public static long Key(int group, int image)
        {
            return ((long)group << 32) | (uint)image;
        }

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Play(AnimData anim, Dictionary<long, Sprite> sprites)
        {
            _anim = anim;
            _sprites = sprites;
            _frameIndex = 0;
            _elemTime = 0;
            _animTime = 0;
            _accumulator = 0f;
            Apply();
        }

        void Update()
        {
            if (_anim == null)
            {
                return;
            }
            _accumulator += Time.deltaTime * TicksPerSecond;
            while (_accumulator >= 1f)
            {
                _accumulator -= 1f;
                AnimAdvance.Step(_anim, ref _frameIndex, ref _elemTime, ref _animTime);
                Apply();
            }
        }

        void Apply()
        {
            AnimFrame frame = AnimAdvance.CurrentFrame(_anim, _frameIndex);
            CurrentDisplayFrame = frame;
            if (frame == null || _sprites == null)
            {
                return;
            }
            if (_sprites.TryGetValue(Key(frame.SpriteGroup, frame.SpriteImage), out Sprite sprite))
            {
                _renderer.sprite = sprite;
            }
            _renderer.flipX = Facing < 0;
        }
    }
}
