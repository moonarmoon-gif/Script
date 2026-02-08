using UnityEngine;

public class PauseSafeSelfDestruct : MonoBehaviour
{
    [SerializeField] private float lifetime;
    private float elapsed;

    public void Initialize(float seconds)
    {
        lifetime = Mathf.Max(0f, seconds);
        elapsed = 0f;

        if (lifetime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (lifetime <= 0f)
        {
            return;
        }

        elapsed += GameStateManager.GetPauseSafeDeltaTime();
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    public static void Schedule(GameObject target, float seconds)
    {
        if (target == null)
        {
            return;
        }

        PauseSafeSelfDestruct existing = target.GetComponent<PauseSafeSelfDestruct>();
        if (existing == null)
        {
            existing = target.AddComponent<PauseSafeSelfDestruct>();
        }

        existing.Initialize(seconds);
    }
}
