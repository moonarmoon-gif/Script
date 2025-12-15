using UnityEngine;

[CreateAssetMenu(fileName = "ManaRegenOnKillFavour", menuName = "Favour Effects/Mana Regen On Kill")]
public class ManaRegenOnKillFavour : FavourEffect
{
    [Header("Mana Regen On Kill Settings")]
    [Tooltip("Mana regeneration per second granted while the buff is active (e.g. 0.5 = +0.5 mana/s).")]
    public float ManaRegen = 0.5f;

    [Tooltip("Duration in seconds that the regen buff stays active after killing an enemy.")]
    public float Duration = 3f;

    private PlayerStats playerStats;
    private float currentAppliedBonus = 0f;
    private float buffEndTime = 0f;
    private int stacks = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerStats = player.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning($"<color=yellow>ManaRegenOnKillFavour could not find PlayerStats on {player.name}.</color>");
            return;
        }

        stacks = 1;
        currentAppliedBonus = 0f;
        buffEndTime = 0f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        // Enhanced: increase mana regen by the same base amount per extra copy
        stacks++;

        if (currentAppliedBonus > 0f && Time.time < buffEndTime)
        {
            ApplyRegenBonus();
        }
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (playerStats == null)
        {
            return;
        }

        ApplyRegenBonus();
        buffEndTime = Time.time + Mathf.Max(0f, Duration);
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerStats == null)
        {
            return;
        }

        if (currentAppliedBonus > 0f && Time.time >= buffEndTime)
        {
            RemoveRegenBonus();
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        RemoveRegenBonus();
    }

    private void ApplyRegenBonus()
    {
        if (playerStats == null)
        {
            return;
        }

        if (stacks <= 0 || ManaRegen <= 0f)
        {
            return;
        }

        float targetBonus = stacks * ManaRegen;
        float delta = targetBonus - currentAppliedBonus;
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        playerStats.manaRegenPerSecond += delta;
        currentAppliedBonus = targetBonus;
    }

    private void RemoveRegenBonus()
    {
        if (playerStats == null)
        {
            return;
        }

        if (!Mathf.Approximately(currentAppliedBonus, 0f))
        {
            playerStats.manaRegenPerSecond -= currentAppliedBonus;
        }

        currentAppliedBonus = 0f;
    }
}
