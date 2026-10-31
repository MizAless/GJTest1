using UnityEngine;

// Place on any future upgrade panel; the theme also recognizes the existing reward panel roots.
public class UpgradeTextMotionScope : MonoBehaviour
{
    [Min(0)] public float Amplitude = 1.5f;
    [Min(0)] public float Speed = 2.3f;
    [Min(0)] public float PhasePerGlyph = 0.55f;
}
