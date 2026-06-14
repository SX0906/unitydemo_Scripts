using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraController : MonoBehaviour
{
    [Header("相机跟踪目标，拖 LookRoot")]
    public Transform cameraTarget;

    [Header("角度限制")]
    public float topClamp = 70f;
    public float bottomClamp = -30f;

    [Header("灵敏度")]
    public float lookSensitivity = 1f;

    [Header("锁定")]
    public bool isLockOn;
    public Transform lockOnTarget;
    public float lockRotateSpeed = 10f;

    private Vector2 lookInput;
    private float yaw;
    private float pitch;

    private const float threshold = 0.01f;

    private void Start()
    {
        if (!cameraTarget)
        {
            Debug.LogError("PlayerCameraController：cameraTarget 没有设置。", this);
            enabled = false;
            return;
        }

        Vector3 euler = cameraTarget.rotation.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
    }

    private void LateUpdate()
    {
        if (!cameraTarget)
            return;

        if (isLockOn && lockOnTarget)
            UpdateLockOnLook();
        else
            UpdateFreeLook();

        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateFreeLook()
    {
        if (lookInput.sqrMagnitude >= threshold)
        {
            yaw += lookInput.x * lookSensitivity;
            pitch -= lookInput.y * lookSensitivity;
        }

        yaw = ClampAngle(yaw, float.MinValue, float.MaxValue);
        pitch = ClampAngle(pitch, bottomClamp, topClamp);
    }

    private void UpdateLockOnLook()
    {
        Vector3 dir = lockOnTarget.position - cameraTarget.position;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(dir.normalized);
        Vector3 targetEuler = targetRotation.eulerAngles;

        float targetYaw = targetEuler.y;
        float targetPitch = targetEuler.x;

        if (targetPitch > 180f)
            targetPitch -= 360f;

        targetPitch = Mathf.Clamp(targetPitch, bottomClamp, topClamp);

        yaw = Mathf.LerpAngle(yaw, targetYaw, lockRotateSpeed * Time.deltaTime);
        pitch = Mathf.Lerp(pitch, targetPitch, lockRotateSpeed * Time.deltaTime);
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }


    private static float ClampAngle(float angle, float min, float max)
    {
        while (angle < -360f)
            angle += 360f;

        while (angle > 360f)
            angle -= 360f;

        return Mathf.Clamp(angle, min, max);
    }
}

