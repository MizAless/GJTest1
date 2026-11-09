using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace GameEconomy
{
    public enum ShardKind { Chaos, Ice, Lava }
    public enum ShardGrade { T1 = 1, T2 = 2, T3 = 3 }

    [Serializable]
    public struct ShardId : IEquatable<ShardId>
    {
        [SerializeField] private ShardKind _kind;
        [SerializeField] private ShardGrade _grade;
        public ShardKind Kind => _kind;
        public ShardGrade Grade => _grade;

        public ShardId(ShardKind kind, ShardGrade grade)
        {
            if (!Enum.IsDefined(typeof(ShardKind), kind) || !Enum.IsDefined(typeof(ShardGrade), grade))
            {
                throw new ArgumentOutOfRangeException(nameof(grade), "Unknown shard kind or grade.");
            }
            _kind = kind;
            _grade = grade;
        }

        public bool IsValid => Enum.IsDefined(typeof(ShardKind), _kind)
            && Enum.IsDefined(typeof(ShardGrade), _grade);
        public bool Equals(ShardId other) => _kind == other._kind && _grade == other._grade;
        public override bool Equals(object value) => value is ShardId other && Equals(other);
        public override int GetHashCode() => ((int)_kind * 397) ^ (int)_grade;
        public override string ToString() => _kind + "/" + _grade;
    }

    public enum InventoryCurrency { Coins, Shard }

    public readonly struct InventoryChange
    {
        public InventoryCurrency Currency { get; }
        public ShardId Shard { get; }
        public int Delta { get; }
        public int Balance { get; }

        public InventoryChange(InventoryCurrency currency, ShardId shard, int delta, int balance)
        {
            Currency = currency;
            Shard = shard;
            Delta = delta;
            Balance = balance;
        }
    }

    public interface IRunInventory
    {
        int Coins { get; }
        event Action<InventoryChange> Changed;
        int GetShardCount(ShardId shard);
        IReadOnlyDictionary<ShardId, int> GetShardSnapshot();
        void AddCoins(int amount);
        bool TrySpendCoins(int amount);
        void AddShards(ShardId shard, int amount = 1);
        bool TrySpendShards(ShardId shard, int amount = 1);
    }

    // Runtime-only, single Unity thread. Configuration assets never contain a wallet.
    public sealed class RunInventory : IRunInventory
    {
        private readonly Dictionary<ShardId, int> _shards = new Dictionary<ShardId, int>();
        public int Coins { get; private set; }
        public event Action<InventoryChange> Changed;

        public int GetShardCount(ShardId shard)
        {
            ValidateShard(shard);
            return _shards.TryGetValue(shard, out int count) ? count : 0;
        }

        public IReadOnlyDictionary<ShardId, int> GetShardSnapshot()
        {
            return new ReadOnlyDictionary<ShardId, int>(new Dictionary<ShardId, int>(_shards));
        }

        public void AddCoins(int amount)
        {
            ValidateAmount(amount);
            Coins = checked(Coins + amount);
            Changed?.Invoke(new InventoryChange(InventoryCurrency.Coins, default, amount, Coins));
        }

        public bool TrySpendCoins(int amount)
        {
            ValidateAmount(amount);
            if (Coins < amount)
            {
                return false;
            }
            Coins -= amount;
            Changed?.Invoke(new InventoryChange(InventoryCurrency.Coins, default, -amount, Coins));
            return true;
        }

        public void AddShards(ShardId shard, int amount = 1)
        {
            ValidateAmount(amount);
            int balance = checked(GetShardCount(shard) + amount);
            _shards[shard] = balance;
            Changed?.Invoke(new InventoryChange(InventoryCurrency.Shard, shard, amount, balance));
        }

        public bool TrySpendShards(ShardId shard, int amount = 1)
        {
            ValidateAmount(amount);
            int previous = GetShardCount(shard);
            if (previous < amount)
            {
                return false;
            }
            int balance = previous - amount;
            if (balance == 0)
            {
                _shards.Remove(shard);
            }
            else
            {
                _shards[shard] = balance;
            }
            Changed?.Invoke(new InventoryChange(InventoryCurrency.Shard, shard, -amount, balance));
            return true;
        }

        private static void ValidateAmount(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
            }
        }

        private static void ValidateShard(ShardId shard)
        {
            if (!shard.IsValid)
            {
                throw new ArgumentException("Invalid shard identifier.", nameof(shard));
            }
        }
    }
}
