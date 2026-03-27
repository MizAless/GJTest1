using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    Rigidbody2D _rigidbody;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }
    
    public void Init(Vector3 direction, float force)
    {
        _rigidbody.velocity = direction * force;
        StartCoroutine(SelfDestroy());
    }

	private IEnumerator SelfDestroy()
	{
		yield return new WaitForSeconds(3f);
		Destroy(gameObject);
	}
}