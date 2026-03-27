using UnityEngine;
using System;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// In-run gold economy. Gold drops from enemies, spent in shops.
/// Resets each run.
/// </summary>
public class GoldSystem : MonoBehaviour
{
    public static GoldSystem Instance { get; private set; }

    int _gold;
    public int Gold => _gold;

    public event Action<int> OnGoldChanged;      // new total
    public event Action<int> OnGoldGained;       // delta amount (for floating text)

    void Awake() { Instance = this; }

    public void Init(int startingGold)
    {
        _gold = startingGold;
        OnGoldChanged?.Invoke(_gold);
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        _gold += amount;
        OnGoldChanged?.Invoke(_gold);
        OnGoldGained?.Invoke(amount);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || _gold < amount) return false;
        _gold -= amount;
        OnGoldChanged?.Invoke(_gold);
        return true;
    }

    public void Reset(int startingGold)
    {
        _gold = startingGold;
        OnGoldChanged?.Invoke(_gold);
    }

    // ─── GOLD DROP ──────────────────────────────────────────────

    /// <summary>Spawn a collectible gold pickup at a position.</summary>
    public static void SpawnGoldDrop(Vector3 position, int amount)
    {
        if (amount <= 0) return;

        var go = new GameObject("GoldDrop");
        go.transform.position = position + Vector3.up * 0.3f;

        // Visual: small golden sphere
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(sphere.GetComponent<SphereCollider>());
        sphere.name = "GoldOrb";
        sphere.transform.parent = go.transform;
        sphere.transform.localPosition = Vector3.zero;
        float scale = Mathf.Lerp(0.12f, 0.25f, Mathf.Clamp01(amount / 20f));
        sphere.transform.localScale = Vector3.one * scale;
        sphere.GetComponent<Renderer>().material = ShaderCache.NewGold();

        // Scatter impulse
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 0.05f;
        rb.linearDamping = 2f;
        Vector3 burst = new Vector3(
            Random.Range(-1.5f, 1.5f),
            Random.Range(2f, 4f),
            Random.Range(-1.5f, 1.5f));
        rb.AddForce(burst, ForceMode.Impulse);

        // Pickup trigger (slightly delayed so it flies first)
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var pickup = go.AddComponent<GoldPickup>();
        pickup.amount = amount;
        pickup.activateDelay = 0.4f;

        // Auto-destroy after 15s if not picked up
        Object.Destroy(go, 15f);
    }

    /// <summary>Calculate gold drop for an enemy based on wave and floor.</summary>
    public static int CalculateEnemyDrop(int wave, bool isBoss, int floor = 1)
    {
        if (isBoss) return 30 + wave * 5 + floor * 10;
        // Regular enemies: base gold + wave scaling + floor scaling
        // Floor scaling ensures gold keeps pace with shop prices on later floors
        int baseGold = Random.Range(4, 9); // 4-8
        int waveBonus = Mathf.FloorToInt(wave * 0.8f);
        int floorBonus = (floor - 1) * 3; // +0/+3/+6/+9/+12 for floors 1-5
        return baseGold + waveBonus + floorBonus;
    }
}

/// <summary>Collectible gold pickup that flies toward the player.</summary>
public class GoldPickup : MonoBehaviour
{
    public int amount;
    public float activateDelay;

    float _timer;
    bool _active;
    Transform _player;
    Rigidbody _rb;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) _player = pc.transform;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (!_active && _timer >= activateDelay)
        {
            _active = true;
            // Switch to kinematic for magnet behavior
            if (_rb != null) { _rb.useGravity = false; _rb.isKinematic = true; }
        }

        if (!_active || _player == null) return;

        // Magnet toward player — always pull toward player once active
        Vector3 toPlayer = _player.position + Vector3.up * 0.5f - transform.position;
        float dist = toPlayer.magnitude;

        if (dist < 1.5f)
        {
            // Collect
            if (GoldSystem.Instance != null)
                GoldSystem.Instance.AddGold(amount);
            SFXSystem.Play(SFXSystem.SFXType.GoldPickup, transform.position);
            GameFeel.SpawnPickupBurst(transform.position, new Color(1f, 0.85f, 0.2f));
            Destroy(gameObject);
            return;
        }

        // Magnet range: only pull when within 8 units (not infinite range)
        if (dist > 8f) return;

        float speed = Mathf.Lerp(14f, 5f, Mathf.Clamp01(dist / 6f));
        transform.position += toPlayer.normalized * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        if (GoldSystem.Instance != null)
            GoldSystem.Instance.AddGold(amount);
        SFXSystem.Play(SFXSystem.SFXType.GoldPickup, transform.position);
        GameFeel.SpawnPickupBurst(transform.position, new Color(1f, 0.85f, 0.2f));
        Destroy(gameObject);
    }
}
