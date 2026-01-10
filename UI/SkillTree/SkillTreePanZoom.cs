using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SkillTreePanZoom : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private float zoomSpeed = 0.15f;
    [SerializeField] private float minZoom = 0.35f;
    [SerializeField] private float maxZoom = 1.6f;

    private bool isPanning;
    private bool panningAltLeft;
    private Vector2 lastMouse;
    private RectTransform parentRect;
    private Canvas canvas;
    private Camera uiCamera;

    private void Reset()
    {
        content = transform as RectTransform;
    }

    private void Awake()
    {
        if (content == null)
        {
            content = transform as RectTransform;
        }

        parentRect = content != null ? content.parent as RectTransform : null;
        canvas = content != null ? content.GetComponentInParent<Canvas>() : null;
        uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
    }

    private bool TryGetLocal(Vector2 screenPos, out Vector2 local)
    {
        RectTransform rect = parentRect != null ? parentRect : content;
        if (rect == null)
        {
            local = default;
            return false;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, uiCamera, out local);
    }

    private void Update()
    {
        if (content == null)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        float wheel = 0f;
        bool alt = Keyboard.current != null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);
        if (Mouse.current != null)
        {
            float raw = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(raw) > 0.01f)
            {
                wheel = raw / 120f;
            }
        }
#else
        return;
#endif
        if (Mathf.Abs(wheel) > 0.01f)
        {
            float current = content.localScale.x;
            float target = current * (1f + wheel * zoomSpeed);
            target = Mathf.Clamp(target, minZoom, maxZoom);
            content.localScale = new Vector3(target, target, 1f);
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && (Mouse.current.middleButton.wasPressedThisFrame || (alt && Mouse.current.leftButton.wasPressedThisFrame)))
        {
            isPanning = true;
            panningAltLeft = alt && Mouse.current.leftButton.wasPressedThisFrame;
            TryGetLocal(Mouse.current.position.ReadValue(), out lastMouse);
        }

        if (Mouse.current != null)
        {
            if (!panningAltLeft && Mouse.current.middleButton.wasReleasedThisFrame)
            {
                isPanning = false;
            }

            if (panningAltLeft && (!alt || Mouse.current.leftButton.wasReleasedThisFrame))
            {
                isPanning = false;
                panningAltLeft = false;
            }
        }

        if (isPanning && Mouse.current != null)
        {
            if (TryGetLocal(Mouse.current.position.ReadValue(), out Vector2 now))
            {
                Vector2 delta = now - lastMouse;
                lastMouse = now;

                float scale = Mathf.Max(0.0001f, content.localScale.x);
                content.anchoredPosition += delta / scale;
            }
        }
#endif
    }
}
