using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
	[SerializeField] private List<ParticleSystem> _vfxs = new List<ParticleSystem>();
    
	private BulletData _bulletData;
	
    private Rigidbody2D _rigidbody;
    
    private bool _shouldDestroy = true;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }
    
    public void Init(BulletData bulletData)
    {
	    _bulletData = bulletData;
	    
	    _bulletData.Init(this);
	    StartCoroutine(SelfDestroyDelayed());
    }
    
    public void Launch(Vector3 direction, float force)
    {
	    _rigidbody.velocity = direction * force;
	    _bulletData.OnLaunch(direction, force);
	    
    }
    
    public void ChangeDirection(Vector3 direction)
    {
	    var force = _rigidbody.velocity.magnitude;
	    
	    _rigidbody.velocity = direction * force;
	    _bulletData.OnLaunch(direction, force);
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
	    if (!other.TryGetComponent<Enemy>(out var enemy)) 
		    return;
	    
	    // переедет 👇
	    _bulletData.OnHitEnemy(enemy, ref _shouldDestroy);
	    OnHit();
	    enemy.TakeDamage();
    }
    
    private void OnHit()
    {
	    if (!_shouldDestroy)
		    return;

	    SelfDestroy();
    }
    
    private void SelfDestroy()
    {
	    if (_vfxs != null)
	    {
		    foreach (var vfx in _vfxs)
		    {
			    vfx.transform.SetParent(null);
			    vfx.Stop();
		    
			    Destroy(vfx.gameObject, vfx.main.duration);
		    }
	    }

	    Destroy(gameObject);
    }

	private IEnumerator SelfDestroyDelayed()
	{
		yield return new WaitForSeconds(3f);
		SelfDestroy();
	}
}