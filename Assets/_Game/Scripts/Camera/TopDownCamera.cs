using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 14f;
    public float pitch = 60f; // degrees from horizontal
    public float cursorOffsetMax = 2f;
    public float smoothSpeed = 8f;

    void LateUpdate()
    {
        if (target == null) return;

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
    }
}
