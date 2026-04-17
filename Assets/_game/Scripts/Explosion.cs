using System.Collections;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(SelfDestroyDelayed());
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<Enemy>(out var enemy)) 
            return;
	    
        enemy.TakeDamage();
    }
    
    private IEnumerator SelfDestroyDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        SelfDestroy();
    }
    
    private void SelfDestroy()
    {
        Destroy(gameObject);
    }
}