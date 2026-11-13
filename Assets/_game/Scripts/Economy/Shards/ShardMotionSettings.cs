using UnityEngine;

namespace GameEconomy.Shards
{
    [CreateAssetMenu(menuName = "Prototype/Shards/Motion settings")]
    public sealed class ShardMotionSettings : ScriptableObject
    {
        [Min(0)] public float HoverSeconds = 1;
        [Min(0)] public float HoverHeight = 0.65f;
        [Min(0)] public float BobHeight = 0.1f;
        [Min(0)] public float BobFrequency = 2;
        [Min(0.1f)] public float FlightSeconds = 0.85f;
        [Min(0)] public float ArcHeight = 2.5f;
        [Min(0)] public float ArcSide = 1.2f;
        public AnimationCurve FlightProgress = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Min(0)] public float SpinDegrees = 70;
        [Min(0.01f)] public float GroundOffset = 0.025f;
    }
}
