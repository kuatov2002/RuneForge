using UnityEngine;
using System;

public class DoorTrigger : MonoBehaviour
{
    public string doorName;
    public static event Action<string> OnDoorEntered;

    bool opened;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            OnDoorEntered?.Invoke(doorName);
    }

    /// <summary>Animate door walls scaling down when room is cleared.</summary>
    public void OpenDoor()
    {
        if (opened) return;
        opened = true;
        SFXSystem.Play(SFXSystem.SFXType.DoorOpen, transform.position);

        // Find nearby wall segments and scale them down
        Collider[] nearby = Physics.OverlapSphere(transform.position, 2f);
        foreach (var col in nearby)
        {
            if (col == null || col.gameObject == gameObject) continue;
            if (col.gameObject.name == "Wall")
            {
                col.gameObject.AddComponent<DoorOpenAnim>();
            }
        }
    }
}

/// <summary>Scales a wall segment down smoothly for door opening effect.</summary>
public class DoorOpenAnim : MonoBehaviour
{
    float timer;
    Vector3 startScale;

    void Start() { startScale = transform.localScale; }

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / 0.5f);
        transform.localScale = Vector3.Lerp(startScale, new Vector3(startScale.x, 0.01f, startScale.z), t);
        if (t >= 1f)
        {
            gameObject.SetActive(false);
            Destroy(this);
        }
    }
}
