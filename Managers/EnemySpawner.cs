using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawning Control")]
    [Tooltip("Enable/Disable enemy spawning completely")]
    public bool enableSpawning = true;

    [System.Serializable]
    public class Wave
    {
        public GameObject enemyPrefab;

        [Tooltip("Time in seconds this wave stays active before moving to the next wave element.")]
        public float NextWaveTimer = 120f;

        [Tooltip("Base spawn interval for this wave element (seconds).")]
        public float SpawnInterval = 1f;

        [Tooltip("If enabled, this wave element is treated as a Boss event.")]
        public bool BossEnemy = false;

        public float BossHealthMultiplier = 1f;
        public float BossDamageMultiplier = 1f;
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

    [Header("First Enemy Off-Camera Speed Boost")]
    [Tooltip("Move-speed multiplier applied to enemies spawned from the very first non-boss wave element while they are off-camera. 1 = no boost.")]
    public float firstEnemyOffCameraSpeedMultiplier = 2f;

    [Tooltip("Maximum duration in seconds that the off-camera speed boost can stay active for a spawned enemy.")]
    public float firstEnemyOffCameraBoostDuration = 3f;

    public float MoveSpeedOffCamersOffset = 0.15f;

    [Header("Enemy Count Spawn Scaling")]
    [Tooltip("Additional spawn interval multiplier per extra enemy type unlocked (e.g., 0.5 = +50% per extra enemy).")]
    public float perExtraEnemySpawnIntervalFactor = 0.5f;

    [Header("Boss Event System")]
    [Tooltip("Menace timer - boss is immune and projectiles don't spawn (seconds)")]
    public float bossMenaceTimer = 5.5f;

    [Tooltip("Duration over which player projectiles should fade away at boss event start.")]
    public float FadeAwayDuration = 0.5f;

    [Tooltip("Projectile cooldown reduction after menace timer ends (0.5 = 50% reduction)")]
    [Range(0f, 1f)]
    public float projectileCooldownReduction = 0.5f;

    [Tooltip("Delay after boss wave starts before the boss actually spawns (seconds)")]
    public float bossSpawnBreatherTime = 1f;

    [Tooltip("Time in seconds after the boss's health reaches 0 before ending the boss event and granting boss rewards.")]
    public float BossDeathRewardTimer = 2.5f;

    [Header("Post-Boss Refill")]
    [Tooltip("Delay after boss defeat before starting smooth health/mana refill (seconds)")]
    public float postBossRefillDelay = 0.5f;

    [Tooltip("Duration over which health and mana should smoothly refill to max (seconds)")]
    public float postBossRefillDuration = 5f;

    [Header("Post-Boss Camera Scaling")]
    [Tooltip("Enable automatic camera zoom-out after each completed boss event.")]
    public bool enableCameraGrowthAfterBoss = true;

    [Tooltip("Amount to increase Camera.main.orthographicSize after each completed boss event.")]
    public float cameraSizeIncreasePerBoss = 1f;

    [Tooltip("Maximum number of boss events that can increase camera size.")]
    public int maxCameraSizeIncrements = 4;

    public float CameraResizeTime = 1f;

    private int currentCameraSizeIncrements = 0;
    private Coroutine cameraResizeRoutine = null;

    private bool lastSpawnedOnLeft = false;
    private float oppositeSpawnBonus = 0.0f;
    private const float OppositeSpawnBaseChance = 75.0f;
    private const float OppositeSpawnIncrease = 5.0f;

    // Boss state
    private bool isBossEventActive = false;
    private bool bossMenaceActive = false;
    private bool bossDeathTriggered = false;
    private bool bossDeathCleanupInProgress = false;
    private GameObject currentBossEnemy = null;

    private float queuedBossExpReward = 0f;

    private int phaseStartWaveIndex = 0;
    private readonly Dictionary<int, float> spawnTimersByWaveIndex = new Dictionary<int, float>();
    private int bossEventsCompleted = 0;

    private float firstEnemyOffCameraBoostWindowEndTime = -1f;

    private float waveElapsedTimer = 0f;
    private bool bossWaveTriggered = false;

    private List<CardRarity> enemyRarityHistory = new List<CardRarity>();

    private Camera mainCamera;

    private bool gameStartSelectionStarted = false;

    public bool IsBossEventActive
    {
        get { return isBossEventActive; }
    }

    public bool IsBossDeathCleanupInProgress
    {
        get { return bossDeathCleanupInProgress; }
    }

    public GameObject CurrentBossEnemy
    {
        get { return currentBossEnemy; }
    }

    void Awake()
    {
        mainCamera = Camera.main;

        phaseStartWaveIndex = Mathf.Max(0, waveNumber);
        spawnTimersByWaveIndex.Clear();
        spawnTimersByWaveIndex[phaseStartWaveIndex] = 0f;

        firstEnemyOffCameraBoostWindowEndTime = -1f;
    }

    private void Start()
    {
        if (!enableSpawning)
        {
            return;
        }

        if (gameStartSelectionStarted)
        {
            return;
        }

        gameStartSelectionStarted = true;
        StartCoroutine(GameStartSelectionSequence());
    }

    private IEnumerator GameStartSelectionSequence()
    {
        CardSelectionManager manager = CardSelectionManager.Instance;
        if (manager == null)
        {
            manager = FindObjectOfType<CardSelectionManager>();
        }

        if (manager != null)
        {
            yield return manager.ShowInitialActiveProjectileSelection(3);

            if (manager.pauseGameOnSelection)
            {
                Time.timeScale = 1f;
            }
        }
    }

    void Update()
    {
        // Check if spawning is enabled
        if (!enableSpawning)
        {
            return;
        }

        // Stop spawning if player is dead
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        if (waves == null || waves.Count == 0)
        {
            return;
        }

        waveNumber = Mathf.Clamp(waveNumber, 0, waves.Count - 1);
        phaseStartWaveIndex = Mathf.Clamp(phaseStartWaveIndex, 0, waveNumber);

        if (isBossEventActive)
        {
            return;
        }

        Wave current = waves[waveNumber];
        if (current == null)
        {
            return;
        }

        if (current.BossEnemy)
        {
            if (!bossWaveTriggered)
            {
                bossWaveTriggered = true;
                StartCoroutine(BossEventSequence(current));
            }
            return;
        }

        int activeEnemyTypeCount = 0;
        for (int i = phaseStartWaveIndex; i <= waveNumber; i++)
        {
            if (i < 0 || i >= waves.Count) continue;
            Wave w = waves[i];
            if (w == null || w.enemyPrefab == null || w.BossEnemy) continue;
            activeEnemyTypeCount++;
        }
        activeEnemyTypeCount = Mathf.Max(1, activeEnemyTypeCount);

        for (int i = phaseStartWaveIndex; i <= waveNumber; i++)
        {
            if (i < 0 || i >= waves.Count) continue;
            Wave w = waves[i];
            if (w == null || w.enemyPrefab == null || w.BossEnemy) continue;
            SpawnEnemiesForWaveIndex(i, activeEnemyTypeCount);
        }

        float duration = Mathf.Max(0f, current.NextWaveTimer);
        waveElapsedTimer += Time.deltaTime;

        if (duration > 0f && waveElapsedTimer >= duration)
        {
            AdvanceWave();
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

    private void SpawnEnemiesForWaveIndex(int waveIndex, int activeEnemyTypeCount)
    {
        if (waveIndex < 0 || waves == null || waves.Count == 0)
        {
            return;
        }

        waveIndex = Mathf.Clamp(waveIndex, 0, waves.Count - 1);
        Wave wave = waves[waveIndex];
        if (wave == null || wave.enemyPrefab == null)
        {
            return;
        }

        float timer;
        if (!spawnTimersByWaveIndex.TryGetValue(waveIndex, out timer))
        {
            timer = 0f;
        }

        timer += Time.deltaTime;
        float interval = GetEffectiveSpawnInterval(waveIndex, wave, activeEnemyTypeCount);
        interval = Mathf.Max(0.01f, interval);

        while (timer >= interval)
        {
            timer -= interval;
            SpawnEnemyFromWave(wave, waveIndex);
        }

        spawnTimersByWaveIndex[waveIndex] = timer;
    }

    private float GetEffectiveSpawnInterval(int waveIndex, Wave wave, int activeEnemyTypeCount)
    {
        float baseInterval = wave != null ? wave.SpawnInterval : 1f;
        if (baseInterval <= 0f)
        {
            baseInterval = 0.1f;
        }

        float intervalAfterFlat = baseInterval;
        if (EnemyScalingSystem.Instance != null)
        {
            float flatFromScaling = EnemyScalingSystem.Instance.GetSpawnIntervalDecreaseTotal();
            if (flatFromScaling > 0f)
            {
                intervalAfterFlat = Mathf.Max(0.05f, baseInterval - flatFromScaling);
            }
        }

        int countForScaling = Mathf.Max(1, activeEnemyTypeCount);
        if (countForScaling > 1)
        {
            float extra = Mathf.Max(0, countForScaling - 1);
            float countFactor = 1f + perExtraEnemySpawnIntervalFactor * extra;
            intervalAfterFlat *= countFactor;
        }

        float finalInterval = Mathf.Max(0.1f, intervalAfterFlat);

        if (EnemyScalingSystem.Instance != null)
        {
            float flatReduction = EnemyScalingSystem.Instance.GetGlobalSpawnIntervalFlatReductionTotal();
            if (flatReduction > 0f)
            {
                finalInterval = Mathf.Max(0.1f, finalInterval - flatReduction);
            }
        }

        return finalInterval;
    }

    private void SpawnEnemyFromWave(Wave wave, int waveIndex)
    {
        if (wave == null || wave.enemyPrefab == null)
        {
            return;
        }

        Vector2 spawnPosition = GetSpawnPosition();
        GameObject spawnedEnemy = Instantiate(wave.enemyPrefab, spawnPosition, Quaternion.identity);

        EnemyCardTag tag = spawnedEnemy.GetComponent<EnemyCardTag>();
        if (tag == null)
        {
            tag = spawnedEnemy.AddComponent<EnemyCardTag>();
        }
        tag.rarity = wave.BossEnemy ? CardRarity.Boss : CardRarity.Common;

        if (!wave.BossEnemy && waveIndex == phaseStartWaveIndex && firstEnemyOffCameraSpeedMultiplier > 1f && firstEnemyOffCameraBoostDuration > 0f)
        {
            StatusController status = spawnedEnemy.GetComponent<StatusController>() ?? spawnedEnemy.GetComponentInChildren<StatusController>();
            if (status != null)
            {
                if (firstEnemyOffCameraBoostWindowEndTime < 0f)
                {
                    firstEnemyOffCameraBoostWindowEndTime = Time.time + firstEnemyOffCameraBoostDuration;
                }

                float remaining = firstEnemyOffCameraBoostWindowEndTime - Time.time;
                if (remaining > 0f)
                {
                    status.ApplyOffCameraSpeedBoost(firstEnemyOffCameraSpeedMultiplier, remaining, MoveSpeedOffCamersOffset);
                }
            }
        }
    }

    private void AdvanceWave()
    {
        if (waves == null || waves.Count == 0)
        {
            return;
        }

        Wave current = waves[waveNumber];
        if (current != null)
        {
            enemyRarityHistory.Add(current.BossEnemy ? CardRarity.Boss : CardRarity.Common);
        }

        if (waveNumber < waves.Count - 1)
        {
            waveNumber++;
        }

        waveElapsedTimer = 0f;
        bossWaveTriggered = false;

        spawnTimersByWaveIndex[waveNumber] = 0f;
    }

    private IEnumerator BossEventSequence(Wave bossWave)
    {
        if (bossWave == null || bossWave.enemyPrefab == null)
        {
            yield break;
        }

        isBossEventActive = true;
        bossDeathTriggered = false;
        bossDeathCleanupInProgress = false;

        if (EnemyScalingSystem.Instance != null)
        {
            EnemyScalingSystem.Instance.PauseScaling();
        }

        while (AreAnyEnemiesAlive())
        {
            yield return new WaitForSeconds(0.5f);
        }

        OrbitalStarManager orbitalManager = FindObjectOfType<OrbitalStarManager>();
        if (orbitalManager != null)
        {
            orbitalManager.OnBossEventStart();
        }

        DestroyAllPlayerProjectiles();
        SetProjectilesSpawnable(false);
        SetAutoFireEnabled(false);

        float fadeDuration = Mathf.Max(0f, FadeAwayDuration);
        if (fadeDuration > 0f)
        {
            yield return new WaitForSeconds(fadeDuration);
        }

        if (CardSelectionManager.Instance != null &&
            CardSelectionManager.Instance.UseFavourSoulSystem &&
            enemyRarityHistory.Count > 0)
        {
            CardSelectionManager.Instance.ShowFavourCards(enemyRarityHistory, false, 3);
            while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
            {
                yield return null;
            }

            if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.pauseGameOnSelection)
            {
                Time.timeScale = 1f;
            }
        }

        if (bossSpawnBreatherTime > 0f)
        {
            yield return new WaitForSeconds(bossSpawnBreatherTime);
        }

        SpawnBossEnemy(bossWave);

        float menaceDuration = bossMenaceTimer;
        float menaceElapsed = 0f;
        bossMenaceActive = true;

        while (menaceElapsed < menaceDuration)
        {
            menaceElapsed += Time.deltaTime;
            yield return null;
        }

        bossMenaceActive = false;

        ResetAndReduceProjectileCooldowns();
        SetProjectilesSpawnable(true);
        SetAutoFireEnabled(true);

        OrbitalStarManager starManager = FindObjectOfType<OrbitalStarManager>();
        if (starManager != null)
        {
            starManager.OnBossEventEnd();
            starManager.RestartAllStarCycles();
        }

        while (!bossDeathTriggered)
        {
            yield return new WaitForSeconds(0.1f);
        }

        float rewardWindow = Mathf.Max(0f, BossDeathRewardTimer);
        if (rewardWindow > 0f)
        {
            float elapsedReward = 0f;
            while (elapsedReward < rewardWindow)
            {
                elapsedReward += Time.deltaTime;
                yield return null;
            }
        }

        yield return null;

        GrantQueuedBossExperience();

        while (AreAnyEnemiesAlive())
        {
            yield return new WaitForSeconds(0.2f);
        }

        if (EnemyScalingSystem.Instance != null)
        {
            EnemyScalingSystem.Instance.ResumeScaling();
            EnemyScalingSystem.Instance.ApplyBonusHealthIncreaseAfterBoss();
            EnemyScalingSystem.Instance.ResetSpawnIntervalMultiplierForNextPhase();
            EnemyScalingSystem.Instance.OnBossEventCompleted();
        }

        bossDeathCleanupInProgress = false;

        if (CardSelectionManager.Instance != null)
        {
            yield return null;
            while (CardSelectionManager.Instance.HasPendingLevelUpStages())
            {
                yield return null;
            }
        }

        if (CardSelectionManager.Instance != null &&
            CardSelectionManager.Instance.UseFavourSoulSystem &&
            enemyRarityHistory.Count > 0)
        {
            CardSelectionManager.Instance.ShowFavourCards(enemyRarityHistory, true, 3);
            while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
            {
                yield return null;
            }

            if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.pauseGameOnSelection)
            {
                Time.timeScale = 1f;
            }
        }

        enemyRarityHistory.Clear();
        RefillPlayerHealthAndMana();
        TryIncreaseCameraSizeAfterBoss();

        currentBossEnemy = null;
        isBossEventActive = false;
        bossDeathTriggered = false;
        bossMenaceActive = false;

        bossEventsCompleted++;

        int nextPhaseIndex = waveNumber + 1;
        if (waves != null && nextPhaseIndex >= 0 && nextPhaseIndex < waves.Count)
        {
            phaseStartWaveIndex = nextPhaseIndex;
            waveNumber = nextPhaseIndex;
            waveElapsedTimer = 0f;
            bossWaveTriggered = false;
            spawnTimersByWaveIndex.Clear();
            spawnTimersByWaveIndex[phaseStartWaveIndex] = 0f;
            firstEnemyOffCameraBoostWindowEndTime = -1f;
        }
    }

    public void QueueBossExperience(int amount)
    {
        QueueBossExperience((float)amount);
    }

    public void QueueBossExperience(float amount)
    {
        if (amount <= 0f) return;
        queuedBossExpReward += amount;
    }

    private void GrantQueuedBossExperience()
    {
        if (queuedBossExpReward <= 0f) return;

        PlayerLevel playerLevel = null;

        if (AdvancedPlayerController.Instance != null)
        {
            playerLevel = AdvancedPlayerController.Instance.GetComponent<PlayerLevel>();
        }
        else if (PlayerController.Instance != null)
        {
            playerLevel = PlayerController.Instance.GetComponent<PlayerLevel>();
        }

        if (playerLevel != null)
        {
            playerLevel.GainExperience(queuedBossExpReward);
        }

        queuedBossExpReward = 0f;
    }

    private void SpawnBossEnemy(Wave bossWave)
    {
        if (bossWave == null || bossWave.enemyPrefab == null)
        {
            return;
        }

        GameObject bossPrefab = bossWave.enemyPrefab;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            return;
        }

        Vector3 center = mainCam.transform.position;
        Vector3 spawnPos = new Vector3(center.x, center.y, 0f);

        NorcthexEnemy prefabNorcthex = bossPrefab.GetComponent<NorcthexEnemy>();
        if (prefabNorcthex != null)
        {
            Vector2 offset = prefabNorcthex.initialSpawnOffset;
            spawnPos = new Vector3(center.x + offset.x, center.y + offset.y, 0f);
        }
        currentBossEnemy = Instantiate(bossPrefab, spawnPos, Quaternion.identity);

        EnemyCardTag bossTag = currentBossEnemy.GetComponent<EnemyCardTag>();
        if (bossTag == null)
        {
            bossTag = currentBossEnemy.AddComponent<EnemyCardTag>();
        }
        bossTag.rarity = CardRarity.Boss;

        bossTag.damageMultiplier = bossWave.BossDamageMultiplier;

        EnemyHealth bossHealth = currentBossEnemy.GetComponent<EnemyHealth>();
        if (bossHealth != null)
        {
            bossHealth.RegisterPostScalingHealthMultiplier(bossWave.BossHealthMultiplier);
            bossHealth.OnDeath += OnBossDeath;
            bossHealth.SetImmuneToBossMenace(true);
        }

        NorcthexEnemy norcthex = currentBossEnemy.GetComponent<NorcthexEnemy>();
        if (norcthex != null)
        {
            int bossIndexForScaling = bossEventsCompleted;
            if (bossIndexForScaling < 0)
            {
                bossIndexForScaling = 0;
            }
            norcthex.ApplyBossSpawnIndex(bossIndexForScaling);
            norcthex.StartBossBehavior();
        }

        StartCoroutine(BossImmunityCoroutine(currentBossEnemy));
    }

    private void OnBossDeath()
    {
        if (bossDeathTriggered)
        {
            return;
        }

        bossDeathTriggered = true;
        bossDeathCleanupInProgress = true;
        KillAllEnemies();
    }

    private IEnumerator BossImmunityCoroutine(GameObject boss)
    {
        if (boss == null) yield break;

        EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
        if (bossHealth != null)
        {
            bossHealth.SetImmuneToBossMenace(true);
        }

        float menaceDuration = bossMenaceTimer;
        yield return new WaitForSeconds(menaceDuration);

        if (boss != null && bossHealth != null)
        {
            bossHealth.SetImmuneToBossMenace(false);
        }
    }

    private void DestroyAllPlayerProjectiles()
    {
        float duration = Mathf.Max(0.01f, FadeAwayDuration);

        TornadoController[] tornadoes = FindObjectsOfType<TornadoController>();
        for (int i = 0; i < tornadoes.Length; i++)
        {
            if (tornadoes[i] != null && tornadoes[i].gameObject != null)
            {
                BeginFadeOutForProjectile(tornadoes[i].gameObject, duration);
            }
        }

        Collapse[] collapses = FindObjectsOfType<Collapse>();
        for (int i = 0; i < collapses.Length; i++)
        {
            if (collapses[i] != null && collapses[i].gameObject != null)
            {
                BeginFadeOutForProjectile(collapses[i].gameObject, duration);
            }
        }

        NovaStar[] novaStars = FindObjectsOfType<NovaStar>();
        for (int i = 0; i < novaStars.Length; i++)
        {
            if (novaStars[i] != null && novaStars[i].gameObject != null)
            {
                BeginFadeOutForProjectile(novaStars[i].gameObject, duration);
            }
        }

        DwarfStar[] dwarfStars = FindObjectsOfType<DwarfStar>();
        for (int i = 0; i < dwarfStars.Length; i++)
        {
            if (dwarfStars[i] != null && dwarfStars[i].gameObject != null)
            {
                BeginFadeOutForProjectile(dwarfStars[i].gameObject, duration);
            }
        }

        GameObject[] projectiles = GameObject.FindGameObjectsWithTag("Projectile");
        for (int i = 0; i < projectiles.Length; i++)
        {
            GameObject proj = projectiles[i];
            if (proj == null) continue;

            HolyShield shield = proj.GetComponent<HolyShield>();
            if (shield != null)
            {
                continue;
            }

            BeginFadeOutForProjectile(proj, duration);
        }
    }

    private void BeginFadeOutForProjectile(GameObject obj, float duration)
    {
        if (obj == null) return;

        Collider2D[] colliders = obj.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        StartCoroutine(FadeOutAndDestroyProjectile(obj, duration));
    }

    private IEnumerator FadeOutAndDestroyProjectile(GameObject obj, float duration)
    {
        if (obj == null) yield break;

        SpriteRenderer[] sprites = obj.GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites.Length == 0)
        {
            Destroy(obj);
            yield break;
        }

        Color[] originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            originalColors[i] = sprites[i] != null ? sprites[i].color : Color.white;
        }

        float elapsed = 0f;
        while (elapsed < duration && obj != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    Color c = originalColors[i];
                    c.a *= alpha;
                    sprites[i].color = c;
                }
            }

            yield return null;
        }

        if (obj != null)
        {
            Destroy(obj);
        }
    }

    private void SetProjectilesSpawnable(bool spawnable)
    {
        ProjectileSpawner spawner = FindObjectOfType<ProjectileSpawner>();
        if (spawner != null)
        {
            spawner.enabled = spawnable;
        }
    }

    private void ResetAndReduceProjectileCooldowns()
    {
        ProjectileSpawner spawner = FindObjectOfType<ProjectileSpawner>();
        if (spawner != null)
        {
            spawner.ResetAndReduceCooldowns(projectileCooldownReduction);
        }

        TornadoController.ApplyBossCooldownReduction(projectileCooldownReduction);
    }

    private void SetAutoFireEnabled(bool enabled)
    {
        AdvancedPlayerController player = FindObjectOfType<AdvancedPlayerController>();
        if (player != null)
        {
            player.enableAutoFire = enabled;
        }
    }

    private void KillAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemy in enemies)
        {
            if (enemy == currentBossEnemy)
            {
                continue;
            }

            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                damageable.TakeDamage(999999f, transform.position, Vector3.zero);
            }
        }
    }

    private void RefillPlayerHealthAndMana()
    {
        GameObject playerObject = null;

        if (AdvancedPlayerController.Instance != null)
        {
            playerObject = AdvancedPlayerController.Instance.gameObject;
        }
        else if (PlayerController.Instance != null)
        {
            playerObject = PlayerController.Instance.gameObject;
        }

        if (playerObject == null)
        {
            return;
        }

        StartCoroutine(SmoothRefillPlayerHealthAndMana(playerObject));
    }

    private IEnumerator SmoothRefillPlayerHealthAndMana(GameObject playerObject)
    {
        if (postBossRefillDelay > 0f)
        {
            yield return new WaitForSeconds(postBossRefillDelay);
        }

        if (playerObject == null)
        {
            yield break;
        }

        PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
        PlayerMana mana = playerObject.GetComponent<PlayerMana>();
        PlayerStats stats = playerObject.GetComponent<PlayerStats>();

        float duration = Mathf.Max(0.1f, postBossRefillDuration);

        float originalHealthRegenPerSecond = 0f;
        float originalManaRegenPerSecond = 0f;

        bool hasStats = stats != null;
        if (hasStats)
        {
            originalHealthRegenPerSecond = stats.healthRegenPerSecond;
            originalManaRegenPerSecond = stats.manaRegenPerSecond;
        }

        float missingHealth = (health != null) ? (health.MaxHealth - health.CurrentHealth) : 0f;
        float missingMana = (mana != null) ? (mana.MaxMana - mana.CurrentMana) : 0f;

        if (hasStats)
        {
            if (missingHealth > 0f)
            {
                stats.healthRegenPerSecond += missingHealth / duration;
            }

            if (missingMana > 0f)
            {
                stats.manaRegenPerSecond += missingMana / duration;
            }
        }

        float endTime = Time.time + duration;
        while (Time.time < endTime)
        {
            yield return null;
        }

        if (hasStats)
        {
            stats.healthRegenPerSecond = originalHealthRegenPerSecond;
            stats.manaRegenPerSecond = originalManaRegenPerSecond;
        }

        if (health != null && health.CurrentHealth < health.MaxHealth)
        {
            health.Heal(health.MaxHealth - health.CurrentHealth);
        }

        if (mana != null && mana.CurrentMana < mana.MaxMana)
        {
            mana.AddMana(mana.MaxMana - mana.CurrentMana);
        }
    }

    private void TryIncreaseCameraSizeAfterBoss()
    {
        if (!enableCameraGrowthAfterBoss)
        {
            return;
        }

        if (currentCameraSizeIncrements >= maxCameraSizeIncrements)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (cameraSizeIncreasePerBoss <= 0f)
        {
            return;
        }

        float oldSize = cam.orthographicSize;
        float targetSize = oldSize + cameraSizeIncreasePerBoss;
        currentCameraSizeIncrements++;

        float resizeTime = Mathf.Max(0f, CameraResizeTime);
        if (resizeTime <= 0f)
        {
            cam.orthographicSize = targetSize;
            return;
        }

        if (cameraResizeRoutine != null)
        {
            StopCoroutine(cameraResizeRoutine);
            cameraResizeRoutine = null;
        }

        cameraResizeRoutine = StartCoroutine(ResizeCameraSizeRoutine(cam, targetSize, resizeTime));
    }

    private IEnumerator ResizeCameraSizeRoutine(Camera cam, float targetSize, float duration)
    {
        if (cam == null)
        {
            cameraResizeRoutine = null;
            yield break;
        }

        float startSize = cam.orthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }

        cam.orthographicSize = targetSize;
        cameraResizeRoutine = null;
    }

    private Vector2 GetSpawnPosition()
    {
        if (minPos != null && maxPos != null)
        {
            float minX = minPos.position.x;
            float maxX = maxPos.position.x;
            float minY = minPos.position.y;
            float maxY = maxPos.position.y;

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
                    spawnPosition = new Vector2(minX, Random.Range(minY, maxY));
                }
                else
                {
                    spawnPosition = new Vector2(Random.Range(minX, (minX + maxX) / 2f), maxY);
                }
            }
            else
            {
                if (weightedRandom < sideSpawnPercentage)
                {
                    spawnPosition = new Vector2(maxX, Random.Range(minY, maxY));
                }
                else
                {
                    spawnPosition = new Vector2(Random.Range((minX + maxX) / 2f, maxX), maxY);
                }
            }

            return spawnPosition;
        }

        return GetCameraBasedSpawnPosition();
    }

    private Vector2 GetCameraBasedSpawnPosition()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return Vector2.zero;
        }

        Vector3 bottomLeftWorld = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, mainCamera.nearClipPlane));
        Vector3 topRightWorld = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, mainCamera.nearClipPlane));

        float minX = bottomLeftWorld.x;
        float maxX = topRightWorld.x;
        float minY = bottomLeftWorld.y;
        float maxY = topRightWorld.y;

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

        float weightedRandom = Random.Range(0f, 100f);

        if (thisSpawnOnLeft)
        {
            if (weightedRandom < sideSpawnPercentage)
            {
                float spawnX = minX - Random.Range(minOffScreenDistance, spawnDistance);
                float spawnY = Random.Range(minY, maxY);
                return new Vector2(spawnX, spawnY);
            }
            else
            {
                float spawnY = maxY + Random.Range(minOffScreenDistance, spawnDistance);
                float spawnX = Random.Range(minX - spawnDistance, (minX + maxX) / 2f);
                return new Vector2(spawnX, spawnY);
            }
        }
        else
        {
            if (weightedRandom < sideSpawnPercentage)
            {
                float spawnX = maxX + Random.Range(minOffScreenDistance, spawnDistance);
                float spawnY = Random.Range(minY, maxY);
                return new Vector2(spawnX, spawnY);
            }
            else
            {
                float spawnY = maxY + Random.Range(minOffScreenDistance, spawnDistance);
                float spawnX = Random.Range((minX + maxX) / 2f, maxX + spawnDistance);
                return new Vector2(spawnX, spawnY);
            }
        }
    }
}