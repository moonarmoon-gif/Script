using UnityEngine;

/// <summary>
/// Lightweight tag component added to spawned enemies so systems
/// (like Favours) can know which card rarity they came from.
/// </summary>
public class EnemyCardTag : MonoBehaviour
{
    public CardRarity rarity = CardRarity.Common;

    public float damageMultiplier = 1f;
}
