using UnityEngine;

[CreateAssetMenu(fileName = "FirstStrikeLowFavour", menuName = "Favour Effects/First Strike Low")]
public class FirstStrikeLowFavour : FavourEffect
{
    [Header("First Strike Low Settings")]
    [Tooltip("Base number of FirstStrike stacks granted when this favour is first obtained.")]
    public int StackNumber = 1;

    [Header("Enhanced")]
    [Tooltip("Additional FirstStrike stacks granted when this favour is enhanced.")]
    public int BonusStack = 1;

    private int currentStacks = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentStacks = Mathf.Max(0, StackNumber);
        ApplyFirstStrike(player, currentStacks);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        int bonus = Mathf.Max(0, BonusStack);
        currentStacks += bonus;
        ApplyFirstStrike(player, bonus);
    }

    private void ApplyFirstStrike(GameObject player, int stacksToAdd)
    {
        if (player == null || stacksToAdd <= 0)
        {
            return;
        }

        StatusController status = player.GetComponent<StatusController>();
        if (status != null)
        {
            status.AddStatus(StatusId.FirstStrike, stacksToAdd, -1f);
        }
    }
}
