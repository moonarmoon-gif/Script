using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

public class RuntimeProjectileRadiusGizmo : MonoBehaviour
{
    public enum GizmoSourceKind
    {
        FireMine,
        NovaStar,
        DwarfStar,
        ElectroBall,
        NuclearStrike,
        ThunderBird
    }

    private GizmoSourceKind kind;

    private GameObject wireObject;
    private GameObject fillObject;
    private LineRenderer wire;
    private MeshFilter fillMeshFilter;
    private MeshRenderer fillMeshRenderer;

    private Material wireMaterial;
    private Material fillMaterial;

    private float lastFillRadius = -1f;

    private const int CircleSegments = 72;

    private static FieldInfo fireMineExplosionRadiusField;
    private static FieldInfo fireMineExplosionOffsetField;

    private static FieldInfo nuclearExplosionRadiusField;
    private static FieldInfo nuclearExplosionOffsetField;
    private static FieldInfo nuclearLandingPositionField;

    private static FieldInfo thunderBirdStrikeRadiusField;
    private static FieldInfo thunderBirdStrikeOffsetField;

    private FireMine fireMine;
    private NovaStar novaStar;
    private DwarfStar dwarfStar;
    private ElectroBall electroBall;
    private NuclearStrike nuclearStrike;
    private ThunderBird thunderBird;

    public void Initialize(GizmoSourceKind sourceKind)
    {
        kind = sourceKind;

        fireMine = GetComponent<FireMine>();
        novaStar = GetComponent<NovaStar>();
        dwarfStar = GetComponent<DwarfStar>();
        electroBall = GetComponent<ElectroBall>();
        nuclearStrike = GetComponent<NuclearStrike>();
        thunderBird = GetComponent<ThunderBird>();

        EnsureRenderers();
        ForceRebuild();
    }

    private void Awake()
    {
        EnsureRenderers();
    }

    private void OnDestroy()
    {
        if (wireObject != null)
        {
            Destroy(wireObject);
            wireObject = null;
            wire = null;
        }

        if (fillObject != null)
        {
            Destroy(fillObject);
            fillObject = null;
            fillMeshFilter = null;
            fillMeshRenderer = null;
        }

        if (wireMaterial != null)
        {
            Destroy(wireMaterial);
            wireMaterial = null;
        }

        if (fillMaterial != null)
        {
            Destroy(fillMaterial);
            fillMaterial = null;
        }
    }

    private void LateUpdate()
    {
        UpdateVisual();
    }

    private void EnsureRenderers()
    {
        if (wire == null)
        {
            wireObject = new GameObject("RuntimeRadiusWire");
            wireObject.transform.SetParent(null, false);
            wireObject.transform.position = Vector3.zero;
            wireObject.transform.rotation = Quaternion.identity;
            wireObject.transform.localScale = Vector3.one;

            wire = wireObject.AddComponent<LineRenderer>();
            wire.useWorldSpace = true;
            wire.loop = true;
            wire.positionCount = CircleSegments;
            wire.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wire.receiveShadows = false;
            wire.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            wireMaterial = new Material(shader);
            wire.material = wireMaterial;

            wire.startWidth = 0.03f;
            wire.endWidth = 0.03f;
            wire.sortingOrder = 5000;
        }

        if (fillMeshFilter == null || fillMeshRenderer == null)
        {
            fillObject = new GameObject("RuntimeRadiusFill");
            fillObject.transform.SetParent(null, false);
            fillObject.transform.position = Vector3.zero;
            fillObject.transform.rotation = Quaternion.identity;
            fillObject.transform.localScale = Vector3.one;

            fillMeshFilter = fillObject.AddComponent<MeshFilter>();
            fillMeshRenderer = fillObject.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            fillMaterial = new Material(shader);
            fillMeshRenderer.material = fillMaterial;
            fillMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fillMeshRenderer.receiveShadows = false;
            fillMeshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            fillMeshRenderer.sortingOrder = 4999;
        }

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            if (wire != null)
            {
                wire.sortingLayerID = sr.sortingLayerID;
                wire.sortingOrder = sr.sortingOrder + 50;
            }

            if (fillMeshRenderer != null)
            {
                fillMeshRenderer.sortingLayerID = sr.sortingLayerID;
                fillMeshRenderer.sortingOrder = sr.sortingOrder + 49;
            }
        }
    }

    private void ForceRebuild()
    {
        if (fillMeshFilter != null)
        {
            fillMeshFilter.mesh = new Mesh();
        }

        lastFillRadius = -1f;
    }

    private void UpdateVisual()
    {
        if (wire == null || fillMeshFilter == null || fillMeshRenderer == null)
        {
            return;
        }

        float radius;
        Vector3 center;
        Color fillColor;
        Color wireColor;
        bool drawFill;

        if (!TryGetCurrentParams(out radius, out center, out fillColor, out wireColor, out drawFill))
        {
            fillMeshRenderer.enabled = false;
            wire.enabled = false;
            return;
        }

        wire.enabled = true;
        wire.startColor = wireColor;
        wire.endColor = wireColor;

        float width = Mathf.Clamp(radius * 0.02f, 0.02f, 0.08f);
        wire.startWidth = width;
        wire.endWidth = width;

        UpdateWireCircle(center, radius);

        fillMeshRenderer.enabled = drawFill;
        if (drawFill)
        {
            fillMaterial.color = fillColor;
            if (fillObject != null)
            {
                fillObject.transform.position = center;
                fillObject.transform.rotation = Quaternion.identity;
                fillObject.transform.localScale = Vector3.one;
            }

            if (lastFillRadius < 0f || Mathf.Abs(radius - lastFillRadius) > 0.001f)
            {
                UpdateFillMesh(radius);
                lastFillRadius = radius;
            }
        }
    }

    private void UpdateWireCircle(Vector3 center, float radius)
    {
        float step = (Mathf.PI * 2f) / CircleSegments;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = step * i;
            float x = Mathf.Cos(a) * radius;
            float y = Mathf.Sin(a) * radius;
            wire.SetPosition(i, new Vector3(center.x + x, center.y + y, center.z));
        }
    }

    private void UpdateFillMesh(float radius)
    {
        Mesh m = fillMeshFilter.mesh;
        if (m == null)
        {
            m = new Mesh();
            fillMeshFilter.mesh = m;
        }

        int vertCount = CircleSegments + 1;
        Vector3[] verts = new Vector3[vertCount];
        int[] tris = new int[CircleSegments * 3];

        verts[0] = Vector3.zero;

        float step = (Mathf.PI * 2f) / CircleSegments;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = step * i;
            float x = Mathf.Cos(a) * radius;
            float y = Mathf.Sin(a) * radius;
            verts[i + 1] = new Vector3(x, y, 0f);
        }

        for (int i = 0; i < CircleSegments; i++)
        {
            int tri = i * 3;
            int a = 0;
            int b = i + 1;
            int c = (i + 2) > CircleSegments ? 1 : (i + 2);

            tris[tri + 0] = a;
            tris[tri + 1] = b;
            tris[tri + 2] = c;
        }

        m.Clear();
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateBounds();
    }

    private bool TryGetCurrentParams(out float radius, out Vector3 center, out Color fillColor, out Color wireColor, out bool drawFill)
    {
        radius = 0f;
        center = transform.position;
        fillColor = Color.clear;
        wireColor = Color.white;
        drawFill = true;

        switch (kind)
        {
            case GizmoSourceKind.FireMine:
            {
                if (fireMine == null) fireMine = GetComponent<FireMine>();
                if (fireMine == null) return false;

                EnsureFireMineReflection();
                if (fireMineExplosionRadiusField == null || fireMineExplosionOffsetField == null) return false;

                float r = (float)fireMineExplosionRadiusField.GetValue(fireMine);
                Vector2 off = (Vector2)fireMineExplosionOffsetField.GetValue(fireMine);

                radius = Mathf.Max(0f, r);
                Vector3 c = fireMine.transform.position + (Vector3)off;
                c.z = fireMine.transform.position.z;
                center = c;

                float alphaMult = Mathf.Clamp01(fireMine.RuntimeGizmoAlphaMultiplier);
                fillColor = new Color(1f, 0.5f, 0f, 0.3f * alphaMult);
                wireColor = new Color(1f, 0.5f, 0f, 0.8f * alphaMult);
                drawFill = true;
                return radius > 0f;
            }

            case GizmoSourceKind.NovaStar:
            {
                if (novaStar == null) novaStar = GetComponent<NovaStar>();
                if (novaStar == null) return false;

                float r;
                if (!TryGetWorldRadiusFromCircleCollider(novaStar.gameObject, out r)) return false;

                radius = Mathf.Max(0f, r);
                center = novaStar.transform.position + new Vector3(novaStar.radiusOffsetX, novaStar.radiusOffsetY, 0f);
                center.z = novaStar.transform.position.z;

                wireColor = novaStar.damageRadiusGizmoColor;
                fillColor = Color.clear;
                drawFill = false;
                return radius > 0f;
            }

            case GizmoSourceKind.DwarfStar:
            {
                if (dwarfStar == null) dwarfStar = GetComponent<DwarfStar>();
                if (dwarfStar == null) return false;

                float r;
                if (!TryGetWorldRadiusFromCircleCollider(dwarfStar.gameObject, out r)) return false;

                radius = Mathf.Max(0f, r);
                center = dwarfStar.transform.position + new Vector3(dwarfStar.radiusOffsetX, dwarfStar.radiusOffsetY, 0f);
                center.z = dwarfStar.transform.position.z;

                wireColor = dwarfStar.damageRadiusGizmoColor;
                fillColor = Color.clear;
                drawFill = false;
                return radius > 0f;
            }

            case GizmoSourceKind.ElectroBall:
            {
                if (electroBall == null) electroBall = GetComponent<ElectroBall>();
                if (electroBall == null) return false;

                radius = Mathf.Max(0f, electroBall.ExplosionRadius);
                center = electroBall.transform.position + (Vector3)electroBall.ExplosionRadiusOffset;
                center.z = electroBall.transform.position.z;

                fillColor = new Color(1f, 1f, 0.6f, 0.25f);
                wireColor = new Color(1f, 1f, 0.6f, 0.85f);
                drawFill = true;
                return radius > 0f;
            }

            case GizmoSourceKind.NuclearStrike:
            {
                if (nuclearStrike == null) nuclearStrike = GetComponent<NuclearStrike>();
                if (nuclearStrike == null) return false;

                EnsureNuclearStrikeReflection();
                if (nuclearExplosionRadiusField == null || nuclearExplosionOffsetField == null) return false;

                float r = (float)nuclearExplosionRadiusField.GetValue(nuclearStrike);
                Vector2 off = (Vector2)nuclearExplosionOffsetField.GetValue(nuclearStrike);

                radius = Mathf.Max(0f, r);
                Vector3 basePos = nuclearStrike.transform.position;
                if (nuclearLandingPositionField != null)
                {
                    Vector3 landing = (Vector3)nuclearLandingPositionField.GetValue(nuclearStrike);
                    if (landing != Vector3.zero)
                    {
                        basePos = landing;
                    }
                }

                center = basePos + (Vector3)off;
                center.z = nuclearStrike.transform.position.z;

                fillColor = new Color(1f, 0f, 0f, 0.3f);
                wireColor = new Color(1f, 0f, 0f, 0.8f);
                drawFill = true;
                return radius > 0f;
            }

            case GizmoSourceKind.ThunderBird:
            {
                if (thunderBird == null) thunderBird = GetComponent<ThunderBird>();
                if (thunderBird == null) return false;

                EnsureThunderBirdReflection();
                if (thunderBirdStrikeRadiusField == null || thunderBirdStrikeOffsetField == null) return false;

                float r = (float)thunderBirdStrikeRadiusField.GetValue(thunderBird);
                Vector2 off = (Vector2)thunderBirdStrikeOffsetField.GetValue(thunderBird);

                radius = Mathf.Max(0f, r);
                center = thunderBird.transform.position + (Vector3)off;
                center.z = thunderBird.transform.position.z;

                fillColor = new Color(1f, 1f, 0f, 0.25f);
                wireColor = new Color(1f, 1f, 0f, 0.9f);
                drawFill = true;
                return radius > 0f;
            }
        }

        return false;
    }

    private static bool TryGetWorldRadiusFromCircleCollider(GameObject go, out float worldRadius)
    {
        worldRadius = 0f;
        if (go == null) return false;

        CircleCollider2D[] circles = go.GetComponents<CircleCollider2D>();
        if (circles == null || circles.Length == 0) return false;

        CircleCollider2D best = null;
        float bestRadius = 0f;

        for (int i = 0; i < circles.Length; i++)
        {
            CircleCollider2D c = circles[i];
            if (c == null) continue;
            if (!c.isTrigger) continue;

            float scaleX = Mathf.Abs(c.transform.lossyScale.x);
            float r = Mathf.Abs(c.radius) * scaleX;
            if (r > bestRadius)
            {
                bestRadius = r;
                best = c;
            }
        }

        if (best == null) return false;
        worldRadius = bestRadius;
        return worldRadius > 0f;
    }

    private static void EnsureFireMineReflection()
    {
        if (fireMineExplosionRadiusField == null)
        {
            fireMineExplosionRadiusField = typeof(FireMine).GetField("explosionRadius", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (fireMineExplosionOffsetField == null)
        {
            fireMineExplosionOffsetField = typeof(FireMine).GetField("explosionRadiusOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }

    private static void EnsureNuclearStrikeReflection()
    {
        if (nuclearExplosionRadiusField == null)
        {
            nuclearExplosionRadiusField = typeof(NuclearStrike).GetField("explosionRadius", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (nuclearExplosionOffsetField == null)
        {
            nuclearExplosionOffsetField = typeof(NuclearStrike).GetField("explosionRadiusOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (nuclearLandingPositionField == null)
        {
            nuclearLandingPositionField = typeof(NuclearStrike).GetField("landingPosition", BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }

    private static void EnsureThunderBirdReflection()
    {
        if (thunderBirdStrikeRadiusField == null)
        {
            thunderBirdStrikeRadiusField = typeof(ThunderBird).GetField("strikeZoneRadius", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (thunderBirdStrikeOffsetField == null)
        {
            thunderBirdStrikeOffsetField = typeof(ThunderBird).GetField("strikeZoneOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
