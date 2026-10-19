using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class PrototypeSpriteAnimation : MonoBehaviour
{
    [SerializeField] private Sprite[] _frames;
    [SerializeField] private float _framesPerSecond = 8f;
    [SerializeField] private bool _faceMovement;
    [SerializeField] private bool _faceNearestEnemy;
    private SpriteRenderer _renderer;
    private Vector3 _previousPosition;
    private Shooter _shooter;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _shooter = GetComponent<Shooter>();
        _previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (_frames != null && _frames.Length > 0)
            _renderer.sprite = _frames[Mathf.FloorToInt(Time.time * _framesPerSecond) % _frames.Length];

        float direction = transform.position.x - _previousPosition.x;
        if (_faceNearestEnemy && _shooter != null)
        {
            var target = _shooter.GetNearestEnemy();
            if (target != null)
                direction = target.transform.position.x - transform.position.x;
        }
        if ((_faceMovement || _faceNearestEnemy) && Mathf.Abs(direction) > 0.001f)
            _renderer.flipX = direction < 0f;
        _previousPosition = transform.position;
    }
}
