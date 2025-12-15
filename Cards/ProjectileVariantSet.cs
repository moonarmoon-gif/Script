using UnityEngine;

/// <summary>
/// Describes all enhanced variants for a single projectile type.
/// One asset per projectile keeps variant names/descriptions in one place
/// while remaining separate from the shared ProjectileCards asset.
/// </summary>
[CreateAssetMenu(fileName = "New Projectile Variant Set", menuName = "Cards/Projectile Variant Set")]
public class ProjectileVariantSet : ScriptableObject
{
    [Tooltip("Projectile type this variant set applies to.")]
    public ProjectileCards.ProjectileType projectileType;

    [System.Serializable]
    public class VariantInfo
    {
        [Tooltip("1-based variant index (1-3). This will be used internally to identify the variant.")]
        public int variantIndex = 1;

        [Tooltip("Display name for this variant in the selection UI.")]
        public string displayName;

        [Tooltip("Detailed description shown on the variant selection card.")]
        [TextArea]
        public string description;

        [Tooltip("Optional icon for this variant.")]
        public Sprite icon;

        // Optional text styling overrides for this variant's card in the
        // selection UI. If left at default values, the UI will use whatever
        // defaults are configured on the button prefab itself.
        public float cardNameFontSize = 36f;
        public Color cardNameColor = Color.blue;
        public float descriptionFontSize = 22f;
        public Color descriptionColor = Color.red;

        public bool enableNameOutline = true;
        public Color nameOutlineColor = Color.black;
        [Range(0f, 1f)]
        public float nameOutlineWidth = 1f;

        public bool enableDescriptionOutline = true;
        public Color descriptionOutlineColor = Color.black;
        [Range(0f, 1f)]
        public float descriptionOutlineWidth = 1f;
    }

    [Tooltip("List of available variants for this projectile (1-3 entries expected).")]
    public VariantInfo[] variants;
}
