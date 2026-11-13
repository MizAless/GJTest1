using UnityEngine;

namespace GameEconomy.Shards
{
    public sealed class ShardPickup : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _shadow;
        [SerializeField] private GameObject[] _qualityMarks;
        private ShardMotionSettings _motion;
        private Vector3 _ground;
        private Vector3 _flightStart;
        private float _age;
        private float _phase;
        private float _side;
        private float _size;
        public ShardId Shard { get; private set; }
        public bool Arrived { get; private set; }

        public void Initialize(ShardId shard, Vector3 ground, ShardMotionSettings motion)
        {
            Shard = shard;
            _motion = motion;
            _ground = ground;
            _phase = Random.value * Mathf.PI * 2;
            _side = Random.value < 0.5f ? -1 : 1;
            _size = 1f + (3 - (int)shard.Grade) * 0.2f;
            _body.localScale *= _size;
            int markCount = 4 - (int)shard.Grade;
            for (int index = 0; index < _qualityMarks.Length; index++)
            {
                _qualityMarks[index].SetActive(index < markCount);
            }
            transform.position = ground;
            _flightStart = ground + Vector3.up * motion.HoverHeight;
            _body.position = _flightStart;
            _shadow.position = ground + Vector3.up * motion.GroundOffset;
        }

        public void Advance(float delta, Vector3 target)
        {
            if (Arrived || delta <= 0)
            {
                return;
            }
            _age += delta;
            float flightAge = Mathf.Max(0, _age - _motion.HoverSeconds);
            float normalized = Mathf.Clamp01(flightAge / Mathf.Max(0.1f, _motion.FlightSeconds));
            Vector3 position;
            if (_age < _motion.HoverSeconds)
            {
                float bob = Mathf.Sin(_age * _motion.BobFrequency * Mathf.PI * 2 + _phase) * _motion.BobHeight;
                position = _ground + Vector3.up * (_motion.HoverHeight + bob);
                _flightStart = position;
            }
            else
            {
                // The squared clock starts gently and accelerates into the collection target.
                float progress = Mathf.Clamp01(_motion.FlightProgress.Evaluate(normalized * normalized));
                Vector3 offset = target - _flightStart;
                Vector3 side = Vector3.Cross(Vector3.up, offset).normalized * _side;
                Vector3 arc = Vector3.up * _motion.ArcHeight + side * _motion.ArcSide;
                position = Vector3.Lerp(_flightStart, target, progress) + arc * Mathf.Sin(progress * Mathf.PI);
            }
            _body.position = position;
            _body.Rotate(Vector3.up, _motion.SpinDegrees * delta, Space.World);
            _shadow.position = new Vector3(position.x, _ground.y + _motion.GroundOffset, position.z);
            float shadowSize = Mathf.Lerp(0.9f, 0.2f, normalized) * _size;
            _shadow.localScale = new Vector3(shadowSize, shadowSize, 1);
            Arrived = normalized >= 1;
        }
    }
}
