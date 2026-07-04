using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform Target;
    public Vector3 Offset = new Vector3(0f, 1.5f, -4f);
    public float SmoothSpeed = 10f;

    private void LateUpdate()
    {
        if (Target == null)
            return;

        var desiredPosition = Target.position + Target.TransformDirection(Offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * SmoothSpeed);

        var lookPoint = Target.position + Vector3.up * 1.5f;
        if (Vector3.Distance(transform.position, lookPoint) > 0.01f)
        {
            var desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * SmoothSpeed);
        }
    }
}
