using UnityEngine;

public class WaterVolume : MonoBehaviour
{
    public float DamagePerSecond = 5f;
    public float WaterSpeedMultiplier = 0.6f;
    public bool AllowJumping;

    private float _damageTimer;

    private void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
            pc.SetInWater(true, WaterSpeedMultiplier, AllowJumping);
    }

    private void OnTriggerStay(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null || pc.HP <= 0) return;

        _damageTimer -= Time.deltaTime;
        if (_damageTimer <= 0f)
        {
            pc.TakeDamage(Mathf.RoundToInt(DamagePerSecond));
            _damageTimer = 1f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
            pc.SetInWater(false, 1f, true);
    }
}
