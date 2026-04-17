using System.Collections.Generic;
using UnityEngine;

public class BulletConfig
{
    public float BaseSpeed = 10f;
    public List<BulletModule> Modules = new List<BulletModule>();
    public BulletRarity Rarity = BulletRarity.Common;
    
    public BulletConfig()
    {
        Modules = new List<BulletModule>();
    }
    
    public BulletConfig(float baseSpeed, BulletRarity rarity = BulletRarity.Common)
    {
        BaseSpeed = baseSpeed;
        Rarity = rarity;
        Modules = new List<BulletModule>();
    }
}

public enum BulletRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}