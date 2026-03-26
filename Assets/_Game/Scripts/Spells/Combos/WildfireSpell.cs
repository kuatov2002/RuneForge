using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fire + Air = Wildfire
/// Fire automatically jumps from burning enemies to all nearby ones.
/// Chain reaction across the entire room.
/// </summary>
public static class WildfireSpell
{
    public static void Cast(Vector3 center, float damage, float chainRadius, bool charged)
    {
        if (charged) { chainRadius *= 1.5f; damage *= 1.3f; }

        // Find initial target(s) near cursor
        Collider[] initial = Physics.OverlapSphere(center, 2f);
        var hit = new HashSet<Health>();
        var toProcess = new Queue<Health>();

        foreach (var c in initial)
        {
            if (c.GetComponent<PlayerController>() != null) continue;
            var hp = c.GetComponent<Health>();
            if (hp != null && !hp.IsDead && hit.Add(hp))
            {
                hp.TakeDamage(damage);
                toProcess.Enqueue(hp);
            }
        }

        // If no direct target, fire a projectile toward cursor
        if (hit.Count == 0)
        {
            // Create a seeking fireball
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                Vector3 dir = (center - player.transform.position);
                dir.y = 0;
                dir.Normalize();
                CreateFirebolt(player.transform.position + dir * 0.6f + Vector3.up * 0.5f,
                    dir, damage, chainRadius, charged);
            }
            return;
        }

        // Chain reaction
        int maxChains = charged ? 20 : 12;
        int chains = 0;
        while (toProcess.Count > 0 && chains < maxChains)
        {
            var current = toProcess.Dequeue();
            if (current == null) continue;

            Collider[] nearby = Physics.OverlapSphere(current.transform.position, chainRadius);
            foreach (var n in nearby)
            {
                if (n.GetComponent<PlayerController>() != null) continue;
                var nhp = n.GetComponent<Health>();
                if (nhp != null && !nhp.IsDead && hit.Add(nhp))
                {
                    nhp.TakeDamage(damage * 0.7f); // Chain damage falloff
                    toProcess.Enqueue(nhp);
                    chains++;

                    // Chain arc visual
                    CreateChainArc(current.transform.position, n.transform.position);
                }
            }
        }

        // VFX flash at origin
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = center + Vector3.up * 0.5f;
        flash.transform.localScale = Vector3.one * 0.4f;
        Color fireCol = new Color(1f, 0.6f, 0.1f);
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(fireCol, 5f);
        flash.AddComponent<FlashShrink>().Init(0.3f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }

    static void CreateFirebolt(Vector3 pos, Vector3 dir, float damage, float chainRadius, bool charged)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "WildfireBolt";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.3f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.8f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.5f, 0.1f), 4f);

        var proj = go.AddComponent<SpellProjectile>();
        proj.Setup(dir, 14f, damage, null, 15f);
        proj.wildfireChainRadius = chainRadius;
        proj.wildfireCharged = charged;
    }

    static void CreateChainArc(Vector3 from, Vector3 to)
    {
        var go = new GameObject("WildfireArc");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 4;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.04f;
        Color fireCol = new Color(1f, 0.5f, 0.1f);
        lr.material = ShaderCache.NewEmissive(fireCol, 3f);
        lr.startColor = fireCol;
        lr.endColor = new Color(1f, 0.3f, 0f);
        Vector3 mid = (from + to) * 0.5f;
        Vector3 perp = Vector3.Cross(to - from, Vector3.up).normalized;
        lr.SetPosition(0, from + Vector3.up * 0.5f);
        lr.SetPosition(1, Vector3.Lerp(from, mid, 0.5f) + perp * Random.Range(-0.3f, 0.3f) + Vector3.up * 0.5f);
        lr.SetPosition(2, Vector3.Lerp(mid, to, 0.5f) + perp * Random.Range(-0.3f, 0.3f) + Vector3.up * 0.5f);
        lr.SetPosition(3, to + Vector3.up * 0.5f);
        Object.Destroy(go, 0.25f);
    }
}
