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

    public float PortalStartDuration = 1f;
    public float PortalSpawnTimer = 1.25f;
    public float SpawnDelay = 0.25f;
    public float EnemySpawnInterval = 1f;

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

    public List<SummoningPortalEntry> SummoningPortals = new List<SummoningPortalEntry>();

    public List<EnemyWaveEntry> CommonEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> UncommonEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> RareEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> EpicEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> LegendaryEnemyWave = new List<EnemyWaveEntry>();
    public List<EnemyWaveEntry> MythicEnemyWave = new List<EnemyWaveEntry>();

    private Animator animator;
    private EnemyHealth enemyHealth;
    private EnemyCardSpawner enemyCardSpawner;

    private Coroutine startupRoutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        enemyHealth = GetComponent<EnemyHealth>();
        enemyCardSpawner = FindObjectOfType<EnemyCardSpawner>();

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleDeath;
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

        StartCoroutine(PlayAttack1Routine());

        float portalSpawnDelay = Mathf.Max(0f, PortalSpawnTimer);
        if (portalSpawnDelay > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(portalSpawnDelay);
        }

        SpawnEnemyWave(CommonEnemyWave, CardRarity.Common);
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

    private void SpawnEnemyWave(List<EnemyWaveEntry> waveEntries, CardRarity rarity)
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

        int count = Mathf.Min(enemies.Count, portalCandidates.Count);
        if (count <= 0) return;

        List<SummoningPortalEntry> selectedPortals = SelectPortalsWithSideSplit(portalCandidates, count);
        if (selectedPortals == null || selectedPortals.Count == 0) return;

        Shuffle(enemies);

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

            StartCoroutine(SpawnFromPortalRoutine(portalInstance, spawnPos, enemies[i], rarity, isRightSide));
        }
    }

    private List<SummoningPortalEntry> SelectPortalsWithSideSplit(List<SummoningPortalEntry> candidates, int desiredCount)
    {
        List<SummoningPortalEntry> valid = new List<SummoningPortalEntry>();
        for (int i = 0; i < candidates.Count; i++)
        {
            SummoningPortalEntry p = candidates[i];
            if (p == null || p.PortalPosition == null) continue;
            valid.Add(p);
        }

        if (desiredCount <= 0 || valid.Count == 0) return new List<SummoningPortalEntry>();

        float referenceX = 0f;
        if (AdvancedPlayerController.Instance != null)
        {
            referenceX = AdvancedPlayerController.Instance.transform.position.x;
        }
        else if (Camera.main != null)
        {
            referenceX = Camera.main.transform.position.x;
        }

        List<SummoningPortalEntry> left = new List<SummoningPortalEntry>();
        List<SummoningPortalEntry> right = new List<SummoningPortalEntry>();
        for (int i = 0; i < valid.Count; i++)
        {
            SummoningPortalEntry p = valid[i];
            if (p.PortalPosition.position.x < referenceX) left.Add(p);
            else right.Add(p);
        }

        Shuffle(left);
        Shuffle(right);

        int leftWanted = desiredCount / 2;
        int rightWanted = desiredCount / 2;
        if ((desiredCount % 2) == 1)
        {
            if (UnityEngine.Random.value < 0.5f) leftWanted++;
            else rightWanted++;
        }

        List<SummoningPortalEntry> selected = new List<SummoningPortalEntry>();

        for (int i = 0; i < leftWanted && i < left.Count; i++) selected.Add(left[i]);
        for (int i = 0; i < rightWanted && i < right.Count; i++) selected.Add(right[i]);

        int remaining = desiredCount - selected.Count;
        if (remaining > 0)
        {
            List<SummoningPortalEntry> extras = new List<SummoningPortalEntry>();
            for (int i = Mathf.Min(leftWanted, left.Count); i < left.Count; i++) extras.Add(left[i]);
            for (int i = Mathf.Min(rightWanted, right.Count); i < right.Count; i++) extras.Add(right[i]);
            Shuffle(extras);
            for (int i = 0; i < remaining && i < extras.Count; i++) selected.Add(extras[i]);
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

    private IEnumerator SpawnFromPortalRoutine(GameObject portalInstance, Vector3 portalWorldPosition, EnemyWaveEntry enemyEntry, CardRarity rarity, bool invertOffsetX)
    {
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

        float interval = EnemySpawnInterval;
        if (interval <= 0f)
        {
            interval = 1f;
        }

        while (enemyHealth == null || enemyHealth.IsAlive)
        {
            if (enemyEntry != null)
            {
                Vector3 pos = portalWorldPosition;
                pos.x += invertOffsetX ? -enemyEntry.Offset.x : enemyEntry.Offset.x;
                pos.y += enemyEntry.Offset.y;
                SpawnEnemyByName(enemyEntry.EnemyName, pos, rarity);
            }
            yield return GameStateManager.WaitForPauseSafeSeconds(interval);
        }
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
        if (animator != null)
        {
            animator.SetBool("death", true);
        }
    }
}
