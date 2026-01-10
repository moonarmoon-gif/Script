using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Displays a floating damage number that rises and fades out
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
[RequireComponent(typeof(CanvasGroup))]
public class DamageNumber : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float riseSpeed = 50f;
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float fadeStart = 0.5f;
    [SerializeField] private Vector2 randomOffset = new Vector2(20f, 10f);

    private TextMeshProUGUI textMesh;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private float timer;
    private Color startColor;
    private Vector2 velocity;

    private void Awake()
    {
        EnsureComponents();
    }

    public void Initialize(float damage, Color color, Vector3 worldPosition)
    {
        InitializeInternal(damage, color, worldPosition, false, false);
    }

    public void InitializeBurn(float damage, Color color, Vector3 worldPosition)
    {
        InitializeInternal(damage, color, worldPosition, true, false);
    }

    public void InitializeCrit(float damage, Color color, Vector3 worldPosition)
    {
        InitializeInternal(damage, color, worldPosition, false, true);
    }

    private void InitializeInternal(float damage, Color color, Vector3 worldPosition, bool isBurn, bool isCrit)
    {
        EnsureComponents();

        if (DamageNumberManager.Instance != null)
        {
            float size = DamageNumberManager.Instance.DamageNumberFontSize;
            if (isBurn)
            {
                size = DamageNumberManager.Instance.StatusDamageNumberFontSize;
            }
            else if (isCrit)
            {
                size = DamageNumberManager.Instance.CriticalFontSize;
            }

            // Adjust font size based on the current camera zoom level so that
            // damage numbers keep a consistent apparent size relative to the
            // game world when the orthographic camera size changes.
            float camScale = DamageNumberManager.Instance.GetCameraFontScale();
            textMesh.fontSize = size * camScale;
            lifetime = DamageNumberManager.Instance.Duration;
            riseSpeed = DamageNumberManager.Instance.FloatSpeed * 50f;
            randomOffset.x = DamageNumberManager.Instance.HorizontalSpread * 50f;

            // Outline is intentionally not modified here to avoid runtime
            // errors when TextMeshProUGUI instances are created dynamically
            // without a fully initialized material.
        }
        
        float displayDamage = damage;
        if (StatusControllerManager.Instance != null)
        {
            displayDamage = StatusControllerManager.Instance.RoundDamage(displayDamage);
        }
        textMesh.text = Mathf.RoundToInt(displayDamage).ToString();
        textMesh.color = color;
        startColor = color;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPosition);
        rectTransform.position = screenPos;

        float offsetX = Random.Range(-randomOffset.x, randomOffset.x);
        float offsetY = Random.Range(0, randomOffset.y);
        velocity = new Vector2(offsetX, riseSpeed + offsetY);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        StartCoroutine(AnimateAndDestroy());
    }

    public void InitializeStatus(string text, Color color, Vector3 worldPosition)
    {
        EnsureComponents();

        if (DamageNumberManager.Instance != null)
        {
            float size = DamageNumberManager.Instance.StatusFontSize;
            if (text == "Executed")
            {
                size = DamageNumberManager.Instance.ExecuteFontSize;
            }
            else if (text == "Immune")
            {
                size = DamageNumberManager.Instance.ImmuneFontSize;
            }
            else if (text == "Nullify")
            {
                size = DamageNumberManager.Instance.NullifyFontSize;
            }
            else if (text == "Reflect")
            {
                size = DamageNumberManager.Instance.ReflectFontSize;
            }

            float camScale = DamageNumberManager.Instance.GetCameraFontScale();
            textMesh.fontSize = size * camScale;
            lifetime = DamageNumberManager.Instance.Duration;
            riseSpeed = DamageNumberManager.Instance.FloatSpeed * 50f;
            randomOffset.x = DamageNumberManager.Instance.HorizontalSpread * 50f;

            // Outline is intentionally not modified here to avoid runtime
            // errors when TextMeshProUGUI instances are created dynamically
            // without a fully initialized material.
        }

        textMesh.text = text;
        textMesh.color = color;
        startColor = color;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPosition);
        rectTransform.position = screenPos;

        float offsetX = Random.Range(-randomOffset.x, randomOffset.x);
        float offsetY = Random.Range(0, randomOffset.y);
        velocity = new Vector2(offsetX, riseSpeed + offsetY);

        StartCoroutine(AnimateAndDestroy());
    }

    private IEnumerator AnimateAndDestroy()
    {
        timer = 0f;

        while (timer < lifetime)
        {
            timer += Time.deltaTime;

            // Move upward
            rectTransform.anchoredPosition += velocity * Time.deltaTime;
            velocity.y -= 20f * Time.deltaTime; // Slight gravity

            // Fade out
            if (timer >= fadeStart)
            {
                float fadeProgress = (timer - fadeStart) / (lifetime - fadeStart);
                Color newColor = startColor;
                newColor.a = Mathf.Lerp(1f, 0f, fadeProgress);
                textMesh.color = newColor;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void EnsureComponents()
    {
        // Try to grab a TextMeshProUGUI on this object first
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshProUGUI>();

            // Fallback: prefab might have the text component on a child
            if (textMesh == null)
            {
                textMesh = GetComponentInChildren<TextMeshProUGUI>();
            }

            // If still not found, auto-create a text child so the prefab
            // does not need to be manually wired to avoid null references.
            if (textMesh == null)
            {
                GameObject textGO = new GameObject("DamageText");
                textGO.transform.SetParent(transform, false);

                // Ensure it has a RectTransform (for UI positioning)
                RectTransform textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.pivot = new Vector2(0.5f, 0.5f);

                textMesh = textGO.AddComponent<TextMeshProUGUI>();
            }
        }

        // Ensure the text has a valid font so outline/material operations are safe
        if (textMesh != null && textMesh.font == null)
        {
            if (TMP_Settings.defaultFontAsset != null)
            {
                textMesh.font = TMP_Settings.defaultFontAsset;
            }
        }

        // Prefer the RectTransform from the text component if available
        if (rectTransform == null)
        {
            rectTransform = textMesh != null
                ? textMesh.GetComponent<RectTransform>()
                : GetComponent<RectTransform>();
        }

        // Ensure a CanvasGroup exists (RequiredComponent should also help)
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        // Disable raycast blocking so damage numbers don't block clicks
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}
