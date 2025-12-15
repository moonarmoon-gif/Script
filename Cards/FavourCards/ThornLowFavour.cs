using UnityEngine;

[CreateAssetMenu(fileName = "ThornLowFavour", menuName = "Favour Effects/Thorn Low")]
public class ThornLowFavour : FavourEffect
{
    [Header("Thorn Low Settings")]
    [Tooltip("Flat Thorn stacks granted when this favour is first obtained.")]
    public int ThornAmount = 10;

    [Header("Enhanced")]
    [Tooltip("Additional Thorn stacks granted when this favour is enhanced.")]
    public int BonusThorn = 10;

    private int currentThornAmount = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentThornAmount = Mathf.Max(0, ThornAmount);
        ApplyThorn(player, currentThornAmount);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        int bonus = Mathf.Max(0, BonusThorn);
        currentThornAmount += bonus;
        ApplyThorn(player, bonus);
    }

    private void ApplyThorn(GameObject player, int stacksToAdd)
    {
        if (player == null || stacksToAdd <= 0)
        {
            return;
        }

        StatusController status = player.GetComponent<StatusController>();
        if (status != null)
        {
            status.AddStatus(StatusId.Thorn, stacksToAdd, -1f);
        }
    }
}
