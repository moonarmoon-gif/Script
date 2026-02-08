using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detects swipe gestures on touch screen or mouse
/// </summary>
public class SwipeDetector : MonoBehaviour
{
    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 50f; // Minimum distance in pixels
    [SerializeField] private float maxSwipeTime = 0.5f; // Maximum time for a swipe
    
    public delegate void SwipeEvent(Vector2 direction);
    public event SwipeEvent OnSwipe;
    
    private Vector2 startPosition;
    private float startTime;
    private bool isSwiping = false;
    
    private void Update()
    {
        // Handle touch input
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            
            if (touch.press.wasPressedThisFrame)
            {
                StartSwipe(touch.position.ReadValue());
            }
            else if (touch.press.isPressed && isSwiping)
            {
                CheckSwipe(touch.position.ReadValue());
            }
            else if (touch.press.wasReleasedThisFrame && isSwiping)
            {
                EndSwipe(touch.position.ReadValue());
            }
        }
        // Handle mouse input (for testing on PC)
        else if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                StartSwipe(Mouse.current.position.ReadValue());
            }
            else if (Mouse.current.leftButton.isPressed && isSwiping)
            {
                CheckSwipe(Mouse.current.position.ReadValue());
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame && isSwiping)
            {
                EndSwipe(Mouse.current.position.ReadValue());
            }
        }
    }
    
    private void StartSwipe(Vector2 position)
    {
        startPosition = position;
        startTime = GameStateManager.PauseSafeTime;
        isSwiping = true;
    }
    
    private void CheckSwipe(Vector2 currentPosition)
    {
        // Optional: Could add visual feedback here
    }
    
    private void EndSwipe(Vector2 endPosition)
    {
        if (!isSwiping) return;
        
        float swipeTime = GameStateManager.PauseSafeTime - startTime;
        float swipeDistance = Vector2.Distance(startPosition, endPosition);
        
        // Check if it's a valid swipe
        if (swipeTime <= maxSwipeTime && swipeDistance >= minSwipeDistance)
        {
            Vector2 swipeDirection = (endPosition - startPosition).normalized;
            OnSwipe?.Invoke(swipeDirection);
            Debug.Log($"<color=yellow>Swipe detected! Direction: {swipeDirection}, Distance: {swipeDistance:F1}px</color>");
        }
        
        isSwiping = false;
    }
    
    /// <summary>
    /// Get the primary swipe direction (Up, Down, Left, Right)
    /// </summary>
    public static SwipeDirection GetSwipeDirection(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        if (angle >= -45f && angle < 45f)
            return SwipeDirection.Right;
        else if (angle >= 45f && angle < 135f)
            return SwipeDirection.Up;
        else if (angle >= -135f && angle < -45f)
            return SwipeDirection.Down;
        else
            return SwipeDirection.Left;
    }
    
    public enum SwipeDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
