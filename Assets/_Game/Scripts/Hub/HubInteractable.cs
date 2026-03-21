using UnityEngine;
using System;

/// <summary>
/// Marks a hub object as interactable. Detects player proximity.
/// </summary>
public class HubInteractable : MonoBehaviour
{
    public string stationId;
    public Color stationColor;

    public static event Action<HubInteractable> OnPlayerEnter;
    public static event Action<HubInteractable> OnPlayerExit;

    bool playerInside;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        playerInside = true;
        OnPlayerEnter?.Invoke(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        playerInside = false;
        OnPlayerExit?.Invoke(this);
    }

    void OnDisable()
    {
        if (playerInside)
        {
            playerInside = false;
            OnPlayerExit?.Invoke(this);
        }
    }
}
