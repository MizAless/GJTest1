using System.Collections;

namespace GameEconomy.Shards
{
    public interface IShardCollectionService
    {
        int PendingCount { get; }
        // Integration waits before pausing/opening the shop or victory screen.
        // Multiple waiters share pickups. Cancellation completes all waiters without credit.
        IEnumerator CollectAll();
        void Cancel();
    }
}
