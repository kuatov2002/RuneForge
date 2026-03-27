using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 10f;
    public float pitch = 50f; // degrees from horizontal
    public float cursorOffsetMax = 2f;
    public float smoothSpeed = 8f;

    // Screen shake (trauma-based)
    float trauma;
    float shakeDecay = 2f;
    float shakeMaxOffset = 0.4f;
    float shakeFrequency = 25f;

    // Zoom
    float zoomTarget;
    float zoomDuration;
    float zoomTimer;
    float zoomFrom;
    bool isZooming;

    public static TopDownCamera Instance { get; private set; }

    void Awake() { Instance = this; }

    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    public void ZoomTo(float targetDistance, float duration)
    {
        zoomFrom = distance;
        zoomTarget = targetDistance;
        zoomDuration = duration;
        zoomTimer = 0;
        isZooming = true;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (isZooming)
        {
            zoomTimer += Time.deltaTime;
            float t = Mathf.Clamp01(zoomTimer / zoomDuration);
            distance = Mathf.Lerp(zoomFrom, zoomTarget, t);
            if (t >= 1f) isZooming = false;
        }

        Vector3 targetPos = target.position;

        // Cursor offset
        var mouse = Mouse.current;
        var cam = GetComponent<Camera>();
        if (mouse != null && cam != null)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane plane = new Plane(Vector3.up, target.position.y);
            if (plane.Raycast(ray, out float dist))
            {
                Vector3 cursorWorld = ray.GetPoint(dist);
                Vector3 offset = (cursorWorld - target.position) * 0.15f;
                offset = Vector3.ClampMagnitude(offset, cursorOffsetMax);
                targetPos += offset;
            }
        }

        // Position camera behind and above, looking down at angle
        float pitchRad = pitch * Mathf.Deg2Rad;
        Vector3 cameraOffset = new Vector3(0, Mathf.Sin(pitchRad), -Mathf.Cos(pitchRad)) * distance;
        Vector3 desiredPos = targetPos + cameraOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
        transform.LookAt(targetPos);

        // Apply screen shake
        if (trauma > 0)
        {
            float shake = trauma * trauma; // Quadratic for smooth feel
            float t = Time.time * shakeFrequency;
            float offsetX = shakeMaxOffset * shake * (Mathf.PerlinNoise(t, 0) * 2f - 1f);
            float offsetY = shakeMaxOffset * shake * (Mathf.PerlinNoise(0, t) * 2f - 1f);
            transform.position += transform.right * offsetX + transform.up * offsetY;
            trauma = Mathf.Max(0, trauma - shakeDecay * Time.deltaTime);
        }
    }
}
