public class PenetrationModule : BulletModule
{
    private int _maxPenetrations;
    
    private int _currentPenetrations = 0;

    public PenetrationModule(int maxPenetrations)
    {
        _maxPenetrations = maxPenetrations;
    }

    public override void OnHitEnemy(Enemy enemy, ref bool shouldDestroy)
    {
        _currentPenetrations++;
        
        shouldDestroy = _currentPenetrations >= _maxPenetrations;
    }
    
    public override void OnReset()
    {
        _currentPenetrations = 0;
    }
}