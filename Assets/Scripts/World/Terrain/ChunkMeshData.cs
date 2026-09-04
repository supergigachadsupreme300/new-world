using UnityEngine;

/// <summary>
/// Pure C# mesh data transferable between threads. Contains only arrays —
/// no Unity API objects. The main thread converts this into a Unity Mesh
/// after dequeuing from the background generation queue.
/// </summary>
public struct ChunkMeshData
{
    public ChunkCoord Coord;
    public ChunkData Data;
    public Vector3[] Vertices;
    public int[] Triangles;
    public Vector2[] UV;
    public Vector3[] Normals;
    public Bounds Bounds;
}
