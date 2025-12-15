using UnityEngine;

[CreateAssetMenu(fileName = "ManaOnStatusDeathFavour", menuName = "Favour Effects/Mana On Status Death")]
public class ManaOnStatusDeathFavour : FavourEffect
{
    [Header("Mana On Status Death Settings")]
    [Tooltip("Amount of mana (max and current) gained per qualifying enemy kill.")]
    public float ManaIncrease = 2f;

    [Tooltip("Maximum total mana this favour can grant over the run.")]
    public float MaxMana = 50f;

    [Header("Enhanced")]
    [Tooltip("Additional mana gained per qualifying kill when this favour is enhanced.")]
    public float BonusManaIncrease = 2f;

    [Tooltip("Additional maximum mana this favour can grant when enhanced.")]
    public float BonusMaxMana = 50f;

    private PlayerMana playerMana;
    private float currentManaIncrease;
    private float currentMaxExtraMana;
    private float totalGrantedMana;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerMana == null)
        {
            playerMana = player.GetComponent<PlayerMana>();
        }

        if (playerMana == null)
        {
            return;
        }

        currentManaIncrease = Mathf.Max(0f, ManaIncrease);
        currentMaxExtraMana = Mathf.Max(0f, MaxMana);
        totalGrantedMana = 0f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerMana == null && player != null)
        {
            playerMana = player.GetComponent<PlayerMana>();
        }

        if (playerMana == null)
        {
            return;
        }

        currentManaIncrease += Mathf.Max(0f, BonusManaIncrease);
        currentMaxExtraMana += Mathf.Max(0f, BonusMaxMana);
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (playerMana == null || enemy == null)
        {
            return;
        }

        StatusController status = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (status == null)
        {
            return;
        }

        if (!HasElementalStatus(status))
        {
            return;
        }

        if (currentManaIncrease <= 0f || currentMaxExtraMana <= 0f)
        {
            return;
        }

        float remainingRoom = Mathf.Max(0f, currentMaxExtraMana - totalGrantedMana);
        if (remainingRoom <= 0f)
        {
            return;
        }

        float grant = Mathf.Min(currentManaIncrease, remainingRoom);
        if (grant <= 0f)
        {
            return;
        }

        playerMana.IncreaseMaxMana(grant);
        totalGrantedMana += grant;
    }

    private static bool HasElementalStatus(StatusController status)
    {
        if (status == null)
        {
            return false;
        }

        // Treat fire, ice, and lightning-related debuffs as elemental.
        if (status.HasStatus(StatusId.Burn) ||
            status.HasStatus(StatusId.Scorched) ||
            status.HasStatus(StatusId.Immolation) ||
            status.HasStatus(StatusId.Slow) ||
            status.HasStatus(StatusId.Frostbite) ||
            status.HasStatus(StatusId.Freeze) ||
            status.HasStatus(StatusId.Static) ||
            status.HasStatus(StatusId.StaticReapply) ||
            status.HasStatus(StatusId.Shocked))
        {
            return true;
        }

        return false;
    }
}
