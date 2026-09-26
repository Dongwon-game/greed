using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast
{
    public sealed class GreedLastCommandButton : Button
    {
        private readonly HashSet<PointerEventData> pendingPresses = new HashSet<PointerEventData>();

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable())
            {
                eventData.eligibleForClick = false;
                return;
            }
            base.OnPointerDown(eventData);
            pendingPresses.Add(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!pendingPresses.Remove(eventData)) eventData.eligibleForClick = false;
            base.OnPointerUp(eventData);
        }

        public void CancelPendingPresses()
        {
            PointerEventData lastPointer = null;
            foreach (PointerEventData pointer in pendingPresses)
            {
                // Keep the module's pointer alive so a stationary touch cannot become a new press.
                pointer.eligibleForClick = false;
                lastPointer = pointer;
            }
            pendingPresses.Clear();
            // Clear tracking first because resetting the visual state can re-enter a disabled transition.
            if (lastPointer != null) base.OnPointerUp(lastPointer);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (state == SelectionState.Disabled) CancelPendingPresses();
            base.DoStateTransition(state, instant);
        }

        protected override void OnDisable()
        {
            CancelPendingPresses();
            base.OnDisable();
        }
    }
}
