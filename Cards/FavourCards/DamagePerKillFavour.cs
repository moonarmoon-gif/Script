using UnityEngine;

/// <summary>
/// Favour effect: damage increases per enemy killed, with stacks that reset
/// if you don't get a kill within a given time window.
/// </summary>
[CreateAssetMenu(fileName = "DamagePerKillFavour", menuName = "Favour Effects/Damage Per Kill")]
public class DamagePerKillFavour : FavourEffect
{
    [Header("Damage Per Kill Settings")]
    public int FuryGainPerKill = 1;

    public int maxStacks = 25;

    public float stackResetTimer = 5f;

    private int currentStacks = 0;
    private float lastStackTime = 0f;
    private StatusController statusController;
    private int sourceKey;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        statusController = player.GetComponent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        currentStacks = 0;
        lastStackTime = Time.time;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        ResetStacks();
        statusController = null;
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (statusController == null)
        {
            return;
        }

        float now = Time.time;

        if (now - lastStackTime > stackResetTimer)
        {
            ResetStacks();
        }

        lastStackTime = now;

        int cap = Mathf.Max(0, maxStacks);
        if (currentStacks < cap)
        {
            int gain = Mathf.Max(0, FuryGainPerKill);
            if (gain > 0)
            {
                int allowed = cap - currentStacks;
                int toAdd = Mathf.Min(gain, allowed);
                if (toAdd > 0)
                {
                    statusController.AddStatus(StatusId.Fury, toAdd, -1f, 0f, null, sourceKey);
                    currentStacks += toAdd;
                }
            }
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (statusController == null || currentStacks <= 0)
        {
            return;
        }

        if (Time.time - lastStackTime > stackResetTimer)
        {
            ResetStacks();
        }
    }

    private void ResetStacks()
    {
        if (statusController != null && currentStacks > 0)
        {
            statusController.ConsumeStacks(StatusId.Fury, currentStacks, sourceKey);
        }

        currentStacks = 0;
    }
}
