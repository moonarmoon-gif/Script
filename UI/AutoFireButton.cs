using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Button script to toggle auto-fire mode
/// Attach to a GameObject with a Button component
/// </summary>
[RequireComponent(typeof(Button))]
public class AutoFireButton : MonoBehaviour
{
    private Button button;
    private bool autoFireEnabled = false;
    
    [Header("Visual Feedback")]
    [Tooltip("Text to show on button (optional - supports both Text and TextMeshProUGUI)")]
    public UnityEngine.UI.Text buttonText;
    
    [Tooltip("TextMeshPro text component (optional)")]
    public TextMeshProUGUI buttonTextTMP;
    
    [Tooltip("Text when auto-fire is off")]
    public string offText = "Enable Auto-Fire";
    
    [Tooltip("Text when auto-fire is on")]
    public string onText = "Disable Auto-Fire";

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ToggleAutoFire);
        
        UpdateButtonText();
    }

    void ToggleAutoFire()
    {
        autoFireEnabled = !autoFireEnabled;
        
        // Find AdvancedPlayerController and toggle auto-fire
        AdvancedPlayerController playerController = FindObjectOfType<AdvancedPlayerController>();
        if (playerController != null)
        {
            playerController.enableAutoFire = autoFireEnabled;
            Debug.Log($"<color=yellow>Auto-Fire: {(autoFireEnabled ? "ENABLED" : "DISABLED")}</color>");
        }
        else
        {
            Debug.LogWarning("AutoFireButton: AdvancedPlayerController not found!");
        }
        
        UpdateButtonText();
    }
    
    void UpdateButtonText()
    {
        string text = autoFireEnabled ? onText : offText;
        
        if (buttonText != null)
        {
            buttonText.text = text;
        }
        
        if (buttonTextTMP != null)
        {
            buttonTextTMP.text = text;
        }
    }
}
