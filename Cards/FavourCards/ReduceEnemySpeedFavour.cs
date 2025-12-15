using UnityEngine;

[CreateAssetMenu(fileName = "BurdenLowFavour", menuName = "Favour Effects/Burden Low")]
public class BurdenLow : FavourEffect
{
    [Header("Burden Low Settings")]
    [Tooltip("Number of Burden stacks applied to all enemies each time this favour is selected.")]
    public int BurdenStacks = 5;

    [Tooltip("How often to rescan for newly spawned enemies (seconds).")]
    public float rescanInterval = 0.5f;

    private int totalStacks = 0;
    private float timeSinceLastScan = 0f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        int toAdd = Mathf.Max(0, BurdenStacks);
        if (toAdd <= 0)
        {
            return;
        }

        totalStacks += toAdd;
        ApplyBurdenToAllEnemies();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        int toAdd = Mathf.Max(0, BurdenStacks);
        if (toAdd <= 0)
        {
            return;
        }

        totalStacks += toAdd;
        ApplyBurdenToAllEnemies();
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (totalStacks <= 0 || rescanInterval <= 0f)
        {
            return;
        }

        timeSinceLastScan += deltaTime;
        if (timeSinceLastScan >= rescanInterval)
        {
            timeSinceLastScan = 0f;
            ApplyBurdenToAllEnemies();
        }
    }

    private void ApplyBurdenToAllEnemies()
    {
        if (totalStacks <= 0)
        {
            return;
        }

        EnemyHealth[] enemies = Object.FindObjectsOfType<EnemyHealth>();
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            StatusController status = enemy.GetComponent<StatusController>();
            if (status == null)
            {
                continue;
            }

            int existing = status.GetStacks(StatusId.Burden);
            int toAdd = Mathf.Max(0, totalStacks - existing);
            if (toAdd > 0)
            {
                status.AddStatus(StatusId.Burden, toAdd, -1f);
            }
        }
    }
}

// Backwards-compatibility alias so existing assets that reference the old
// ReduceEnemySpeedFavour script continue to function using BurdenLow logic.
public class ReduceEnemySpeedFavour : BurdenLow { }
