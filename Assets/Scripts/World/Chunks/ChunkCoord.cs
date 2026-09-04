using System;

/// <summary>
/// Immutable 2D chunk coordinate pair. Identifies a chunk in the infinite
/// XZ plane. Deterministic equality/hashing so chunks can be keyed robustly
/// in dictionaries and hash sets.
/// </summary>
[Serializable]
public struct ChunkCoord : IEquatable<ChunkCoord>
{
    public int X;
    public int Z;

    public ChunkCoord(int x, int z)
    {
        X = x;
        Z = z;
    }

    public bool Equals(ChunkCoord other)
    {
        return X == other.X && Z == other.Z;
    }

    public override bool Equals(object obj)
    {
        return obj is ChunkCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            // Large prime mixing keeps hash distributed across both axes.
            int hash = 17;
            hash = hash * 31 + X;
            hash = hash * 31 + Z;
            return hash;
        }
    }

    public static bool operator ==(ChunkCoord a, ChunkCoord b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(ChunkCoord a, ChunkCoord b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return $"({X}, {Z})";
    }

    public ChunkCoord North => new ChunkCoord(X, Z + 1);
    public ChunkCoord South => new ChunkCoord(X, Z - 1);
    public ChunkCoord East => new ChunkCoord(X + 1, Z);
    public ChunkCoord West => new ChunkCoord(X - 1, Z);
}
