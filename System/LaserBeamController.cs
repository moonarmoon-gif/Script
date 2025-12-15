using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Handles press-and-hold laser beam firing
/// Spawns laser projectiles continuously while holding the screen
/// Laser follows finger/mouse movement
/// </summary>
public class LaserBeamController : MonoBehaviour
{
    [Header("Laser Prefab")]
    [Tooltip("Laser beam projectile prefab")]
    [SerializeField] private GameObject laserPrefab;
    
    [Header("Spawn Settings")]
    [Tooltip("Transform where lasers spawn from")]
    [SerializeField] private Transform firePoint;
    
    [Tooltip("Camera reference")]
    [SerializeField] private Camera cam;
    
    [Header("Fire Rate")]
    [Tooltip("Time between laser spawns (seconds)")]
    [SerializeField] private float fireInterval = 0.1f;
    
    [Tooltip("Initial delay before continuous firing starts")]
    [SerializeField] private float initialDelay = 0.1f;
    
    [Header("Mana Settings")]
    [Tooltip("Mana cost per laser")]
    [SerializeField] private int manaCostPerLaser = 2;
    
    [Tooltip("Check mana before firing")]
    [SerializeField] private bool requireMana = true;
    
    // Private variables
    private bool isHolding = false;
    private Vector2 holdPosition;
    private float holdStartTime;
    private float lastFireTime;
    private List<LaserBeamProjectile> activeLasers = new List<LaserBeamProjectile>();
    private PlayerMana playerMana;
    
    // Public property to check if laser is active
    public bool IsLaserActive => isHolding;
    
    private void Awake()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }
        
        playerMana = GetComponent<PlayerMana>();
        
        if (playerMana == null && requireMana)
        {
            Debug.LogWarning("PlayerMana component not found! Laser will fire without mana cost.");
            requireMana = false;
        }
    }
    
    private void Update()
    {
        // Handle input
        HandleInput();
        
        // Update laser direction if holding
        if (isHolding)
        {
            UpdateLaserDirection();
            
            // Fire lasers continuously
            if (Time.time - holdStartTime >= initialDelay)
            {
                if (Time.time - lastFireTime >= fireInterval)
                {
                    FireLaser();
                    lastFireTime = Time.time;
                }
            }
        }
        
        // Clean up destroyed lasers
        activeLasers.RemoveAll(laser => laser == null);
    }
    
    private void HandleInput()
    {
        // Touch input
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            
            if (touch.press.wasPressedThisFrame)
            {
                StartHold(touch.position.ReadValue());
            }
            else if (touch.press.isPressed && isHolding)
            {
                holdPosition = touch.position.ReadValue();
            }
            else if (touch.press.wasReleasedThisFrame && isHolding)
            {
                EndHold();
            }
        }
        // Mouse input (for PC testing)
        else if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                StartHold(Mouse.current.position.ReadValue());
            }
            else if (Mouse.current.leftButton.isPressed && isHolding)
            {
                holdPosition = Mouse.current.position.ReadValue();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame && isHolding)
            {
                EndHold();
            }
        }
    }
    
    private void StartHold(Vector2 screenPosition)
    {
        // Check if touching UI
        if (MobileUIButton.IsAnyButtonPressed())
        {
            return;
        }
        
        isHolding = true;
        holdPosition = screenPosition;
        holdStartTime = Time.time;
        lastFireTime = Time.time - fireInterval; // Allow immediate first shot
        
        Debug.Log($"<color=yellow>Laser hold started at {screenPosition}</color>");
        
        // Fire first laser immediately
        FireLaser();
    }
    
    private void EndHold()
    {
        isHolding = false;
        
        Debug.Log($"<color=yellow>Laser hold ended. Total lasers fired: {activeLasers.Count}</color>");
        
        // Stop all active lasers
        foreach (var laser in activeLasers)
        {
            if (laser != null)
            {
                laser.StopLaser();
            }
        }
        
        activeLasers.Clear();
    }
    
    private void FireLaser()
    {
        if (laserPrefab == null)
        {
            Debug.LogError("Laser prefab not assigned!");
            return;
        }
        
        if (firePoint == null)
        {
            Debug.LogError("Fire point not assigned!");
            return;
        }
        
        if (cam == null)
        {
            Debug.LogError("Camera not assigned!");
            return;
        }
        
        // Check mana
        if (requireMana && playerMana != null)
        {
            if (!playerMana.Spend(manaCostPerLaser))
            {
                Debug.Log("<color=orange>Not enough mana to fire laser!</color>");
                EndHold(); // Stop firing if out of mana
                return;
            }
        }
        
        // Convert screen position to world position
        Ray ray = cam.ScreenPointToRay(holdPosition);
        Plane gamePlane = new Plane(Vector3.forward, firePoint.position.z);
        
        if (!gamePlane.Raycast(ray, out float enter))
        {
            Debug.LogWarning("Raycast failed!");
            return;
        }
        
        Vector3 worldPosition = ray.GetPoint(enter);
        Vector2 direction = (worldPosition - firePoint.position).normalized;
        
        // Spawn laser
        GameObject laserObj = Instantiate(laserPrefab, firePoint.position, Quaternion.identity);
        LaserBeamProjectile laser = laserObj.GetComponent<LaserBeamProjectile>();
        
        if (laser != null)
        {
            laser.Initialize(direction);
            activeLasers.Add(laser);
            Debug.Log($"<color=orange>Laser fired! Direction: {direction}, Active lasers: {activeLasers.Count}</color>");
        }
        else
        {
            Debug.LogError("LaserBeamProjectile component not found on prefab!");
            Destroy(laserObj);
        }
    }
    
    private void UpdateLaserDirection()
    {
        if (activeLasers.Count == 0) return;
        
        // Convert screen position to world position
        Ray ray = cam.ScreenPointToRay(holdPosition);
        Plane gamePlane = new Plane(Vector3.forward, firePoint.position.z);
        
        if (!gamePlane.Raycast(ray, out float enter))
        {
            return;
        }
        
        Vector3 worldPosition = ray.GetPoint(enter);
        Vector2 direction = (worldPosition - firePoint.position).normalized;
        
        // Update direction of the most recent laser
        if (activeLasers.Count > 0)
        {
            LaserBeamProjectile lastLaser = activeLasers[activeLasers.Count - 1];
            if (lastLaser != null)
            {
                lastLaser.UpdateDirection(direction);
            }
        }
    }
    
    /// <summary>
    /// Public method to enable/disable laser firing
    /// </summary>
    public void SetLaserEnabled(bool enabled)
    {
        if (!enabled && isHolding)
        {
            EndHold();
        }
        
        this.enabled = enabled;
    }
}
