using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Advanced gesture detector for dual-thumb swipes and directional swipes
/// - Dual horizontal swipes (outward from center): Swap fire/ice
/// - Directional swipes (ANY direction): Spawn projectile in swipe direction
/// </summary>
public class AdvancedGestureDetector : MonoBehaviour
{
    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 80f; // Minimum distance in pixels
    [SerializeField] private float maxSwipeTime = 0.6f; // Maximum time for a swipe
    
    [Header("Dual Swipe Settings")]
    [SerializeField] private float dualSwipeTimeWindow = 0.3f; // Time window for dual swipes
    [SerializeField] private float horizontalAngleTolerance = 30f; // Degrees from horizontal (180°)
    
    [Header("Directional Swipe Settings")]
    [SerializeField] private float diagonalAngleTolerance = 25f; // No longer used - kept for compatibility
    [SerializeField] private float minDiagonalSwipeDistance = 100f; // Minimum distance for directional swipe
    
    [Header("Debug Visualization")]
    [Tooltip("Show touch points and center line for debugging")]
    public bool showDebugVisualization = false;
    
    // Events
    public delegate void DualHorizontalSwipeEvent();
    public event DualHorizontalSwipeEvent OnDualHorizontalSwipe;
    
    // Directional swipe event - triggers on ANY swipe direction (not just diagonal)
    // isLeftDiagonal: true if swiping left (X < 0), false if swiping right (X > 0)
    public delegate void DiagonalSwipeEvent(bool isLeftDiagonal, Vector2 swipeDirection, Vector2 startPos, Vector2 endPos);
    public event DiagonalSwipeEvent OnDiagonalSwipe; // Name kept for compatibility
    
    // Touch tracking
    private class TouchData
    {
        public int fingerId;
        public Vector2 startPosition;
        public float startTime;
        public bool isActive;
    }
    
    private Dictionary<int, TouchData> activeTouches = new Dictionary<int, TouchData>();
    private List<TouchData> recentSwipes = new List<TouchData>();
    
    private void Update()
    {
        // Check if card selection is active - if so, disable gesture detection
        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            return; // Don't process any gestures while card selection is active
        }
        
        if (Touchscreen.current != null)
        {
            HandleTouchInput();
        }
        else if (Mouse.current != null)
        {
            HandleMouseInput(); // For PC testing
        }
        
        // Clean up old swipes from recent list
        recentSwipes.RemoveAll(t => GameStateManager.PauseSafeTime - t.startTime > dualSwipeTimeWindow);
    }
    
    private void HandleTouchInput()
    {
        var touches = Touchscreen.current.touches;
        
        for (int i = 0; i < touches.Count; i++)
        {
            var touch = touches[i];
            
            int fingerId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();
            bool justStarted = touch.press.wasPressedThisFrame;
            bool justEnded = touch.press.wasReleasedThisFrame;
            
            // Handle touch start
            if (justStarted)
            {
                Debug.Log($"<color=cyan>[GestureDetector] Touch {fingerId} started at {position}</color>");
                StartTouch(fingerId, position);
            }
            // Handle touch end - MUST check this even if !isInProgress
            else if (justEnded)
            {
                Debug.Log($"<color=cyan>[GestureDetector] Touch {fingerId} ended at {position}</color>");
                if (activeTouches.ContainsKey(fingerId))
                {
                    EndTouch(fingerId, position);
                }
                // Touch ended without start - ignore
            }
        }
    }
    
    private void HandleMouseInput()
    {
        // Simulate touch with mouse for PC testing
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log($"<color=cyan>[GestureDetector] Mouse pressed!</color>");
            StartTouch(0, Mouse.current.position.ReadValue());
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame && activeTouches.ContainsKey(0))
        {
            Debug.Log($"<color=cyan>[GestureDetector] Mouse released!</color>");
            EndTouch(0, Mouse.current.position.ReadValue());
        }
    }
    
    private void StartTouch(int fingerId, Vector2 position)
    {
        Debug.Log($"<color=cyan>[GestureDetector] StartTouch - FingerID: {fingerId}, Position: {position}</color>");
        
        TouchData touchData = new TouchData
        {
            fingerId = fingerId,
            startPosition = position,
            startTime = GameStateManager.PauseSafeTime,
            isActive = true
        };
        activeTouches[fingerId] = touchData;
    }

    private void EndTouch(int fingerId, Vector2 endPosition)
    {
        Debug.Log($"<color=cyan>[GestureDetector] EndTouch - fingerID: {fingerId}, Position: {endPosition}</color>");

        if (!activeTouches.ContainsKey(fingerId))
        {
            return; // No touch data - ignore
        }

        TouchData touchData = activeTouches[fingerId];
        float swipeTime = GameStateManager.PauseSafeTime - touchData.startTime;
        float swipeDistance = Vector2.Distance(touchData.startPosition, endPosition);

        Debug.Log($"<color=cyan>[GestureDetector] Swipe analysis: Time={swipeTime:F2}s, Distance={swipeDistance:F1}px</color>");
        Debug.Log($"<color=cyan>[GestureDetector] Max swipe time: {maxSwipeTime}s, Min distance: {minSwipeDistance}px</color>");

        // Check if it's a valid swipe
        if (swipeTime <= maxSwipeTime && swipeDistance >= minSwipeDistance)
        {
            Vector2 swipeDirection = (endPosition - touchData.startPosition).normalized;
            float angle = Mathf.Atan2(swipeDirection.y, swipeDirection.x) * Mathf.Rad2Deg;

            Debug.Log($"<color=cyan>[GestureDetector] Valid swipe! Angle: {angle:F1}°, Direction: {swipeDirection}</color>");

            // Check for horizontal swipe FIRST (for dual swipe detection)
            bool isHorizontal = IsHorizontalSwipe(angle, touchData.startPosition);
            if (isHorizontal)
            {
                Debug.Log($"<color=magenta>[GestureDetector] Horizontal swipe detected - checking for dual swipe</color>");
                // Add to recent swipes for dual detection
                recentSwipes.Add(touchData);

                // Check if there's another recent horizontal swipe in opposite direction
                CheckForDualHorizontalSwipe(touchData, swipeDirection);
            }

            // ALWAYS trigger directional swipe for ANY direction (not just diagonal)
            // This allows shooting in any direction!
            if (swipeDistance >= minDiagonalSwipeDistance)
            {
                // Determine if it's left or right based on X direction
                bool isLeftDiagonal = swipeDirection.x < 0;

                Debug.Log($"<color=blue>✅ DIRECTIONAL SWIPE DETECTED! Angle: {angle:F1}° Distance: {swipeDistance:F1}px</color>");
                Debug.Log($"<color=blue>Swipe vector: {swipeDirection}, Start: {touchData.startPosition}, End: {endPosition}</color>");
                Debug.Log($"<color=blue>Direction: {(isLeftDiagonal ? "LEFT" : "RIGHT")} (X: {swipeDirection.x:F2})</color>");
                Debug.Log($"<color=blue>Invoking OnDiagonalSwipe event with direction data</color>");
                OnDiagonalSwipe?.Invoke(isLeftDiagonal, swipeDirection, touchData.startPosition, endPosition);
            }
            else
            {
                Debug.Log($"<color=yellow>[GestureDetector] Swipe too short for projectile. Distance: {swipeDistance:F1}px (min: {minDiagonalSwipeDistance}px)</color>");
            }
            activeTouches.Remove(fingerId);
        }
    }
    
    /// <summary>
    /// Must be going upward (positive Y component)
    /// </summary>
    private bool IsDiagonalSwipe(float angle, Vector2 direction, out bool isLeftDiagonal)
    {
        isLeftDiagonal = false;
        
        // Must be going upward
        if (direction.y <= 0)
            return false;
        
        // Left diagonal: \ (45° to 90°, or in range [45-tolerance, 90+tolerance])
        // Right diagonal: / (90° to 135°, or in range [90-tolerance, 135+tolerance])
        
        // Left diagonal \ : angle around 45° (bottom-left to top-right)
        if (Mathf.Abs(angle - 45f) <= diagonalAngleTolerance)
        {
            isLeftDiagonal = false; // Actually right swipe /
            return true;
        }
        
        // Right diagonal / : angle around 135° (bottom-right to top-left)
        if (Mathf.Abs(angle - 135f) <= diagonalAngleTolerance)
        {
            isLeftDiagonal = true; // Actually left swipe \
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if swipe is horizontal (0° or 180° with tolerance)
    /// </summary>
    private bool IsHorizontalSwipe(float angle, Vector2 startPos)
    {
        // Normalize angle to 0-360
        if (angle < 0) angle += 360f;
        
        // Check if angle is close to 0° (right) or 180° (left)
        bool isHorizontalRight = Mathf.Abs(angle) <= horizontalAngleTolerance;
        bool isHorizontalLeft = Mathf.Abs(angle - 180f) <= horizontalAngleTolerance;
        
        return isHorizontalRight || isHorizontalLeft;
    }
    
    /// <summary>
    /// Check if there's a dual horizontal swipe (both hands swiping outward from center)
    /// </summary>
    private void CheckForDualHorizontalSwipe(TouchData currentSwipe, Vector2 currentDirection)
    {
        float screenCenterX = Screen.width / 2f;
        bool currentIsLeft = currentSwipe.startPosition.x < screenCenterX;
        bool currentSwipingOutward = (currentIsLeft && currentDirection.x < 0) || (!currentIsLeft && currentDirection.x > 0);
        
        // Current swipe must be moving outward from center
        if (!currentSwipingOutward)
            return;
        
        // Look for another recent swipe on the opposite side
        foreach (var otherSwipe in recentSwipes)
        {
            if (otherSwipe.fingerId == currentSwipe.fingerId)
                continue;
            
            bool otherIsLeft = otherSwipe.startPosition.x < screenCenterX;
            
            // Must be on opposite sides
            if (otherIsLeft == currentIsLeft)
                continue;
            
            // Check if they happened within the time window
            if (Mathf.Abs(otherSwipe.startTime - currentSwipe.startTime) <= dualSwipeTimeWindow)
            {
                Debug.Log($"<color=red>Dual horizontal swipe detected! Left & Right swipe outward from center</color>");
                OnDualHorizontalSwipe?.Invoke();
                
                // Remove the matched swipe to prevent duplicate detection
                recentSwipes.Remove(otherSwipe);
                return;
            }
        }
    }
    
    /// <summary>
    /// Visualize touch points for debugging
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugVisualization)
            return;
        
        // Draw active touches
        foreach (var touch in activeTouches.Values)
        {
            if (touch.isActive)
            {
                GUI.Box(new Rect(touch.startPosition.x - 25, Screen.height - touch.startPosition.y - 25, 50, 50), $"T{touch.fingerId}");
            }
        }
        
        // Draw screen center line
        GUI.Box(new Rect(Screen.width / 2f - 2, 0, 4, Screen.height), "");
    }
}
