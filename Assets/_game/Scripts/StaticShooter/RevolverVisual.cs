using UnityEngine;

public sealed class RevolverVisual : MonoBehaviour
{
    [SerializeField] private Transform _muzzle;
    [SerializeField] private Transform _visual;
    [SerializeField] private ParticleSystem _muzzleFlash;
    [SerializeField, Min(0)] private float _kickDistance = 0.07f;
    private float _recoil;
    public Transform Muzzle => _muzzle;

    public void AimAt(Vector3 target, Vector3 up)
    {
        Vector3 direction = target - transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, up);
        }
    }

    public void UpdatePose(float deltaTime)
    {
        _recoil = Mathf.MoveTowards(_recoil, 0, deltaTime * 10);
        _visual.localPosition = Vector3.back * (_recoil * _kickDistance);
        _visual.localRotation = Quaternion.Euler(-_recoil * 8, 0, 0);
    }

    public void Fire()
    {
        _recoil = 1;
        if (_muzzleFlash != null)
        {
            _muzzleFlash.Play(true);
        }
    }
}
