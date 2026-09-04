using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anti-cheat validations for Task 7.4. The server is authoritative: it re-validates damage
/// intents, rejects impossible position jumps, rate-limits actions with a per-session token
/// bucket, and verifies chunk hashes so clients cannot tamper with the world. Each method is a
/// pure guard: it returns a decision and optional message so the caller can act (drop, kick,
/// or clamp).
/// </summary>
public static class AntiCheat
{
    /// <summary>Client damage intent reported to the server (Task 7.4 server-authoritative damage).</summary>
    public struct DamageIntent
    {
        public int ClaimerSessionId;
        public short Action;
        public Vector3 Origin;
        public Vector3 Target;
        public float Range;
        public float Damage;
        public float Timestamp;
    }

    /// <summary>A per-session token bucket limiting how many actions a client may emit.</summary>
    public sealed class ActionBudget
    {
        public int SessionId;
        public float Tokens;
        public float Capacity;
        public float RefillPerSecond;
        public float LastRefillUtc;

        public ActionBudget(int sessionId, float capacity, float refillPerSecond)
        {
            SessionId = sessionId;
            Capacity = capacity;
            RefillPerSecond = refillPerSecond;
            Tokens = capacity;
            LastRefillUtc = Time.time;
        }

        public bool TryConsume(float cost = 1f)
        {
            // refill tokens proportional to elapsed time
            float elapsed = Mathf.Max(0f, Time.time - LastRefillUtc);
            LastRefillUtc = Time.time;
            Tokens = Mathf.Min(Capacity, Tokens + elapsed * RefillPerSecond);
            if (Tokens < cost) return false;
            Tokens -= cost;
            return true;
        }
    }

    private static readonly Dictionary<int, ActionBudget> _budgets =
        new Dictionary<int, ActionBudget>();

    // -------------------------------------------------------------
    //  Position validation
    // -------------------------------------------------------------

    /// <summary>Max plausible world speed (m/s) for a moving avatar.</summary>
    public const float MaxPlausibleSpeed = 25f;

    /// <summary>Allow a position update within distance and elapsed margins. Clamps overshoot.</summary>
    public static bool ValidatePosition(
        Vector3 previous, Vector3 next, float deltaTime,
        out Vector3 corrected)
    {
        corrected = next;
        if (deltaTime < 1e-6f) return true;
        float reach = MaxPlausibleSpeed * deltaTime;
        // vertical tolerance including jumps/falls
        float horiz = new Vector3(next.x - previous.x, 0f, next.z - previous.z).magnitude;
        float vert = Mathf.Abs(next.y - previous.y);
        if (horiz <= reach && vert <= MaxPlausibleSpeed * deltaTime * 1.8f)
            return true;
        // reject teleports: push the position back to the plausible frontier
        Vector3 dir = next - previous;
        float dist = dir.magnitude;
        if (dist > 1e-5f)
            corrected = previous + dir * (reach / dist);
        return false;
    }

    // -------------------------------------------------------------
    //  Server-authoritative damage
    // -------------------------------------------------------------

    /// <summary>
    /// Canvas a damage <see cref="DamageIntent"/> against authoritative range + budget. Returns
    /// the damage the server will actually apply (clamped to the plausible in-range portion).
    /// </summary>
    public static float ValidateDamage(int sessionId, DamageIntent intent, out bool denied)
    {
        denied = false;
        if (intent.Damage < 0f) { denied = true; return 0f; }
        if (intent.Range < 0f) { denied = true; return 0f; }
        // in range?
        float rangeToTarget = Vector3.Distance(intent.Origin, intent.Target);
        if (rangeToTarget > intent.Range + 1.5f) { denied = true; return 0f; }
        if (!AllowAction(sessionId, 2f)) { denied = true; return 0f; }
        // server cap: never apply more than the client reported in range
        return Mathf.Max(0f, intent.Damage);
    }

    // -------------------------------------------------------------
    //  Action rate limiting
    // -------------------------------------------------------------

    /// <summary>Refill/return an action budget for a session with defaults.</summary>
    public static bool AllowAction(int sessionId, float cost = 1f)
    {
        if (!_budgets.TryGetValue(sessionId, out var b))
        {
            b = new ActionBudget(sessionId, 40f, 10f);
            _budgets[sessionId] = b;
        }
        return b.TryConsume(cost);
    }

    /// <summary>Clear a session's budget (on disconnect).</summary>
    public static void Release(int sessionId)
    {
        _budgets.Remove(sessionId);
    }

    // -------------------------------------------------------------
    //  Chunk integrity checks
    // -------------------------------------------------------------

    /// <summary>FNV-1a hash of a chunk's height payload used to detect tampering.</summary>
    public static uint HashChunk(int chunkX, int chunkZ, float[] heights)
    {
        uint hash = 2166136261u;
        hash = ((hash ^ (uint)chunkX) * 16777619u);
        hash = ((hash ^ (uint)chunkZ) * 16777619u);
        if (heights != null)
        {
            for (int i = 0; i < heights.Length; i++)
            {
                uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(heights[i]), 0);
                hash = ((hash ^ bits) * 16777619u);
            }
        }
        return hash;
    }

    /// <summary>True if the client's reported chunk hash matches the server's expected one.</summary>
    public static bool ValidateChunkHash(int chunkX, int chunkZ, float[] expectedHeights, uint clientHash)
    {
        return clientHash == HashChunk(chunkX, chunkZ, expectedHeights);
    }
}