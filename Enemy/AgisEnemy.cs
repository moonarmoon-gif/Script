using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyHealth))]
public class AgisEnemy : MonoBehaviour
{
    public float TeleportInDuration = 1f;

    public float Attack1Duration = 1f;
    public float Attack2Duration = 1f;
    public float Attack3Duration = 1f;
    public float Attack4Duration = 1f;
    public float Attack5Duration = 1f;

    public float TeleportOutDuration = 1f;
    public float DeathDuration = 1f;

    public bool PlayDeathAnimationOnDeath = true;

    public float PortalStartDuration = 1f;
    public float PortalAnimationTimer = 1.25f;
    public float SpawnDelay = 0.25f;
    public float EnemySpawnInterval = 1f;

    public float BuffDebuffMinTimer = 5f;
    public float BuffDebuffMaxTimer = 10f;

    public int PortalsSpawnedPerCast = 0;

    public GameObject PortalPrefab;

    [System.Serializable]
    public class SummoningPortalEntry
    {
        public Transform PortalPosition;
    }

    [System.Serializable]
    public class EnemyWaveEntry
    {
        public string EnemyName;
        public Vector2 Offset;
    }

    [System.Serializable]
    public class GlobalStatusEffect
    {
        public StatusId statusId;
        public int stacks;
        public float durationSeconds;
        public bool applyToPlayer;
        public bool applyToAllEnemies;
        public bool includeBossEnemies;
    }

    public List<SummoningPortalEntry> SummoningPortals = new List<SummoningPortalEntry>();

    public List<EnemyWaveEntry> CommonEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> UncommonEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> RareEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> EpicEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> LegendaryEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> MythicEnemyWave = new List<EnemyWaveEntry>();

    public GlobalStatusEffect Attack2GlobalEffect = new GlobalStatusEffect();
    public GlobalStatusEffect Attack4GlobalEffect = new GlobalStatusEffect();
    public GlobalStatusEffect Attack5GlobalEffect = new GlobalStatusEffect();

    private Animator animator;
    private EnemyHealth enemyHealth;
    private EnemyCardSpawner enemyCardSpawner;

    private Coroutine startupRoutine;
    private Coroutine buffDebuffRoutine;

    private bool isDead;

    private bool attackInProgress;
    private Coroutine thresholdAttackRoutine;
    private int queuedAttack3Count;
    private bool attack3InProgress;
    private bool threshold75Triggered;
    private bool threshold50Triggered;
    private bool threshold25Triggered;

    private sealed class ActiveSummoningPortal
    {
        public GameObject portalInstance;
        public Vector3 portalWorldPosition;
        public bool invertOffsetX;
        public SummoningPortalEntry portalEntry;
        public EnemyWaveEntry enemyEntry;
        public CardRarity rarity;
        public bool spawnReady;
    }

    private readonly List<ActiveSummoningPortal> activePortals = new List<ActiveSummoningPortal>();
    private CardRarity currentPortalRarity = CardRarity.Common;
    private bool portalSummoningPaused;

    private Coroutine globalPortalSpawnRoutine;
    private float globalTimeUntilNextPortalSpawn;
    private bool globalNeedsImmediatePortalSpawnAfterResume;
    private bool globalHadReadyPortals;

    private readonly Dictionary<CardRarity, Queue<EnemyWaveEntry>> uniqueEnemyBagsByRarity = new Dictionary<CardRarity, Queue<EnemyWaveEntry>>();

    private static readonly int[] FixedPortalSpawnOrder = { 0, 11, 1, 10, 2, 9, 3, 8, 4, 7, 5, 6 };
    private int fixedPortalSpawnCursor;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        enemyHealth = GetComponent<EnemyHealth>();
        enemyCardSpawner = FindObjectOfType<EnemyCardSpawner>();

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleDeath;
            enemyHealth.OnHealthChanged += HandleHealthChanged;
        }

        ApplySpawnPointOffset();
    }

    private void Start()
    {
        if (startupRoutine != null)
        {
            StopCoroutine(startupRoutine);
        }
        startupRoutine = StartCoroutine(StartupRoutine());
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleDeath;
            enemyHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void ApplySpawnPointOffset()
    {
        GameObject spawnPointObj = null;
        try
        {
            spawnPointObj = GameObject.FindGameObjectWithTag("AgisSpawnPoint");
        }
        catch (UnityException)
        {
            spawnPointObj = null;
        }
        Transform spawnPointTransform = spawnPointObj != null ? spawnPointObj.transform : null;
        if (spawnPointTransform == null)
        {
            return;
        }

        Vector3 spawnPos = spawnPointTransform.position;
        float additionalCameraSize = 0f;
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float defaultSize = 12f;
            CameraBottomAnchor anchor = cam.GetComponent<CameraBottomAnchor>();
            if (anchor != null)
            {
                defaultSize = anchor.DefaultCameraSize;
            }

            additionalCameraSize = Mathf.Max(0f, cam.orthographicSize - defaultSize);
        }

        transform.position = new Vector3(spawnPos.x, spawnPos.y + additionalCameraSize, transform.position.z);
    }

    private IEnumerator StartupRoutine()
    {
        if (animator != null)
        {
            animator.SetBool("idle", false);
        }

        float teleportIn = Mathf.Max(0f, TeleportInDuration);
        if (teleportIn > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(teleportIn);
        }

        if (animator != null)
        {
            animator.SetBool("idle", true);
        }

        while (enemyHealth != null && enemyHealth.IsAlive && enemyHealth.IsImmuneToBossMenace)
        {
            yield return null;
        }

        if (GetAttack1RepeatCount() > 0)
        {
            yield return PlayAttack1SequenceRoutine();
        }

        if (buffDebuffRoutine != null)
        {
            StopCoroutine(buffDebuffRoutine);
        }
        buffDebuffRoutine = StartCoroutine(BuffDebuffAttackLoopRoutine());
    }

    private IEnumerator BuffDebuffAttackLoopRoutine()
    {
        float timeUntilNextAttack = -1f;

        while (enemyHealth != null && enemyHealth.IsAlive)
        {
            if (attack3InProgress)
            {
                timeUntilNextAttack = -1f;
                yield return null;
                continue;
            }

            if (attackInProgress)
            {
                yield return null;
                continue;
            }

            if (timeUntilNextAttack < 0f)
            {
                float min = Mathf.Max(0f, BuffDebuffMinTimer);
                float max = Mathf.Max(min, BuffDebuffMaxTimer);
                timeUntilNextAttack = UnityEngine.Random.Range(min, max);
            }

            if (timeUntilNextAttack > 0f)
            {
                float dt = GameStateManager.GetPauseSafeDeltaTime();
                if (dt > 0f)
                {
                    timeUntilNextAttack -= dt;
                }
                yield return null;
                continue;
            }

            if (enemyHealth == null || !enemyHealth.IsAlive)
            {
                yield break;
            }

            timeUntilNextAttack = -1f;

            List<int> rolls = new List<int>(4);
            if (GetAttack1RepeatCount() > 0)
            {
                rolls.Add(0);
            }
            rolls.Add(1);
            rolls.Add(2);
            rolls.Add(3);

            int pick = rolls[UnityEngine.Random.Range(0, rolls.Count)];
            switch (pick)
            {
                case 0:
                    yield return PlayAttack1SequenceRoutine();
                    break;
                case 1:
                    yield return PlayAttackRoutine("attack2", Mathf.Max(0f, Attack2Duration));
                    break;
                case 2:
                    yield return PlayAttackRoutine("attack4", Mathf.Max(0f, Attack4Duration));
                    break;
                default:
                    yield return PlayAttackRoutine("attack5", Mathf.Max(0f, Attack5Duration));
                    break;
            }
        }
    }

    private IEnumerator PlayAttackRoutine(string attackParam, float duration)
    {
        if (animator == null)
        {
            yield break;
        }

        if (string.Equals(attackParam, "attack1", StringComparison.OrdinalIgnoreCase) && !CanSpawnAnotherPortal())
        {
            if (HasAnimatorParameter(animator, "idle", AnimatorControllerParameterType.Bool))
            {
                animator.SetBool("idle", true);
            }
            yield break;
        }

        while (attackInProgress && enemyHealth != null && enemyHealth.IsAlive)
        {
            yield return null;
        }

        attackInProgress = true;

        try
        {
            if (string.Equals(attackParam, "attack1", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(SpawnPortalAfterDelayRoutine());
            }
            else if (string.Equals(attackParam, "attack2", StringComparison.OrdinalIgnoreCase))
            {
                ApplyGlobalStatusEffect(Attack2GlobalEffect);
            }
            else if (string.Equals(attackParam, "attack4", StringComparison.OrdinalIgnoreCase))
            {
                ApplyGlobalStatusEffect(Attack4GlobalEffect);
            }
            else if (string.Equals(attackParam, "attack5", StringComparison.OrdinalIgnoreCase))
            {
                ApplyGlobalStatusEffect(Attack5GlobalEffect);
            }

            if (HasAnimatorParameter(animator, "idle", AnimatorControllerParameterType.Bool))
            {
                animator.SetBool("idle", false);
            }

            if (HasAnimatorParameter(animator, attackParam, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(attackParam);
            }
            else if (HasAnimatorParameter(animator, attackParam, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(attackParam, true);
            }

            if (duration > 0f)
            {
                yield return GameStateManager.WaitForPauseSafeSeconds(duration);
            }
            else
            {
                yield return null;
            }

            if (animator != null && HasAnimatorParameter(animator, attackParam, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(attackParam, false);
            }

            if (animator != null && HasAnimatorParameter(animator, "idle", AnimatorControllerParameterType.Bool))
            {
                animator.SetBool("idle", true);
            }
        }
        finally
        {
            attackInProgress = false;
        }
    }

    private int GetAttack1RepeatCount()
    {
        int repeats = Mathf.Max(1, PortalsSpawnedPerCast);
        int remaining = GetRemainingPortalSlots();
        if (remaining <= 0)
        {
            return 0;
        }
        return Mathf.Min(repeats, remaining);
    }

    private IEnumerator PlayAttack1SequenceRoutine()
    {
        int repeats = GetAttack1RepeatCount();
        if (repeats <= 0)
        {
            yield break;
        }
        for (int i = 0; i < repeats; i++)
        {
            yield return PlayAttackRoutine("attack1", Mathf.Max(0f, Attack1Duration));
        }
    }

    private List<EnemyWaveEntry> GetWaveForRarity(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Uncommon:
                return UncommonEnemyWave;
            case CardRarity.Rare:
                return RareEnemyWave;
            case CardRarity.Epic:
                return EpicEnemyWave;
            case CardRarity.Legendary:
                return LegendaryEnemyWave;
            case CardRarity.Mythic:
                return MythicEnemyWave;
            default:
                return CommonEnemyWave;
        }
    }

    private static List<EnemyWaveEntry> FilterValidWaveEntries(List<EnemyWaveEntry> waveEntries)
    {
        List<EnemyWaveEntry> enemies = new List<EnemyWaveEntry>();
        if (waveEntries == null)
        {
            return enemies;
        }

        for (int i = 0; i < waveEntries.Count; i++)
        {
            EnemyWaveEntry entry = waveEntries[i];
            if (entry == null) continue;
            if (string.IsNullOrWhiteSpace(entry.EnemyName)) continue;
            enemies.Add(entry);
        }

        return enemies;
    }

    private EnemyWaveEntry GetRandomEnemyEntryForCurrentRarity()
    {
        List<EnemyWaveEntry> enemies = FilterValidWaveEntries(GetWaveForRarity(currentPortalRarity));
        if (enemies.Count == 0)
        {
            return null;
        }

        int idx = UnityEngine.Random.Range(0, enemies.Count);
        return enemies[idx];
    }

    private int GetMaxPortalSlots()
    {
        if (SummoningPortals == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < SummoningPortals.Count; i++)
        {
            SummoningPortalEntry p = SummoningPortals[i];
            if (p == null || p.PortalPosition == null) continue;
            count++;
        }
        return count;
    }

    private int GetActivePortalCount()
    {
        int count = 0;
        for (int i = 0; i < activePortals.Count; i++)
        {
            ActiveSummoningPortal p = activePortals[i];
            if (p == null) continue;
            if (p.portalInstance == null) continue;
            count++;
        }
        return count;
    }

    private int GetRemainingPortalSlots()
    {
        return Mathf.Max(0, GetMaxPortalSlots() - GetActivePortalCount());
    }

    private bool CanSpawnAnotherPortal()
    {
        if (PortalPrefab == null) return false;
        return GetRemainingPortalSlots() > 0;
    }

    private bool IsPortalEntryInUse(SummoningPortalEntry portalEntry)
    {
        if (portalEntry == null) return false;

        for (int i = 0; i < activePortals.Count; i++)
        {
            ActiveSummoningPortal p = activePortals[i];
            if (p == null) continue;
            if (p.portalInstance == null) continue;
            if (p.portalEntry == portalEntry) return true;
        }

        return false;
    }

    private Queue<EnemyWaveEntry> BuildUniqueEnemyCycleQueue(CardRarity rarity)
    {
        List<EnemyWaveEntry> waveEntries = FilterValidWaveEntries(GetWaveForRarity(rarity));
        Dictionary<string, List<EnemyWaveEntry>> byName = new Dictionary<string, List<EnemyWaveEntry>>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < waveEntries.Count; i++)
        {
            EnemyWaveEntry entry = waveEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.EnemyName))
            {
                continue;
            }

            if (!byName.TryGetValue(entry.EnemyName, out var list))
            {
                list = new List<EnemyWaveEntry>();
                byName[entry.EnemyName] = list;
            }

            list.Add(entry);
        }

        List<EnemyWaveEntry> picks = new List<EnemyWaveEntry>();
        foreach (var kvp in byName)
        {
            List<EnemyWaveEntry> candidates = kvp.Value;
            if (candidates == null || candidates.Count == 0) continue;
            picks.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        }

        Shuffle(picks);

        Queue<EnemyWaveEntry> q = new Queue<EnemyWaveEntry>();
        for (int i = 0; i < picks.Count; i++)
        {
            q.Enqueue(picks[i]);
        }

        return q;
    }

    private EnemyWaveEntry GetNextUniqueEnemyEntry(CardRarity rarity)
    {
        if (!uniqueEnemyBagsByRarity.TryGetValue(rarity, out Queue<EnemyWaveEntry> q) || q == null)
        {
            q = BuildUniqueEnemyCycleQueue(rarity);
            uniqueEnemyBagsByRarity[rarity] = q;
        }

        if (q.Count == 0)
        {
            q = BuildUniqueEnemyCycleQueue(rarity);
            uniqueEnemyBagsByRarity[rarity] = q;
        }

        if (q.Count == 0)
        {
            return null;
        }

        return q.Dequeue();
    }

    private void UpgradePortalRarity()
    {
        int current = (int)currentPortalRarity;
        int next = Mathf.Clamp(current + 1, (int)CardRarity.Common, (int)CardRarity.Mythic);
        currentPortalRarity = (CardRarity)next;

        uniqueEnemyBagsByRarity.Remove(currentPortalRarity);

        for (int i = 0; i < activePortals.Count; i++)
        {
            ActiveSummoningPortal portal = activePortals[i];
            if (portal == null || portal.portalInstance == null)
            {
                continue;
            }

            portal.rarity = currentPortalRarity;
        }

        for (int i = 0; i < activePortals.Count; i++)
        {
            ActiveSummoningPortal portal = activePortals[i];
            if (portal == null || portal.portalInstance == null)
            {
                continue;
            }

            portal.enemyEntry = GetNextUniqueEnemyEntry(currentPortalRarity);
        }

        if (animator != null && HasAnimatorParameter(animator, "idle", AnimatorControllerParameterType.Bool))
        {
            animator.SetBool("idle", true);
        }
    }

    private IEnumerator SpawnPortalAfterDelayRoutine()
    {
        float portalSpawnDelay = Mathf.Max(0f, PortalAnimationTimer);
        if (portalSpawnDelay > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(portalSpawnDelay);
        }

        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            yield break;
        }

        if (!CanSpawnAnotherPortal())
        {
            yield break;
        }

        EnemyWaveEntry entry = GetNextUniqueEnemyEntry(currentPortalRarity);
        if (entry == null)
        {
            yield break;
        }

        SpawnSinglePortal(entry, currentPortalRarity);
    }

    private void SpawnSinglePortal(EnemyWaveEntry entry, CardRarity rarity)
    {
        if (entry == null)
        {
            return;
        }

        if (!CanSpawnAnotherPortal())
        {
            return;
        }

        if (SummoningPortals == null || SummoningPortals.Count == 0)
        {
            return;
        }

        List<SummoningPortalEntry> portalCandidates = new List<SummoningPortalEntry>();
        for (int i = 0; i < SummoningPortals.Count; i++)
        {
            SummoningPortalEntry p = SummoningPortals[i];
            if (p == null || p.PortalPosition == null) continue;
            if (IsPortalEntryInUse(p)) continue;
            portalCandidates.Add(p);
        }
        if (portalCandidates.Count == 0)
        {
            return;
        }

        List<SummoningPortalEntry> selectedPortals = SelectPortalsWithSideSplit(portalCandidates, 1);
        if (selectedPortals == null || selectedPortals.Count == 0)
        {
            return;
        }

        float referenceX = 0f;
        if (AdvancedPlayerController.Instance != null)
        {
            referenceX = AdvancedPlayerController.Instance.transform.position.x;
        }
        else if (Camera.main != null)
        {
            referenceX = Camera.main.transform.position.x;
        }

        SummoningPortalEntry portal = selectedPortals[0];
        if (portal == null || portal.PortalPosition == null)
        {
            return;
        }

        Vector3 spawnPos = portal.PortalPosition.position;
        bool isRightSide = spawnPos.x > referenceX;

        GameObject portalInstance = null;
        if (PortalPrefab != null)
        {
            portalInstance = Instantiate(PortalPrefab, spawnPos, Quaternion.identity);
            SpriteRenderer[] renderers = portalInstance.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers != null)
            {
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] != null)
                    {
                        renderers[r].flipX = isRightSide;
                    }
                }
            }
        }

        ActiveSummoningPortal active = new ActiveSummoningPortal
        {
            portalInstance = portalInstance,
            portalWorldPosition = spawnPos,
            invertOffsetX = isRightSide,
            portalEntry = portal,
            enemyEntry = entry,
            rarity = rarity
        };
        activePortals.Add(active);

        StartCoroutine(SpawnFromPortalRoutine(active));
    }

    private void ApplyGlobalStatusEffect(GlobalStatusEffect effect)
    {
        if (effect == null) return;

        int stacks = Mathf.Max(0, effect.stacks);
        if (stacks <= 0) return;

        float duration = effect.durationSeconds;

        if (effect.applyToPlayer)
        {
            AdvancedPlayerController player = AdvancedPlayerController.Instance;
            if (player == null)
            {
                player = FindObjectOfType<AdvancedPlayerController>();
            }

            if (player != null)
            {
                StatusController sc = player.GetComponent<StatusController>();
                if (sc != null)
                {
                    sc.AddStatus(effect.statusId, stacks, duration);
                }
            }
        }

        if (effect.applyToAllEnemies)
        {
            EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyHealth e = enemies[i];
                if (e == null || !e.IsAlive) continue;

                if (!effect.includeBossEnemies)
                {
                    EnemyCardTag tag = e.GetComponent<EnemyCardTag>() ?? e.GetComponentInParent<EnemyCardTag>();
                    if (tag != null && tag.rarity == CardRarity.Boss)
                    {
                        continue;
                    }
                }

                StatusController sc = e.GetComponent<StatusController>();
                if (sc == null) continue;

                sc.AddStatus(effect.statusId, stacks, duration);
            }
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (enemyHealth == null || !enemyHealth.IsAlive) return;

        float ratio = max > 0f ? current / max : 0f;

        if (!threshold75Triggered && ratio <= 0.75f)
        {
            threshold75Triggered = true;
            queuedAttack3Count++;
        }

        if (!threshold50Triggered && ratio <= 0.50f)
        {
            threshold50Triggered = true;
            queuedAttack3Count++;
        }

        if (!threshold25Triggered && ratio <= 0.25f)
        {
            threshold25Triggered = true;
            queuedAttack3Count++;
        }

        if (queuedAttack3Count > 0 && thresholdAttackRoutine == null)
        {
            thresholdAttackRoutine = StartCoroutine(ThresholdAttack3Routine());
        }
    }

    private IEnumerator ThresholdAttack3Routine()
    {
        while (enemyHealth != null && enemyHealth.IsAlive && queuedAttack3Count > 0)
        {
            while (attackInProgress && enemyHealth != null && enemyHealth.IsAlive)
            {
                yield return null;
            }

            if (enemyHealth == null || !enemyHealth.IsAlive)
            {
                break;
            }

            if (queuedAttack3Count <= 0)
            {
                break;
            }

            queuedAttack3Count--;

            portalSummoningPaused = true;

            enemyHealth.SetImmuneToBossMenace(true);
            attack3InProgress = true;
            try
            {
                yield return PlayAttackRoutine("attack3", Mathf.Max(0f, Attack3Duration));
            }
            finally
            {
                attack3InProgress = false;
                if (enemyHealth != null)
                {
                    enemyHealth.SetImmuneToBossMenace(false);
                }
            }

            UpgradePortalRarity();
            portalSummoningPaused = false;

            if (animator != null && HasAnimatorParameter(animator, "idle", AnimatorControllerParameterType.Bool))
            {
                animator.SetBool("idle", true);
            }
            yield return null;
        }

        thresholdAttackRoutine = null;
    }

    private IEnumerator PlayAttack1Routine()
    {
        float duration = Mathf.Max(0f, Attack1Duration);
        if (duration <= 0f) yield break;

        if (animator != null)
        {
            if (HasAnimatorParameter(animator, "idle", AnimatorControllerParameterType.Bool))
            {
                animator.SetBool("idle", false);
            }

            if (HasAnimatorParameter(animator, "attack1", AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger("attack1");
            }
            else if (HasAnimatorParameter(animator, "attack1", AnimatorControllerParameterType.Bool))
            {
                animator.SetBool("attack1", true);
            }
        }

        yield return GameStateManager.WaitForPauseSafeSeconds(duration);

        if (animator != null)
        {
            if (HasAnimatorParameter(animator, "attack1", AnimatorControllerParameterType.Bool))
            {
                animator.SetBool("attack1", false);
            }

            if (HasAnimatorParameter(animator, "idle", AnimatorControllerParameterType.Bool))
            {
                animator.SetBool("idle", true);
            }
        }
    }

    private static bool HasAnimatorParameter(Animator anim, string name, AnimatorControllerParameterType type)
    {
        if (anim == null) return false;

        AnimatorControllerParameter[] parameters = anim.parameters;
        if (parameters == null) return false;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];
            if (p != null && p.type == type && p.name == name)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnEnemyWave(List<EnemyWaveEntry> waveEntries, CardRarity rarity, int desiredPortalCount = -1)
    {
        if (waveEntries == null || waveEntries.Count == 0) return;
        if (SummoningPortals == null || SummoningPortals.Count == 0) return;

        List<EnemyWaveEntry> enemies = new List<EnemyWaveEntry>();
        for (int i = 0; i < waveEntries.Count; i++)
        {
            EnemyWaveEntry entry = waveEntries[i];
            if (entry == null) continue;
            if (string.IsNullOrWhiteSpace(entry.EnemyName)) continue;
            enemies.Add(entry);
        }

        List<SummoningPortalEntry> portalCandidates = new List<SummoningPortalEntry>();
        for (int i = 0; i < SummoningPortals.Count; i++)
        {
            SummoningPortalEntry p = SummoningPortals[i];
            if (p == null || p.PortalPosition == null) continue;
            portalCandidates.Add(p);
        }

        int count;
        if (desiredPortalCount > 0)
        {
            count = Mathf.Min(desiredPortalCount, portalCandidates.Count);
        }
        else
        {
            count = portalCandidates.Count;
        }
        if (count <= 0) return;

        List<SummoningPortalEntry> selectedPortals = SelectPortalsWithSideSplit(portalCandidates, count);
        if (selectedPortals == null || selectedPortals.Count == 0) return;

        Shuffle(enemies);

        List<EnemyWaveEntry> spawnAssignments = new List<EnemyWaveEntry>(count);
        int idx = 0;
        while (spawnAssignments.Count < count && enemies.Count > 0)
        {
            if (idx % enemies.Count == 0)
            {
                Shuffle(enemies);
            }
            spawnAssignments.Add(enemies[idx % enemies.Count]);
            idx++;
        }

        float referenceX = 0f;
        if (AdvancedPlayerController.Instance != null)
        {
            referenceX = AdvancedPlayerController.Instance.transform.position.x;
        }
        else if (Camera.main != null)
        {
            referenceX = Camera.main.transform.position.x;
        }

        int spawnCount = Mathf.Min(count, selectedPortals.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            SummoningPortalEntry portal = selectedPortals[i];
            if (portal == null || portal.PortalPosition == null) continue;

            Vector3 spawnPos = portal.PortalPosition.position;
            bool isRightSide = spawnPos.x > referenceX;

            GameObject portalInstance = null;
            if (PortalPrefab != null)
            {
                portalInstance = Instantiate(PortalPrefab, spawnPos, Quaternion.identity);
                SpriteRenderer[] renderers = portalInstance.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers != null)
                {
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        if (renderers[r] != null)
                        {
                            renderers[r].flipX = isRightSide;
                        }
                    }
                }
            }

            EnemyWaveEntry entry = spawnAssignments.Count > 0 ? spawnAssignments[i % spawnAssignments.Count] : null;

            if (entry == null)
            {
                continue;
            }

            ActiveSummoningPortal active = new ActiveSummoningPortal
            {
                portalInstance = portalInstance,
                portalWorldPosition = spawnPos,
                invertOffsetX = isRightSide,
                portalEntry = portal,
                enemyEntry = entry,
                rarity = rarity
            };
            activePortals.Add(active);
            StartCoroutine(SpawnFromPortalRoutine(active));
        }
    }

    private List<SummoningPortalEntry> SelectPortalsWithSideSplit(List<SummoningPortalEntry> candidates, int desiredCount)
    {
        List<SummoningPortalEntry> selected = new List<SummoningPortalEntry>();
        if (candidates == null || candidates.Count == 0)
        {
            return selected;
        }

        if (desiredCount <= 0)
        {
            return selected;
        }

        HashSet<SummoningPortalEntry> candidateSet = new HashSet<SummoningPortalEntry>();
        for (int i = 0; i < candidates.Count; i++)
        {
            SummoningPortalEntry p = candidates[i];
            if (p == null || p.PortalPosition == null) continue;
            candidateSet.Add(p);
        }

        if (candidateSet.Count == 0)
        {
            return selected;
        }

        int safety = Mathf.Max(candidateSet.Count, 1) * Mathf.Max(FixedPortalSpawnOrder.Length, 1);
        while (selected.Count < desiredCount && safety-- > 0)
        {
            int orderIdx = fixedPortalSpawnCursor % FixedPortalSpawnOrder.Length;
            int portalIdx = FixedPortalSpawnOrder[orderIdx];
            fixedPortalSpawnCursor++;

            if (SummoningPortals == null || portalIdx < 0 || portalIdx >= SummoningPortals.Count)
            {
                continue;
            }

            SummoningPortalEntry portal = SummoningPortals[portalIdx];
            if (portal == null || portal.PortalPosition == null)
            {
                continue;
            }

            if (!candidateSet.Contains(portal))
            {
                continue;
            }

            if (IsPortalEntryInUse(portal))
            {
                continue;
            }

            if (selected.Contains(portal))
            {
                continue;
            }

            selected.Add(portal);
        }

        return selected;
    }

    private static void Shuffle<T>(List<T> list)
    {
        if (list == null) return;

        for (int i = 0; i < list.Count - 1; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private IEnumerator SpawnFromPortalRoutine(ActiveSummoningPortal portal)
    {
        if (portal == null)
        {
            yield break;
        }

        GameObject portalInstance = portal.portalInstance;
        Vector3 portalWorldPosition = portal.portalWorldPosition;

        Animator portalAnimator = null;
        if (portalInstance != null)
        {
            portalAnimator = portalInstance.GetComponent<Animator>();
            if (portalAnimator == null)
            {
                portalAnimator = portalInstance.GetComponentInChildren<Animator>();
            }
        }

        float portalStart = Mathf.Max(0f, PortalStartDuration);
        if (portalStart > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(portalStart);
        }

        if (portalAnimator != null)
        {
            portalAnimator.SetBool("loop", true);
        }

        float delay = Mathf.Max(0f, SpawnDelay);
        if (delay > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(delay);
        }

        if (enemyHealth != null && !enemyHealth.IsAlive)
        {
            yield break;
        }

        portal.spawnReady = true;

        if (globalPortalSpawnRoutine == null)
        {
            globalTimeUntilNextPortalSpawn = 0f;
            globalHadReadyPortals = false;
            globalPortalSpawnRoutine = StartCoroutine(GlobalPortalSpawnRoutine());
        }
    }

    private IEnumerator GlobalPortalSpawnRoutine()
    {
        float interval = EnemySpawnInterval;
        if (interval <= 0f)
        {
            interval = 1f;
        }

        while (enemyHealth != null && enemyHealth.IsAlive)
        {
            if (portalSummoningPaused)
            {
                globalNeedsImmediatePortalSpawnAfterResume = true;
                yield return null;
                continue;
            }

            bool hasReadyPortals = false;
            for (int i = 0; i < activePortals.Count; i++)
            {
                ActiveSummoningPortal p = activePortals[i];
                if (p == null) continue;
                if (!p.spawnReady) continue;
                hasReadyPortals = true;
                break;
            }

            if (hasReadyPortals && !globalHadReadyPortals)
            {
                globalTimeUntilNextPortalSpawn = 0f;
            }
            globalHadReadyPortals = hasReadyPortals;

            if (!hasReadyPortals)
            {
                yield return null;
                continue;
            }

            if (globalNeedsImmediatePortalSpawnAfterResume)
            {
                globalTimeUntilNextPortalSpawn = 0f;
                globalNeedsImmediatePortalSpawnAfterResume = false;
            }

            if (globalTimeUntilNextPortalSpawn <= 0f)
            {
                for (int i = 0; i < activePortals.Count; i++)
                {
                    ActiveSummoningPortal p = activePortals[i];
                    if (p == null) continue;
                    if (!p.spawnReady) continue;

                    EnemyWaveEntry enemyEntry = p.enemyEntry;
                    if (enemyEntry == null) continue;

                    Vector3 pos = p.portalWorldPosition;
                    pos.x += p.invertOffsetX ? -enemyEntry.Offset.x : enemyEntry.Offset.x;
                    pos.y += enemyEntry.Offset.y;
                    SpawnEnemyByName(enemyEntry.EnemyName, pos, p.rarity);
                }

                globalTimeUntilNextPortalSpawn = interval;
                yield return null;
                continue;
            }

            float dt = GameStateManager.GetPauseSafeDeltaTime();
            if (dt <= 0f)
            {
                yield return null;
                continue;
            }

            globalTimeUntilNextPortalSpawn -= dt;
            yield return null;
        }

        globalPortalSpawnRoutine = null;
    }

    private void SpawnEnemyByName(string enemyName, Vector3 spawnPosition, CardRarity rarity)
    {
        if (string.IsNullOrEmpty(enemyName))
        {
            return;
        }

        GameObject prefab = ResolveEnemyPrefab(enemyName);
        if (prefab == null)
        {
            return;
        }

        GameObject spawnedEnemy = Instantiate(prefab, spawnPosition, Quaternion.identity);
        if (spawnedEnemy == null)
        {
            return;
        }

        EnemyCardTag[] tags = spawnedEnemy.GetComponentsInChildren<EnemyCardTag>(true);
        if (tags == null || tags.Length == 0)
        {
            EnemyCardTag created = spawnedEnemy.AddComponent<EnemyCardTag>();
            tags = new EnemyCardTag[] { created };
        }
        for (int i = 0; i < tags.Length; i++)
        {
            if (tags[i] != null)
            {
                tags[i].rarity = rarity;
            }
        }

        EnemyExpData exp = spawnedEnemy.GetComponent<EnemyExpData>();
        if (exp != null)
        {
            exp.SetGrantExpEnabled(false);
        }
    }

    private GameObject ResolveEnemyPrefab(string enemyName)
    {
        if (enemyCardSpawner == null)
        {
            enemyCardSpawner = FindObjectOfType<EnemyCardSpawner>();
        }

        if (enemyCardSpawner == null || enemyCardSpawner.availableEnemyCards == null)
        {
            return null;
        }

        for (int i = 0; i < enemyCardSpawner.availableEnemyCards.Count; i++)
        {
            EnemyCards card = enemyCardSpawner.availableEnemyCards[i];
            if (card == null || card.enemyPrefab == null)
            {
                continue;
            }

            if (string.Equals(card.enemyPrefab.name, enemyName, StringComparison.OrdinalIgnoreCase))
            {
                return card.enemyPrefab;
            }
        }

        return null;
    }

    private void HandleDeath()
    {
        if (isDead)
        {
            return;
        }
        isDead = true;

        StopAllCoroutines();

        startupRoutine = null;
        buffDebuffRoutine = null;
        thresholdAttackRoutine = null;
        globalPortalSpawnRoutine = null;

        portalSummoningPaused = true;

        float animDuration = Mathf.Max(0f, PlayDeathAnimationOnDeath ? DeathDuration : TeleportOutDuration);

        if (animator != null)
        {
            if (PlayDeathAnimationOnDeath)
            {
                if (HasAnimatorParameter(animator, "death", AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool("death", true);
                }
                else if (HasAnimatorParameter(animator, "death", AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger("death");
                }
            }
            else
            {
                if (HasAnimatorParameter(animator, "teleportout", AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool("teleportout", true);
                }
                else if (HasAnimatorParameter(animator, "teleportout", AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger("teleportout");
                }
            }
        }

        StartCoroutine(EndAndDestroyAllPortalsRoutine());

        float portalCleanupDelay = Mathf.Max(0f, PortalStartDuration) + 0.05f;
        float destroyDelay = Mathf.Max(animDuration, portalCleanupDelay);
        PauseSafeSelfDestruct.Schedule(gameObject, destroyDelay);
    }

    private IEnumerator EndAndDestroyAllPortalsRoutine()
    {
        for (int i = 0; i < activePortals.Count; i++)
        {
            ActiveSummoningPortal p = activePortals[i];
            if (p == null || p.portalInstance == null) continue;

            Animator portalAnimator = p.portalInstance.GetComponent<Animator>();
            if (portalAnimator == null)
            {
                portalAnimator = p.portalInstance.GetComponentInChildren<Animator>();
            }

            if (portalAnimator != null)
            {
                if (HasAnimatorParameter(portalAnimator, "loop", AnimatorControllerParameterType.Bool))
                {
                    portalAnimator.SetBool("loop", false);
                }

                if (HasAnimatorParameter(portalAnimator, "end", AnimatorControllerParameterType.Bool))
                {
                    portalAnimator.SetBool("end", true);
                }
            }
        }

        float wait = Mathf.Max(0f, PortalStartDuration);
        if (wait > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(wait);
        }

        for (int i = 0; i < activePortals.Count; i++)
        {
            ActiveSummoningPortal p = activePortals[i];
            if (p == null || p.portalInstance == null) continue;
            Destroy(p.portalInstance);
            p.portalInstance = null;
        }

        activePortals.Clear();
    }
}
