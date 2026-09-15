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
        public bool MouseEnabled { get; set; }
        public bool MouseDown { get; set; }
        public bool MouseUp { get; set; }
        public bool MouseHeld { get; set; }
        public Vector2 MousePosition { get; set; }
        public override bool mousePresent => MouseEnabled;
        public override bool touchSupported => false;
        public override int touchCount => 0;
        public override Vector2 mousePosition => MousePosition;
        public override Vector2 mouseScrollDelta => Vector2.zero;
        public override bool GetMouseButtonDown(int button) => button == 0 && MouseDown;
        public override bool GetMouseButtonUp(int button) => button == 0 && MouseUp;
        public override bool GetMouseButton(int button) => button == 0 && MouseHeld;
        public override bool GetButtonDown(string buttonName) => buttonName == "Submit" || buttonName == "Cancel";
        public override float GetAxisRaw(string axisName) => axisName == "Horizontal" ? Horizontal : 0f;
        public void OnSubmit(BaseEventData data) => Submits++;
        public void OnCancel(BaseEventData data) => Cancels++;
        public void OnMove(AxisEventData data) => Moves++;
    }
}
#endif
