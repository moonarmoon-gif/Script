using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1)]
public class InputManager : MonoBehaviour
{
    public delegate void StartTouchEvent(Vector2 position, float time);
    public event StartTouchEvent OnStartTouch;

    public delegate void EndTouchEvent(Vector2 position, float time);
    public event EndTouchEvent OnEndTouch;

    private TouchControls touchControls;

    private void Awake()
    {
        touchControls = new TouchControls();
    }

    private void OnEnable()
    {
        touchControls.Touch.TouchPress.started += OnTouchStarted;
        touchControls.Touch.TouchPress.canceled += OnTouchEnded;
        touchControls.Enable();
    }

    private void OnDisable()
    {
        touchControls.Touch.TouchPress.started -= OnTouchStarted;
        touchControls.Touch.TouchPress.canceled -= OnTouchEnded;
        touchControls.Disable();
    }

    private static Vector2 GetCurrentPointerPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }
        if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }
        return Vector2.zero;
    }

    private void OnTouchStarted(InputAction.CallbackContext context)
    {
        Vector2 screenPosition = GetCurrentPointerPosition();
        OnStartTouch?.Invoke(screenPosition, (float)context.startTime);
    }

    private void OnTouchEnded(InputAction.CallbackContext context)
    {
        Vector2 screenPosition = GetCurrentPointerPosition();
        OnEndTouch?.Invoke(screenPosition, (float)context.time);
    }
}