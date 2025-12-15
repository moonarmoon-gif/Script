using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class TestTouch : MonoBehaviour
{
    private InputManager inputManager;
    private Camera cameraMain;
    private AdvancedPlayerController playerController;
    
    [Header("Tap Detection Settings")]
    [SerializeField] private float maxTapDuration = 0.3f; // Max time for a tap
    [SerializeField] private float maxTapMovement = 50f; // Max movement in pixels for a tap
    
    // Track touches to detect taps vs swipes
    private Dictionary<int, TouchInfo> activeTouches = new Dictionary<int, TouchInfo>();
    
    private class TouchInfo
    {
        public Vector2 startPosition;
        public float startTime;
    }

    private void Awake()
    {
        inputManager = GetComponent<InputManager>();
        cameraMain = Camera.main;
        playerController = GetComponent<AdvancedPlayerController>();

        if (inputManager == null) Debug.LogError("InputManager component not found on this GameObject.", this);
        if (cameraMain == null) Debug.LogError("Main Camera not found.", this);
        if (playerController == null) Debug.LogError("AdvancedPlayerController component not found!", this);
    }

    private void OnEnable()
    {
        if (inputManager != null)
        {
            inputManager.OnStartTouch += HandleTouchStart;
            inputManager.OnEndTouch += HandleTouchEnd;
        }
    }

    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.OnStartTouch -= HandleTouchStart;
            inputManager.OnEndTouch -= HandleTouchEnd;
        }
    }
    
    /// <summary>
    /// Record touch start position and time
    /// </summary>
    private void HandleTouchStart(Vector2 screenPosition, float time)
    {
        Debug.Log($"<color=cyan>HandleTouchStart called! Position: {screenPosition}, Time: {time}</color>");
        
        // Use simple key 0 for primary touch/mouse
        activeTouches[0] = new TouchInfo
        {
            startPosition = screenPosition,
            startTime = time
        };
    }
    
    /// <summary>
    /// Check if touch was a tap (quick touch without movement)
    /// Only fire projectile on TAP, not on swipe
    /// </summary>
    private void HandleTouchEnd(Vector2 screenPosition, float time)
    {
        Debug.Log($"<color=cyan>HandleTouchEnd called! Position: {screenPosition}, Time: {time}</color>");
        
        if (playerController == null)
        {
            Debug.LogError("<color=red>PlayerController is null!</color>");
            return;
        }
        
        // Check if card selection is active - if so, don't fire projectile
        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            Debug.Log("<color=orange>Touch blocked - Card selection is active</color>");
            activeTouches.Clear();
            return;
        }
        
        // Check if touching UI element - if so, don't fire projectile
        if (IsPointerOverUI())
        {
            Debug.Log("<color=orange>Touch blocked - Touching UI element</color>");
            activeTouches.Clear();
            return;
        }
        
        // Check if any UI button is being pressed - if so, don't fire projectile
        if (MobileUIButton.IsAnyButtonPressed())
        {
            Debug.Log("<color=orange>Touch blocked - UI button is being pressed</color>");
            activeTouches.Clear();
            return;
        }
        
        // Check if we have a recorded touch start (use key 0 for primary)
        if (activeTouches.ContainsKey(0))
        {
            TouchInfo touchInfo = activeTouches[0];
            float duration = time - touchInfo.startTime;
            float movement = Vector2.Distance(touchInfo.startPosition, screenPosition);
            
            Debug.Log($"<color=magenta>Touch analysis: Duration={duration:F2}s, Movement={movement:F1}px</color>");
            
            // Only fire if it was a quick tap without much movement
            if (duration <= maxTapDuration && movement <= maxTapMovement)
            {
                Debug.Log($"<color=green>✅ TAP detected! Duration: {duration:F2}s, Movement: {movement:F1}px - Firing projectile</color>");
                playerController.HandleTouchInput(screenPosition);
            }
            else
            {
                Debug.Log($"<color=yellow>⚠️ SWIPE detected! Duration: {duration:F2}s, Movement: {movement:F1}px - NOT firing (gesture detector will handle)</color>");
            }
            
            activeTouches.Remove(0);
        }
        else
        {
            Debug.LogWarning("<color=orange>No touch start recorded! This might be a mouse click or gesture detector touch.</color>");
            // For mouse clicks or if touch start wasn't recorded, just fire
            // This ensures backward compatibility
            Debug.Log("<color=green>Firing projectile anyway (mouse or missing touch start)</color>");
            playerController.HandleTouchInput(screenPosition);
        }
    }
    
    /// <summary>
    /// Check if pointer is over UI element
    /// </summary>
    private bool IsPointerOverUI()
    {
        // Check for mouse
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }
        
        // Check for touch
        if (Touchscreen.current != null)
        {
            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Touchscreen.current.touches[i].touchId.ReadValue()))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
}