using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    public enum WeaponType
    {
        Slash,
        Thrust,
        Ranged,
    }

    public WeaponType weaponType;
    public Vector2 grabOffset = Vector2.zero;
    public int damage = 10;
    public float attackCooldownSec = 1f;
    public int range = 0;
    public GameObject? equippedCharacter;
    public GameObject? projectilePrefab;
    public bool isAttacking = false;
    private float? lastAttackTimeSec;
    public float thrustDurationSec = 0.4f;
    public float slashDurationSec = 0.4f;
    public int slashMaxAngleDeg = 400;
    public int launchHeight = 20;
    public float launchDurationSec = 0.5f;
    System.Action? animateFunction;
    System.Action? onAttackDone;
    Collider2D? physicsCollider;

    void Awake()
    {
        UpdateSortingLayer();
    }

    void Start()
    {
        // One collider for physics, one for interacting with mouse
        // Wish Unity got something better, even just naming...
        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            if (!collider.isTrigger)
            {
                physicsCollider = collider;
                break;
            }
        }
        if (physicsCollider != null)
        {
            physicsCollider.enabled = false;
        }
    }

    void Update()
    {
        if (animateFunction is not null)
        {
            animateFunction();
        }
        if (!CanShowPickupIndicator())
        {
            GetComponent<SpriteRenderer>().color = Color.white;
        }
    }

    bool CanShowPickupIndicator()
    {
        if (equippedCharacter)
        {
            return false;
        }
        if (GlobalControl.player == null)
        {
            return false;
        }
        var weaponPickupRange = GlobalControl.player.GetComponent<Character>().weaponPickupRange;
        return (GlobalControl.player.transform.position - transform.position).magnitude < weaponPickupRange;
    }

    void OnMouseOver()
    {
        if (CanShowPickupIndicator())
        {
            GetComponent<SpriteRenderer>().color = Color.yellow;
        }
    }

    void OnMouseExit()
    {
        if (CanShowPickupIndicator())
        {
            GetComponent<SpriteRenderer>().color = Color.white;
        }
    }

    public void Attack()
    {
        if (!CanAttack())
        {
            return;
        }
        lastAttackTimeSec = Time.time;
        switch (weaponType)
        {
            case WeaponType.Slash:
                SlashAttack();
                break;
            case WeaponType.Thrust:
                ThrustAttack();
                break;
            case WeaponType.Ranged:
                RangedAttack();
                break;
        }
    }

    /// <summary>
    /// Not attacking nor in cooldown
    /// </summary>
    public bool CanAttack()
    {
        return !isAttacking && !InCooldown();
    }

    public bool InCooldown()
    {
        return lastAttackTimeSec != null && Time.time - lastAttackTimeSec < attackCooldownSec;
    }

    void BaseMeleeAttackDone()
    {
        isAttacking = false;
        animateFunction = null;
        physicsCollider!.enabled = false;
        var characterComponent = equippedCharacter!.GetComponent<Character>();
        characterComponent.shouldRotateWeapon = true;
    }

    void ThrustAttack()
    {
        var endTime = Time.time + thrustDurationSec;
        var curve = AnimationCurve.Linear(Time.time, 0, Time.time + thrustDurationSec / 2, range);
        curve.postWrapMode = WrapMode.PingPong;
        animateFunction = () =>
        {
            if (Time.time > endTime)
            {
                onAttackDone?.Invoke();
                return;
            }
            transform.localPosition = new Vector3(0, curve.Evaluate(Time.time), 0);
        };
        onAttackDone = DoneThrustAttack;
        isAttacking = true;
        physicsCollider!.enabled = true;
        var characterComponent = equippedCharacter!.GetComponent<Character>();
        characterComponent.shouldRotateWeapon = false;
    }

    void DoneThrustAttack()
    {
        BaseMeleeAttackDone();
    }

    void SlashAttack()
    {
        var characterComponent = equippedCharacter!.GetComponent<Character>();
        var startAngle = characterComponent.grabPointTransform.rotation.eulerAngles.z;
        var shouldFlipSlashRotation = Mathf.Abs(startAngle) > 180;
        var endAngleDeg = shouldFlipSlashRotation ? -slashMaxAngleDeg : slashMaxAngleDeg;
        var endTime = Time.time + slashDurationSec;
        var curve = AnimationCurve.Linear(Time.time, startAngle, endTime, startAngle + endAngleDeg);
        animateFunction = () =>
        {
            if (Time.time > endTime)
            {
                onAttackDone?.Invoke();
                return;
            }
            var characterComponent = equippedCharacter!.GetComponent<Character>();
            characterComponent.grabPointTransform.rotation = Quaternion.AngleAxis(curve.Evaluate(Time.time), Vector3.forward);
        };
        onAttackDone = DoneSlashAttack;
        isAttacking = true;
        physicsCollider!.enabled = true;
        characterComponent.shouldRotateWeapon = false;
    }

    void DoneSlashAttack()
    {
        BaseMeleeAttackDone();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider != equippedCharacter && collision.gameObject.CompareTag("Player"))
        {
            collision.gameObject.GetComponent<Character>().Hit(damage);
        }
    }

    void RangedAttack()
    {
        var firePoint = transform.Find("FirePoint");
        var projectile = Instantiate(projectilePrefab, firePoint.position, equippedCharacter!.transform.rotation)!;
        var projectileComponent = projectile.GetComponent<Projectile>();
        var characterComponent = equippedCharacter.GetComponent<Character>();
        projectileComponent.damage = damage;
        projectileComponent.fromCharacter = equippedCharacter;
        projectile.transform.rotation = characterComponent.grabPointTransform.rotation;
        projectile.GetComponent<Rigidbody2D>().AddForce(
            characterComponent.grabPointTransform.up * projectileComponent.speed
        );
        // projectile.GetComponent<Rigidbody2D>().AddForce(new Vector2(100, 0));
    }

    void UpdateSortingLayer()
    {
        if (equippedCharacter)
        {
            GetComponent<SpriteRenderer>().sortingLayerName = "Weapon";
        }
        else
        {
            GetComponent<SpriteRenderer>().sortingLayerName = "Ground Item";
            transform.position = new Vector3(transform.position.x, transform.position.y, -1);
        }
    }

    void Launch()
    {
        var endTime = Time.time + launchDurationSec;
        var initialY = transform.position.y;
        var heightY = initialY + launchHeight;
        // Want a ease out up and ease in down
        var curve = AnimationCurve.EaseInOut(Time.time, initialY, Time.time + launchDurationSec / 2, heightY);
        curve.postWrapMode = WrapMode.PingPong;
        // Can't edit this directly, remove and re-add needed
        var keyframe = curve.keys[0];
        var tangent = launchHeight / (launchDurationSec / 2);
        keyframe.outTangent = tangent;
        keyframe.inTangent = tangent;
        curve.MoveKey(0, keyframe);
        animateFunction = () =>
        {
            if (Time.time > endTime)
            {
                DoneLaunch();
                return;
            }
            transform.position = new Vector3(transform.position.x, curve.Evaluate(Time.time), transform.position.z);
        };
    }

    void DoneLaunch()
    {
        animateFunction = null;
    }

    public void PickupBy(GameObject? character)
    {
        if (character == equippedCharacter)
        {
            return;
        }
        if (character == null)
        {
            onAttackDone?.Invoke();
            transform.SetParent(null);
            // transform.position = character.transform.position;
            equippedCharacter = null;
            Launch();
        }
        else
        {
            DoneLaunch();
            transform.SetParent(character.GetComponent<Character>().grabPointTransform);
            transform.SetLocalPositionAndRotation(grabOffset, Quaternion.identity);
            GetComponent<SpriteRenderer>().color = Color.white;
            equippedCharacter = character;
        }
        UpdateSortingLayer();
    }
}
