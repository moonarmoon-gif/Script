using UnityEngine;

[CreateAssetMenu(fileName = "CloseFuryFavour", menuName = "Favour Effects/Close Fury")]
public class CloseFuryFavour : FavourEffect
{
    [Header("Close Fury Settings")]
    public float Radius = 6f;
    public int FuryGain = 1;

    [Header("Enhanced")]
    public int BonusFuryGain = 1;

    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private StatusController playerStatus;
    private Transform playerTransform;

    private int sourceKey;
    private int stacksGranted;
    private float rescanTimer;

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
        playerTransform = player.transform;

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        stacksGranted = 0;
        rescanTimer = 0f;

        UpdateFuryStacks(true);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        FuryGain += Mathf.Max(0, BonusFuryGain);
        UpdateFuryStacks(true);
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerStatus == null || playerTransform == null)
        {
            return;
        }

        rescanTimer -= deltaTime;
        if (rescanTimer > 0f)
        {
            return;
        }

        rescanTimer = 0.1f;
        UpdateFuryStacks(false);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStatus != null && stacksGranted > 0)
        {
            playerStatus.ConsumeStacks(StatusId.Fury, stacksGranted, sourceKey);
        }

        stacksGranted = 0;
        playerStatus = null;
        playerTransform = null;
    }

    private void UpdateFuryStacks(bool force)
    {
        if (playerStatus == null || playerTransform == null)
        {
            return;
        }

        float radius = Mathf.Max(0f, Radius);
        int perEnemy = Mathf.Max(0, FuryGain);

        int desired = 0;
        if (radius > 0f && perEnemy > 0)
        {
            EnemyHealth[] enemies = Object.FindObjectsOfType<EnemyHealth>();
            if (enemies != null && enemies.Length > 0)
            {
                float radiusSq = radius * radius;

                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyHealth enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive)
                    {
                        continue;
                    }

                    Transform t = enemy.transform;
                    if (t == null)
                    {
                        continue;
                    }

                    Vector3 diff = t.position - playerTransform.position;
                    if (diff.sqrMagnitude <= radiusSq)
                    {
                        desired += perEnemy;
                    }
                }
            }
        }

        int delta = desired - stacksGranted;
        if (delta > 0)
        {
            playerStatus.AddStatus(StatusId.Fury, delta, -1f, 0f, null, sourceKey);
            stacksGranted += delta;
        }
        else if (delta < 0)
        {
            playerStatus.ConsumeStacks(StatusId.Fury, -delta, sourceKey);
            stacksGranted += delta;
        }
    }
}
