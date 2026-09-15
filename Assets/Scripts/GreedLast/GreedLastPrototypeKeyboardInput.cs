#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;

namespace GreedLast
{
    // A transient input-module test double; excluded entirely from player builds.
    public sealed class GreedLastPrototypeKeyboardInput : BaseInput, ISubmitHandler, ICancelHandler, IMoveHandler
    {
        public int Submits { get; private set; }
        public int Cancels { get; private set; }
        public int Moves { get; private set; }
        public float Horizontal { get; set; }
        public override bool mousePresent => false;
        public override bool touchSupported => false;
        public override int touchCount => 0;
        public override Vector2 mousePosition => Vector2.zero;
        public override bool GetButtonDown(string buttonName) => buttonName == "Submit" || buttonName == "Cancel";
        public override float GetAxisRaw(string axisName) => axisName == "Horizontal" ? Horizontal : 0f;
        public void OnSubmit(BaseEventData data) => Submits++;
        public void OnCancel(BaseEventData data) => Cancels++;
        public void OnMove(AxisEventData data) => Moves++;
    }
}
#endif
