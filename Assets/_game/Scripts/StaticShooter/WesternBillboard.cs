using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class WesternBillboard : MonoBehaviour
{
    [SerializeField] private bool _faceCamera = true;
    [SerializeField] private bool _groundShadow = true;
    [SerializeField, Range(0, 1)] private float _shadowDepth = 0.2f;
    private Camera _camera;

    private void Start()
    {
        _camera = Camera.main;
        if (_groundShadow)
        {
            Sprite sprite = GetComponent<SpriteRenderer>().sprite;
            SpriteRenderer shadow = SoftGroundShadow.Create(transform, Vector3.up * 0.02f,
                new Vector2(sprite.bounds.size.x * 0.8f, sprite.bounds.size.y * _shadowDepth), -250);
            shadow.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
    }

    private void LateUpdate()
    {
        if (!_faceCamera || _camera == null)
        {
            return;
        }
        Vector3 awayFromCamera = transform.position - _camera.transform.position;
        awayFromCamera.y = 0;
        if (awayFromCamera.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(awayFromCamera, Vector3.up);
        }
    }
}
