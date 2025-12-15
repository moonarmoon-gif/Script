using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawning Control")]
    [Tooltip("Enable/Disable enemy spawning completely")]
    public bool enableSpawning = true;
    
    [Tooltip("Use EnemyCard spawning system instead of traditional wave spawning (UNCHECKED = EnemySpawner, CHECKED = EnemyCardSpawner)")]
    public bool useEnemyCardSystem = false;
    
    [System.Serializable]
    public class Wave
    {
        public GameObject enemyPrefab;
        public float spawnTimer;
        public float spawnInterval;
        public int enemiesPerWave;
        public int spawnedEnemyCount;
        [Tooltip("Wait for all enemies to be dead before spawning this wave")]
        public bool clearFieldBeforeSpawn = false;

        [Header("Health And Exp Multiplier")]
        [Tooltip("Multiplier for enemy health and exp reward (1 = normal, 2 = double, 3 = triple, etc.)")]
        public float healthAndExpMultiplier = 1f;
        
        
        [HideInInspector] public bool hasStartedSpawning = false;
    }

    public List<Wave> waves;
    public int waveNumber;

    [Header("Spawn Bounds")]
    [Tooltip("Child transform marking minimum spawn position (bottom-left)")]
    public Transform minPos;
    
    [Tooltip("Child transform marking maximum spawn position (top-right)")]
    public Transform maxPos;
    
    [Tooltip("Use camera-based spawning if minPos/maxPos not assigned")]
    public bool useCameraSpawning = true;

    // Camera-based spawning settings (used if minPos/maxPos not assigned)
    public float spawnDistance = 5.0f;
    
    [Tooltip("Minimum distance outside camera view to spawn (prevents visible spawning)")]
    public float minOffScreenDistance = 2.0f;
    
    [Tooltip("Percentage chance to spawn on side (vs top). 50 = 50% side, 50% top")]
    [Range(0f, 100f)]
    public float sideSpawnPercentage = 50f;

    [Header("Exclusion Zone Settings")]
    [Tooltip("Height from bottom of screen where enemies will NOT spawn (excludes top area)")]
    public float topExclusionHeight = 8.0f;
    
    [Tooltip("Width from center where enemies will NOT spawn (excludes center horizontally)")]
    public float centerExclusionWidth = 3.0f;

    private bool lastSpawnedOnLeft = false;
    private float oppositeSpawnBonus = 0.0f;
    private const float OppositeSpawnBaseChance = 75.0f;
    private const float OppositeSpawnIncrease = 5.0f;

    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        // Check if spawning is enabled
        if (!enableSpawning)
        {
            return;
        }
        
        // CRITICAL: If EnemyCard system is enabled, disable this spawner
        if (useEnemyCardSystem)
        {
            return; // EnemyCardSpawner will handle spawning instead
        }
        
        // Stop spawning if player is dead
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }
        
        // Check if current wave requires clearing field before spawn
        if (waves[waveNumber].clearFieldBeforeSpawn && !waves[waveNumber].hasStartedSpawning)
        {
            // Check if any enemies are still alive
            if (AreAnyEnemiesAlive())
            {
                // Wait for all enemies to die
                return;
            }
            else
            {
                // Field is clear, allow spawning
                waves[waveNumber].hasStartedSpawning = true;
                Debug.Log($"<color=cyan>Wave {waveNumber}: Field cleared, starting spawn</color>");
            }
        }
        
        waves[waveNumber].spawnTimer += Time.deltaTime;
        if (waves[waveNumber].spawnTimer >= waves[waveNumber].spawnInterval)
        {
            waves[waveNumber].spawnTimer = 0;
            SpawnEnemy();
        }
        if (waves[waveNumber].spawnedEnemyCount >= waves[waveNumber].enemiesPerWave)
        {
            waves[waveNumber].spawnedEnemyCount = 0;
            waves[waveNumber].hasStartedSpawning = false; // Reset for next cycle
            waveNumber++;
        }
        if (waveNumber >= waves.Count)
        {
            waveNumber = 0;
        }
    }
    
    private bool AreAnyEnemiesAlive()
    {
        // Find all EnemyHealth components in the scene
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();
        
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy != null && enemy.IsAlive)
            {
                return true; // At least one enemy is alive
            }
        }
        
        return false; // No enemies alive
    }

    private void SpawnEnemy()
    {
        Vector2 spawnPosition;
        
        // Use minPos/maxPos if assigned, otherwise use camera-based spawning
        if (minPos != null && maxPos != null)
        {
            spawnPosition = GetBoundsBasedSpawnPoint();
        }
        else
        {
            spawnPosition = GetAlternatingSpawnPoint();
        }
        
        GameObject spawnedEnemy = Instantiate(waves[waveNumber].enemyPrefab, spawnPosition, transform.rotation);
        
        // Apply health and exp multiplier
        float multiplier = waves[waveNumber].healthAndExpMultiplier;
        if (multiplier != 1f)
        {
            // Multiply health
            EnemyHealth enemyHealth = spawnedEnemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.MultiplyMaxHealth(multiplier);
                Debug.Log($"<color=cyan>Enemy spawned with {multiplier}x health: {enemyHealth.MaxHealth}</color>");
            }
            
            // Multiply exp
            EnemyExpData enemyExp = spawnedEnemy.GetComponent<EnemyExpData>();
            if (enemyExp != null)
            {
                enemyExp.MultiplyExpReward(multiplier);
                Debug.Log($"<color=cyan>Enemy spawned with {multiplier}x exp: {enemyExp.ExpReward}</color>");
            }
        }
        
        waves[waveNumber].spawnedEnemyCount++;
    }
    
    private Vector2 GetBoundsBasedSpawnPoint()
    {
        float minX = minPos.position.x;
        float maxX = maxPos.position.x;
        float minY = minPos.position.y;
        float maxY = maxPos.position.y;
        
        // Randomly choose spawn side
        float randomSide = Random.Range(0f, 100f);
        bool shouldSpawnOpposite = false;

        if (lastSpawnedOnLeft && randomSide < (OppositeSpawnBaseChance + oppositeSpawnBonus))
        {
            shouldSpawnOpposite = true;
        }
        else if (!lastSpawnedOnLeft && randomSide < (OppositeSpawnBaseChance + oppositeSpawnBonus))
        {
            shouldSpawnOpposite = true;
        }

        bool thisSpawnOnLeft;
        if ((lastSpawnedOnLeft && shouldSpawnOpposite) || (!lastSpawnedOnLeft && !shouldSpawnOpposite))
        {
            thisSpawnOnLeft = false;
        }
        else
        {
            thisSpawnOnLeft = true;
        }

        if (thisSpawnOnLeft == lastSpawnedOnLeft)
        {
            oppositeSpawnBonus += OppositeSpawnIncrease;
        }
        else
        {
            oppositeSpawnBonus = 0.0f;
        }

        lastSpawnedOnLeft = thisSpawnOnLeft;
        
        Vector2 spawnPosition;
        float weightedRandom = Random.Range(0f, 100f);
        
        if (thisSpawnOnLeft)
        {
            if (weightedRandom < sideSpawnPercentage)
            {
                // Spawn on left side
                spawnPosition = new Vector2(minX, Random.Range(minY, maxY));
            }
            else
            {
                // Spawn on top-left
                spawnPosition = new Vector2(Random.Range(minX, (minX + maxX) / 2f), maxY);
            }
        }
        else
        {
            if (weightedRandom < sideSpawnPercentage)
            {
                // Spawn on right side
                spawnPosition = new Vector2(maxX, Random.Range(minY, maxY));
            }
            else
            {
                // Spawn on top-right
                spawnPosition = new Vector2(Random.Range((minX + maxX) / 2f, maxX), maxY);
            }
        }
        
        return spawnPosition;
    }

    private Vector2 GetAlternatingSpawnPoint()
    {
        float randomSide = Random.Range(0f, 100f);
        bool shouldSpawnOpposite = false;

        // Check if we should spawn on the opposite side based on the bonus probability
        if (lastSpawnedOnLeft && randomSide < (OppositeSpawnBaseChance + oppositeSpawnBonus))
        {
            shouldSpawnOpposite = true;
        }
        else if (!lastSpawnedOnLeft && randomSide < (OppositeSpawnBaseChance + oppositeSpawnBonus))
        {
            shouldSpawnOpposite = true;
        }

        // Determine if we are spawning left or right
        bool thisSpawnOnLeft;
        if ((lastSpawnedOnLeft && shouldSpawnOpposite) || (!lastSpawnedOnLeft && !shouldSpawnOpposite))
        {
            thisSpawnOnLeft = false; // Spawn on the right
        }
        else
        {
            thisSpawnOnLeft = true; // Spawn on the left
        }

        // Apply the bonus logic
        if (thisSpawnOnLeft == lastSpawnedOnLeft)
        {
            oppositeSpawnBonus += OppositeSpawnIncrease;
        }
        else
        {
            oppositeSpawnBonus = 0.0f;
        }

        lastSpawnedOnLeft = thisSpawnOnLeft;

        // Get the off-screen spawn point using the previous weighted logic for better consistency
        return GetWeightedSpawnPoint(thisSpawnOnLeft);
    }

    private Vector2 GetWeightedSpawnPoint(bool spawnLeft)
    {
        Vector3 bottomLeftWorld = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, mainCamera.nearClipPlane));
        Vector3 topRightWorld = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, mainCamera.nearClipPlane));

        float minX = bottomLeftWorld.x;
        float maxX = topRightWorld.x;
        float minY = bottomLeftWorld.y;
        float maxY = topRightWorld.y;

        float topSpawnRandomX = Random.Range(minX - spawnDistance, maxX + spawnDistance);

        // Weighted random is now distributed across the two chosen sides (e.g., left and top-left).
        float weightedRandom = Random.Range(0f, 100f);

        // Calculate the exclusion thresholds
        float exclusionYThreshold = minY + topExclusionHeight;
        Vector3 centerWorld = mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, mainCamera.nearClipPlane));
        float centerX = centerWorld.x;
        
        Vector2 spawnPosition;
        
        if (spawnLeft)
        {
            if (weightedRandom < 75.0f) // 75% chance to spawn on the side (left)
            {
                // Spawn on left side, but NOT in the top exclusion area
                float randomY = Random.Range(minY, maxY + spawnDistance);
                
                // If Y is in exclusion zone, clamp it to below the threshold
                if (randomY > exclusionYThreshold)
                {
                    randomY = Random.Range(minY, exclusionYThreshold);
                }
                
                // Ensure minimum off-screen distance
                float spawnX = minX - Random.Range(minOffScreenDistance, spawnDistance);
                spawnPosition = new Vector2(spawnX, randomY);
            }
            else // 25% chance to spawn at the top (left half)
            {
                // Spawn at top, but respect both height AND width exclusions
                float randomY = Random.Range(minY, exclusionYThreshold);
                
                // Spawn on left side, but not too close to center
                float maxXForLeft = centerX - centerExclusionWidth;
                float randomX = Random.Range(minX - spawnDistance, maxXForLeft);
                
                // Ensure minimum off-screen distance
                float spawnY = maxY + Random.Range(minOffScreenDistance, spawnDistance);
                spawnPosition = new Vector2(randomX, spawnY);
            }
        }
        else // Spawn on the right side
        {
            if (weightedRandom < 75.0f) // 75% chance to spawn on the side (right)
            {
                // Spawn on right side, but NOT in the top exclusion area
                float randomY = Random.Range(minY, maxY + spawnDistance);
                
                // If Y is in exclusion zone, clamp it to below the threshold
                if (randomY > exclusionYThreshold)
                {
                    randomY = Random.Range(minY, exclusionYThreshold);
                }
                
                // Ensure minimum off-screen distance
                float spawnX = maxX + Random.Range(minOffScreenDistance, spawnDistance);
                spawnPosition = new Vector2(spawnX, randomY);
            }
            else // 25% chance to spawn at the top (right half)
            {
                // Spawn at top, but respect both height AND width exclusions
                float randomY = Random.Range(minY, exclusionYThreshold);
                
                // Spawn on right side, but not too close to center
                float minXForRight = centerX + centerExclusionWidth;
                float randomX = Random.Range(minXForRight, maxX + spawnDistance);
                
                // Ensure minimum off-screen distance
                float spawnY = maxY + Random.Range(minOffScreenDistance, spawnDistance);
                spawnPosition = new Vector2(randomX, spawnY);
            }
        }
        
        return spawnPosition;
    }
}