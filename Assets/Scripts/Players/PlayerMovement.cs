using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using SmartConsole;
using Unity.VisualScripting;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Animation")]
    public Animator animator;

    [Header("Attack")]
    public int numberOfAttack = 3;
    private int currentAttackCount = -1;
    private float attackDuration = .5f;
    private float currentAttackDuration = 0f;
    private bool isAttacking = false;
    public Transform swordPosition;


    public int CurrentAttackCount
    {
        get => currentAttackCount;
        private set => currentAttackCount = value > numberOfAttack ? 0 : value;
    }

    [Header("Run")]
    public float normalSpeed;
    public float maxSpeed;
    public float currentSpeed;
    public bool isFacingRight = true;

    [Header("Jump")]
    [Range(0, 15)]
    public float jumpVelocity;
    [Range(0, 15)]
    public float secondJumpVelocity;
    public float jumpFallMultiplication;
    public float maxJumpPower;
    public float normalGravity;
    public LayerMask groundLayer;
    public Transform groundCheck;
    public int countJump = 0;

    [Header("Wall Slide")]
    public bool isWallSlide = false;
    public float wallSlideSpeed = 2f;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask wallLayer;
    private bool isWallJumping;
    private float wallJumpingDirection;
    private readonly float wallJumpingTime = .2f;
    private readonly float wallJumpingDuration = .4f;
    private float wallJumpingCouter;
    private Vector2 wallJumpPower = new Vector2(2f, 20f);

    [Header("Component")]
    public Rigidbody2D mRigidbody2D;

    //value movement
    private float horizontal;
    private float vertical;



    [Header("NetworkVariable")]
    public NetworkVariable<MyCustomData> myCustomData = new(new MyCustomData
    {
        _bool = true,
        _int = 0,
    }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [SerializeField] private Transform spawnedObjectPrefab;
    private Transform prefabTransform;

    public struct MyCustomData : INetworkSerializable
    {
        public int _int;
        public bool _bool;
        public FixedString128Bytes message;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _int);
            serializer.SerializeValue(ref _bool);
        }
    }

    public override void OnNetworkSpawn()
    {
        myCustomData.OnValueChanged += (prevValue, newValue) =>
        {
            Debug.Log(OwnerClientId + "; CustomData: " + myCustomData.Value._int + " " + myCustomData.Value._bool + " message: " + myCustomData.Value.message);
        };
    }

    void Start()
    {
        mRigidbody2D = GetComponent<Rigidbody2D>();
        currentSpeed = normalSpeed;
    }

    private void FixedUpdate()
    {
        ModifyPhysic();

        //for run method
        if (!isWallJumping && !isAttacking)
        {
            mRigidbody2D.velocity = new Vector2(currentSpeed * horizontal, mRigidbody2D.velocity.y);
        }
    }

    [ClientRpc]
    void TestClientRpc()
    {
        Debug.Log("ClientRpcTest: " + OwnerClientId);
    }

    [ServerRpc]
    void TestServerRpc()
    {
        TestClientRpc();
        Debug.Log("ServerRpcTest: " + OwnerClientId);
    }

    void Update()
    {
        if (!IsOwner) return;

        if (isWallJumping)
        {
            Debug.Log(isWallJumping);
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            TestClientRpc();
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            TestServerRpc();
        }

        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");

        //update horizontal value for animator
        animator.SetFloat("horizontal", horizontal);

        Attack();

        if (!isAttacking)
        {
            Run();
            BetterJump();

            WallSlide();
            WallJump();
        }

        if (!isWallJumping)
        {
            Flip();
        }
    }

    public void Run()
    {
        if (Mathf.Abs(horizontal) <= 0.3f)
        {
            currentSpeed -= Time.deltaTime * 3;
            if (currentSpeed <= normalSpeed) currentSpeed = normalSpeed;
            animator.speed = 0.8f;
        }
        else
        {
            currentSpeed += Time.deltaTime * 6;
            if (currentSpeed >= maxSpeed) currentSpeed = maxSpeed;
            animator.speed = 1f;
        }

        if (Mathf.Abs(horizontal) > 0 && IsGrounded())
        {
            animator.SetBool("isRunning", true);
        }
        else
        {
            animator.SetBool("isRunning", false);
        }
    }

    public void Flip()
    {
        if (horizontal > 0f && !isFacingRight || horizontal < 0f && isFacingRight)
        {
            isFacingRight = !isFacingRight;
            Vector3 tempScale = transform.localScale;
            tempScale.x *= -1f;
            transform.localScale = tempScale;
        }
    }

    public void ModifyPhysic()
    {

    }

    public void WallSlide()
    {
        if (IsWalled() && !IsGrounded() && horizontal != 0f)
        {
            isWallSlide = true;
            mRigidbody2D.velocity = new Vector2(mRigidbody2D.velocity.y, Mathf.Clamp(mRigidbody2D.velocity.y, -wallSlideSpeed, float.MaxValue));
        }
        else
        {
            isWallSlide = false;
        }

        animator.SetBool("isSliding", isWallSlide);
        if (isWallSlide)
        {
            animator.SetBool("isJumping", false);
            animator.SetBool("isFalling", false);
        }
    }

    [ServerRpc]
    public void FlipServerRpc(bool isFlip)
    {
        FlipClientRpc(isFlip);
        if (IsLocalPlayer) return;
        GetComponent<SpriteRenderer>().flipX = isFlip;
    }

    [ClientRpc]
    public void FlipClientRpc(bool isFlip)
    {
        if (IsLocalPlayer) return;
        GetComponent<SpriteRenderer>().flipX = isFlip;
    }

    public void WallJump()
    {
        if (isWallSlide)
        {
            isWallSlide = false;
            wallJumpingDirection = -transform.localScale.x;
            wallJumpingCouter = wallJumpingTime;

            CancelInvoke(nameof(StopWallJumping));
        }
        else
        {
            wallJumpingCouter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump") && wallJumpingCouter > 0f)
        {
            FlipClientRpc(false);

            isWallJumping = true;
            mRigidbody2D.velocity = new Vector2(wallJumpingDirection * wallJumpPower.x, wallJumpPower.y);
            wallJumpingCouter = 0f;

            if (transform.localScale.x != wallJumpingDirection)
            {
                Vector3 localScale1 = transform.localScale;
                isFacingRight = !isFacingRight;
                localScale1.x *= -1f;
                transform.localScale = localScale1;
            }
        }

        Invoke(nameof(StopWallJumping), wallJumpingDuration);
    }

    private void StopWallJumping()
    {
        isWallJumping = false;
    }

    public void BetterJump()
    {
        if (countJump == 0 && Input.GetButtonDown("Jump"))
        {
            //Debug.Log(countJump);
            countJump++;
            mRigidbody2D.velocity += Vector2.up * jumpVelocity;
            if (Input.GetButtonDown("Jump") && countJump <= 1)
            {
                mRigidbody2D.velocity += Vector2.up * secondJumpVelocity;
            }
        }

        if (mRigidbody2D.velocity.y < 0)
        {
            //Debug.Log("Jump Down");
            animator.speed = 1f;
            mRigidbody2D.velocity += (3f - 1) * Physics2D.gravity.y * Time.deltaTime * Vector2.up;
        }
        else if (mRigidbody2D.velocity.y > 0 && !Input.GetButton("Jump"))
        {
            //Debug.Log("Low Jump");
            animator.speed = 0.8f;
            mRigidbody2D.velocity += (2f - 1) * Physics2D.gravity.y * Time.deltaTime * Vector2.up;
        }

        if (IsGrounded())
        {
            //reset count jump
            countJump = 0;
        }

        if (mRigidbody2D.velocity.y > 1f)
        {
            animator.SetBool("isJumping", true);
            animator.SetBool("isFalling", false);
        }
        else if (mRigidbody2D.velocity.y < -0.000001f && !IsGrounded())
        {
            animator.SetBool("isJumping", false);
            animator.SetBool("isFalling", true);
        }
        else if (mRigidbody2D.velocity.y <= 0.000001f && mRigidbody2D.velocity.y >= -0.000001f || IsGrounded())
        {
            animator.SetBool("isJumping", false);
            animator.SetBool("isFalling", false);
        }
    }
    
    void Attack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            CurrentAttackCount+=1;
            currentAttackDuration = 0;
            animator.SetInteger("attackCount", CurrentAttackCount);
            animator.SetBool("isAttacking", true);
            isAttacking = true;
        }

        // modify attack duration for each attack
        if(CurrentAttackCount == 1)
        {
            attackDuration = 1.67f;
        }else if(CurrentAttackCount == 2)
        {
            attackDuration = 2.30f;
        }
        else if(CurrentAttackCount == 0)
        {
            attackDuration = .6f;
        }

        // update current attack time
        if(CurrentAttackCount > -1)
        {
            currentAttackDuration += Time.deltaTime;
        }

        // handle when attacktime equal attack duration
        if(currentAttackDuration >= attackDuration)
        {
            CurrentAttackCount = -1;
            currentAttackDuration = 0f;
            animator.SetInteger("attackCount", CurrentAttackCount);
            animator.SetBool("isAttacking", false);
            isAttacking = false;
        }

        // hanlde bool variable isAttacking when player pause attack
        if (CurrentAttackCount <= -1 && animator.GetBool("isAttacking") && currentAttackDuration == 0f)
        {
            animator.SetBool("isAttacking", false);
            isAttacking = false;
        }
    }

    public bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, .2f, groundLayer);
    }

    public bool IsWalled()
    {
        return Physics2D.OverlapCircle(wallCheck.position, .2f, wallLayer);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(groundCheck.position, 0.2f);
        Gizmos.DrawWireSphere(wallCheck.position, 0.2f);
        Gizmos.DrawWireSphere(swordPosition.position, 0.5f);
    }
}
