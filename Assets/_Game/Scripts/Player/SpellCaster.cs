using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class SpellCaster : MonoBehaviour
{
    public SpellData[] spellSlots = new SpellData[2];
    public int activeSlot;

    public event Action OnSpellChanged;

    float cooldownTimer;
    AuraForm activeAura;
    OrbitForm activeOrbit;

    public SpellData ActiveSpell => spellSlots[activeSlot];

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.qKey.wasPressedThisFrame)
        {
            DeactivatePassiveForms();
            activeSlot = (activeSlot + 1) % 2;
            ActivatePassiveForms();
            OnSpellChanged?.Invoke();
        }

        cooldownTimer -= Time.deltaTime;

        var spell = ActiveSpell;
        if (spell == null || !spell.IsComplete) return;

        // Aura is passive - auto-activates
        if (spell.form.formType == FormType.Aura && activeAura == null)
            ActivatePassiveForms();

        // Non-passive forms: cast on LMB
        if (spell.form.formType != FormType.Aura)
        {
            if (mouse.leftButton.wasPressedThisFrame && cooldownTimer <= 0)
            {
                Cast(spell);
                cooldownTimer = spell.form.cooldown;
            }
        }
    }

    void ActivatePassiveForms()
    {
        var spell = ActiveSpell;
        if (spell == null || !spell.IsComplete) return;

        if (spell.form.formType == FormType.Aura && activeAura == null)
        {
            float dmg = CalculateDamage(spell);
            activeAura = gameObject.AddComponent<AuraForm>();
            activeAura.Init(spell.form.auraRadius, dmg, spell.element, spell.form.cooldown);
        }
    }

    void DeactivatePassiveForms()
    {
        if (activeAura != null) { activeAura.Deactivate(); activeAura = null; }
        if (activeOrbit != null) { activeOrbit.Cleanup(); activeOrbit = null; }
    }

    void Cast(SpellData spell)
    {
        int count = 1;
        float spreadAngle = 0;
        float damageMult = 1f;

        bool useSplit = spell.modifier != null && spell.modifier.modifierType == ModifierType.Split;
        if (useSplit)
        {
            damageMult = spell.modifier.damageMultiplier;
            // Split compatibility check
            bool compatible = spell.form.formType switch
            {
                FormType.Aura => false,
                _ => true
            };
            if (compatible)
            {
                count = spell.modifier.splitCount;
                spreadAngle = spell.modifier.splitSpreadAngle;
            }
        }

        float damage = spell.element.baseDamage * damageMult;

        switch (spell.form.formType)
        {
            case FormType.Bolt:
                FireDirectional(spell, count, spreadAngle, damage, (dir, dmg) =>
                {
                    CreateBolt(dir, spell, dmg);
                });
                break;

            case FormType.Cone:
                FireDirectional(spell, count, spreadAngle, damage, (dir, dmg) =>
                {
                    ConeAttack.Fire(transform.position, dir, spell.form.coneAngle, spell.form.coneRange, dmg, spell.element);
                });
                break;

            case FormType.Beam:
                FireDirectional(spell, count, spreadAngle, damage, (dir, dmg) =>
                {
                    BeamAttack.Fire(transform.position, dir, spell.form.beamRange, spell.form.beamWidth, dmg, spell.element);
                });
                break;

            case FormType.Orbit:
                if (activeOrbit != null) break; // Already active
                int orbCount = spell.form.orbitCount;
                if (useSplit) orbCount += spell.modifier.splitCount;
                activeOrbit = gameObject.AddComponent<OrbitForm>();
                activeOrbit.Init(spell.form.orbitRadius, spell.form.orbitSpeed, orbCount, damage, spell.element, spell.form.orbitDuration);
                break;

            case FormType.Trap:
                PlaceTraps(spell, count, damage);
                break;
        }
    }

    void FireDirectional(SpellData spell, int count, float spreadAngle, float damage, Action<Vector3, float> fireAction)
    {
        Vector3 baseDir = transform.forward;
        float startAngle = -(count - 1) * spreadAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * spreadAngle;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * baseDir;
            fireAction(dir, damage);
        }
    }

    void CreateBolt(Vector3 dir, SpellData spell, float damage)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bolt";
        go.transform.position = transform.position + dir * 0.6f + Vector3.up * 0.5f;
        go.transform.localScale = Vector3.one * 0.25f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.8f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = spell.element.color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", spell.element.color * 3f);
        go.GetComponent<Renderer>().material = mat;

        var proj = go.AddComponent<SpellProjectile>();
        proj.Setup(dir, spell.form.projectileSpeed, damage, spell.element, spell.form.range);
    }

    void PlaceTraps(SpellData spell, int count, float damage)
    {
        var mouse = Mouse.current;
        if (mouse == null || Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.up, 0);
        if (!ground.Raycast(ray, out float d)) return;

        Vector3 basePos = ray.GetPoint(d);

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = count > 1 ? UnityEngine.Random.insideUnitSphere * 1.5f : Vector3.zero;
            offset.y = 0;
            TrapForm.Create(basePos + offset, damage, spell.element, spell.form.trapRadius, spell.form.trapArmTime);
        }
    }

    float CalculateDamage(SpellData spell)
    {
        float dmg = spell.element.baseDamage;
        if (spell.modifier != null) dmg *= spell.modifier.damageMultiplier;
        return dmg;
    }

    void OnDisable()
    {
        DeactivatePassiveForms();
    }
}
