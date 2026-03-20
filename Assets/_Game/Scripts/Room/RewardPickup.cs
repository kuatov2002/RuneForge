using UnityEngine;
using System;

public class RewardPickup : MonoBehaviour
{
    public Action onPickedUp;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        onPickedUp?.Invoke();
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
