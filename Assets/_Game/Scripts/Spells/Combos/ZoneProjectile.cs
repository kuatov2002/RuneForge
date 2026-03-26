using UnityEngine;
using System;

public class ZoneProjectile : MonoBehaviour
{
    Vector3 _dir;
    float _speed;
    float _gravity;
    float _maxDist;
    Vector3 _startPos;
    Vector3 _velocity;
    bool _hit;
    Action<Vector3> _onImpact;

    public void Init(Vector3 dir, float speed, float gravity, float maxDist, Color trailColor, Action<Vector3> onImpact)
    {
        _dir = dir.normalized;
        _speed = speed;
        _gravity = gravity;
        _maxDist = maxDist;
        _startPos = transform.position;
        _velocity = _dir * speed;
        _onImpact = onImpact;

        // Trail
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.startWidth = 0.25f;
        trail.endWidth = 0f;
        trail.time = 0.2f;
        trail.material = ShaderCache.NewEmissive(trailColor, 3f);
        trail.startColor = trailColor;
        Color endCol = trailColor; endCol.a = 0f;
        trail.endColor = endCol;
    }

    void Update()
    {
        if (_hit) return;
        _velocity.y -= _gravity * Time.deltaTime;
        transform.position += _velocity * Time.deltaTime;

        // Ground check (y <= 0.05)
        if (_gravity > 0 && transform.position.y <= 0.05f)
        {
            Vector3 pos = transform.position; pos.y = 0;
            Impact(pos);
            return;
        }

        if (Vector3.Distance(_startPos, transform.position) > _maxDist)
            Impact(transform.position);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_hit) return;
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<ZoneProjectile>() != null) return;
        if (other.GetComponent<FireballProjectile>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;
        // Don't hit existing zones
        if (other.GetComponent<MagmaPoolZone>() != null) return;
        if (other.GetComponent<SteamCloudZone>() != null) return;
        if (other.GetComponent<PermafrostZone>() != null) return;

        Impact(transform.position);
    }

    void Impact(Vector3 pos)
    {
        if (_hit) return;
        _hit = true;
        pos.y = 0;
        _onImpact?.Invoke(pos);
        Destroy(gameObject);
    }
}
