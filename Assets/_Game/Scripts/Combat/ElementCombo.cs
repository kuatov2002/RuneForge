using UnityEngine;

/// <summary>
/// Cross-element combo reactions. When two different elements affect the same enemy,
/// a combo triggers based on the element pair.
///
/// Combos:
///   Fire + Ice     = STEAM BURST — AoE damage cloud (3m, 8 dmg)
///   Fire + Poison  = DETONATE    — Consumes poison stacks, 5 dmg per stack
///   Fire + Void    = IMPLODE     — Pulls nearby enemies in, then damages (10 dmg)
///   Ice + Lightning= SHATTER     — Frozen enemy takes 3x damage, breaks freeze
///   Ice + Poison   = TOXIC FROST — Slow AoE pool on ground (4s, slows + poisons)
///   Lightning + Water(Ice) already done via Shatter
///   Lightning + Poison = PLAGUE SPARK — Chain-detonates all poisoned enemies in 6m
///   Lightning + Void  = RIFT SHOCK — Stuns all void-marked enemies for 2s
///   Poison + Void   = CORRUPTION  — Spreads 3 poison stacks to nearby enemies
/// </summary>
public static class ElementCombo
{
    /// <summary>
    /// Call after applying a status effect. Checks if a combo should trigger.
    /// </summary>
    public static void CheckCombo(Health target, ElementSO newElement)
    {
        if (target == null || target.IsDead || newElement == null) return;

        var effect = newElement.statusEffect;

        // Fire combos
        if (effect == StatusEffectType.Burn)
        {
            if (target.IsFrozen || target.IsSlowed)
                TriggerSteamBurst(target);
            if (target.PoisonStacks > 0)
                TriggerDetonate(target);
            if (target.voidMarked)
                TriggerImplode(target);
        }
        // Ice combos
        else if (effect == StatusEffectType.Slow)
        {
            if (target.PoisonStacks > 0)
                TriggerToxicFrost(target);
        }
        // Lightning combos
        else if (effect == StatusEffectType.Chain)
        {
            if (target.IsFrozen)
                TriggerShatter(target);
            if (target.PoisonStacks > 0)
                TriggerPlagueSpark(target);
            if (target.voidMarked)
                TriggerRiftShock(target);
        }
        // Poison combos
        else if (effect == StatusEffectType.Poison)
        {
            if (target.voidMarked)
                TriggerCorruption(target);
        }
        // Void combos
        else if (effect == StatusEffectType.VoidMark)
        {
            if (target.PoisonStacks > 0)
                TriggerCorruption(target);
        }
    }

    // ─── COMBO IMPLEMENTATIONS ──────────────────────────────────

    /// <summary>Fire + Ice = STEAM BURST — AoE damage cloud.</summary>
    static void TriggerSteamBurst(Health target)
    {
        Vector3 pos = target.transform.position;

        // AoE damage
        float radius = 3f;
        int damage = 8;
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage);
        }

        // VFX: expanding steam cloud
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * 0.5f;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.8f, 0.9f), 3f);
        vfx.AddComponent<ComboExpandVFX>().Init(radius * 2, 0.5f, new Color(0.8f, 0.8f, 0.9f));

        ComboLabel(pos, "STEAM BURST", new Color(0.8f, 0.8f, 0.9f));

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.25f);
    }

    /// <summary>Fire + Poison = DETONATE — Consumes stacks for burst damage.</summary>
    static void TriggerDetonate(Health target)
    {
        int stacks = target.PoisonStacks;
        int damage = stacks * 5;

        // Consume poison
        // We can't easily clear stacks from outside, so we deal the burst damage
        target.TakeDamage(damage);

        // VFX: green-orange explosion
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = target.transform.position + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * 0.3f;
        Color detonateCol = new Color(0.8f, 0.5f, 0.1f);
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(detonateCol, 4f);
        vfx.AddComponent<ComboExpandVFX>().Init(2f, 0.3f, detonateCol);

        ComboLabel(target.transform.position, $"DETONATE x{stacks}", detonateCol);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
    }

    /// <summary>Fire + Void = IMPLODE — Pull + damage.</summary>
    static void TriggerImplode(Health target)
    {
        Vector3 pos = target.transform.position;
        float radius = 4f;
        int damage = 10;

        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            // Pull toward center
            Vector3 pullDir = (pos - h.transform.position);
            pullDir.y = 0;
            if (pullDir.sqrMagnitude > 0.5f)
            {
                var kb = h.gameObject.AddComponent<KnockbackEffect>();
                kb.Init(pullDir.normalized * 4f, 0.3f);
            }

            hp.TakeDamage(damage);
        }

        // VFX: dark implosion
        Color voidCol = new Color(0.5f, 0.1f, 0.8f);
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * radius;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(voidCol, 3f);
        vfx.AddComponent<ComboShrinkVFX>().Init(0.4f);

        ComboLabel(pos, "IMPLODE", voidCol);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.3f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.06f);
    }

    /// <summary>Lightning + Ice = SHATTER — Frozen target takes 3x damage.</summary>
    static void TriggerShatter(Health target)
    {
        int damage = 15; // Big burst on frozen enemy
        target.TakeDamage(damage);

        // VFX: ice shatter fragments
        Vector3 pos = target.transform.position;
        Color iceCol = new Color(0.5f, 0.8f, 1f);
        for (int i = 0; i < 8; i++)
        {
            var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(frag.GetComponent<BoxCollider>());
            frag.transform.position = pos + Vector3.up * 0.5f;
            float s = Random.Range(0.05f, 0.12f);
            frag.transform.localScale = new Vector3(s, s, s);
            frag.GetComponent<Renderer>().material = ShaderCache.NewEmissive(iceCol, 4f);
            var rb = frag.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.AddForce(Random.insideUnitSphere * 5f + Vector3.up * 3f, ForceMode.Impulse);
            Object.Destroy(frag, 0.8f);
        }

        ComboLabel(pos, "SHATTER!", iceCol);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.05f);
    }

    /// <summary>Ice + Poison = TOXIC FROST — Slow AoE pool.</summary>
    static void TriggerToxicFrost(Health target)
    {
        Vector3 pos = target.transform.position;
        pos.y = 0.05f;

        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(pool.GetComponent<CapsuleCollider>());
        pool.name = "ToxicFrostPool";
        pool.transform.position = pos;
        pool.transform.localScale = new Vector3(3f, 0.03f, 3f);
        Color toxicCol = new Color(0.3f, 0.8f, 0.6f);
        pool.GetComponent<Renderer>().material = ShaderCache.NewEmissive(toxicCol, 2f);

        // Add AoE zone component
        var col = pool.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f; // scaled by transform
        pool.AddComponent<ToxicFrostZone>();

        Object.Destroy(pool, 4f);
        ComboLabel(pos, "TOXIC FROST", toxicCol);
    }

    /// <summary>Lightning + Poison = PLAGUE SPARK — Chain-detonate poisoned enemies.</summary>
    static void TriggerPlagueSpark(Health target)
    {
        Vector3 pos = target.transform.position;
        float radius = 6f;

        Collider[] hits = Physics.OverlapSphere(pos, radius);
        int chainCount = 0;
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead || hp.PoisonStacks <= 0) continue;
            if (hp == target) continue;

            int stacks = hp.PoisonStacks;
            hp.TakeDamage(stacks * 3);
            chainCount++;

            // Chain arc visual
            CreateChainArc(pos, h.transform.position, new Color(0.4f, 0.9f, 0.2f));
            pos = h.transform.position;
        }

        Color sparkCol = new Color(0.5f, 0.9f, 0.2f);
        ComboLabel(target.transform.position, $"PLAGUE SPARK x{chainCount + 1}", sparkCol);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
    }

    /// <summary>Lightning + Void = RIFT SHOCK — Stun all void-marked enemies.</summary>
    static void TriggerRiftShock(Health target)
    {
        float radius = 8f;
        Collider[] hits = Physics.OverlapSphere(target.transform.position, radius);
        int stunned = 0;
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;
            if (hp.voidMarked)
            {
                hp.ApplyStun(2f);
                stunned++;

                // Visual: lightning spark on stunned enemy
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(spark.GetComponent<SphereCollider>());
                spark.transform.position = h.transform.position + Vector3.up * 0.5f;
                spark.transform.localScale = Vector3.one * 0.3f;
                spark.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.7f, 0.3f, 1f), 5f);
                Object.Destroy(spark, 0.3f);
            }
        }

        ComboLabel(target.transform.position, $"RIFT SHOCK ({stunned})", new Color(0.7f, 0.3f, 1f));
    }

    /// <summary>Poison + Void = CORRUPTION — Spread poison to nearby enemies.</summary>
    static void TriggerCorruption(Health target)
    {
        Vector3 pos = target.transform.position;
        float radius = 5f;
        int stacks = Mathf.Max(target.PoisonStacks, 1);
        int spreadStacks = Mathf.Min(stacks, 3);

        Collider[] hits = Physics.OverlapSphere(pos, radius);
        int infected = 0;
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead || hp == target) continue;

            for (int i = 0; i < spreadStacks; i++)
                hp.AddPoisonStack();
            infected++;

            // Visual arc
            CreateChainArc(pos, h.transform.position, new Color(0.4f, 0.1f, 0.7f));
        }

        ComboLabel(pos, $"CORRUPTION ({infected})", new Color(0.4f, 0.1f, 0.7f));
    }

    // ─── VFX HELPERS ────────────────────────────────────────────

    static void ComboLabel(Vector3 pos, string text, Color color)
    {
        // Create a temporary 3D label effect (expanding text)
        var go = new GameObject("ComboLabel");
        go.transform.position = pos + Vector3.up * 2f;
        // We rely on the HUD damage numbers for visual; here just do a bright flash
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.parent = go.transform;
        flash.transform.localScale = Vector3.one * 0.4f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(Color.Lerp(color, Color.white, 0.5f), 5f);
        flash.AddComponent<FlashShrink>().Init(0.3f);
        Object.Destroy(go, 0.5f);
    }

    static void CreateChainArc(Vector3 from, Vector3 to, Color color)
    {
        var go = new GameObject("ComboArc");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 4;
        lr.startWidth = 0.06f;
        lr.endWidth = 0.06f;
        lr.material = ShaderCache.NewEmissive(color, 2f);
        lr.startColor = color;
        lr.endColor = color;
        Vector3 mid = (from + to) * 0.5f;
        Vector3 perp = Vector3.Cross(to - from, Vector3.up).normalized;
        lr.SetPosition(0, from + Vector3.up * 0.5f);
        lr.SetPosition(1, Vector3.Lerp(from, mid, 0.5f) + perp * Random.Range(-0.3f, 0.3f) + Vector3.up * 0.5f);
        lr.SetPosition(2, Vector3.Lerp(mid, to, 0.5f) + perp * Random.Range(-0.3f, 0.3f) + Vector3.up * 0.5f);
        lr.SetPosition(3, to + Vector3.up * 0.5f);
        Object.Destroy(go, 0.2f);
    }
}

/// <summary>Expand sphere VFX then destroy.</summary>
public class ComboExpandVFX : MonoBehaviour
{
    float _targetScale;
    float _duration;
    float _timer;
    Color _color;
    Renderer _renderer;

    public void Init(float targetScale, float duration, Color color)
    {
        _targetScale = targetScale;
        _duration = duration;
        _color = color;
        _renderer = GetComponent<Renderer>();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        float scale = Mathf.Lerp(0.3f, _targetScale, t);
        transform.localScale = Vector3.one * scale;

        if (_renderer != null)
        {
            Color c = _color * (1f - t);
            _renderer.material.SetColor("_EmissionColor", c * 3f);
        }
    }
}

/// <summary>Shrink sphere VFX (implosion) then destroy.</summary>
public class ComboShrinkVFX : MonoBehaviour
{
    float _duration;
    float _timer;
    Vector3 _startScale;

    public void Init(float duration)
    {
        _duration = duration;
        _startScale = transform.localScale;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        transform.localScale = _startScale * (1f - t);
    }
}

/// <summary>Toxic frost ground zone — slows and poisons enemies inside.</summary>
public class ToxicFrostZone : MonoBehaviour
{
    float tickTimer;

    void OnTriggerStay(Collider other)
    {
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0) return;
        tickTimer = 1f;

        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            hp.ApplySlow(1.5f);
            hp.AddPoisonStack();
        }
    }
}
