using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerMovement : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody body;
    private InputAction moveAction;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        moveAction = inputActions.FindActionMap(actionMapName, true).FindAction(moveActionName, true);
        moveAction.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    private void FixedUpdate()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
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
            return;
        }

        direction = Vector3.ClampMagnitude(direction, 1f);

        body.MovePosition(body.position + direction * moveSpeed * Time.fixedDeltaTime);
        body.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
    }
}
