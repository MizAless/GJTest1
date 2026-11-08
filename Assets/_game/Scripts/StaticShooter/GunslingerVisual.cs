using UnityEngine;

public sealed class GunslingerVisual : MonoBehaviour
{
    [SerializeField] private Transform _weaponSocket;
    public Transform WeaponSocket => _weaponSocket;
}
