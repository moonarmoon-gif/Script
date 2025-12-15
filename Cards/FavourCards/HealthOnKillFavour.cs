using UnityEngine;

[CreateAssetMenu(fileName = "HealthOnKillFavour", menuName = "Favour Effects/Health On Kill")]
public class HealthOnKillFavour : FavourEffect
{
    [Header("Health On Kill Settings")]
    [Tooltip("Base health gained on each enemy kill.")]
    public float HealthIncreasePerKill = 1f;

    [Tooltip("Base maximum total health this favour can add.")]
    public float MaximumHealth = 100f;

    private float currentIncreasePerKill;
    private float currentMaxIncrease;
    private float totalAdded;
    private PlayerHealth playerHealth;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogWarning($"<color=yellow>HealthOnKillFavour could not find PlayerHealth on {player.name}.</color>");
            return;
        }

        currentIncreasePerKill = HealthIncreasePerKill;
        currentMaxIncrease = MaximumHealth;
        totalAdded = 0f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            return;
        }

        currentIncreasePerKill += HealthIncreasePerKill;
        currentMaxIncrease += MaximumHealth;
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (playerHealth == null)
        {
            return;
        }

        if (totalAdded >= currentMaxIncrease)
        {
            return;
        }

        float remainingCapacity = currentMaxIncrease - totalAdded;
        float amountToAdd = Mathf.Min(currentIncreasePerKill, remainingCapacity);

        if (amountToAdd <= 0f)
        {
            return;
        }

        playerHealth.IncreaseMaxHealth(amountToAdd);
        totalAdded += amountToAdd;
    }
}
