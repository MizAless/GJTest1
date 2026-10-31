using UnityEngine;

// Last-resort stereo-linked peak protection on the listener, not a loudness boost.
// The audio thread uses only the supplied buffer and local state; no allocations or Unity objects.
public class AudioSafetyLimiter : MonoBehaviour
{
    private float _gain = 1;

    private void OnAudioFilterRead(float[] data, int channels)
    {
        for (int frame = 0; frame < data.Length; frame += channels)
        {
            float peak = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(data[frame + channel]));
            }

            float target = peak > 0.88f ? 0.88f / peak : 1;
            if (target < _gain)
            {
                _gain = target;
            }
            else
            {
                _gain = Mathf.Lerp(_gain, target, 0.0004f);
            }

            for (int channel = 0; channel < channels; channel++)
            {
                data[frame + channel] *= _gain;
            }
        }
    }
}
