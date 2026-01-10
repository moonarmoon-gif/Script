using UnityEngine;

[CreateAssetMenu(fileName = "ExecuteOnLowHealthFavour", menuName = "Favour Effects 2/Execute On Low Health")]
public class ExecuteOnLowHealthFavour : FavourEffect
{
    [Header("Execute On Low Health")]
    public float HealthThreshold = 5f;

    [Header("Enhanced")]
    public float BonusHealthThreshold = 5f;

    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private StatusController playerStatus;
    private int sourceKey;
    private int executeStacksGranted;

    protected override int GetMaxPickLimit()
    {
        return MaxPickLimit;
    }

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerStatus = player.GetComponent<StatusController>();
        if (playerStatus == null)
        {
            return;
        }

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        if (executeStacksGranted <= 0)
        {
            executeStacksGranted = 1;
            playerStatus.AddStatus(StatusId.Execute, 1, -1f, 0f, null, sourceKey);
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        HealthThreshold += Mathf.Max(0f, BonusHealthThreshold);

        if (executeStacksGranted <= 0)
        {
            OnApply(player, manager, sourceCard);
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStatus != null && executeStacksGranted > 0)
        {
            playerStatus.ConsumeStacks(StatusId.Execute, executeStacksGranted, sourceKey);
        }

        executeStacksGranted = 0;
        playerStatus = null;
    }

    public override float GetExecuteThresholdPercent(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        return Mathf.Clamp(HealthThreshold, 0f, 100f);
    }
}
