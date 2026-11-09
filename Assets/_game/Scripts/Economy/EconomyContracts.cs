using System;
using System.Collections;
using UnityEngine;

namespace GameEconomy
{
    public interface IRarityMultiplierProvider
    {
        float GetRarityMultiplier(int completedWave);
    }

    public readonly struct CoinCollectionRequest
    {
        public RunEconomyContext Run { get; }
        public Transform Muzzle { get; }
        public Transform CollectionTarget { get; }

        public CoinCollectionRequest(RunEconomyContext run, Transform muzzle, Transform collectionTarget)
        {
            Run = run;
            Muzzle = muzzle;
            CollectionTarget = collectionTarget;
        }
    }

    public interface ICoinCollectionService
    {
        bool IsCollecting { get; }
        IEnumerator CollectAll(CoinCollectionRequest request);
        void Cancel();
    }

    public readonly struct CraftRequest
    {
        public AmmoItem Target { get; }
        public bool IsModule { get; }
        public ShardId Shard { get; }

        public CraftRequest(AmmoItem target, bool isModule, ShardId shard)
        {
            Target = target;
            IsModule = isModule;
            Shard = shard;
        }
    }

    public enum CraftCode
    {
        Unavailable, Ready, Success, InvalidTarget, InsufficientShards, AffixLimit, NoEligibleProperties, RunEnded
    }

    public readonly struct CraftCheck
    {
        public CraftCode Code { get; }
        public string Reason { get; }
        public bool CanApply => Code == CraftCode.Ready;
        public CraftCheck(CraftCode code, string reason = null)
        {
            Code = code;
            Reason = reason;
        }
    }

    public readonly struct CraftedAffix
    {
        public AmmoPropertyKind Property { get; }
        public string TierName { get; }
        public int PowerRank { get; }
        public float Value { get; }
        public CraftedAffix(AmmoPropertyKind property, string tierName, int powerRank, float value)
        {
            Property = property;
            TierName = tierName;
            PowerRank = powerRank;
            Value = value;
        }
    }

    public readonly struct CraftResult
    {
        public CraftCode Code { get; }
        public CraftedAffix Affix { get; }
        public string Reason { get; }
        public bool Succeeded => Code == CraftCode.Success;
        public CraftResult(CraftCode code, CraftedAffix affix = default, string reason = null)
        {
            Code = code;
            Affix = affix;
            Reason = reason;
        }
    }

    // Implementations are injected with the SAME run context and AmmoDropTable.
    public interface ICraftingService
    {
        CraftCheck CanApply(CraftRequest request);
        CraftResult TryApply(CraftRequest request);
    }
}
