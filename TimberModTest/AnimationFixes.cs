using HarmonyLib;
using Timberborn.CharacterMovementSystem;
using Timberborn.EntitySystem;
using UnityEngine;

namespace TimberModTest
{
    [ManualMethodOverwrite]
    [HarmonyPatch(typeof(MovementAnimator), nameof(MovementAnimator.Update), typeof(float))]
    public class AnimatedPathFollowerUpdatePathcer
    {

        static bool Prefix(MovementAnimator __instance, float deltaTime)
        {
            float time = Time.time;
            EntityComponent entity = __instance.GetComponentFast<EntityComponent>();
            if (entity != null)
            {
                var tickProgressService = SingletonManager.GetSingleton<TickProgressService>();
                // For the movement animation, use interpolated time based on
                // how many buckets we've ticked (i.e. how close to the next
                // time update).
                time = tickProgressService.TimeAtLastTick(entity) +
                    Time.fixedDeltaTime *
                    tickProgressService.PercentTicked(entity);
                //Plugin.Log($"{entity.EntityId}:\n" +
                //    $"index: {TickProgressService.GetEntityBucketIndex(entity)}\n" +
                //    $"ticked: {TickProgressService.HasTicked(entity)}\n" +
                //    $"last: {TickProgressService.TimeAtLastTick(entity)}\n" +
                //    $"perc: {TickProgressService.PercentTicked(entity)}\n" +
                //    $"time: {Time.time} -> {time}");
            }
            else
            {
                Plugin.LogWarning("missing entity component!");
            }

            // Use the interpolated time
            __instance._animatedPathFollower.Update(time);

            // Otherwise, update as usual
            if (!__instance._animatedPathFollower.ReachedDestination())
            {
                __instance.UpdateTransform(deltaTime);
            }
            else if (!__instance._animatedPathFollower.Stopped)
            {
                __instance.StopAnimatingMovement();
            }
            __instance.InvokeAnimationUpdate();

            // We've replaced the original method, so skip it
            return false;
        }
    }
}
