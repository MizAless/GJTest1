using UnityEngine;

public abstract class BulletModule
{
    protected string _impactVFXPath;
    protected GameObject _impactVFXPrefab;
    
    public Bullet Bullet { get; private set; }
    
    public void SetImpactVFX(GameObject vfxPrefab)
    {
        _impactVFXPrefab = vfxPrefab;
    }

    public virtual void OnInit(Bullet bullet)
    {
        Bullet = bullet;
    }
    
    public virtual void OnUpdate() { }
    public virtual void OnLaunch(Vector3 direction, float force) { }
    public virtual void OnHitEnemy(Enemy enemy, ref bool shouldDestroy) { }
    public virtual void OnDestroy() { }
    public virtual void OnReset() { }
}