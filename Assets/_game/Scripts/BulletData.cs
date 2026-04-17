using System.Collections.Generic;
using UnityEngine;

public class BulletData
{
    public BulletConfig Config;

    public BulletData(BulletConfig config)
    {
        Config = config;
    }

    public void Init(Bullet bullet)
    {
        foreach (var module in Config.Modules)
        {
            module.OnInit(bullet);
        }
    }

    public virtual void OnUpdate()
    {
        foreach (var module in Config.Modules)
        {
            module.OnUpdate();
        }
    }

    public virtual void OnLaunch(Vector3 direction, float force)
    {
        foreach (var module in Config.Modules)
        {
            module.OnLaunch(direction, force);
        }
    }

    public virtual void OnHitEnemy(Enemy enemy, ref bool shouldDestroy)
    {
        foreach (var module in Config.Modules)
        {   
            module.OnHitEnemy(enemy, ref shouldDestroy);
        }
    }

    public virtual void OnDestroy()
    {
        foreach (var module in Config.Modules)
        {
            module.OnDestroy();
        }
    }

    public void OnReset()
    {
        foreach (var module in Config.Modules)
        {
            module.OnReset();
        }
    }
}