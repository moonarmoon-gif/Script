using UnityEngine;

[CreateAssetMenu(fileName = "FuryOnDamage", menuName = "Favour Effects/Fury On Damage")]
public class FuryOnDamageFavour : FavourEffect
{
    [Header("Fury On Damage Settings")]
    [Tooltip("Fury stacks gained each time the player takes at least 1 real HP damage.")]
    public int FuryGained = 1;

    [Tooltip("Maximum Fury stacks this favour can maintain at once. 0 = unlimited.")]
    public int MaxStacks = 0;

    [Tooltip("Duration in pause-safe seconds before all Fury from this favour expires.")]
    public float CountdownTimer = 5f;

    [Header("Enhanced")]
    [Tooltip("Additional MaxStacks granted each time this favour is chosen again. Ignored when MaxStacks is 0.")]
    public int BonusMaxStacks = 0;

    private StatusController statusController;
    private int sourceKey;
    private int currentMaxStacks;
    private float expiryTime = -1f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (!TryInitialize(player, true))
        {
            return;
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (!TryInitialize(player, false))
        {
            return;
        }

        if (currentMaxStacks > 0)
        {
            currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (statusController == null)
        {
            return;
        }

        int grantedStacks = GetGrantedStacks();
        if (grantedStacks <= 0 || expiryTime < 0f)
        {
            return;
        }

        if (GameStateManager.PauseSafeTime >= expiryTime)
        {
            ResetGrantedStacks();
        }
    }

    public override void OnPlayerDamageFinalized(GameObject player, GameObject attacker, float finalDamage, bool isStatusTick, bool isAoeDamage, FavourEffectManager manager)
    {
        if (statusController == null || finalDamage < 1f)
        {
            return;
        }

        int gain = Mathf.Max(0, FuryGained);
        if (gain <= 0)
        {
            return;
        }

        int grantedStacks = GetGrantedStacks();
        if (currentMaxStacks > 0)
        {
            int remainingCapacity = currentMaxStacks - grantedStacks;
            if (remainingCapacity <= 0)
            {
                return;
            }

            gain = Mathf.Min(gain, remainingCapacity);
        }

        statusController.AddStatus(StatusId.Fury, gain, -1f, 0f, null, sourceKey);
        expiryTime = GameStateManager.PauseSafeTime + Mathf.Max(0f, CountdownTimer);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        ResetGrantedStacks();
        statusController = null;
    }

    private bool TryInitialize(GameObject player, bool resetRuntimeState)
    {
        if (player == null)
        {
            return false;
        }

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (statusController == null)
        {
            return false;
        }

        if (sourceKey == 0)
        {
            sourceKey = Mathf.Abs(GetInstanceID());
            if (sourceKey == 0)
            {
                sourceKey = 1;
            }
        }

        if (resetRuntimeState)
        {
            currentMaxStacks = Mathf.Max(0, MaxStacks);
            expiryTime = -1f;
        }
        else if (currentMaxStacks == 0 && MaxStacks > 0)
        {
            currentMaxStacks = Mathf.Max(0, MaxStacks);
        }

        return true;
    }

    private int GetGrantedStacks()
    {
        if (statusController == null)
        {
            return 0;
        }

        return Mathf.Max(0, statusController.GetStacks(StatusId.Fury, sourceKey));
    }

    private void ResetGrantedStacks()
    {
        if (statusController == null)
        {
            return;
        }

        int grantedStacks = GetGrantedStacks();
        if (grantedStacks > 0)
        {
            statusController.ConsumeStacks(StatusId.Fury, grantedStacks, sourceKey);
        }

        expiryTime = -1f;
    }
}
