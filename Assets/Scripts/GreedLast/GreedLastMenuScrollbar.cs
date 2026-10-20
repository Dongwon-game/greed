using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast
{
    public sealed class GreedLastMenuScrollbar : Scrollbar
    {
        private PointerEventData activePointer;
        private int canceledFrame = -1;

        public override void OnPointerDown(PointerEventData pointer)
        {
            if (!IsActive() || !IsInteractable() || pointer.button != PointerEventData.InputButton.Left
                || activePointer != null || (Application.isPlaying && canceledFrame == Time.frameCount)
                || !GreedLastPointerInput.IsFreshPress(pointer))
            {
                pointer.eligibleForClick = false;
                return;
            }
            activePointer = pointer;
            base.OnPointerDown(pointer);
        }

        public override void OnPointerUp(PointerEventData pointer)
        {
            if (!ReferenceEquals(activePointer, pointer)) return;
            activePointer = null;
            base.OnPointerUp(pointer);
            StopAllCoroutines();
        }

        public override void OnBeginDrag(PointerEventData pointer)
        {
            if (ReferenceEquals(activePointer, pointer)) base.OnBeginDrag(pointer);
        }

        public override void OnDrag(PointerEventData pointer)
        {
            if (ReferenceEquals(activePointer, pointer)) base.OnDrag(pointer);
        }

        public void CancelPendingGestures()
        {
            if (Application.isPlaying) canceledFrame = Time.frameCount;
            PointerEventData pointer = activePointer;
            activePointer = null;
            if (pointer != null)
            {
                pointer.eligibleForClick = false;
                base.OnPointerUp(pointer);
            }
            // Native track paging repeats outside OnDrag; stop this control's own repeat as well.
            StopAllCoroutines();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (state == SelectionState.Disabled) CancelPendingGestures();
            base.DoStateTransition(state, instant);
        }

        protected override void OnDisable()
        {
            CancelPendingGestures();
            base.OnDisable();
        }
    }
}
