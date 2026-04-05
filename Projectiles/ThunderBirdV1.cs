using UnityEngine;

[RequireComponent(typeof(ThunderBird))]
public class ThunderBirdV1 : MonoBehaviour
{
    [Header("Enhanced Variant 1 - Dual Thunder")]
    [Tooltip("Strike radius increase for Enhanced Variant 1 (0.25 = +25%)")]
    [SerializeField] private float enhancedStrikeRadiusIncrease = 0.25f;

    [Tooltip("Speed bonus for Enhanced Variant 1")]
    [SerializeField] private float variant1SpeedBonus = 10f;

    [Tooltip("Cooldown reduction for Enhanced Variant 1 (0.25 = 25% reduction)")]
    [SerializeField] private float variant1CooldownReduction = 0.25f;

    [Tooltip("Base cooldown for Enhanced Variant 1 (seconds). If 0, falls back to ProjectileCards.runtimeSpawnInterval or script cooldown.")]
    [SerializeField] private float variant1BaseCooldown = 0f;

    [Tooltip("Size multiplier for Enhanced Variant 1 (Dual Thunder). Applies on top of normal size modifiers.")]
    [SerializeField] private float variant1SizeMultiplier = 1f;

    private ThunderBird bird;

    private static bool hasPendingPair = false;
    private static int pendingPairFrame = -999;
    private static bool pendingPairFirstSpawnFromLeft = true;
    private static bool pendingPairFirstSpawnTop = true;
    private static int pendingPairBirdCount = 0;

    private bool isActive;

    private void Awake()
    {
        bird = GetComponent<ThunderBird>();
    }

    public void Configure(bool active)
    {
        isActive = active;
    }

    public bool IsActive => isActive;

    public float GetStrikeRadiusMultiplier()
    {
        return isActive ? (1f + enhancedStrikeRadiusIncrease) : 1f;
    }

    public float GetSpeedBonus()
    {
        return isActive ? variant1SpeedBonus : 0f;
    }

    public bool TryGetBaseCooldownOverride(out float baseCooldownOverride)
    {
        baseCooldownOverride = variant1BaseCooldown;
        return isActive && variant1BaseCooldown > 0f;
    }

    public float GetCooldownMultiplier()
    {
        return isActive ? (1f - variant1CooldownReduction) : 1f;
    }

    public float GetSizeMultiplier()
    {
        return isActive ? variant1SizeMultiplier : 1f;
    }

    public bool TryApplyVariant1SpawnPosition(Vector3 requestedSpawnPosition)
    {
        if (!isActive || bird == null) return false;

        Transform minPos = bird.MinPos;
        Transform maxPos = bird.MaxPos;
        if (minPos == null || maxPos == null) return false;

        float minY = minPos.position.y;
        float maxY = maxPos.position.y;
        float midY = (minY + maxY) / 2f;

        bool pendingExpired = (Time.frameCount - pendingPairFrame) > 2;
        if (!hasPendingPair || pendingExpired || pendingPairBirdCount >= 2)
        {
            hasPendingPair = true;
            pendingPairFrame = Time.frameCount;
            pendingPairFirstSpawnFromLeft = Random.value < 0.5f;
            pendingPairFirstSpawnTop = Random.value < 0.5f;
            pendingPairBirdCount = 0;
        }

        pendingPairBirdCount++;
        bool isFirstBirdInPair = pendingPairBirdCount == 1;

        bool spawnInTopZone;
        float spawnX;
        bool movingRight;
        bool spawnedFromLeft;

        if (isFirstBirdInPair)
        {
            spawnedFromLeft = pendingPairFirstSpawnFromLeft;
            spawnInTopZone = pendingPairFirstSpawnTop;
        }
        else
        {
            spawnedFromLeft = !pendingPairFirstSpawnFromLeft;
            spawnInTopZone = !pendingPairFirstSpawnTop;
            hasPendingPair = false;
        }

        spawnX = spawnedFromLeft ? minPos.position.x : maxPos.position.x;
        movingRight = spawnedFromLeft;

        float spawnY;
        int maxAttempts = 10;
        int attempt = 0;
        bool validPosition = false;

        do
        {
            spawnY = spawnInTopZone ? Random.Range(midY, maxY) : Random.Range(minY, midY);
            Vector3 testPos = new Vector3(spawnX, spawnY, requestedSpawnPosition.z);
            validPosition = !bird.CheckBirdOverlap(testPos);
            attempt++;
        } while (!validPosition && attempt < maxAttempts);

        Vector3 finalPosition = new Vector3(spawnX, spawnY, requestedSpawnPosition.z);
        transform.position = finalPosition;

        bird.RecordSpawnPosition(finalPosition);
        bird.SetMovementDirection(movingRight, spawnedFromLeft);

        return true;
    }
}
