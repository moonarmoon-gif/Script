using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Button script to toggle SOUL experience gain on/off.
/// Attach to a GameObject with a Button component.
/// </summary>
[RequireComponent(typeof(Button))]
public class NoSoulExpGain : MonoBehaviour
{
    private Button button;
    private bool soulExpGainDisabled = false;

    [Header("Visual Feedback")]
    [Tooltip("Text to show on button (optional - supports both Text and TextMeshProUGUI)")]
    public UnityEngine.UI.Text buttonText;

    [Tooltip("TextMeshPro text component (optional)")]
    public TextMeshProUGUI buttonTextTMP;

    [Tooltip("Text when soul exp gain is enabled")]
    public string enabledText = "Disable Soul Exp";

    [Tooltip("Text when soul exp gain is disabled")]
    public string disabledText = "Enable Soul Exp";

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ToggleSoulExpGain);

        // Sync initial state from the system if possible
        soulExpGainDisabled = !FavourExpUI.SoulExpGainEnabled;

        UpdateButtonText();
    }

    void ToggleSoulExpGain()
    {
        soulExpGainDisabled = !soulExpGainDisabled;

        // Toggle soul exp gain in the real system
        FavourExpUI.SetSoulExpGainEnabled(!soulExpGainDisabled);

        Debug.Log($"<color=yellow>Soul Exp Gain: {(soulExpGainDisabled ? "DISABLED" : "ENABLED")}</color>");

        UpdateButtonText();
    }

    void UpdateButtonText()
    {
        string text = soulExpGainDisabled ? disabledText : enabledText;

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