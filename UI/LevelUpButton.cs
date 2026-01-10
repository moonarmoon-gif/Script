using UnityEngine;
using UnityEngine.UI;

public class LevelUpButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The button component (auto-assigned if on same GameObject)")]
    public Button levelUpButton;

    [Header("Level Up Settings")]
    [Tooltip("How many levels to add per click")]
    public int levelsPerClick = 1;

    private void Awake()
    {
        // Auto-assign button if not set
        if (levelUpButton == null)
        {
            levelUpButton = GetComponent<Button>();
        }

        if (levelUpButton == null)
        {
            Debug.LogError("LevelUpButton: No Button component found!");
            return;
        }

        // Add click listener
        levelUpButton.onClick.AddListener(OnLevelUpButtonClicked);
    }

    private void OnLevelUpButtonClicked()
    {
        // Find the player's level system (PlayerLevel component)
        PlayerLevel playerLevel = FindObjectOfType<PlayerLevel>();
        if (playerLevel != null)
        {
            // Add levels by giving EXACT XP needed for each level
            for (int i = 0; i < levelsPerClick; i++)
            {
                // Get the EXACT amount of XP needed for the NEXT level
                float xpNeeded = playerLevel.ExpToNextLevelExact;
                playerLevel.GainExperience(xpNeeded);
                Debug.Log($"<color=green>Level Up Button: Gave {xpNeeded:F2} XP! New level: {playerLevel.CurrentLevel}</color>");
            }
            return;
        }

        Debug.LogWarning("LevelUpButton: Could not find PlayerLevel component!");
    }

    private void OnDestroy()
    {
        // Clean up listener
        if (levelUpButton != null)
        {
            levelUpButton.onClick.RemoveListener(OnLevelUpButtonClicked);
        }
    }
}