using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Unity.Cinemachine;

public class Player : BaseEntity
{
    [Header("External References")]
    public DungeonGenerator generator;
    
    [Header("Hammer Settings (Heavy)")]
    public float maxChargeTime = 0.5f;
    public float hammerAOE = 2.5f;

    [Header("Light Attack Settings (Spammable)")]
    public float lightAttackRate = 0.2f; 
    public float lightAttackRange = 1.5f; 
    public float lightAttackDuration = 0.1f; 
    private float _nextAttackTime = 0f;

    [Header("References")]
    public Transform hammerPivot;
    public Transform attackPoint;
    public LayerMask enemyLayers;
    public float goldCount;
    public float experienceCount;

    [Header("UI Reference")]
    public Slider chargeMeter;
    public GameObject meterCanvas;

    [Header("Hit Stop")]
    public HitStopManager hitStopManager;
    [Range(0.01f, 1f)] public float lightHitStopTimeScale = 0.14f;
    public float lightHitStopDuration = 0.02f;
    [Range(0.01f, 1f)] public float heavyHitStopTimeScale = 0.08f;
    public float heavyHitStopDuration = 0.045f;

    [Header("Impact Feedback")]
    public CinemachineImpulseSource lightHitImpulse;
    public GameObject hitVfxPrefab;
    public AudioSource hitAudioSource;
    public AudioClip lightHitSfx;
    public AudioClip heavyHitSfx;
    [Range(0.5f, 1f)] public float lightHitPitch = 0.92f;
    [Range(0.5f, 1f)] public float heavyHitPitch = 0.82f;

    private float _currentCharge = 0f;
    private bool _isCharging = false;
    private Rigidbody2D _rb; // FİZİK İÇİN ŞART
    private CinemachineImpulseSource _defaultImpulseSource;

    protected override void Awake()
    {
        base.Awake();
        // Rigidbody referansını al ve ayarla
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f; // 2D Top-down olduğu için yerçekimini kapat
        _rb.freezeRotation = true; // Karakterin devrilmesini engelle
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Duvar delmeyi engeller
        _defaultImpulseSource = GetComponent<CinemachineImpulseSource>();

        if (hitStopManager == null)
            hitStopManager = HitStopManager.Instance;
    }

    void Update()
    {
        HandleHammerCharge(); // Sağ Tık
        HandleLightAttack();  // Sol Tık
    }

    // FİZİK HAREKETİ BURADA OLMALI
    void FixedUpdate()
    {
        Move();
    }

    protected override void Move()
    {
        float moveX = Input.GetAxisRaw("Horizontal"); 
        float moveY = Input.GetAxisRaw("Vertical");   

        Vector2 direction = new Vector2(moveX, moveY).normalized;
        
        // Şarj olurken yavaşla, normalde tam hız git
        float currentSpeed = _isCharging ? stats.moveSpeed * 0.3f : stats.moveSpeed;
        
        // KRİTİK DÜZELTME: transform.Translate SİLİNDİ, Rigidbody Velocity GELDİ!
        _rb.linearVelocity = direction * currentSpeed;

        // --- FLIP MANTIĞI ---
        if (moveX > 0) transform.localScale = new Vector3(1f, 1f, 1f);
        else if (moveX < 0) transform.localScale = new Vector3(-1f, 1f, 1f);
    }

    private void HandleLightAttack()
    {
        if (_isCharging) return;
        if (Input.GetButtonDown("Fire1") && Time.time >= _nextAttackTime)
        {
            LightAttack();
            _nextAttackTime = Time.time + lightAttackRate;
        }
    }

    private void LightAttack()
    {
        StartCoroutine(LightSwingRoutine());
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, lightAttackRange, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target == null) continue;
            target.TakeDamage(stats.lightAttackDamage, false);
            if (successfulHits == 0)
                firstHitPosition = enemy.ClosestPoint(attackPoint.position);
            successfulHits++;
        }

        if (successfulHits > 0)
        {
            SpawnHitVfx(firstHitPosition);
            PlayImpactFeedback(false);
        }
    }

    private IEnumerator LightSwingRoutine()
    {
        hammerPivot.localRotation = Quaternion.Euler(0, 0, -45f);
        yield return new WaitForSeconds(lightAttackDuration);
        hammerPivot.localRotation = Quaternion.identity;
    }

    private void HandleHammerCharge()
    {
        if (Input.GetButton("Fire2"))
        {
            _isCharging = true;
            meterCanvas.SetActive(true);
            _currentCharge += Time.deltaTime;
            _currentCharge = Mathf.Clamp(_currentCharge, 0f, maxChargeTime);
            chargeMeter.value = _currentCharge / maxChargeTime; 
            hammerPivot.localRotation = Quaternion.Euler(0, 0, (_currentCharge / maxChargeTime) * 90f);
        }

        if (Input.GetButtonUp("Fire2"))
        {
            if (_currentCharge >= maxChargeTime)
            {
                hammerPivot.localRotation = Quaternion.identity;
                HammerSlam(); 
            }
            ResetCharge();
        }
    }

    private void HammerSlam()
    {
        CinemachineImpulseSource source = _defaultImpulseSource;
        if (source != null) source.GenerateImpulse(); 
        
        if (generator != null) generator.BreakWallsInArea(attackPoint.position, hammerAOE);

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, hammerAOE, enemyLayers);
        int successfulHits = 0;
        Vector3 firstHitPosition = attackPoint.position;
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target == null) continue;
            target.TakeDamage(stats.heavyAttackDamage, true);
            if (successfulHits == 0)
                firstHitPosition = enemy.ClosestPoint(attackPoint.position);
            successfulHits++;
        }

        if (successfulHits > 0)
        {
            SpawnHitVfx(firstHitPosition);
            PlayImpactFeedback(true);
        }
    }

    private void PlayImpactFeedback(bool isHeavy)
    {
        if (hitStopManager == null)
            hitStopManager = HitStopManager.Instance != null
                ? HitStopManager.Instance
                : Object.FindAnyObjectByType<HitStopManager>();

        if (hitStopManager == null)
        {
            GameObject managerObject = new GameObject("HitStopManager");
            hitStopManager = managerObject.AddComponent<HitStopManager>();
        }

        if (hitStopManager != null)
        {
            float timeScale = isHeavy ? heavyHitStopTimeScale : lightHitStopTimeScale;
            float duration = isHeavy ? heavyHitStopDuration : lightHitStopDuration;
            hitStopManager.TriggerHitStop(timeScale, duration);
        }

        if (!isHeavy)
        {
            CinemachineImpulseSource lightImpulseSource = lightHitImpulse != null ? lightHitImpulse : _defaultImpulseSource;
            if (lightImpulseSource != null)
                lightImpulseSource.GenerateImpulse();
        }

        PlayHitSfx(isHeavy);
    }

    private void PlayHitSfx(bool isHeavy)
    {
        if (hitAudioSource == null) return;

        AudioClip clip = isHeavy ? heavyHitSfx : lightHitSfx;
        if (clip == null) return;

        float previousPitch = hitAudioSource.pitch;
        hitAudioSource.pitch = isHeavy ? heavyHitPitch : lightHitPitch;
        hitAudioSource.PlayOneShot(clip);
        hitAudioSource.pitch = previousPitch;
    }

    private void SpawnHitVfx(Vector3 worldPos)
    {
        if (hitVfxPrefab == null) return;
        Instantiate(hitVfxPrefab, worldPos, Quaternion.identity);
    }

    private void ResetCharge()
    {
        _isCharging = false;
        _currentCharge = 0f;
        chargeMeter.value = 0f;
        meterCanvas.SetActive(false);
        hammerPivot.localRotation = Quaternion.identity;
    }
}