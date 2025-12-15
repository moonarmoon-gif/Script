using UnityEngine;

[CreateAssetMenu(fileName = "FocusMediumFavour", menuName = "Favour Effects/Focus Medium")] 
public class FocusMediumFavour : FavourEffect
{
    [Header("Focus Medium Settings")]
    [Tooltip("Number of Focus stacks granted when the shield condition is met.")]
    public int FocusStack = 2;

    [Tooltip("Maximum Focus stacks this favour can grant in total.")]
    public int MaxStacks = 2;

    [Header("Enhanced")]
    [Tooltip("Additional Focus stacks granted when this favour is enhanced.")]
    public int BonusFocusStack = 2;

    [Tooltip("Additional maximum Focus stacks allowed when this favour is enhanced.")]
    public int BonusMaxStacks = 2;

    private PlayerHealth playerHealth;
    private StatusController statusController;

    private int currentFocusStack;
    private int currentMaxStacks;
    private int stacksGranted;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (playerHealth == null || statusController == null)
        {
            return;
        }

        currentFocusStack = Mathf.Max(0, FocusStack);
        currentMaxStacks = Mathf.Max(0, MaxStacks);

        // Reset and clamp our internal counter so it never exceeds the max for
        // this run. We only track stacks granted by this favour instance.
        stacksGranted = 0;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerHealth == null || statusController == null)
        {
            OnApply(player, manager, sourceCard);
        }

        currentFocusStack += Mathf.Max(0, BonusFocusStack);
        currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
        currentMaxStacks = Mathf.Max(0, currentMaxStacks);

        stacksGranted = Mathf.Clamp(stacksGranted, 0, currentMaxStacks);
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerHealth == null || statusController == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (currentFocusStack <= 0 || currentMaxStacks <= 0)
        {
            return;
        }

        if (stacksGranted >= currentMaxStacks)
        {
            return;
        }

        if (!HasAnyShield())
        {
            return;
        }

        int remaining = currentMaxStacks - stacksGranted;
        if (remaining <= 0)
        {
            return;
        }

        int toGrant = Mathf.Min(currentFocusStack, remaining);
        if (toGrant <= 0)
        {
            return;
        }

        statusController.AddStatus(StatusId.Focus, toGrant, -1f);
        stacksGranted += toGrant;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (statusController != null && stacksGranted > 0)
        {
            statusController.ConsumeStacks(StatusId.Focus, stacksGranted);
        }

        stacksGranted = 0;
        playerHealth = null;
        statusController = null;
    }

    private bool HasAnyShield()
    {
        // Treat an active HolyShield as a valid shield source.
        if (HolyShield.ActiveShield != null && HolyShield.ActiveShield.IsAlive)
        {
            return true;
        }

        // Future-proof: also treat generic SHIELD status as a shield source if
        // it is ever used in the status system.
        if (statusController != null && statusController.GetStacks(StatusId.Shield) > 0)
        {
            return true;
        }

        return false;
    }
}
