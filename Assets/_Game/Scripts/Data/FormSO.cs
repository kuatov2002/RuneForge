using UnityEngine;

public enum FormType { Bolt, Cone, Beam, Aura, Orbit, Trap }

[CreateAssetMenu(menuName = "RuneForge/Form")]
public class FormSO : ScriptableObject
{
    public string formName;
    public FormType formType;

    [Header("Bolt")]
    public float range = 15f;
    public float projectileSpeed = 14f;

    [Header("Cone")]
    public float coneAngle = 45f;
    public float coneRange = 3f;

    [Header("Beam")]
    public float beamRange = 20f;
    public float beamWidth = 0.2f;

    [Header("Aura")]
    public float auraRadius = 2.5f;

    [Header("Orbit")]
    public float orbitRadius = 1.8f;
    public float orbitSpeed = 250f;
    public int orbitCount = 3;
    public float orbitDuration = 4f;

    [Header("Trap")]
    public float trapRadius = 1.5f;
    public float trapArmTime = 0.5f;

    [Header("Common")]
    public float cooldown = 0.3f;
}
