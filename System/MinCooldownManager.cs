using UnityEngine;

public class MinCooldownManager : MonoBehaviour
{
    public static MinCooldownManager Instance { get; private set; }

    [Header("Minimum Cooldowns (seconds)")]
    public float FireMine = 0.1f;
    public float FrostMine = 0.1f;
    public float Collapse = 0.1f;
    public float ElementalBeam = 0.1f;
    public float ElementalBeamV2 = 0.1f;
    public float FireTalon = 0.2f;
    public float IceTalon = 0.2f;
    public float HolyShield = 0.1f;
    public float NuclearStrike = 0.1f;
    public float ThunderBird = 0.1f;
    public float ThunderBirdV1 = 0.1f;
    public float ThunderBirdV2 = 5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public float ClampCooldown(ProjectileCards card, float cooldownSeconds)
    {
        float min = GetMinCooldownSeconds(card);
        if (min <= 0f)
        {
            return cooldownSeconds;
        }

        return Mathf.Max(min, cooldownSeconds);
    }

    public float GetMinCooldownSeconds(ProjectileCards card)
    {
        if (card == null)
        {
            return 0f;
        }

        GameObject prefab = card.projectilePrefab;
        if (prefab == null)
        {
            return 0f;
        }

        if (prefab.GetComponent<FrostMine>() != null)
        {
            return FrostMine;
        }

        if (prefab.GetComponent<FireMine>() != null)
        {
            return FireMine;
        }

        if (prefab.GetComponent<Collapse>() != null)
        {
            return Collapse;
        }

        ElementalBeam beam = prefab.GetComponent<ElementalBeam>();
        if (beam != null)
        {
            int enhancedVariant = ProjectileCardLevelSystem.Instance != null
                ? ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card)
                : 0;

            bool hasVariant2 = ProjectileCardLevelSystem.Instance != null && ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);
            return (hasVariant2 || enhancedVariant == 2) ? ElementalBeamV2 : ElementalBeam;
        }

        if (prefab.GetComponent<ProjectileFireTalon>() != null)
        {
            return FireTalon;
        }

        if (prefab.GetComponent<ProjectileIceTalon>() != null)
        {
            return IceTalon;
        }

        if (prefab.GetComponent<HolyShield>() != null)
        {
            return HolyShield;
        }

        if (prefab.GetComponent<NuclearStrike>() != null)
        {
            return NuclearStrike;
        }

        ThunderBird bird = prefab.GetComponent<ThunderBird>();
        if (bird != null)
        {
            int enhancedVariant = ProjectileCardLevelSystem.Instance != null
                ? ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card)
                : 0;

            bool hasVariant2 = ProjectileCardLevelSystem.Instance != null && ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);

            if (hasVariant2 || enhancedVariant == 2)
            {
                return ThunderBirdV2;
            }

            if (enhancedVariant == 1)
            {
                return ThunderBirdV1;
            }

            return ThunderBird;
        }

        return 0f;
    }
}
