using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Button script to toggle auto-fire mode
/// Attach to a GameObject with a Button component
/// </summary>
[RequireComponent(typeof(Button))]
public class AutoFireButton : MonoBehaviour
{
    private Button button;
    private bool autoFireEnabled = false;

    private AdvancedPlayerController cachedController;
    
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

        SyncFromController();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SyncFromController();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncFromController();
    }

    void ToggleAutoFire()
    {
        if (cachedController == null)
        {
            cachedController = AdvancedPlayerController.Instance;
        }

        if (cachedController == null)
        {
            cachedController = FindObjectOfType<AdvancedPlayerController>(true);
        }

        if (cachedController == null)
        {
            Debug.LogWarning("AutoFireButton: AdvancedPlayerController not found!");
            SyncFromController();
            return;
        }

        autoFireEnabled = !cachedController.enableAutoFire;
        cachedController.enableAutoFire = autoFireEnabled;
        Debug.Log($"<color=yellow>Auto-Fire: {(autoFireEnabled ? "ENABLED" : "DISABLED")}</color>");
        UpdateButtonText();
    }

    private void SyncFromController()
    {
        if (cachedController == null)
        {
            cachedController = AdvancedPlayerController.Instance;
        }

        if (cachedController == null)
        {
            cachedController = FindObjectOfType<AdvancedPlayerController>(true);
        }

        if (cachedController != null)
        {
            autoFireEnabled = cachedController.enableAutoFire;
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
