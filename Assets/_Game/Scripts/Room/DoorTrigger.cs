using UnityEngine;
using System;

public class DoorTrigger : MonoBehaviour
{
    public string doorName;
    public static event Action<string> OnDoorEntered;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            OnDoorEntered?.Invoke(doorName);
    }
}
