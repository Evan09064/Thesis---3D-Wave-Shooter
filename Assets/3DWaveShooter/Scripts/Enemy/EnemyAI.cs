using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public class EnemyAI : MonoBehaviour
{
    [Header("Target")]
    public GameObject target;                           //Target object to move towards and attack.
    public TargetType targetType;                       //Type of target (player, enemy, etc).

    [Header("Distances")]
    public float attackDistance;                        //Distance from the target at which the enemy will attack them.

    [Header("Navigation")]
    public List<Vector3> path = new List<Vector3>();    //Navigation path to move along.
    private float pathUpdateRate = 0.5f;                //How often will the navigation path be updated?
    private float lastPathUpdateTime;                   //Last time the path was updated.

    [Header("Components")]
    public Enemy enemy;                                 //Enemy's enemy component.
    public Rigidbody rig;                               //Enemy's rigidbody component.
    public NavMeshAgent agent;                          //Enemy's NavMeshAgent component.

    //Private values
    private float lastAttackTime;                       //Last time the enemy attacked.

    void Start ()
    {
        //Get missing components.
        if(!enemy) enemy = GetComponent<Enemy>();
        if(!rig) rig = GetComponent<Rigidbody>();
        if(!agent) GetComponent<NavMeshAgent>();
    }

    void Update ()
    {
        //Return if we don't have a target or we're dead.
        if(!target || enemy.state == EnemyState.Dead)
            return;

        //Check distance to target to change state.
        DistanceCheck();

        //Can we move? Then move.
        if(enemy.canMove)
        {
            if(enemy.state == EnemyState.Chasing)
            {
                Move();

                //Generate a path towards the target every 'pathUpdateRate' seconds.
                if(Time.time - lastPathUpdateTime > pathUpdateRate)
                {
                    lastPathUpdateTime = Time.time;
                    GenerateNewPath();
                }
            }
        }

        //Can we attack?
        if(enemy.canAttack)
        {
            //Are we attacking?
            if(enemy.state == EnemyState.Attacking)
            {
                //Do we have the correct time to attack?
                if(Time.time - lastAttackTime > enemy.attackRate)
                {
                    lastAttackTime = Time.time;
                    AttackTarget();
                }
            }
        }
    }

    //Checks distance to target.
    void DistanceCheck ()
    {
        //Calculate distance between enemy and target.
        float dist = Vector3.Distance(transform.position, target.transform.position);

        //Are we in the attack range? Then start attacking.
        if(dist <= attackDistance && enemy.state != EnemyState.Attacking)
        {
            enemy.state = EnemyState.Attacking;
            enemy.anim.SetBool("Moving", false);
        }
        //Otherwise keep chasing the target.
        else if(dist > attackDistance && enemy.state != EnemyState.Chasing)
        {
            enemy.state = EnemyState.Chasing;
            enemy.anim.SetBool("Moving", true);
        }
    }

    //Moves the enemy towards the target.
    void Move()
    {
        // Do we have a path we can follow?
        if (path.Count > 0)
        {
            // If we're right next to the path point, remove it.
            if (Vector3.Distance(transform.position, path[0]) < 1)
                path.RemoveAt(0);

            // No path yet? Return.
            if (path.Count == 0)
                return;

            // Determine the “chase target” position:
            Vector3 targetPos = path[0];
            if (path.Count == 1)
                targetPos = target.transform.position;

            // If spread-mode is OFF, use the existing simple chase:
            if (!GameManager.inst.spreadEnemiesEnabled)
            {
                // ——— Original chase code ———
                transform.position += transform.forward * enemy.moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.Euler(transform.rotation.x, GetRotationToPosition(targetPos), transform.rotation.z),
                    10 * Time.deltaTime
                );
            }
            // If spread-mode is ON, compute a separation vector + chase vector:
            else
            {
                // 1) Compute direction toward the chase target:
                Vector3 toTarget = (targetPos - transform.position).normalized;

                // 2) Compute a repulsion vector from any nearby enemies:
                Vector3 separation = Vector3.zero;
                // Find all colliders within 'separationRadius'
                Collider[] hits = Physics.OverlapSphere(transform.position, GameManager.inst.separationRadius);
                foreach (var col in hits)
                {
                    // We only care about other enemies—assume each enemy GameObject has tag "Enemy":
                    if (col.gameObject == this.gameObject) 
                        continue; // skip self
                    if (!col.CompareTag("Enemy")) 
                        continue; // skip anything that's not an enemy

                    // Direction away from that neighbor:
                    Vector3 diff = transform.position - col.transform.position;
                    float d = diff.magnitude;
                    if (d < Mathf.Epsilon) 
                        continue; // avoid divide-by-zero

                    // If they're too close (< desiredSeparation), push away:
                    if (d < GameManager.inst.desiredSeparation)
                    {
                        // A simple linear repulsion: (desiredSeparation - d)
                        Vector3 push = diff.normalized * (GameManager.inst.desiredSeparation - d);
                        separation += push;
                    }
                }

                // 3) Normalize and scale the separation vector:
                if (separation != Vector3.zero)
                {
                    separation = separation.normalized * GameManager.inst.antiSocialCoefficient;
                }

                // 4) Combine chase + separation, then move & rotate:
                Vector3 moveDir = (toTarget + separation).normalized;
                transform.position += moveDir * enemy.moveSpeed * Time.deltaTime;

                // Rotate to face moveDir:
                float desiredYAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.Euler(transform.rotation.x, desiredYAngle, transform.rotation.z),
                    10 * Time.deltaTime
                );
            }
        }
    }


    //Generates a new path from the enemy to the target by using NavMesh.
    void GenerateNewPath ()
    {
        NavMeshPath navPath = new NavMeshPath();

        agent.enabled = true;
        agent.CalculatePath(target.transform.position, navPath);
        agent.enabled = false;

        path = navPath.corners.ToList();
    }

    //Deals damage to the target.
    void AttackTarget ()
    {
        if(targetType == TargetType.Player)
            Player.inst.TakeDamage(enemy.attackDamage);
        else if(targetType == TargetType.Enemy)
            target.GetComponent<Enemy>().TakeDamage(enemy.attackDamage);

        enemy.anim.SetTrigger("Attack");
    }

    //Returns a direction between two points.
    public Vector3 GetDirection (Vector3 a, Vector3 b)
    {
        return (b - a).normalized;
    }

    //Returns an angle (y axis) from the enemy to a position.
    float GetRotationToPosition (Vector3 pos)
    {
        Vector3 dir = GetDirection(transform.position, pos);
        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        return angle;
    }
}

public enum TargetType
{
    Player,
    Enemy
}