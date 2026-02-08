using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Mobile UI button that can detect tap, hold, and release
/// Blocks input from passing through to game world
/// </summary>
public class MobileUIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // Static list to track if any UI button is being pressed
    private static HashSet<MobileUIButton> pressedButtons = new HashSet<MobileUIButton>();
    
    /// <summary>
    /// Check if any UI button is currently being pressed
    /// </summary>
    public static bool IsAnyButtonPressed()
    {
        return pressedButtons.Count > 0;
    }
    public enum ButtonType
    {
        SwapTerrain = 0,    // Swap fire/ice (like Spacebar)
        Projectile1 = 1,    // Switch to projectile set 0 (like key 1)
        Projectile1Alt = 2  // Legacy: previously set 3 button; now also switches to set 1
    }
    
    [Header("Button Settings")]
    [SerializeField] private ButtonType buttonType;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    
    [Header("Hold Settings")]
    [SerializeField] private bool supportHold = false;
    [SerializeField] private float holdInterval = 0.1f; // Time between hold triggers
    
    private bool isPressed = false;
    private float lastHoldTime = 0f;
    
    private void Start()
    {
        if (buttonImage == null)
        {
            buttonImage = GetComponent<Image>();
        }
        
        if (buttonImage != null)
        {
            buttonImage.color = normalColor;
        }
    }
    
    private void Update()
    {
        if (isPressed && supportHold)
        {
            if (Time.time - lastHoldTime >= holdInterval)
            {
                ExecuteButtonAction();
                lastHoldTime = Time.time;
            }
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        lastHoldTime = Time.time;
        pressedButtons.Add(this); // Track that this button is pressed
        
        if (buttonImage != null)
        {
            buttonImage.color = pressedColor;
        }
        
        ExecuteButtonAction();
        Debug.Log($"<color=yellow>Mobile button pressed: {buttonType}</color>");
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        pressedButtons.Remove(this); // Remove from pressed buttons
        
        if (buttonImage != null)
        {
            buttonImage.color = normalColor;
        }
    }
    
    private void OnDisable()
    {
        // Clean up if button is disabled while pressed
        if (isPressed)
        {
            pressedButtons.Remove(this);
            isPressed = false;
        }
    }
    
    private void ExecuteButtonAction()
    {
        if (AdvancedPlayerController.Instance == null) return;
        
        switch (buttonType)
        {
            case ButtonType.SwapTerrain:
                AdvancedPlayerController.Instance.SwapProjectileSides();
                Debug.Log("<color=cyan>Swapped terrain/projectiles (Fire ↔ Ice)</color>");
                break;
                
            case ButtonType.Projectile1:
                AdvancedPlayerController.Instance.SwitchToProjectileSet(0);
                Debug.Log("<color=orange>Switched to Projectile Set 1 (Fireball/Icicle)</color>");
                break;

            case ButtonType.Projectile1Alt:
                AdvancedPlayerController.Instance.SwitchToProjectileSet(0);
                Debug.Log("<color=orange>Switched to Projectile Set 1 (Fireball/Icicle)</color>");
                break;
        }
    }
}
