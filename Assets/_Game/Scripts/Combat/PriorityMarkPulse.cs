using UnityEngine;

/// <summary>
/// Pulses the emission intensity of a renderer's material to draw attention
/// to a priority target. Attach to the mark cylinder or beacon.
/// </summary>
public class PriorityMarkPulse : MonoBehaviour
{
    public Color baseColor = new Color(1f, 0.2f, 0.2f);
    public float minIntensity = 3f;
    public float maxIntensity = 10f;
    public float pulseSpeed = 3f;
    public float rotateSpeed = 60f;

    Renderer rend;
    MaterialPropertyBlock mpb;

    void Start()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (rend == null) return;

        // Pulse emission
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_EmissionColor", baseColor * intensity);
        rend.SetPropertyBlock(mpb);

        // Slow rotation for visual interest
        if (rotateSpeed > 0f)
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }
}
