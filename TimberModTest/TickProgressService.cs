using Timberborn.EntitySystem;
using Timberborn.TickSystem;
using UnityEngine;

namespace BeaverBuddies
{
    public class TickProgressService : RegisteredSingleton
    {
        public TickableBucketService TickableBucketService { get; private set; }
        private TickingService _tickingService;

        public TickProgressService(ITickableBucketService _tickableBucketService, TickingService tickingService)
        {
            TickableBucketService = _tickableBucketService as TickableBucketService;
            _tickingService = tickingService;
        }

        public int GetEntityBucketIndex(EntityComponent tickableEntity)
        {
            return TickableBucketService.GetEntityBucketIndex(tickableEntity.EntityId);
        }

        public bool HasTicked(EntityComponent tickableEntity)
        {
            int bucketIndex = GetEntityBucketIndex(tickableEntity);

            // If the next ticked bucket is 0, this means we either just ticked the replay
            // service (meaning *no* entities have ticked this tick) or or we're just about to
            // (meaning *all* entities have ticked this tick).
            if (TickableBucketService._nextTickedBucketIndex == 0)
            {
                // We've only ticked at the start of the tick, before the replay service
                // has ticked.
                return TickingService.IsAtStartOfTick(TickableBucketService)
                    && !_tickingService.HasTickedReplayService;
            }

            int lastTickedBucket = TickableBucketService._nextTickedBucketIndex - 1;
            if (lastTickedBucket < 0) lastTickedBucket += TickableBucketService.NumberOfBuckets;

            return bucketIndex <= lastTickedBucket;
        }

        public float PercentTicked(EntityComponent tickableEntity)
        {
            int bucketIndex = GetEntityBucketIndex(tickableEntity);
            int currentIndex = TickableBucketService._nextTickedBucketIndex;
            // Figure out how many buckets have ticked since this entity's bucket
            int numerator = currentIndex - bucketIndex;
            // If it hasn't ticked yet, add the number of buckets to tick (plus 1 for singletons)
            if (numerator <= 0) numerator += TickableBucketService.NumberOfBuckets + 1;
            return (float)(numerator) / (TickableBucketService.NumberOfBuckets + 1);
        }

        public float TimeAtLastTick(EntityComponent tickableEntity)
        {
            float time = Time.time;
            if (HasTicked(tickableEntity))
            {
                return time;
            }
            return time - Time.fixedDeltaTime;
        }
    }
}
