using UnityEngine;

[CreateAssetMenu(fileName = "FocusLowFavour", menuName = "Favour Effects/Focus Low")] 
public class FocusLowFavour : FavourEffect
{
    [Header("Focus Low Settings")]
    [Tooltip("Focus stacks granted each time the no-HP-damage window completes.")]
    public int FocusGain = 1;

    [Tooltip("Maximum Focus stacks this favour can push the player to.")]
    public int MaxStacks = 2;

    [Header("Enhanced")]
    [Tooltip("Additional Focus stacks granted per enhancement.")]
    public int BonusFocusGain = 1;

    [Tooltip("Additional maximum Focus stacks allowed per enhancement.")]
    public int BonusMaxStacks = 2;

    private const float NoHpDamageWindowSeconds = 5f;

    private PlayerHealth playerHealth;
    private StatusController statusController;
    private float lastHpDamageTime;
    private float lastKnownHealth;
    private int currentFocusGain;
    private int currentMaxStacks;
    private bool subscribedToHealth;
    // Tracks how many Focus stacks this favour has granted so we can
    // remove exactly our own contribution when HP damage is taken or the
    // favour is removed, without touching Focus stacks from other sources.
    private int focusStacksGranted;

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

        currentFocusGain = Mathf.Max(0, FocusGain);
        currentMaxStacks = Mathf.Max(0, MaxStacks);

        lastKnownHealth = playerHealth.CurrentHealth;
        lastHpDamageTime = Time.time;
        focusStacksGranted = 0;

        if (!subscribedToHealth)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            subscribedToHealth = true;
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerHealth == null || statusController == null)
        {
            OnApply(player, manager, sourceCard);
        }
        else
        {
            currentFocusGain += Mathf.Max(0, BonusFocusGain);
            currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerHealth == null || statusController == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (currentFocusGain <= 0 || currentMaxStacks <= 0)
        {
            return;
        }

        if (Time.time < lastHpDamageTime + NoHpDamageWindowSeconds)
        {
            return;
        }

        int existing = statusController.GetStacks(StatusId.Focus);
        if (existing >= currentMaxStacks)
        {
            return;
        }

        int room = currentMaxStacks - existing;
        int toAdd = Mathf.Min(room, currentFocusGain);
        if (toAdd <= 0)
        {
            return;
        }

        statusController.AddStatus(StatusId.Focus, toAdd, -1f);

        // Track only the stacks this favour has contributed so we can
        // cleanly remove them later without affecting other Focus sources.
        focusStacksGranted += toAdd;

        // Require another full window without HP damage before granting more.
        lastHpDamageTime = Time.time;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (subscribedToHealth && playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
        }

        // When this favour is removed, also remove any Focus stacks it
        // previously granted so they do not linger permanently.
        if (statusController != null && focusStacksGranted > 0)
        {
            statusController.ConsumeStacks(StatusId.Focus, focusStacksGranted);
        }

        focusStacksGranted = 0;
        subscribedToHealth = false;
        playerHealth = null;
        statusController = null;
    }

    private void HandleHealthChanged(float current, float max)
    {
        // Only reset the timer when actual HP is lost (not when healing or
        // when damage is absorbed entirely by shields).
        if (current < lastKnownHealth - 0.01f)
        {
            lastHpDamageTime = Time.time;

            // As soon as the player takes real HP damage, remove all Focus
            // stacks granted by this favour so the buff truly only applies
            // while taking no HP damage.
            if (statusController != null && focusStacksGranted > 0)
            {
                statusController.ConsumeStacks(StatusId.Focus, focusStacksGranted);
                focusStacksGranted = 0;
            }
        }

        lastKnownHealth = current;
    }
}
