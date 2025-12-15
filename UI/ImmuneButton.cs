using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Button script to toggle player immunity
/// Attach to a GameObject with a Button component
/// </summary>
[RequireComponent(typeof(Button))]
public class ImmuneButton : MonoBehaviour
{
    private Button button;
    private bool immuneEnabled = false;
    
    [Header("Visual Feedback")]
    [Tooltip("Text to show on button (optional - supports both Text and TextMeshProUGUI)")]
    public UnityEngine.UI.Text buttonText;
    
    [Tooltip("TextMeshPro text component (optional)")]
    public TextMeshProUGUI buttonTextTMP;
    
    [Tooltip("Text when immunity is off")]
    public string offText = "Enable Immune";
    
    [Tooltip("Text when immunity is on")]
    public string onText = "Disable Immune";

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ToggleImmunity);
        
        UpdateButtonText();
    }

    void ToggleImmunity()
    {
        immuneEnabled = !immuneEnabled;
        
        // Find PlayerHealth and toggle immunity
        PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.immune = immuneEnabled;
            Debug.Log($"<color=yellow>Player Immunity: {(immuneEnabled ? "ENABLED" : "DISABLED")}</color>");
        }
        else
        {
            Debug.LogWarning("ImmuneButton: PlayerHealth not found!");
        }
        
        UpdateButtonText();
    }
    
    void UpdateButtonText()
    {
        string text = immuneEnabled ? onText : offText;
        
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
