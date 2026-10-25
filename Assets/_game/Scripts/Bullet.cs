using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [SerializeField] List<ParticleSystem> _vfxs = new List<ParticleSystem>();
    ShotContext _context;
    CombatProfile _profile;
    Rigidbody2D _body;
    HashSet<int> _visited;
    int _pierce, _bounces;
    float _expires;
    bool _destroying;
    void Awake()
    {
        _body=GetComponent<Rigidbody2D>(); _body.gravityScale=0;
        _body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        foreach(var c in GetComponents<Collider2D>()) c.isTrigger=true;
    }
    public void Configure(ShotContext context, CombatProfile profile, Vector3 direction, float speed, HashSet<int> visited)
    {
        _context=context; _profile=profile; _visited=new HashSet<int>(visited);
        _pierce=profile.Properties.Pierce; _bounces=profile.Properties.Ricochet; _expires=Time.time+1.2f;
        _body.velocity=direction.normalized*speed;
        var sprite=GetComponent<SpriteRenderer>(); if(sprite!=null) sprite.color=profile.Color;
    }
    public void ChangeDirection(Vector3 direction) { _body.velocity=direction.normalized*_body.velocity.magnitude; }
    void Update() { if (_context==null || !_context.Active || Time.time>_expires) Finish(); }
    void OnTriggerEnter2D(Collider2D other)
    {
        if(_destroying || _context==null || !_context.Active || !other.TryGetComponent<Enemy>(out var enemy) || !enemy.IsAlive) return;
        if(!_visited.Add(enemy.GetInstanceID())) return;
        if(++_context.Hits>160) { Finish(); return; }
        Vector3 position=enemy.transform.position, direction=_body.velocity.normalized;
        _context.HitTargets.Add(enemy);
        enemy.ReceiveHit(_profile,direction);
        var branches=G.Enemies.Where(e=>e!=null&&e.IsAlive&&!_visited.Contains(e.GetInstanceID())).OrderBy(e=>(e.transform.position-position).sqrMagnitude).Take(Mathf.Min(8,_profile.Properties.Split)).ToArray();
        foreach(var next in branches) _context.Owner.SpawnProjectile(_context,_profile,position,(next.transform.position-position).normalized,_visited);
        if(_pierce>0) { _pierce--; return; }
        if(_bounces>0)
        {
            var next=Shooter.Nearest(position,_visited);
            if(next!=null) { _bounces--; ChangeDirection(next.transform.position-transform.position); return; }
        }
        Finish();
    }
    void Finish()
    {
        if(_destroying) return; _destroying=true;
        foreach(var c in GetComponents<Collider2D>()) c.enabled=false;
        if(_vfxs!=null) foreach(var fx in _vfxs) if(fx!=null) { fx.transform.SetParent(null); fx.Stop(); Destroy(fx.gameObject,1); }
        Destroy(gameObject);
    }
    void OnDestroy() { if(_context!=null) _context.Pending=Mathf.Max(0,_context.Pending-1); }
}
