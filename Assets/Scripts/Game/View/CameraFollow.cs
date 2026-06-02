using UnityEngine;

namespace Lockstep.Game.View
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Wiring")]
        public BattleScene BattleScene;

        [Header("Behavior")]
        public bool ClampInBounds = true;
        public float SmoothTime = 0.15f;
        public float ZOffset = -10f;

        Transform _target;
        Vector3 _vel;
        Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (BattleScene != null)
                BattleScene.OnLocalPlayerSpawned += SetTarget;
        }

        public void SetTarget(Transform t)
        {
            _target = t;
        }

        void LateUpdate()
        {
            if (_target == null) return;

            float gx = _target.position.x;
            float gy = _target.position.y;

            if (ClampInBounds && BattleScene != null)
            {
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;
                float maxX = Mathf.Max(0f, BattleScene.MapHalfWidth - halfW);
                float maxY = Mathf.Max(0f, BattleScene.MapHalfHeight - halfH);
                gx = Mathf.Clamp(gx, -maxX, maxX);
                gy = Mathf.Clamp(gy, -maxY, maxY);
            }

            var goal = new Vector3(gx, gy, ZOffset);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref _vel, SmoothTime);
        }
    }
}
