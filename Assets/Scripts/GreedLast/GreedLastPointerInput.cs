using UnityEngine;
using UnityEngine.EventSystems;

namespace GreedLast
{
    internal static class GreedLastPointerInput
    {
        internal static bool IsFreshPress(PointerEventData pointer)
        {
            if (!Application.isPlaying || pointer.pointerId < 0) return true;
            if (EventSystem.current == null
                || EventSystem.current.currentInputModule is not StandaloneInputModule module) return true;

            // Legacy UI synthesizes Down for a newly seen finger, even if its first phase is Ended.
            BaseInput input = module.input;
            for (int i = 0; i < input.touchCount; i++)
            {
                Touch touch = input.GetTouch(i);
                if (touch.fingerId == pointer.pointerId) return touch.phase == TouchPhase.Began;
            }
            return false;
        }
    }
}
