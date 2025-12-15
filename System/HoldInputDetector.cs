using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Detects press-and-hold input for continuous actions (like laser beam)
/// Works with both mouse and touch input
/// </summary>
public class HoldInputDetector : MonoBehaviour
{
    [Header("Hold Settings")]
    [SerializeField] private float holdInterval = 0.1f; // Time between hold triggers
    [SerializeField] private float initialDelay = 0.2f; // Delay before hold starts repeating
    
    public delegate void HoldEvent(Vector2 screenPosition, float holdDuration);
    public event HoldEvent OnHoldStart;
    public event HoldEvent OnHolding;
    public event HoldEvent OnHoldEnd;
    
    private bool isHolding = false;
    private Vector2 holdPosition;
    private float holdStartTime;
    private float lastHoldTriggerTime;
    private Coroutine holdCoroutine;
    
    private void Update()
    {
        // Handle touch input
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
            
            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                StartHold(touchPos);
            }
            else if (isHolding)
            {
                UpdateHold(touchPos);
            }
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame && isHolding)
        {
            EndHold();
        }
        // Handle mouse input (for PC testing)
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                StartHold(mousePos);
            }
            else if (isHolding)
            {
                UpdateHold(mousePos);
            }
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame && isHolding)
        {
            EndHold();
        }
    }
    
    private void StartHold(Vector2 position)
    {
        isHolding = true;
        holdPosition = position;
        holdStartTime = Time.time;
        lastHoldTriggerTime = Time.time;
        
        OnHoldStart?.Invoke(position, 0f);
        
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
        }
        holdCoroutine = StartCoroutine(HoldRoutine());
        
        Debug.Log($"<color=yellow>Hold started at {position}</color>");
    }
    
    private void UpdateHold(Vector2 position)
    {
        holdPosition = position;
    }
    
    private void EndHold()
    {
        if (!isHolding) return;
        
        float holdDuration = Time.time - holdStartTime;
        OnHoldEnd?.Invoke(holdPosition, holdDuration);
        
        isHolding = false;
        
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
        
        Debug.Log($"<color=yellow>Hold ended. Duration: {holdDuration:F2}s</color>");
    }
    
    private IEnumerator HoldRoutine()
    {
        // Wait for initial delay
        yield return new WaitForSeconds(initialDelay);
        
        // Continuous hold triggers
        while (isHolding)
        {
            float holdDuration = Time.time - holdStartTime;
            OnHolding?.Invoke(holdPosition, holdDuration);
            lastHoldTriggerTime = Time.time;
            
            yield return new WaitForSeconds(holdInterval);
        }
    }
    
    /// <summary>
    /// Get current hold duration (0 if not holding)
    /// </summary>
    public float GetHoldDuration()
    {
        return isHolding ? Time.time - holdStartTime : 0f;
    }
    
    /// <summary>
    /// Check if currently holding
    /// </summary>
    public bool IsHolding()
    {
        return isHolding;
    }
}
