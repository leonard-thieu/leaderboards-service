using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    [ExcludeFromCodeCoverage]
    internal sealed class FakeLeaderboardsStoreClient : ILeaderboardsStoreClient
    {
        public Task<int> BulkInsertAsync<TEntity>(IEnumerable<TEntity> items, CancellationToken cancellationToken) where TEntity : class
        {
            throw new NotImplementedException();
        }

        public Task<int> BulkUpsertAsync<TEntity>(IEnumerable<TEntity> items, BulkUpsertOptions options, CancellationToken cancellationToken) where TEntity : class
        {
            return Task.FromResult(items.Count());
        }

        public void Dispose() { }
    }
}
