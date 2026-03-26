using UnityEngine;
using System;
using Object = UnityEngine.Object;

public class RewardPickup : MonoBehaviour
{
    public Action onPickedUp;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        onPickedUp?.Invoke();
    }
}

/// <summary>Lore stone that shows text when player walks into it.</summary>
public class LoreStonePickup : MonoBehaviour
{
    public string loreText;
    bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (other.GetComponent<PlayerController>() == null) return;
        triggered = true;

        var hud = Object.FindAnyObjectByType<GameHUD>();
        if (hud != null) hud.ShowHint(loreText, 4f);

        // Fade out lore stone
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) if (r != null) r.material.color *= 0.3f;
        Destroy(gameObject, 2f);
    }
}

public class FloatBob : MonoBehaviour
{
    float startY;
    float offset;

    void Start()
    {
        startY = transform.position.y;
        offset = UnityEngine.Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        var pos = transform.position;
        pos.y = startY + Mathf.Sin(Time.time * 2f + offset) * 0.15f;
        transform.position = pos;

        // Slow rotation
        transform.Rotate(0, 60f * Time.deltaTime, 0);
    }
}
