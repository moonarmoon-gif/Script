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

    public float PortalAnimationDuration = 1f;
    public float SpawnDelay = 0.25f;

    public Transform SpawnPoint;

    public GameObject PortalPrefab;

    [System.Serializable]
    public class PortalSpawnEntry
    {
        public Transform PortalPosition;
        public string EnemyName;
    }

    public List<PortalSpawnEntry> CommonPortal = new List<PortalSpawnEntry>();
    public List<PortalSpawnEntry> UncommonPortal = new List<PortalSpawnEntry>();
    public List<PortalSpawnEntry> RarePortal = new List<PortalSpawnEntry>();
    public List<PortalSpawnEntry> EpicPortal = new List<PortalSpawnEntry>();
    public List<PortalSpawnEntry> LegendaryPortal = new List<PortalSpawnEntry>();
    public List<PortalSpawnEntry> MythicPortal = new List<PortalSpawnEntry>();

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
        if (SpawnPoint == null)
        {
            return;
        }

        Vector3 localOffset = SpawnPoint.localPosition;
        transform.position = transform.position - localOffset;
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

        SpawnPortalGroup(CommonPortal, CardRarity.Common);
    }

    private void SpawnPortalGroup(List<PortalSpawnEntry> entries, CardRarity rarity)
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            PortalSpawnEntry entry = entries[i];
            if (entry == null || entry.PortalPosition == null)
            {
                continue;
            }

            Vector3 spawnPos = entry.PortalPosition.position;

            if (PortalPrefab != null)
            {
                Instantiate(PortalPrefab, spawnPos, Quaternion.identity);
            }

            StartCoroutine(SpawnFromPortalRoutine(spawnPos, entry.EnemyName, rarity));
        }
    }

    private IEnumerator SpawnFromPortalRoutine(Vector3 portalWorldPosition, string enemyName, CardRarity rarity)
    {
        float portalAnim = Mathf.Max(0f, PortalAnimationDuration);
        if (portalAnim > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(portalAnim);
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

        SpawnEnemyByName(enemyName, portalWorldPosition, rarity);
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
