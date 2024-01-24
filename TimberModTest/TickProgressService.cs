using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.EntitySystem;
using Timberborn.TickSystem;
using UnityEngine;

namespace TimberModTest
{
    public class TickProgressService
    {
        private TickableBucketService tickableBucketService;

        public TickProgressService(ITickableBucketService _tickableBucketService)
        {
            tickableBucketService = _tickableBucketService as TickableBucketService;
            SingletonManager.Register(this);
        }

        public int GetEntityBucketIndex(EntityComponent tickableEntity)
        {
            if (tickableBucketService == null) return 0;
            return tickableBucketService.GetEntityBucketIndex(tickableEntity.EntityId);
        }

        public bool HasTicked(EntityComponent tickableEntity)
        {
            if (tickableBucketService == null) return false;
            int bucketIndex = GetEntityBucketIndex(tickableEntity);
            return bucketIndex < tickableBucketService._nextTickedBucketIndex;
        }

        public float PercentTicked(EntityComponent tickableEntity)
        {
            if (tickableBucketService == null) return 0;
            int bucketIndex = GetEntityBucketIndex(tickableEntity);
            int currentIndex = tickableBucketService._nextTickedBucketIndex;
            // Figure out how many buckets have ticked since this entity's bucket
            int numerator = currentIndex - bucketIndex;
            // If it hasn't ticked yet, add the number of buckets to tick (plus 1 for singletons)
            if (numerator <= 0) numerator += tickableBucketService.NumberOfBuckets + 1;
            return (float)(numerator) / (tickableBucketService.NumberOfBuckets + 1);
        }

        public float TimeAtLastTick(EntityComponent tickableEntity)
        {
            if (tickableBucketService == null) return 0;
            float time = Time.time;
            if (HasTicked(tickableEntity))
            {
                return time;
            }
            return time - Time.fixedDeltaTime;
        }
    }
}
