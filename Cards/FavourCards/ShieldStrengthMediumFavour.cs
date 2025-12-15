using UnityEngine;

[CreateAssetMenu(fileName = "ShieldStrengthMediumFavour", menuName = "Favour Effects/Shield Strength Medium")] 
public class ShieldStrengthMediumFavour : FavourEffect
{
    [Header("Shield Strength Medium Settings")]
    [Tooltip("Base ShieldStrength stacks granted when this favour is first picked.")]
    public int ShieldStrengthValue = 10;

    [Header("Enhanced")]
    [Tooltip("Additional ShieldStrength stacks granted each time this favour is picked again (enhanced).")]
    public int BonusShieldStrength = 10;

    private int currentShieldStrengthValue = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentShieldStrengthValue = Mathf.Max(0, ShieldStrengthValue);
        ApplyShieldStrength(player, currentShieldStrengthValue);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        int bonus = Mathf.Max(0, BonusShieldStrength);
        currentShieldStrengthValue += bonus;
        ApplyShieldStrength(player, bonus);
    }

    private void ApplyShieldStrength(GameObject player, int stacksToAdd)
    {
        if (player == null || stacksToAdd <= 0)
        {
            return;
        }

        StatusController status = player.GetComponent<StatusController>();
        if (status != null)
        {
            // ShieldStrength is a permanent buff that scales HolyShield damage
            // reduction; grant stacks with infinite duration (0f).
            status.AddStatus(StatusId.ShieldStrength, stacksToAdd, -1f);
        }
    }
}
