public static class PenetrationBullet
{
    public static BulletData Create()
    {
        var config = new BulletConfig(10, BulletRarity.Common);
        config.Modules.Add(new PenetrationModule(3));

        return new BulletData(config);
    }
}


public static class RicochetBullet
{
    public static BulletData Create()
    {
        var config = new BulletConfig(10, BulletRarity.Common);
        config.Modules.Add(new RicochetModule(2));

        return new BulletData(config);
    }
}

public static class ExplosionBullet
{
    public static BulletData Create()
    {
        var config = new BulletConfig(10, BulletRarity.Common);
        config.Modules.Add(new ExplosionModule());

        return new BulletData(config);
    }
}

public static class DefaultBullet
{
    public static BulletData Create()
    {
        var config = new BulletConfig(10, BulletRarity.Common);

        return new BulletData(config);
    }
}