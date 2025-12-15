using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Button script to toggle experience gain on/off
/// Attach to a GameObject with a Button component
/// </summary>
[RequireComponent(typeof(Button))]
public class NoExpGainButton : MonoBehaviour
{
    private Button button;
    private bool expGainDisabled = false;
    
    [Header("Visual Feedback")]
    [Tooltip("Text to show on button (optional - supports both Text and TextMeshProUGUI)")]
    public UnityEngine.UI.Text buttonText;
    
    [Tooltip("TextMeshPro text component (optional)")]
    public TextMeshProUGUI buttonTextTMP;
    
    [Tooltip("Text when exp gain is enabled")]
    public string enabledText = "Disable Exp";
    
    [Tooltip("Text when exp gain is disabled")]
    public string disabledText = "Enable Exp";

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ToggleExpGain);
        
        UpdateButtonText();
    }

    void ToggleExpGain()
    {
        expGainDisabled = !expGainDisabled;
        
        // Find PlayerLevel and toggle exp gain
        PlayerLevel playerLevel = FindObjectOfType<PlayerLevel>();
        if (playerLevel != null)
        {
            playerLevel.SetExpGainEnabled(!expGainDisabled);
            Debug.Log($"<color=yellow>Exp Gain: {(expGainDisabled ? "DISABLED" : "ENABLED")}</color>");
        }
        else
        {
            Debug.LogWarning("NoExpGainButton: PlayerLevel not found!");
        }
        
        UpdateButtonText();
    }
    
    void UpdateButtonText()
    {
        string text = expGainDisabled ? disabledText : enabledText;
        
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
