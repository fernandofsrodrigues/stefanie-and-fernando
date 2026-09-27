using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float movementSpeed = 1.0f;
    
    [SerializeField]
    private float jumpSpeed = 5.0f;

    [SerializeField]
    private Rigidbody2D playerRigidBody;

    [SerializeField]
    private Transform groundCheck;

    [SerializeField]
    private float groundCheckRadius = 0.2f;

    [SerializeField]
    private LayerMask groundLayer;

    private float horizontalInput;
    private bool isGrounded;
    private bool jumpPressed;
    // SF extension: share the classroom motor between keyboard, companion AI and test input.
    private bool externalControl;
    private bool frozen;
    private float jumpBuffer;
    private float coyoteTime;
    public bool IsGrounded => isGrounded;
    public Rigidbody2D Body => playerRigidBody;
    public Func<float,float> HorizontalConstraint;

    public void Configure(Rigidbody2D body, Transform feet, LayerMask ground, float speed = 5.2f, float jump = 10f)
    {
        playerRigidBody = body; groundCheck = feet; groundLayer = ground;
        movementSpeed = speed; jumpSpeed = jump; groundCheckRadius = .14f; externalControl = true;
    }

    public void SetCommand(float horizontal, bool jump)
    {
        horizontalInput = Mathf.Clamp(horizontal, -1.8f, 1.8f);
        if (jump) jumpPressed = true;
    }

    public void Freeze(bool value)
    {
        frozen = value;
        if (playerRigidBody != null) playerRigidBody.simulated = !value;
        if (value) { jumpPressed = false; jumpBuffer = 0; horizontalInput = 0; }
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (frozen || externalControl || Keyboard.current == null) return;
        horizontalInput = Keyboard.current.dKey.ReadValue() - Keyboard.current.aKey.ReadValue();
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpPressed = true;
        }
        
    }

    private void FixedUpdate()
    {
        if (frozen || playerRigidBody == null || groundCheck == null) return;
        CheckGround();
        coyoteTime = isGrounded && playerRigidBody.linearVelocity.y <= .1f ? .1f : coyoteTime - Time.fixedDeltaTime;
        if (jumpPressed) { jumpBuffer = .15f; jumpPressed = false; }
        else jumpBuffer -= Time.fixedDeltaTime;
        Move(horizontalInput);

        if (jumpBuffer > 0 && coyoteTime > 0)
        {
            Jump();
            jumpBuffer = 0; coyoteTime = 0;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            Debug.Log("Hey! I don't have a reference to my GroundCheck!");
            return;
        }

        if (isGrounded)
        {
            Gizmos.color = Color.green;    
        }
        else
        {
            Gizmos.color = Color.red;
        }
        
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        
    }

    private void Move(float movementInput)
    {
        float horizontalVelocity = movementInput * movementSpeed;
        if(externalControl)
        {
            float acceleration=Mathf.Abs(movementInput)<.01f?38:26;
            horizontalVelocity=Mathf.MoveTowards(playerRigidBody.linearVelocity.x,horizontalVelocity,acceleration*Time.fixedDeltaTime);
            if(HorizontalConstraint!=null)
            {float next=playerRigidBody.position.x+horizontalVelocity*Time.fixedDeltaTime;float allowed=HorizontalConstraint(next);horizontalVelocity=(allowed-playerRigidBody.position.x)/Time.fixedDeltaTime;}
        }
        Vector2 movementVelocity = new Vector2(horizontalVelocity, playerRigidBody.linearVelocity.y);
        playerRigidBody.linearVelocity = movementVelocity;
    }

    private void Jump()
    {
        if (isGrounded == false && coyoteTime <= 0)
        {
            return;
        }

        Vector2 jumpVelocity = new Vector2(playerRigidBody.linearVelocity.x, jumpSpeed);
        playerRigidBody.linearVelocity = jumpVelocity;
    }

    private void CheckGround()
    {
        Collider2D detectedGround = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        isGrounded = detectedGround != null;
    }
}
