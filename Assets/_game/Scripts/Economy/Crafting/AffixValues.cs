using System;

namespace GameEconomy
{
    // Storage access only. All eligible values and weights still come from AmmoDropTable.
    internal static class AffixValues
    {
        public static float Read(AmmoProperties properties, AmmoPropertyKind kind)
        {
            switch (kind)
            {
                case AmmoPropertyKind.Ricochet: return properties.Ricochet;
                case AmmoPropertyKind.Pierce: return properties.Pierce;
                case AmmoPropertyKind.Fan: return properties.Fan;
                case AmmoPropertyKind.Burst: return properties.Burst;
                case AmmoPropertyKind.Split: return properties.Split;
                case AmmoPropertyKind.Burn: return properties.Burn;
                case AmmoPropertyKind.Chill: return properties.Chill;
                case AmmoPropertyKind.Bleed: return properties.Bleed;
                case AmmoPropertyKind.Poison: return properties.Poison;
                case AmmoPropertyKind.Puncture: return properties.Puncture;
                case AmmoPropertyKind.Overload: return properties.Overload ? 1 : 0;
                case AmmoPropertyKind.More: return properties.More;
                case AmmoPropertyKind.ProjectileMore: return properties.ProjectileMore;
                case AmmoPropertyKind.DotMore: return properties.DotMore;
                case AmmoPropertyKind.AreaMore: return properties.AreaMore;
                case AmmoPropertyKind.AfflictedMore: return properties.AfflictedMore;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static bool IsValid(AmmoProperties properties, out int count)
        {
            count = 0;
            foreach (AmmoPropertyKind kind in Enum.GetValues(typeof(AmmoPropertyKind)))
            {
                float value = Read(properties, kind);
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                {
                    return false;
                }
                if (value > 0)
                {
                    count++;
                }
            }
            return true;
        }
    }
}
