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
    public float lightAttackRate = 0.2f; // Vuruşlar arası bekleme (Spam hızı)
    public float lightAttackRange = 1.5f; // Hafif vuruşun menzili biraz daha dar
    public float lightAttackDuration = 0.1f; // 1/5 oranındaki animasyon hızı
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

    private float _currentCharge = 0f;
    private bool _isCharging = false;
    

    void Start()
    {
    }


    void Update()
    {
        Move();
        HandleHammerCharge(); // Sağ Tık (Fire2)
        HandleLightAttack();  // Sol Tık (Fire1)
    }

    protected override void Move()
    {
        float moveX = Input.GetAxis("Horizontal"); 
        float moveY = Input.GetAxis("Vertical");   

        Vector3 direction = new Vector3(moveX, moveY, 0).normalized;
        
        // Şarj olurken yavaşla, normalde tam hız git
        float currentSpeed = _isCharging ? stats.moveSpeed * 0.3f : stats.moveSpeed;
        transform.Translate(direction * currentSpeed * Time.deltaTime);

        if (moveX > 0)
        {
        // Sağa gidiyorsa ölçeği normale çevir
        transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (moveX < 0)
        {
        // Sola gidiyorsa X ekseninde aynala
        transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    // --- SOL TIK: HAFİF SALDIRI ---
    private void HandleLightAttack()
    {
        // Eğer ağır saldırı şarj ediyorsak hafif vuruş yapma
        if (_isCharging) return;

        if (Input.GetButtonDown("Fire1") && Time.time >= _nextAttackTime)
        {
            LightAttack();
            _nextAttackTime = Time.time + lightAttackRate;
        }
    }

    private void LightAttack()
    {
        // Görsel savurma
        StartCoroutine(LightSwingRoutine());

        // Hasar kontrolü
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, lightAttackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target != null)
            {
                // Normal hasar ver (Çarpan yok)
                target.TakeDamage(stats.lightAttackDamage, false);
                
                // Hafif vuruş için minimalist bir Hit-Stop (Çok kısa)
                // StartCoroutine(HitStop(0.03f)); 
            }
        }
    }

    private IEnumerator LightSwingRoutine()
    {
        // Çekici hızlıca ileri savur (45 derece yeterli hafif vuruş için)
        hammerPivot.localRotation = Quaternion.Euler(0, 0, -45f);
        
        // 0.1 saniye bekle (Senin istediğin 1/5 süre)
        yield return new WaitForSeconds(lightAttackDuration);

        // Eski haline getir
        hammerPivot.localRotation = Quaternion.identity;
    }

    // --- SAĞ TIK: AĞIR SALDIRI (KODUNUN AYNI HALİ) ---
    private void HandleHammerCharge()
    {
        if (Input.GetButton("Fire2"))
        {
            _isCharging = true;
            meterCanvas.SetActive(true);

            _currentCharge += Time.deltaTime;
            _currentCharge = Mathf.Clamp(_currentCharge, 0f, maxChargeTime);

            float progress = _currentCharge / maxChargeTime;
            chargeMeter.value = progress; 

            hammerPivot.localRotation = Quaternion.Euler(0, 0, progress * 90f);
        }

        if (Input.GetButtonUp("Fire2"))
        {
            if (_currentCharge >= maxChargeTime)
            {
                hammerPivot.localRotation = Quaternion.identity;
                HammerSlam(); 
            }
            else
            {
                hammerPivot.localRotation = Quaternion.identity;
            }

            ResetCharge();
        }
    }

    private void HammerSlam()
{
    Debug.Log("AĞIR ÇEKİÇ VURULDU!");
    
   CinemachineImpulseSource source = GetComponent<CinemachineImpulseSource>();
    
    if (source != null)
    {
        source.GenerateImpulse(); 
        //Debug.Log("Sarsıntı sinyali gönderildi!");
    }
    else 
    {
        Debug.LogError("HATA: Player üzerinde CinemachineImpulseSource bulunamadı!");
    }

    
    if (generator != null)
        {
            generator.BreakWallsInArea(attackPoint.position, hammerAOE);
        }

    // Mevcut düşman hasarı kodun devam ediyor...
    Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, hammerAOE, enemyLayers);
    foreach (Collider2D enemy in hitEnemies)
    {
        IDamageable target = enemy.GetComponent<IDamageable>();
        if (target != null) target.TakeDamage(stats.heavyAttackDamage, true);
    }
}

    private void ResetCharge()
    {
        _isCharging = false;
        _currentCharge = 0f;
        chargeMeter.value = 0f;
        meterCanvas.SetActive(false);
    }
}