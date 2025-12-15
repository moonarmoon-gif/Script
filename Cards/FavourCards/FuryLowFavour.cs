using UnityEngine;

[CreateAssetMenu(fileName = "FuryLowFavour", menuName = "Favour Effects/Fury Low")]
public class FuryLowFavour : FavourEffect
{
    [Header("Fury Low Settings")]
    [Tooltip("Base number of Fury stacks granted when this favour is first picked.")]
    public int FuryAmount = 2;

    [Header("Enhanced")]
    [Tooltip("Additional Fury stacks granted each time this favour is picked again (enhanced).")]
    public int BonusFury = 2;

    private int currentFuryAmount = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentFuryAmount = Mathf.Max(0, FuryAmount);
        ApplyFury(player, currentFuryAmount);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        int bonus = Mathf.Max(0, BonusFury);
        currentFuryAmount += bonus;
        ApplyFury(player, bonus);
    }

    private void ApplyFury(GameObject player, int stacksToAdd)
    {
        if (player == null || stacksToAdd <= 0)
        {
            return;
        }

        StatusController status = player.GetComponent<StatusController>();
        if (status != null)
        {
            // Fury is a permanent buff: grant stacks with infinite duration (0f).
            status.AddStatus(StatusId.Fury, stacksToAdd, -1f);
        }
    }
}
