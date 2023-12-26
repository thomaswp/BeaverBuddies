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
        private static TickableBucketService tickableBucketService;

        public TickProgressService(ITickableBucketService _tickableBucketService)
        {
            tickableBucketService = _tickableBucketService as TickableBucketService;
        }

        public static int GetEntityBucketIndex(EntityComponent tickableEntity)
        {
            return tickableBucketService.GetEntityBucketIndex(tickableEntity.EntityId); ;
        }

        public static bool HasTicked(EntityComponent tickableEntity)
        {
            int bucketIndex = GetEntityBucketIndex(tickableEntity);
            return bucketIndex < tickableBucketService._nextTickedBucketIndex;
        }

        public static float PercentTicked(EntityComponent tickableEntity)
        {
            int bucketIndex = GetEntityBucketIndex(tickableEntity);
            int currentIndex = tickableBucketService._nextTickedBucketIndex;
            // Figure out how many buckets have ticked since this entity's bucket
            int numerator = currentIndex - bucketIndex;
            // If it hasn't ticked yet, add the number of buckets to tick (plus 1 for singletons)
            if (numerator <= 0) numerator += tickableBucketService.NumberOfBuckets + 1;
            return (float)(numerator) / (tickableBucketService.NumberOfBuckets + 1);
        }

        public static float TimeAtLastTick(EntityComponent tickableEntity)
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
