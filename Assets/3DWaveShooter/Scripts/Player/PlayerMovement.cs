using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the player's movement and rotation from inputs.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Components")]
    public Rigidbody rig;                   //Player's rigidbody component.

    [Header("Movement Metrics")]
    public float movingSpeedThreshold = 0.1f; // Speed threshold to consider the player "moving"
    
    // Metrics for tracking movement.
    public float totalDistanceTraveled = 0f;
    public float totalTimeMoving = 0f;
    public float totalTimeIdle = 0f;
    
    // For path efficiency calculations.
    private Vector3 waveStartPosition;
    
    // To track movement frame-to-frame.
    private Vector3 lastPosition;

    [Header("Speed Modifiers")]
    public float speedMultiplier = 1f;


    void Awake ()
    {
        //Get missing components
        if(!rig) rig = GetComponent<Rigidbody>();
    }

     void Start ()
    {
        // Initialize tracking variables.
        lastPosition = transform.position;
        waveStartPosition = transform.position;  // Set at the beginning of a new round or wave.
    }

    void Update ()
    {
        //If the player is able to move, then move the player.
        if(Player.inst.canMove)
        {
            Move();
        }
    }

    void LateUpdate ()
    {
        //If the player is able to move, then make them look at the mouse.
        if(Player.inst.canMove)
        {
            Look();
        }
    }

    //Moves the player based on keyboard inputs.
    void Move ()
    {
        // Only update movement metrics when the wave is active.
        if (!GameManager.inst.waveInProgress)
            return;
        //Get the horizontal and vertical keyboard inputs.
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");

        //Use joystick direction if mobile controls is enabled.
        if(MobileControls.inst.enableMobileControls)
        {
            x = MobileControls.inst.movementJoystick.dir.x;
            y = MobileControls.inst.movementJoystick.dir.y;
        }

        //Get the forward and right direction of the camera.
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;

        //Since we don't need the Y axis, we can remove it.
        camForward.y = 0;
        camRight.y = 0;

        //Normalise the directions since a rotated camera will cause issues.
        camForward.Normalize();
        camRight.Normalize();

        //Create a direction for the player to move at, which is relative to the camera.
        Vector3 dir = (camForward * y) + (camRight * x);

        //Update player state.
       
        if(dir.magnitude > 0)
        {
            totalTimeMoving += Time.deltaTime;

            if(Player.inst.state != PlayerState.Moving)
            {
                Player.inst.state = PlayerState.Moving;
                Player.inst.anim.SetBool("Moving", true);
            }
        }
        else
        {
            totalTimeIdle += Time.deltaTime;

            if(Player.inst.state != PlayerState.Idle)
            {
                Player.inst.state = PlayerState.Idle;
                Player.inst.anim.SetBool("Moving", false);
            }
        }
        
        //Finally set that as the player's velocity, also including the player's move speed.
        rig.linearVelocity = dir * Player.inst.moveSpeed * speedMultiplier;
    
        // ===== Movement Metrics Tracking (MY CODE) =====

        // Current position and distance moved since last frame.
        Vector3 currentPosition = transform.position;
        float distanceThisFrame = Vector3.Distance(currentPosition, lastPosition);
        totalDistanceTraveled += distanceThisFrame;

        // Update last position.
        lastPosition = currentPosition;
    }


    //Rotate the player so they're facing the mouse cursor.
    void Look ()
    {
        //Are we playing on desktop?
        if(!MobileControls.inst.enableMobileControls)
        {
            //Create a plane and shoot a raycast at it to get the world position of our mouse cursor.
            Plane rayPlane = new Plane(Vector3.up, transform.position);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            float rayDist;
            Vector3 worldPos = Vector3.zero;

            if(rayPlane.Raycast(ray, out rayDist))
                worldPos = ray.GetPoint(rayDist);

            //Get the direction of it relative to the player.
            Vector3 dir = (worldPos - transform.position).normalized;

            //Convert that direction to an angle we can apply to the player.
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            //Set the angle to be the player's Y rotation.
            transform.rotation = Quaternion.Euler(transform.rotation.x, angle, transform.rotation.z);
        }
        //Are we playing on mobile?
        else
        {
            MobileJoystick joy = MobileControls.inst.movementJoystick;
            
            //Is the joystick not in the center?
            if(joy.dir.magnitude != 0)
            {
                //Get an angle from the joystick's direction.
                float angle = Mathf.Atan2(joy.dir.x, joy.dir.y) * Mathf.Rad2Deg;

                //Set the angle to be the player's Y rotation.
                transform.rotation = Quaternion.Euler(transform.rotation.x, angle, transform.rotation.z);
            }
        }
    }
    public float AverageSpeed ()
    {
        return totalTimeMoving > 0 ? totalDistanceTraveled / totalTimeMoving : 0f;
    }

    // Helper method to calculate path efficiency.
    public float PathEfficiency ()
    {
        float straightLineDistance = Vector3.Distance(waveStartPosition, transform.position);
        return totalDistanceTraveled > 0 ? straightLineDistance / totalDistanceTraveled : 0f;
    }
}
