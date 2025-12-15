using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Button script to give player infinite mana
/// Attach to a GameObject with a Button component
/// </summary>
[RequireComponent(typeof(Button))]
public class InfiniteManaButton : MonoBehaviour
{
    private Button button;
    
    [Header("Settings")]
    [Tooltip("Mana value to set (default 999999)")]
    public int infiniteManaValue = 999999;
    
    [Header("Visual Feedback")]
    [Tooltip("Text to show on button (optional - supports both Text and TextMeshProUGUI)")]
    public UnityEngine.UI.Text buttonText;
    
    [Tooltip("TextMeshPro text component (optional)")]
    public TextMeshProUGUI buttonTextTMP;
    
    [Tooltip("Text for button")]
    public string buttonLabel = "Infinite Mana";

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(GiveInfiniteMana);
        
        if (buttonText != null)
        {
            buttonText.text = buttonLabel;
        }
        
        if (buttonTextTMP != null)
        {
            buttonTextTMP.text = buttonLabel;
        }
    }

    void GiveInfiniteMana()
    {
        // Find PlayerMana and set mana
        PlayerMana playerMana = FindObjectOfType<PlayerMana>();
        if (playerMana != null)
        {
            playerMana.MaxMana = infiniteManaValue;
            playerMana.CurrentMana = infiniteManaValue;
            Debug.Log($"<color=cyan>Infinite Mana Activated! MaxMana and CurrentMana set to {infiniteManaValue}</color>");
        }
        else
        {
            Debug.LogWarning("InfiniteManaButton: PlayerMana not found!");
        }
    }
}
