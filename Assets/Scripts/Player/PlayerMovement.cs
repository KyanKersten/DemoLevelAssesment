using UnityEngine;
using System.Collections;
using FirstGearGames.SmoothCameraShaker;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float jumpForce = 7.5f;
    [SerializeField] float rollForce = 6.0f;
    [SerializeField] Collider2D attackHitbox;
    [SerializeField] float attackHitboxActiveTime = 0.2f;
    [SerializeField] SpriteRenderer hitboxVisual;
    [SerializeField] int maxHealth = 5;
    [SerializeField] private GameObject[] hearts;
    [SerializeField] private Sprite grayedOutHeartSprite;

    [Header("Blocking")]
    [SerializeField] private Collider2D blockHitbox;
    [SerializeField] private SpriteRenderer blockHitboxVisual;
    [SerializeField] private bool blockNegatesDamage = true;
    [SerializeField] private int maxBlockHits = 3;
    [SerializeField] private float blockResetCooldown = 2.0f;

    [Header("VFX")]
    [SerializeField] private GameObject hitEnemyVFX;  // VFX for hitting enemy
    [SerializeField] private GameObject blockImpactVFX; // VFX for blocking attack
    public ShakeData shakeData; // Shake data for camera shake effect
    
    [SerializeField] private AudioSource audioSource; // Reference to the AudioSource
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip hitBlockSound;
    
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool grounded = false;
    private float delayToIdle = 0f;
    private float inputX = 0f;
    private bool jumpPressed = false;
    private int currentAttack = 0;
    private float timeSinceAttack = 0f;
    private bool rolling = false;
    private int facingDirection = 1;
    private float rollDuration = 8.0f / 14.0f;
    private float rollCurrentTime = 0f;
    private float hitboxOriginalLocalX;
    private bool isFacingRight = true;
    private bool isInvincible = false;
    private int currentHealth;
    private Coroutine flashCoroutine;
    private bool isDead = false;
    private bool isBlocking = false;
    private int currentBlockHits;
    private Coroutine blockResetCoroutine;
    private bool isStunned = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (attackHitbox != null)
            attackHitbox.enabled = false;
        hitboxOriginalLocalX = attackHitbox.transform.localPosition.x;
        currentHealth = maxHealth;
        currentBlockHits = maxBlockHits;

        if (blockHitbox != null)
            blockHitbox.enabled = false;
        if (blockHitboxVisual != null)
            blockHitboxVisual.enabled = false;
    }

    void Update()
    {
        if (isDead || isStunned) return;

        timeSinceAttack += Time.deltaTime;

        if (rolling)
            rollCurrentTime += Time.deltaTime;

        if (rollCurrentTime > rollDuration)
        {
            rolling = false;
            isInvincible = false;
        }

        inputX = Input.GetAxisRaw("Horizontal");

        if (inputX > 0 && !isFacingRight)
            Flip();
        else if (inputX < 0 && isFacingRight)
            Flip();

        facingDirection = isFacingRight ? 1 : -1;

        if (attackHitbox != null)
        {
            attackHitbox.transform.localPosition = new Vector3(
                Mathf.Abs(hitboxOriginalLocalX) * facingDirection,
                attackHitbox.transform.localPosition.y,
                attackHitbox.transform.localPosition.z
            );
        }

        if (Input.GetMouseButtonDown(0) && timeSinceAttack > 0.25f && !rolling && !isBlocking)
        {
            currentAttack++;
            if (currentAttack > 3)
                currentAttack = 1;
            if (timeSinceAttack > 1.0f)
                currentAttack = 1;
            animator.SetTrigger("Attack" + currentAttack);
            timeSinceAttack = 0f;

            if (attackHitbox != null)
                StartCoroutine(EnableHitboxTemporarily());
            
            if (audioSource != null && attackSound != null)
            {
                audioSource.PlayOneShot(attackSound);
            }
        }

        if (Input.GetMouseButtonDown(1) && !rolling && currentBlockHits > 0)
        {
            animator.SetTrigger("Block");
            animator.SetBool("IdleBlock", true);
            isBlocking = true;

            if (blockHitbox != null)
                blockHitbox.enabled = true;
            if (blockHitboxVisual != null)
                blockHitboxVisual.enabled = true;
        }

        if (Input.GetMouseButtonUp(1))
        {
            animator.SetBool("IdleBlock", false);
            isBlocking = false;

            if (blockHitbox != null)
                blockHitbox.enabled = false;
            if (blockHitboxVisual != null)
                blockHitboxVisual.enabled = false;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && !rolling)
        {
            rolling = true;
            isInvincible = true;
            rollCurrentTime = 0f;
            animator.SetTrigger("Roll");
            rb.linearVelocity = new Vector2(facingDirection * rollForce, rb.linearVelocity.y);
        }

        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressed = true;

        if (Mathf.Abs(inputX) > Mathf.Epsilon)
        {
            delayToIdle = 0.05f;
            animator.SetInteger("AnimState", 1);
        }
        else
        {
            delayToIdle -= Time.deltaTime;
            if (delayToIdle < 0)
                animator.SetInteger("AnimState", 0);
        }
    }

    void FixedUpdate()
    {
        if (isDead || isStunned) return;

        if (!rolling)
            rb.linearVelocity = new Vector2(inputX * moveSpeed, rb.linearVelocity.y);

        if (jumpPressed && grounded && !rolling)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            grounded = false;
            animator.SetBool("Grounded", false);
            animator.SetTrigger("Jump");
        }

        animator.SetFloat("AirSpeedY", rb.linearVelocity.y);
        jumpPressed = false;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead) return;

        if (collision.contacts[0].normal.y > 0.5f)
        {
            grounded = true;
            animator.SetBool("Grounded", true);
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (isDead) return;

        grounded = false;
        animator.SetBool("Grounded", false);
    }

    IEnumerator EnableHitboxTemporarily()
    {
        attackHitbox.enabled = true;
        if (hitboxVisual != null)
            hitboxVisual.enabled = true;
        yield return new WaitForSeconds(attackHitboxActiveTime);
        attackHitbox.enabled = false;
        if (hitboxVisual != null)
            hitboxVisual.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (attackHitbox != null && attackHitbox.enabled && other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(1);

                // Spawn hitEnemyVFX at enemy's position
                if (hitEnemyVFX != null)
                {
                    Instantiate(hitEnemyVFX, other.transform.position + new Vector3(0, 2, 0), Quaternion.identity);
                }

                // Trigger camera shake for attacking
                CameraShakerHandler.Shake(shakeData);
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (isInvincible || isDead) return;

        if (isBlocking && blockNegatesDamage && currentBlockHits > 0)
        {
            currentBlockHits--;
            Debug.Log("Attack blocked! Remaining blocks: " + currentBlockHits);

            if (audioSource != null && hitBlockSound != null)
            {
                audioSource.PlayOneShot(hitBlockSound);
            }
            
            if (blockImpactVFX != null)
            {
                Instantiate(blockImpactVFX, transform.position + new Vector3(0, 2f, 0), Quaternion.identity);
            }

            // Trigger camera shake for blocking
            CameraShakerHandler.Shake(shakeData);

            if (blockResetCoroutine != null)
                StopCoroutine(blockResetCoroutine);
            blockResetCoroutine = StartCoroutine(ResetBlockHitsAfterDelay());

            if (currentBlockHits <= 0)
            {
                Debug.Log("Shield broken!");
                isBlocking = false;
                animator.SetBool("IdleBlock", false);

                if (blockHitbox != null)
                    blockHitbox.enabled = false;
                if (blockHitboxVisual != null)
                    blockHitboxVisual.enabled = false;

                StartCoroutine(Stun(1.0f));
            }

            return;
        }

        currentHealth -= amount;
        UpdateHearts();

        animator.SetTrigger("Hurt");

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRed());

        Debug.Log("Player took " + amount + " damage! Health: " + currentHealth);

        // Trigger camera shake for taking damage
        CameraShakerHandler.Shake(shakeData);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator ResetBlockHitsAfterDelay()
    {
        yield return new WaitForSeconds(blockResetCooldown);
        currentBlockHits = maxBlockHits;
        Debug.Log("Shield recovered!");
    }

    IEnumerator Stun(float duration)
    {
        isStunned = true;
        animator.SetTrigger("Hurt");
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    IEnumerator FlashRed()
    {
        spriteRenderer.color = Color.red;
        float timer = 0f;
        float flashTime = 0.3f;
        while (timer < flashTime)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        spriteRenderer.color = Color.white;
        flashCoroutine = null;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        spriteRenderer.color = Color.white;
        animator.SetTrigger("Death");
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;
        Debug.Log("Player died!");
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    public void UpdateHearts()
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].SetActive(i < currentHealth);
        }
    }
}
