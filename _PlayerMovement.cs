using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class _PlayerMovement : MonoBehaviour
{   
    /// ////////////////////////////////////////
    // All the Movement Variabls or Constants //
    ////////////////////////////////////////////
    private CharacterController _cc;
    private Vector3 playerVel = Vector3.zero;
    private float horizontal;
    private float vertical;
    private bool jumpQueued = false;
    public float dashCallTime = 0;
    public float shootCallTime = 0;

    [Header("Misc Constants")]
    [SerializeField] private float jumpSpeed = 8.0f;
    [SerializeField] private float friction = 6.0f;
    [SerializeField] private float gravity = 20.0f;
    public float dashCooldown = 5.0f;
    public float shootCooldown = 0.5f;
    [SerializeField] private float launchForce = 50f;
    [SerializeField] private LayerMask ground;
    [SerializeField] private Transform Camera;

    [Header("Ground Movement Constants")]
    [SerializeField] private float groundMaxSpeed = 7.0f;
    [SerializeField] private float groundAccel = 10.0f;
    [SerializeField] private float groundDecel = 10.0f;

    [Header("Air Movement Constants")]
    [SerializeField] private float airMaxSpeed = 7.0f;
    [SerializeField] private float airAccel = 2.0f;
    [SerializeField] private float airDecel = 2.0f;

    [Header("Strafeing Movement Constants")]
    [SerializeField] private float strafeMaxSpeed = 1.0f;
    [SerializeField] private float strafeAccel = 25.0f;

    [Header("Keybinds")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [Tooltip("0 = LMB, 1 = RMB, 2 = MMB, 3 = M4, 4 = M5")]
    [SerializeField] private int dashKey = 2;
    [Tooltip("0 = LMB, 1 = RMB, 2 = MMB, 3 = M4, 4 = M5")]
    [SerializeField] private int rocketKey = 0;

    private void Start()
    {
        //Initialize Character Controller Component
        _cc = GetComponent<CharacterController>();
    }

    private void Update()
    {   
        //Check for WASD, ScrollWheel, Jump, and Rocket inputs
        float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");
        vertical = Input.GetAxisRaw("Vertical");
        horizontal = Input.GetAxisRaw("Horizontal");
        if (Input.GetKey(jumpKey) || scrollWheelInput != 0)
        {
            jumpQueued = true;
        }
        else
        {
            jumpQueued = false;
        }
        if (Input.GetMouseButtonDown(rocketKey) && Time.time-shootCallTime >= shootCooldown)
        {
            RocketShoot();
            shootCallTime = Time.time;
        }
        if (Input.GetMouseButtonDown(dashKey) && Time.time-dashCallTime >= dashCooldown)
        {
            Dash();
            dashCallTime = Time.time;
        }

        //Grounded Movement
        if (_cc.isGrounded || (OnRamp() && Mathf.Abs(_cc.velocity.y) < 8f))
        {
            GroundedMovement();
        }
        
        //Aerial Movement
        else if (!_cc.isGrounded || Mathf.Abs(_cc.velocity.y) > 8f)
        {
            AerialMovement();
        }
        
        //Apply the Movement
        _cc.Move(playerVel*Time.deltaTime);
    }


    private void OnControllerColliderHit(ControllerColliderHit collisionInfo)
    {
        //layer 3 is the ground layer so I apply a fix to wall collision aslong as said collision isn't with a wall
        if (collisionInfo.gameObject.layer != 3)
        {
            CollisionMovement(collisionInfo.normal, _cc.velocity);
        }
    }

    private void GroundedMovement()
    {
        //If player is bhopping don't apply friction when they touch the ground
        if (!jumpQueued)
        {
            AddFriction(1.0f);
        }
        else
        {
            AddFriction(0f);
        }

        //Calculate Wished Direction and Wished Speed and apply
        Vector3 wishdir = new Vector3(horizontal, 0, vertical);
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();
        float wishspeed = wishdir.magnitude;
        wishspeed *= groundMaxSpeed;
        Accelerate(wishdir, wishspeed, groundAccel);

        //Gravity whil grounded
        playerVel.y = -gravity * Time.deltaTime;

        //Handle Jumps
        if (jumpQueued)
        {
            playerVel.y = jumpSpeed;
            jumpQueued = false;
        }
    }

    private void AerialMovement()
    {
        float acceleration;
        Vector3 wishdir = new Vector3(horizontal, 0, vertical);
        wishdir = transform.TransformDirection(wishdir);
        float wishSpeed = wishdir.magnitude*airMaxSpeed;
        wishdir.Normalize();
        //Check if the player velocity vector and wished direction vector are facing the same direction and either accelerate or decelerate
        if (Vector3.Dot(playerVel, wishdir) < 0)
        {
            acceleration = airDecel;
        }
        else
        {
            acceleration = airAccel;
        }

        //Check if player is strafing and if so accelerate
        if(vertical == 0 && horizontal != 0)
        {
            if (wishSpeed > strafeMaxSpeed)
                {
                    wishSpeed = strafeMaxSpeed;
                }
                acceleration = strafeAccel;
        }
        Accelerate(wishdir, wishSpeed, acceleration);

        //Gravity whilst in air
        playerVel.y -= gravity * Time.deltaTime;
    }

 
    private void AddFriction(float amount)
    {
        Vector3 vec = playerVel; 
        vec.y = 0;
        float speed = vec.magnitude;
        float drop = 0;
        if (_cc.isGrounded)
        {
            float control = speed < groundDecel ? groundDecel : speed;
            drop = control * friction * Time.deltaTime * amount;
        }
        float newSpeed = speed - drop;
        if (newSpeed < 0)
        {
            newSpeed = 0;
        }

        if (speed > 0)
        {
            newSpeed /= speed;
        }
        playerVel.x *= newSpeed;
        playerVel.z *= newSpeed;
    }

    private void Accelerate(Vector3 wishDir, float wishSpeed, float amount)
    {
        //Allows for the enormous speed gain when strafing by calculating the current speed using the dot product (it's how the source engine does it which I am taking inspiration from)
        float currentspeed = Vector3.Dot(playerVel, wishDir);
        float addspeed = wishSpeed - currentspeed;
        if (addspeed <= 0)
        {
            return;
        }
        float accelspeed = amount * Time.deltaTime * wishSpeed;
        if (accelspeed > addspeed)
        {
            accelspeed = addspeed;
        }
        playerVel.x += accelspeed * wishDir.x;
        playerVel.z += accelspeed * wishDir.z;
    }

    private void CollisionMovement(Vector3 wallNormal, Vector3 velocity)
    {
        float dotProduct = Vector3.Dot(wallNormal, velocity);
        Vector3 parallelVelocity = dotProduct*wallNormal;
        Vector3 perpendicularVelocity = velocity - parallelVelocity;
        playerVel = perpendicularVelocity;
        Debug.DrawLine(transform.position, transform.position+perpendicularVelocity);
    }

    private void Dash()
    {   
        //simple stuff
        float speed = _cc.velocity.magnitude;
        Vector3 dashdir;
        if (vertical != 0 && horizontal != 0)
        {
            dashdir = new Vector3(horizontal, 0, vertical);
            dashdir = transform.TransformDirection(dashdir);
        }
        else
        {
            dashdir = transform.forward;
            
        }
        
        dashdir.Normalize();
        playerVel.x = speed*dashdir.x;
        playerVel.z = speed*dashdir.z;
    }
    private void RocketShoot()
    {
        //also simple stuff
        RaycastHit hit;
        if (Physics.SphereCast(Camera.position, 0.05f, Camera.forward, out hit, 5))
        {
            float hitLen = hit.distance;
            if (hitLen < 5f)
            {
                Vector3 hitPos = hit.point;
                Vector3 hitDir = (transform.position-hitPos).normalized;
                Vector3 final = hitDir*launchForce*(-2f/9f*hitLen+(10f/9f));
                playerVel += final;
            }
        }
    }

    private bool OnRamp()
    {
        RaycastHit hit;
        float angle = 0;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 1.25f))
        {
            angle = Mathf.Abs(Vector3.Angle(hit.normal, Vector3.up));
        }
        if (angle > 1)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    
}
