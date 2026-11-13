using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameEconomy
{
    public sealed class ShardCraftingService : ICraftingService
    {
        private readonly RunEconomyContext _run;
        private readonly AmmoDropTable _table;
        private readonly BulletContainer _container;
        private bool _committing;

        public ShardCraftingService(RunEconomyContext run, AmmoDropTable table, BulletContainer container = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _table = table != null ? table : throw new ArgumentNullException(nameof(table));
            _container = container;
        }

        public CraftCheck CanApply(CraftRequest request)
        {
            return Check(request, out _, out _);
        }

        private CraftCheck Check(CraftRequest request, out List<CraftPropertyPool> pools, out ShardGradeRule grade)
        {
            pools = null;
            grade = null;
            if (!_run.IsActive)
            {
                return new CraftCheck(CraftCode.RunEnded, "Забег завершён.");
            }
            if (_committing)
            {
                return new CraftCheck(CraftCode.Unavailable, "Предыдущее улучшение ещё выполняется.");
            }
            AmmoItem target = request.Target;
            if (target == null || target.Properties == null || target.IsTriggerPayload
                || !request.Shard.IsValid || !Enum.IsDefined(typeof(BulletRarity), target.Rarity)
                || !Enum.IsDefined(typeof(AmmoTrigger), target.Trigger)
                || (request.IsModule && target.Archetype != null)
                || !AffixValues.IsValid(target.Properties, out int count))
            {
                return new CraftCheck(CraftCode.InvalidTarget, "Некорректный предмет или осколок.");
            }
            RunEconomySettings settings = _run.Settings;
            if (request.IsModule && !settings.AllowModuleCrafting)
            {
                return new CraftCheck(CraftCode.InvalidTarget, "Улучшение модулей отключено.");
            }
            if (_container != null && _container.Initialized)
            {
                bool inCartridges = Array.IndexOf(_container.Cartridges, target) >= 0;
                bool inSockets = Array.IndexOf(_container.Sockets, target) >= 0;
                if ((request.IsModule && inCartridges) || (!request.IsModule && inSockets))
                {
                    return new CraftCheck(CraftCode.InvalidTarget, "Тип предмета не совпадает с установленным слотом.");
                }
            }
            int triggers = target.Trigger == AmmoTrigger.None ? 0 : 1;
            if (count >= settings.MaximumAdditionalProperties || triggers > settings.MaximumAdditionalTriggers)
            {
                return new CraftCheck(CraftCode.AffixLimit, "Достигнут лимит дополнительных свойств или триггеров.");
            }
            ShardKindRule kind = settings.ShardKinds?.Find(rule => rule != null && rule.Kind == request.Shard.Kind);
            grade = settings.ShardGrades?.Find(rule => rule != null && rule.Grade == request.Shard.Grade);
            if (kind == null || grade == null || (!kind.AllowAllProperties && kind.AllowedProperties == null)
                || grade.Cost <= 0 || grade.ExcludedWeakestTiers < 0 || grade.PowerWeightExponent < 0
                || float.IsNaN(grade.PowerWeightExponent) || float.IsInfinity(grade.PowerWeightExponent))
            {
                return new CraftCheck(CraftCode.Unavailable, "Не настроены правила этого осколка.");
            }
            if (_run.Inventory.GetShardCount(request.Shard) < grade.Cost)
            {
                return new CraftCheck(CraftCode.InsufficientShards, "Недостаточно осколков нужного вида и качества.");
            }
            pools = CraftAffixPool.Build(_table, request, kind, grade);
            if (pools.Count == 0)
            {
                return new CraftCheck(CraftCode.NoEligibleProperties, "Нет доступного нового свойства.");
            }
            return new CraftCheck(CraftCode.Ready, "Можно добавить одно новое свойство.");
        }

        public CraftResult TryApply(CraftRequest request)
        {
            CraftCheck check = Check(request, out List<CraftPropertyPool> pools, out ShardGradeRule grade);
            if (!check.CanApply)
            {
                return new CraftResult(check.Code, reason: check.Reason);
            }
            CraftPropertyPool pool = pools[CraftAffixPool.PickIndex(pools.Select(entry => (double)entry.Property.Weight).ToArray())];
            AmmoPropertyTier tier = pool.Tiers[CraftAffixPool.PickIndex(pool.TierWeights)];
            float value = tier.RollValue(pool.Property.Property);
            AmmoProperties original = request.Target.Properties;
            AmmoProperties modified = original.Copy();
            AmmoGenerator.Apply(modified, pool.Property.Property, value);
            _committing = true;
            try
            {
                request.Target.Properties = modified;
                try
                {
                    RefreshInstalled(request.Target);
                }
                catch
                {
                    request.Target.Properties = original;
                    RefreshInstalled(request.Target);
                    throw;
                }
                // No callbacks occur between the check above and the mutation. Inventory
                // observers see the committed item and profile when the debit is announced.
                if (!_run.Inventory.TrySpendShards(request.Shard, grade.Cost))
                {
                    request.Target.Properties = original;
                    RefreshInstalled(request.Target);
                    return new CraftResult(CraftCode.InsufficientShards, reason: "Осколок уже израсходован.");
                }
                var affix = new CraftedAffix(pool.Property.Property, tier.Name, tier.PowerRank, value);
                return new CraftResult(CraftCode.Success, affix, "Добавлено новое свойство.");
            }
            finally
            {
                _committing = false;
            }
        }

        private void RefreshInstalled(AmmoItem target)
        {
            if (_container != null && _container.Initialized
                && (Array.IndexOf(_container.Cartridges, target) >= 0 || Array.IndexOf(_container.Sockets, target) >= 0))
            {
                _container.RefreshProfiles();
            }
        }
    }
}
