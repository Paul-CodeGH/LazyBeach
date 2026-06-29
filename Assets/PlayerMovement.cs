using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public sealed class PlayerMovement : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float jumpVelocity = 3f;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private float groundCheckInset = 0.12f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private Rigidbody body;
    private Collider bodyCollider;
    private PhysicsMaterial frictionlessMaterial;
    private InputAction moveAction;
    private InputAction jumpAction;
    private bool jumpQueued;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        ApplyFrictionlessColliderMaterial();
    }

    private void OnEnable()
    {
        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, true);
        moveAction = actionMap.FindAction(moveActionName, true);
        jumpAction = actionMap.FindAction(jumpActionName, true);

        jumpAction.performed += OnJumpPerformed;
        moveAction.Enable();
        jumpAction.Enable();
    }

    private void OnDisable()
    {
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
        }

        moveAction?.Disable();
        jumpAction?.Disable();
        jumpQueued = false;
    }

    private void OnDestroy()
    {
        if (frictionlessMaterial != null)
        {
            Destroy(frictionlessMaterial);
        }
    }

    private void FixedUpdate()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        HandleJump();

        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = forward * input.y + right * input.x;

        if (direction.sqrMagnitude < 0.0001f)
        {
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
            body.angularVelocity = Vector3.zero;
            return;
        }

        direction.Normalize();

        body.linearVelocity = new Vector3(direction.x * moveSpeed, body.linearVelocity.y, direction.z * moveSpeed);
        body.angularVelocity = Vector3.zero;
        body.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        jumpQueued = true;
    }

    private void HandleJump()
    {
        if (!jumpQueued)
        {
            return;
        }

        jumpQueued = false;

        if (!IsGrounded())
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.y = jumpVelocity;
        body.linearVelocity = velocity;
    }

    private bool IsGrounded()
    {
        Bounds bounds = bodyCollider.bounds;
        float insetX = Mathf.Min(groundCheckInset, bounds.extents.x - 0.01f);
        float insetZ = Mathf.Min(groundCheckInset, bounds.extents.z - 0.01f);
        float x = Mathf.Max(0f, bounds.extents.x - insetX);
        float z = Mathf.Max(0f, bounds.extents.z - insetZ);
        float rayDistance = groundCheckDistance + 0.05f;
        float rayStartY = bounds.min.y + 0.05f;

        Vector3 center = new Vector3(bounds.center.x, rayStartY, bounds.center.z);

        return IsGroundBelow(center, rayDistance)
            || IsGroundBelow(center + transform.right * x + transform.forward * z, rayDistance)
            || IsGroundBelow(center + transform.right * x - transform.forward * z, rayDistance)
            || IsGroundBelow(center - transform.right * x + transform.forward * z, rayDistance)
            || IsGroundBelow(center - transform.right * x - transform.forward * z, rayDistance);
    }

    private bool IsGroundBelow(Vector3 origin, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance, groundLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];

            if (hit.collider != bodyCollider && !hit.transform.IsChildOf(transform) && hit.normal.y > 0.65f)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyFrictionlessColliderMaterial()
    {
        frictionlessMaterial = new PhysicsMaterial("Player Frictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        bodyCollider.sharedMaterial = frictionlessMaterial;
    }
}
