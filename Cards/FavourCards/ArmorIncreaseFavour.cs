using UnityEngine;

[CreateAssetMenu(fileName = "ArmorIncreaseFavour", menuName = "Favour Effects/Armor Increase")]
public class ArmorIncreaseFavour : FavourEffect
{
    [Header("Armor Increase Settings")]
    [Tooltip("Flat armor gained each time this favour is taken.")]
    public float ArmorGain = 2f;

    private PlayerStats playerStats;
    private PlayerLevel playerLevel;
    private StatusController statusController;
    private float totalArmorGranted = 0f;
    private int totalArmorStacksGranted = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        CachePlayerComponents(player);

        if (playerStats == null)
        {
            return;
        }

        ApplyArmor(ArmorGain);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        CachePlayerComponents(player);

        if (playerStats == null)
        {
            return;
        }

        ApplyArmor(ArmorGain);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null)
        {
            return;
        }

        if (totalArmorGranted == 0f)
        {
            return;
        }

        playerStats.armor = Mathf.Max(0f, playerStats.armor - totalArmorGranted);

        if (playerLevel == null)
        {
            playerLevel = playerStats.GetComponent<PlayerLevel>();
        }

        if (playerLevel != null)
        {
            playerLevel.armor = playerStats.armor;
        }

        if (statusController != null && totalArmorStacksGranted > 0)
        {
            statusController.ConsumeStacks(StatusId.Armor, totalArmorStacksGranted);
        }

        totalArmorGranted = 0f;
        totalArmorStacksGranted = 0;
    }

    private void CachePlayerComponents(GameObject player)
    {
        if (playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerLevel == null)
        {
            playerLevel = player.GetComponent<PlayerLevel>();
        }

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }
    }

    private void ApplyArmor(float amount)
    {
        if (amount == 0f || playerStats == null)
        {
            return;
        }

        playerStats.armor = Mathf.Max(0f, playerStats.armor + amount);
        totalArmorGranted += amount;

        if (playerLevel != null)
        {
            playerLevel.armor = playerStats.armor;
        }

        if (statusController != null)
        {
            int stacks = Mathf.RoundToInt(Mathf.Max(0f, amount));
            if (stacks > 0)
            {
                statusController.AddStatus(StatusId.Armor, stacks, -1f);
                totalArmorStacksGranted += stacks;
            }
        }
    }
}