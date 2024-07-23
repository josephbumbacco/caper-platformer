using System.Collections;
using UnityEngine;
using TMPro;

public class PlayerMovementWithStrafes : MonoBehaviour
{
    public CharacterController controller;
    public Transform GroundCheck;
    public LayerMask GroundMask;
    public LayerMask LavaMask;

    public TextMeshProUGUI speedLabel;
    public TextMeshProUGUI topSpeedLabel;
    public TextMeshProUGUI directionLabel;
    public TextMeshProUGUI jumpQueueLabel;
    public TextMeshProUGUI dashLabel;

    private float wishspeed2;
    private float gravity = -20f;
    private float normalGravity = -20f;
    private float increasedGravity = -30f; // Adjust the increased gravity as needed
    private float increasedGravityDuration = 0.5f; // Duration of increased gravity effect
    float wishspeed;

    public float GroundDistance = 0.4f;
    public float moveSpeed = 7.0f;
    public float runAcceleration = 14f;
    public float runDeacceleration = 10f;
    public float airAcceleration = 2.0f;
    public float airDeacceleration = 2.0f;
    public float airControl = 0.3f;
    public float sideStrafeAcceleration = 50f;
    public float sideStrafeSpeed = 1f;
    public float jumpSpeed = 8.0f;
    public float friction = 6f;
    private float playerTopVelocity = 0;
    public float playerFriction = 0f;
    public float maxSpeed;
    public float forceMagnitude;
    public float maxDashDistance;
    private Vector3 dashStartPos;
    public float dashDecelerationTime = 0.5f; // Time to smoothly decelerate after the dash

    float speedLimit = 20f;
    float addspeed;
    float accelspeed;
    float currentspeed;
    float zspeed;
    float speed;
    float dot;
    float k;
    float accel;
    float newspeed;
    float control;
    float drop;

    public bool JumpQueue = false;
    public bool wishJump = false;
    public bool isDashing = false;
    public bool speedLimitBool = true;

    private Vector3 lastPos;
    private Vector3 moved;

    public Vector3 PlayerVel;
    public float ModulasSpeed;
    public float ZVelocity;
    public float XVelocity;

    public Vector3 moveDirection;
    public Vector3 moveDirectionNorm;
    private Vector3 playerVelocity;
    Vector3 wishdir;
    Vector3 vec;

    public Transform playerView;
    public Transform spawnPos;
    private Transform spawnPosReal;

    public float x;
    public float z;

    public bool IsGrounded;
    public bool IsLava;

    public Transform player;
    Vector3 udp;

    public float bounceHeight = 1.5f; // Initial bounce height
    public float bounceDamping = 0.6f; // Damping factor for bounce height
    private float bounceCooldown = 0.0f; // Cooldown time between bounces
    private float lastBounceTime = -0.1f; // Track the last bounce time to manage cooldown

    private void Start()
    {
        lastPos = player.position;
    }

    void Update()
    {
        #region //UI, Feel free to remove the region.

        moved = player.position - lastPos;
        lastPos = player.position;
        PlayerVel = moved / Time.fixedDeltaTime;

        ZVelocity = Mathf.Abs(PlayerVel.z);
        XVelocity = Mathf.Abs(PlayerVel.x);

        ModulasSpeed = Mathf.Sqrt(PlayerVel.z * PlayerVel.z + PlayerVel.x * PlayerVel.x);

        #endregion

        IsGrounded = Physics.CheckSphere(GroundCheck.position, GroundDistance, GroundMask);
        IsLava = Physics.CheckSphere(GroundCheck.position, GroundDistance, LavaMask);

        if (IsLava)
        {
            controller.enabled = false;
            controller.transform.position = spawnPos.position;
            controller.enabled = true;
        }

        QueueJump();

        /* Movement, here's the important part */
        if (controller.isGrounded)
        {
            if (playerVelocity.x > maxSpeed && playerVelocity.z > maxSpeed)
            {
                speedLimitBool = false;
            }
            isDashing = false;
            GroundMove();
            ApplyBounce(); // Apply bounce effect when grounded
        }
        else if (!controller.isGrounded)
            AirMove();

        if (speedLimitBool)
        {
            // Apply speed limit
            ApplySpeedLimit();
        }

        // Apply force when primary mouse button is clicked
        if (Input.GetMouseButtonDown(0))
        {
            if (!isDashing)
            {
                bounceHeight = 1.5f;
                ApplyForceInFacingDirection();
            }
        }

        // Gradually slow down the dash
        if (isDashing)
        {

            float dashDistance = Vector3.Distance(dashStartPos, transform.position);
            if (dashDistance >= maxDashDistance)
            {
                StartCoroutine(SlowDownDash());
            }
        }

        // Apply downward force when Shift key is pressed
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            playerVelocity.y += gravity * 2 * Time.deltaTime; // Adjust the multiplier as needed for the desired downward force
        }

        // Move the controller
        controller.Move(playerVelocity * Time.deltaTime);

        // Calculate top velocity
        udp = playerVelocity;
        udp.y = 0;
        if (udp.magnitude > playerTopVelocity)
        {
            playerTopVelocity = udp.magnitude;
        }

        topSpeedLabel.text = "top speed: " + playerTopVelocity.ToString();
        speedLabel.text = "speed: " + Mathf.Round(speed);
        directionLabel.text = "direction: " + wishdir;
        jumpQueueLabel.text = "jump queue: " + JumpQueue;
        dashLabel.text = "dash: " + isDashing;
    }

    public void SetMovementDir()
    {
        x = Input.GetAxis("Horizontal");
        z = Input.GetAxis("Vertical");
    }

    void QueueJump()
    {
        if (Input.GetButtonDown("Jump"))
        {
            bounceHeight = 1.5f;
            if (IsGrounded)
            {
                // If on the ground, immediately apply the jump
                wishJump = true;
            }
            else
            {
                // Queue the jump if in the air
                JumpQueue = true;
            }
        }

        // Apply the queued jump if on the ground
        if (IsGrounded && JumpQueue)
        {
            wishJump = true;
            JumpQueue = false;
        }
    }

    public void Accelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        currentspeed = Vector3.Dot(playerVelocity, wishdir);
        addspeed = wishspeed - currentspeed;
        if (addspeed <= 0)
            return;
        accelspeed = accel * Time.deltaTime * wishspeed;
        if (accelspeed > addspeed)
            accelspeed = addspeed;

        playerVelocity.x += accelspeed * wishdir.x;
        playerVelocity.z += accelspeed * wishdir.z;
    }

    public void AirMove()
    {
        SetMovementDir();

        wishdir = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        wishdir = transform.TransformDirection(wishdir);

        wishspeed = wishdir.magnitude;

        wishspeed *= 7f;

        wishdir.Normalize();
        moveDirectionNorm = wishdir;

        // Aircontrol
        wishspeed2 = wishspeed;
        if (Vector3.Dot(playerVelocity, wishdir) < 0)
            accel = airDeacceleration;
        else
            accel = airAcceleration;

        // If the player is ONLY strafing left or right
        if (Input.GetAxis("Horizontal") == 0 && Input.GetAxis("Vertical") != 0)
        {
            if (wishspeed > sideStrafeSpeed)
                wishspeed = sideStrafeSpeed;
            accel = sideStrafeAcceleration;
        }

        Accelerate(wishdir, wishspeed, accel);

        AirControl(wishdir, wishspeed2);

        // !Aircontrol

        // Apply gravity
        playerVelocity.y += gravity * Time.deltaTime;

        void AirControl(Vector3 wishdir, float wishspeed)
        {
            if (Input.GetAxis("Horizontal") == 0 || wishspeed == 0)
                return;

            zspeed = playerVelocity.y;
            playerVelocity.y = 0;
            speed = playerVelocity.magnitude;
            playerVelocity.Normalize();

            dot = Vector3.Dot(playerVelocity, wishdir);
            k = 32;
            k *= airControl * dot * dot * Time.deltaTime;

            if (dot > 0)
            {
                playerVelocity.x = playerVelocity.x * speed + wishdir.x * k;
                playerVelocity.y = playerVelocity.y * speed + wishdir.y * k;
                playerVelocity.z = playerVelocity.z * speed + wishdir.z * k;

                playerVelocity.Normalize();
                moveDirectionNorm = playerVelocity;
            }

            playerVelocity.x *= speed;
            playerVelocity.y = zspeed;
            playerVelocity.z *= speed;
        }
    }

    public void GroundMove()
    {
        if (!wishJump)
            ApplyFriction(1.0f);
        else
            ApplyFriction(0);

        SetMovementDir();

        wishdir = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();
        moveDirectionNorm = wishdir;

        wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        Accelerate(wishdir, wishspeed, runAcceleration);

        playerVelocity.y = 0;

        if (wishJump)
        {
            playerVelocity.y = jumpSpeed;
            wishJump = false;
        }

        void ApplyFriction(float t)
        {
            vec = playerVelocity;
            vec.y = 0f;
            speed = vec.magnitude;
            drop = 0f;

            if (controller.isGrounded)
            {
                control = speed < runDeacceleration ? runDeacceleration : speed;
                drop = control * friction * Time.deltaTime * t;
            }

            newspeed = speed - drop;
            playerFriction = newspeed;
            if (newspeed < 0)
                newspeed = 0;
            if (speed > 0)
                newspeed /= speed;

            if (playerVelocity.x < speedLimit && playerVelocity.y < speedLimit)
            {
                playerVelocity.x *= newspeed;
                playerVelocity.z *= newspeed;
            }
        }
    }

    void ApplySpeedLimit()
    {
        Vector3 horizontalVelocity = new Vector3(playerVelocity.x, 0, playerVelocity.z);
        if (horizontalVelocity.magnitude > maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;
            playerVelocity.x = horizontalVelocity.x;
            playerVelocity.z = horizontalVelocity.z;
        }

        if (playerVelocity.y > maxSpeed)
        {
            playerVelocity.y = maxSpeed / 2;
            playerVelocity.x = maxSpeed / 2;
            playerVelocity.z = maxSpeed / 2;
        }
        else if (playerVelocity.y < -maxSpeed)
        {
            playerVelocity.y = -maxSpeed;
        }
    }

    void ApplyForceInFacingDirection()
    {
        speedLimitBool = false;
        isDashing = true;
        dashStartPos = transform.position;
        Vector3 forceDirection = playerView.forward;
        playerVelocity += forceDirection * forceMagnitude;
        StartCoroutine(ResetSpeedLimitAfterDelay(0.2f)); // Adjust the delay as needed
    }

    private IEnumerator ResetSpeedLimitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        speedLimitBool = true;
        ApplySpeedLimit();
    }

    private IEnumerator SlowDownDash()
    {
        float elapsed = 0f;
        Vector3 initialVelocity = playerVelocity;

        // Define the minimum velocity to which the player will slow down
        float minVelocityMagnitude = 30f; // Adjust this value as needed

        while (elapsed < dashDecelerationTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dashDecelerationTime;

            // Lerp velocity from initial to min speed
            float targetSpeed = Mathf.Lerp(initialVelocity.magnitude, minVelocityMagnitude, t);
            Vector3 targetVelocity = initialVelocity.normalized * targetSpeed;

            // Apply the target velocity to playerVelocity
            playerVelocity = targetVelocity;

            yield return null;
        }

        // Ensure playerVelocity is at least the minVelocityMagnitude after coroutine ends
        if (playerVelocity.magnitude < minVelocityMagnitude)
        {
            playerVelocity = playerVelocity.normalized * minVelocityMagnitude;
        }

        isDashing = false;

        // Temporarily increase gravity for a faster fall
        gravity = increasedGravity;
        yield return new WaitForSeconds(increasedGravityDuration);
        gravity = normalGravity;
    }

    private void ApplyBounce()
    {
        
        if (IsGrounded && Time.time - lastBounceTime >= bounceCooldown)
        {
            // Check if we are falling
            if (playerVelocity.y <= 0)
            {
                // Apply bounce if the bounce height is still positive
                if (bounceHeight > 0)
                {
                    playerVelocity.y = Mathf.Sqrt(bounceHeight * -2f * gravity); // Calculate the bounce velocity
                    bounceHeight *= bounceDamping; // Reduce bounce height for the next bounce
                    lastBounceTime = Time.time; // Update last bounce time
                }
            }
        }
    }
}