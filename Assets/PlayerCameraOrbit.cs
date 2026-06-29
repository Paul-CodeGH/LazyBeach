using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerCameraOrbit : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private float distance = 5f;
    [SerializeField] private float targetHeight = 1.8f;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 70f;

    private InputAction lookAction;
    private float yaw;
    private float pitch;

    private void Awake()
    {
        yaw = transform.eulerAngles.y;
        pitch = Mathf.Clamp(NormalizeAngle(transform.eulerAngles.x), minPitch, maxPitch);

        Vector3 offset = transform.position - target.position;
        Vector3 flatOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
        Vector3 orbitOffset = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
        Vector3 flatOrbitOffset = Vector3.ProjectOnPlane(orbitOffset, Vector3.up);

        if (flatOffset.sqrMagnitude > 0.0001f && flatOrbitOffset.sqrMagnitude > 0.0001f)
        {
            distance = flatOffset.magnitude / flatOrbitOffset.magnitude;
            targetHeight = offset.y - orbitOffset.y * distance;
        }
        else
        {
            targetHeight = offset.y;
        }
    }

    private void OnEnable()
    {
        lookAction = inputActions.FindActionMap(actionMapName, true).FindAction(lookActionName, true);
        lookAction.Enable();
    }

    private void OnDisable()
    {
        lookAction?.Disable();
    }

    private void LateUpdate()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();

        yaw += look.x * lookSensitivity;
        pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPosition = target.position + Vector3.up * targetHeight;

        transform.position = targetPosition + orbitRotation * new Vector3(0f, 0f, -distance);
        transform.rotation = Quaternion.LookRotation(targetPosition - transform.position, Vector3.up);
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
