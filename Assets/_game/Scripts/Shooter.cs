using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class ShotContext
{
    public Shooter Owner;
    public int Pending, Spawned, Hits;
    public bool Active = true, LimitReported;
    public readonly HashSet<Enemy> HitTargets = new HashSet<Enemy>();
    public bool Reserve()
    {
        if (!Active || Spawned >= 160)
        {
            if (!LimitReported && RunUI.Instance != null) RunUI.Instance.Note("Предел цепочки: 160 снарядов.");
            LimitReported = true; return false;
        }
        Spawned++; Pending++; return true;
    }
}

public class Shooter : MonoBehaviour
{
    [SerializeField] BulletContainer _bulletContainer;
    [SerializeField] Bullet _bulletPrefab;
    [SerializeField] ParticleSystem _shootVFX;
    [SerializeField] float _shootForce = 30;
    [SerializeField, Min(0)] private float _shotInterval = 0.15f;
    private float _nextShotTime;
    private int _activeShots;
    private int _launchingShots;
    private int _startedShots;
    public BulletContainer Container => _bulletContainer;
    public bool IsFiring => _activeShots > 0;
    public int CompletedShots { get; private set; }
    readonly HashSet<Enemy> _pastTargets = new HashSet<Enemy>();
    void Start() { _bulletContainer.Init(); }
    public Enemy GetNearestEnemy() => Nearest(transform.position,null);
    public static Enemy Nearest(Vector3 from, HashSet<int> excluded)
    {
        return G.Enemies.Where(e=>e!=null&&e.IsAlive&&(excluded==null||!excluded.Contains(e.GetInstanceID()))).OrderBy(e=>(e.transform.position-from).sqrMagnitude).FirstOrDefault();
    }
    public bool TryFire()
    {
        if (Time.time < _nextShotTime || (RunDirector.Instance != null && !RunDirector.Instance.CombatActive))
        {
            return false;
        }
        if (!_bulletContainer.BeginShot(out int slot))
        {
            return false;
        }

        // Cadence is measured from launch, independent of burst length and projectile lifetime.
        _nextShotTime = Time.time + _shotInterval;
        _activeShots++;
        _launchingShots++;
        _startedShots++;
        StartCoroutine(Fire(slot, _startedShots));
        return true;
    }
    private IEnumerator Fire(int slot, int shotNumber)
    {
        var context = new ShotContext { Owner=this };
        AmmoItem[] layers = _bulletContainer.Layers(slot).ToArray();
        foreach (var layer in layers)
            if (layer.Trigger == AmmoTrigger.ShotNext || layer.Trigger == AmmoTrigger.ShotPrevious || layer.Trigger == AmmoTrigger.ShotTwo)
                foreach (int target in _bulletContainer.Destinations(slot,layer.Trigger)) _bulletContainer.Grant(slot,target,layer);
        var profile = _bulletContainer.Profiles[slot].Copy();
        yield return LaunchVolley(context,profile);
        foreach (var layer in layers)
        {
            if (layer.Trigger == AmmoTrigger.ShotNextEffect)
            {
                int next = _bulletContainer.Next(slot+1);
                if (RunUI.Instance != null) RunUI.Instance.Transfer(slot,next);
                yield return LaunchVolley(context,_bulletContainer.Profiles[next].Copy());
            }
        }
        _launchingShots--;
        // Reload after emission finishes, without waiting for projectiles to hit or expire.
        if (_launchingShots == 0 && _bulletContainer.BulletsRemaining == 0)
        {
            StartCoroutine(_bulletContainer.Reload());
        }
        float end = Time.time + 3f;
        while (context.Pending > 0 && Time.time < end) yield return null;
        // Resolve ticks and past-target effects in cartridge order, even if later shots hit sooner.
        while (CompletedShots + 1 != shotNumber)
        {
            yield return null;
        }
        foreach (var layer in layers)
            if (layer.Trigger == AmmoTrigger.ShotPastTargets)
                foreach (var enemy in _pastTargets.ToArray())
                    if (enemy != null && enemy.IsAlive) { enemy.ReceiveHit(profile,Vector3.zero); context.HitTargets.Add(enemy); }
        context.Active = false;
        _pastTargets.Clear();
        foreach (var enemy in context.HitTargets) if (enemy != null && enemy.IsAlive) _pastTargets.Add(enemy);
        Enemy.TickAllStatuses(G.Enemies);
        CompletedShots++;
        _activeShots--;
    }
    IEnumerator LaunchVolley(ShotContext context, CombatProfile profile)
    {
        for (int repeat = 0; repeat <= profile.Properties.Burst; repeat++)
        {
            var target = GetNearestEnemy();
            Vector3 direction;
            if (target != null) direction = target.transform.position-transform.position;
            else
            {
                var camera = Camera.main; var mouse = Input.mousePosition;
                mouse.z = transform.position.z-camera.transform.position.z;
                direction = camera.ScreenToWorldPoint(mouse)-transform.position;
            }
            direction.z=0; if(direction.sqrMagnitude<.001f) direction=Vector3.right;
            direction.Normalize();
            int count = 1 + profile.Properties.Fan;
            for(int n=0;n<count;n++)
            {
                float angle = (n-(count-1)*.5f)*12f;
                Vector3 ray = Quaternion.Euler(0,0,angle)*direction;
                SpawnProjectile(context,profile,transform.position,ray,new HashSet<int>());
            }
            OnShoot(direction);
            if (repeat < profile.Properties.Burst) yield return new WaitForSeconds(.09f);
        }
    }
    public void SpawnProjectile(ShotContext context, CombatProfile profile, Vector3 from, Vector3 direction, HashSet<int> visited)
    {
        if (!context.Reserve()) return;
        var bullet = Instantiate(_bulletPrefab,from,Quaternion.identity);
        bullet.Configure(context,profile.Copy(),direction,_shootForce,visited);
    }
    void OnShoot(Vector3 direction)
    {
        if (Camera.main != null)
        {
            Camera.main.transform.DOKill(true);
            Camera.main.transform.DOPunchPosition(Vector3.one*.055f,.13f,12,1);
        }
        if (_shootVFX != null) { _shootVFX.transform.rotation=Quaternion.LookRotation(direction); _shootVFX.Play(); }
    }
    void Update()
    {
        bool pointer = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if ((Input.GetMouseButtonDown(0)&&!pointer)||Input.GetKeyDown(KeyCode.Space)) TryFire();
    }
}
