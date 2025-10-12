using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BeaverBuddies.Util
{
    public static class TimeoutUtils
    {
        public static void RunAfterFrames(MonoBehaviour behavior, Action action, int frames = 1)
        {
            behavior.StartCoroutine(RunAfterFramesCoroutine(action, frames));
        }

        private static IEnumerator RunAfterFramesCoroutine(Action action, int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
            action?.Invoke();
        }
    }
}
