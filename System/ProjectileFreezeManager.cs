using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages freezing all projectiles when player dies
/// Stops movement, spawning, and damage but allows animations to continue
/// </summary>
public class ProjectileFreezeManager : MonoBehaviour
{
    public static ProjectileFreezeManager Instance { get; private set; }
    
    private bool isFrozen = false;
    private List<MonoBehaviour> frozenScripts = new List<MonoBehaviour>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Subscribe to player death event
        PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.OnDeath += FreezeAllProjectiles;
            Debug.Log("<color=cyan>ProjectileFreezeManager subscribed to player death event</color>");
        }
        else
        {
            Debug.LogWarning("<color=yellow>ProjectileFreezeManager: PlayerHealth not found!</color>");
        }
    }
    
    public bool IsFrozen => isFrozen;
    
    /// <summary>
    /// Freeze all projectiles and enemies - stop movement, spawning, damage, and animations
    /// </summary>
    public void FreezeAllProjectiles()
    {
        if (isFrozen) return;
        
        isFrozen = true;
        Debug.Log("<color=red>★ FREEZING ALL PROJECTILES AND ENEMIES ★</color>");
        
        // Stop ProjectileSpawner from spawning new projectiles
        ProjectileSpawner[] spawners = FindObjectsOfType<ProjectileSpawner>();
        foreach (var spawner in spawners)
        {
            spawner.enabled = false;
            frozenScripts.Add(spawner);
            Debug.Log($"<color=yellow>Disabled ProjectileSpawner on {spawner.gameObject.name}</color>");
        }
        
        // Freeze all active projectiles
        FreezeProjectileType<ProjectileFireTalon>();
        FreezeProjectileType<ProjectileIceTalon>();
        FreezeProjectileType<ElementalBeam>();
        FreezeProjectileType<PlayerProjectiles>();
        FreezeProjectileType<FireMine>();
        FreezeProjectileType<ThunderBird>();
        FreezeProjectileType<NuclearStrike>();
        FreezeProjectileType<CinderCryoBloom>();
        FreezeProjectileType<TornadoController>();
        FreezeProjectileType<ClawProjectile>();
        
        // Freeze all enemies
        FreezeAllEnemies();
        
        Debug.Log($"<color=red>★ FREEZE COMPLETE: {frozenScripts.Count} scripts disabled ★</color>");
    }
    
    /// <summary>
    /// Freeze all enemies - stop movement, attacks, and animations
    /// </summary>
    private void FreezeAllEnemies()
    {
        // Find all GameObjects with "Enemy" tag
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        
        foreach (GameObject enemy in enemies)
        {
            // Freeze Rigidbody2D
            Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }
            
            // Freeze Animator (stop all animations)
            Animator anim = enemy.GetComponent<Animator>();
            if (anim != null)
            {
                anim.speed = 0f;
            }
            
            // Disable all enemy scripts
            MonoBehaviour[] scripts = enemy.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                // Don't disable EnemyHealth (so they can still die)
                if (script != null && !(script is EnemyHealth) && script.enabled)
                {
                    script.enabled = false;
                    frozenScripts.Add(script);
                }
            }
        }
        
        Debug.Log($"<color=cyan>Froze {enemies.Length} enemies</color>");
    }
    
    private void FreezeProjectileType<T>() where T : MonoBehaviour
    {
        T[] projectiles = FindObjectsOfType<T>();
        
        foreach (var proj in projectiles)
        {
            // Freeze Rigidbody2D (stop movement)
            Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false; // Completely disable physics
            }
            
            // Disable the script (stops Update, coroutines, etc.)
            proj.enabled = false;
            frozenScripts.Add(proj);
            
            // Keep Animator enabled so animations continue
            Animator anim = proj.GetComponent<Animator>();
            if (anim != null)
            {
                anim.enabled = true; // Ensure animator stays enabled
            }
            
            // Also check child animators
            Animator[] childAnims = proj.GetComponentsInChildren<Animator>();
            foreach (var childAnim in childAnims)
            {
                childAnim.enabled = true;
            }
            
            Debug.Log($"<color=yellow>Frozen {typeof(T).Name}: {proj.gameObject.name}</color>");
        }
        
        if (projectiles.Length > 0)
        {
            Debug.Log($"<color=cyan>Froze {projectiles.Length} {typeof(T).Name} projectiles</color>");
        }
    }
    
    /// <summary>
    /// Unfreeze all projectiles (for respawn/restart)
    /// </summary>
    public void UnfreezeAllProjectiles()
    {
        if (!isFrozen) return;
        
        isFrozen = false;
        Debug.Log("<color=green>★ UNFREEZING ALL PROJECTILES ★</color>");
        
        foreach (var script in frozenScripts)
        {
            if (script != null)
            {
                script.enabled = true;
                
                // Re-enable physics
                Rigidbody2D rb = script.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.simulated = true;
                }
            }
        }
        
        frozenScripts.Clear();
        Debug.Log("<color=green>★ UNFREEZE COMPLETE ★</color>");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= FreezeAllProjectiles;
        }
    }
}
