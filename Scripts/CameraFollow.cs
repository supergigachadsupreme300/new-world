using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform Target;
    public Vector3 Offset = Vector3.zero;
    public float SmoothSpeed = 10f;

    private void LateUpdate()
    {
        if (Target == null)
            return;

        var desiredPosition = Target.position + Offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * SmoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, Target.rotation, Time.deltaTime * SmoothSpeed);
    }
}
