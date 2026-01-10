using UnityEditor;

/// <summary>
/// Custom inspector for FavourCards that hides the old BaseCard.description
/// field so only the Favour-specific public/private descriptions are shown.
/// </summary>
[CustomEditor(typeof(FavourCards))]
public class FavourCardsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw everything except the inherited BaseCard.description field
        DrawPropertiesExcluding(serializedObject, "description");

        serializedObject.ApplyModifiedProperties();
    }
}
