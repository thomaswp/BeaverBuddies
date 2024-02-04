using HarmonyLib;
using Timberborn.Beavers;
using Timberborn.CharacterModelSystem;
using Timberborn.CharacterMovementSystem;
using Timberborn.Characters;
using Timberborn.EntitySystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace BeaverBuddies
{
    [ManualMethodOverwrite]
    [HarmonyPatch(typeof(MovementAnimator), nameof(MovementAnimator.Update), typeof(float))]
    public class AnimatedPathFollowerUpdatePathcer
    {
        static bool Prefix(MovementAnimator __instance, float deltaTime)
        {
            if (EventIO.IsNull) return true;

            //Vector3 position = Vector3.zero;

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

                //if (entity.EntityId.ToString() == "00355d1d-36fd-f115-9c90-6a54dda73a85")
                //{
                //    Plugin.Log($"{entity.EntityId} (${entity.GetComponentFast<Character>().FirstName}) :\n" +
                //        $"index: {tickProgressService.GetEntityBucketIndex(entity)}\n" +
                //        $"nextTick: {tickProgressService.TickableBucketService._nextTickedBucketIndex}\n" +
                //        $"ticked: {tickProgressService.HasTicked(entity)}\n" +
                //        $"last: {tickProgressService.TimeAtLastTick(entity)}\n" +
                //        $"perc: {tickProgressService.PercentTicked(entity)}\n" +
                //        $"time: {Time.time} -> {time}");
                //    position = __instance._animatedPathFollower.CurrentPosition;
                //}
            }
            else
            {
                Plugin.LogWarning("missing entity component!");
            }

            // Use the interpolated time
            __instance._animatedPathFollower.Update(time);

            // Otherwise, update as usual
            if (!__instance._animatedPathFollower.Stopped)
            {
                __instance.UpdateTransform(deltaTime);
            }
            __instance.InvokeAnimationUpdate();

            //if (position != Vector3.zero)
            //{
            //    Plugin.Log($"pos: {position} -> {__instance._animatedPathFollower.CurrentPosition}");
            //    Vector3 dir = __instance._animatedPathFollower.CurrentPosition - position;
            //    Plugin.Log($"XDir: {Mathf.Sign(dir.x)}, ZDir: {Mathf.Sign(dir.z)}");
            //}

            // We've replaced the original method, so skip it
            return false;
        }
    }
}
