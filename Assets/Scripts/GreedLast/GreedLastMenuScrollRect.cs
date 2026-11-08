using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast
{
    public sealed class GreedLastMenuScrollRect : ScrollRect, IPointerDownHandler, IPointerUpHandler
    {
        private PointerEventData activePointer;
        private int canceledFrame = -1;

        internal bool HasActiveGesture => activePointer != null;

        public bool TryKeyboardScroll(GreedLastMenuScrollCommand command)
        {
            if (!IsActive() || !vertical || content == null || viewport == null || activePointer != null
                || (verticalScrollbar is GreedLastMenuScrollbar bar && bar.HasActiveGesture)
                || (Application.isPlaying && canceledFrame == Time.frameCount)) return false;
            float overflow = content.rect.height - viewport.rect.height;
            if (overflow <= 1f || viewport.rect.height <= 1f) return false;
            float current = Mathf.Clamp01(verticalNormalizedPosition);
            float page = viewport.rect.height * 0.85f / overflow;
            float target;
            switch (command)
            {
                case GreedLastMenuScrollCommand.PageUp: target = current + page; break;
                case GreedLastMenuScrollCommand.PageDown: target = current - page; break;
                case GreedLastMenuScrollCommand.Top: target = 1f; break;
                case GreedLastMenuScrollCommand.Bottom: target = 0f; break;
                default: return false;
            }
            StopMovement();
            verticalNormalizedPosition = Mathf.Clamp01(target);
            return true;
        }

        public void OnPointerDown(PointerEventData pointer)
        {
            if (!IsActive() || pointer.button != PointerEventData.InputButton.Left || activePointer != null
                || (Application.isPlaying && canceledFrame == Time.frameCount)
                || (verticalScrollbar is GreedLastMenuScrollbar bar && bar.HasActiveGesture)
                || !GreedLastPointerInput.IsFreshPress(pointer))
            {
                pointer.eligibleForClick = false;
                return;
            }
            activePointer = pointer;
        }

        public void OnPointerUp(PointerEventData pointer)
        {
            if (!ReferenceEquals(activePointer, pointer)) return;
            base.OnEndDrag(pointer);
            activePointer = null;
        }

        public override void OnInitializePotentialDrag(PointerEventData pointer)
        {
            if (ReferenceEquals(activePointer, pointer)) base.OnInitializePotentialDrag(pointer);
        }

        public override void OnScroll(PointerEventData pointer)
        {
            if (Application.isPlaying && canceledFrame == Time.frameCount) return;
            base.OnScroll(pointer);
        }

        public override void OnBeginDrag(PointerEventData pointer)
        {
            if (ReferenceEquals(activePointer, pointer)) base.OnBeginDrag(pointer);
        }

        public override void OnDrag(PointerEventData pointer)
        {
            if (ReferenceEquals(activePointer, pointer)) base.OnDrag(pointer);
        }

        public override void OnEndDrag(PointerEventData pointer)
        {
            OnPointerUp(pointer);
        }

        public void CancelPendingGestures()
        {
            if (Application.isPlaying) canceledFrame = Time.frameCount;
            PointerEventData pointer = activePointer;
            activePointer = null;
            StopMovement();
            if (pointer == null) return;
            pointer.eligibleForClick = false;
            // Cancel both an established drag and a press waiting to cross the drag threshold.
            base.OnEndDrag(pointer);
        }

        protected override void OnDisable()
        {
            CancelPendingGestures();
            base.OnDisable();
        }
    }
}
