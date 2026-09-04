using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class WorldBuilder
{
    public bool HitRock(GameObject rockRoot, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (rockRoot == null || !_rocks.Contains(rockRoot))
            return false;

        if (_rockCrackStates.TryGetValue(rockRoot, out var state))
        {
            if (state.IsDestroyed) return false;
            state.HitCount++;
            UpdateRockCracks(rockRoot, state);

            if (state.HitCount >= 4)
            {
                state.IsDestroyed = true;
                foreach (var crack in state.Cracks)
                {
                    if (crack.Obj != null) Destroy(crack.Obj);
                }
                _rockCrackStates.Remove(rockRoot);
                SpawnRockDebris(rockRoot);
                Destroy(rockRoot);
                _rocks.Remove(rockRoot);
                return true;
            }
            return false;
        }

        state = new RockCrackState { RockRoot = rockRoot, HitCount = 1 };
        UpdateRockCracks(rockRoot, state);
        _rockCrackStates[rockRoot] = state;
        return false;
    }

    private void UpdateRockCracks(GameObject rockRoot, RockCrackState state)
    {
        var rock = rockRoot.transform.Find("Rock");
        if (rock == null) return;

        float w = rock.localScale.x;
        float h = rock.localScale.y;
        float d = rock.localScale.z;
        float minDim = Mathf.Min(w, h, d);
        Vector3 rockPos = rock.localPosition;

        if (state.Cracks.Count == 0)
        {
            // First hit â€” spawn cracks evenly across all 6 faces
            for (int f = 0; f < 6; f++)
            {
                for (int i = 0; i < 4; i++)
                {
                    var data = new RockCrackData();
                    data.Face = f;
                    GetFaceGeometry(data.Face, w, h, d, out Vector3 centerOff, out Vector3 tanU, out Vector3 tanV, out Vector3 _, out float extU, out float extV);
                    data.Length = Random.Range(minDim * 0.2f, minDim * 0.4f);
                    data.Thickness = Random.Range(0.025f, 0.045f);
                    data.Angle = Random.Range(0f, Mathf.PI);
                    float margin = Mathf.Min(0.02f, Mathf.Min(extU, extV) * 0.1f);
                    float avail = Mathf.Min(extU, extV) * 0.5f - margin;
                    float halfLen = Mathf.Min(data.Length * 0.5f, Mathf.Max(0.005f, avail * 0.9f));
                    data.PosU = Random.Range(-extU * 0.5f + halfLen + margin, extU * 0.5f - halfLen - margin);
                    data.PosV = Random.Range(-extV * 0.5f + halfLen + margin, extV * 0.5f - halfLen - margin);
                    data.Obj = BuildCrackPrimitive(rockRoot);
                    ApplyCrackTransform(data.Obj, data, w, h, d, rockPos);
                    state.Cracks.Add(data);
                }
            }
        }
        else
        {
            // Subsequent hits â€” grow existing cracks
            float growthLen = minDim * 0.25f;
            float growthThick = 0.008f;

            for (int i = state.Cracks.Count - 1; i >= 0; i--)
            {
                var data = state.Cracks[i];
                if (data.Obj == null) { state.Cracks.RemoveAt(i); continue; }

                float newLen = data.Length + growthLen;
                float newThick = data.Thickness + growthThick;
                float halfLen = newLen * 0.5f;
                float cosA = Mathf.Cos(data.Angle);
                float sinA = Mathf.Sin(data.Angle);

                GetFaceGeometry(data.Face, w, h, d, out Vector3 centerOff, out Vector3 tanU, out Vector3 tanV, out Vector3 _, out float extU, out float extV);
                float halfU = extU * 0.5f;
                float halfV = extV * 0.5f;

                float t1 = float.MaxValue, t2 = float.MaxValue;
                float e1u = data.PosU + cosA * halfLen;
                float e1v = data.PosV + sinA * halfLen;
                if (Mathf.Abs(e1u) > halfU || Mathf.Abs(e1v) > halfV)
                    t1 = RayRectIntersection(data.PosU, data.PosV, cosA, sinA, halfU, halfV);
                float e2u = data.PosU - cosA * halfLen;
                float e2v = data.PosV - sinA * halfLen;
                if (Mathf.Abs(e2u) > halfU || Mathf.Abs(e2v) > halfV)
                    t2 = RayRectIntersection(data.PosU, data.PosV, -cosA, -sinA, halfU, halfV);

                float cappedHalf = halfLen;
                if (t1 < float.MaxValue && t1 < cappedHalf) cappedHalf = t1;
                if (t2 < float.MaxValue && t2 < cappedHalf) cappedHalf = t2;

                float excessTotal = (halfLen - cappedHalf) * 2f;

                data.Length = Mathf.Max(0.01f, cappedHalf * 2f);
                data.Thickness = newThick;
                ApplyCrackTransform(data.Obj, data, w, h, d, rockPos);

                // Branching â€” existing cracks spawn side branches
                float branchChance = 0.3f;
                if (data.Length > minDim * 0.12f && Random.value < branchChance)
                {
                    int branchCount = Random.Range(1, 3);
                    for (int b = 0; b < branchCount; b++)
                    {
                        float branchT = Random.Range(-data.Length * 0.35f, data.Length * 0.35f);
                        float branchAngle = data.Angle + Random.Range(0.4f, 1.3f) * (Random.value > 0.5f ? 1f : -1f);
                        float branchLen = data.Length * Random.Range(0.25f, 0.55f);
                        float branchThick = data.Thickness * Random.Range(0.4f, 0.7f);

                        float bU = data.PosU + cosA * branchT;
                        float bV = data.PosV + sinA * branchT;

                        // Ensure branch center is within face bounds, otherwise skip
                        if (Mathf.Abs(bU) > halfU * 0.9f || Mathf.Abs(bV) > halfV * 0.9f)
                            continue;

                        var branchData = new RockCrackData
                        {
                            Face = data.Face,
                            PosU = bU,
                            PosV = bV,
                            Angle = branchAngle,
                            Length = Mathf.Max(0.04f, branchLen),
                            Thickness = branchThick,
                            Obj = BuildCrackPrimitive(rockRoot)
                        };
                        ApplyCrackTransform(branchData.Obj, branchData, w, h, d, rockPos);
                        state.Cracks.Add(branchData);
                    }
                }

                if (excessTotal > 0.02f)
                {
                    int extraCount = Mathf.Max(1, Mathf.RoundToInt(excessTotal / 0.15f));
                    for (int e = 0; e < extraCount; e++)
                    {
                        var newData = new RockCrackData();
                        newData.Face = Random.Range(0, 6);
                        GetFaceGeometry(newData.Face, w, h, d, out Vector3 nc, out Vector3 ntU, out Vector3 ntV, out Vector3 _, out float nExtU, out float nExtV);
                        float lenPortion = excessTotal / extraCount * Random.Range(0.7f, 1.3f);
                        newData.Length = Mathf.Max(0.04f, lenPortion);
                        newData.Thickness = Random.Range(0.015f, 0.03f);
                        newData.Angle = Random.Range(0f, Mathf.PI);
                        float hLen = newData.Length * 0.5f;
                        float mg = Mathf.Min(0.02f, Mathf.Min(nExtU, nExtV) * 0.1f);
                        newData.PosU = Random.Range(-nExtU * 0.5f + hLen + mg, nExtU * 0.5f - hLen - mg);
                        newData.PosV = Random.Range(-nExtV * 0.5f + hLen + mg, nExtV * 0.5f - hLen - mg);
                        newData.Obj = BuildCrackPrimitive(rockRoot);
                        ApplyCrackTransform(newData.Obj, newData, w, h, d, rockPos);
                        state.Cracks.Add(newData);
                    }
                }
            }
        }
    }

    private void SpawnRockDebris(GameObject rockRoot)
    {
        Vector3 origin = rockRoot.transform.position;
        var rock = rockRoot.transform.Find("Rock");
        float totalVolume = 0.027f;
        if (rock != null)
        {
            Vector3 s = rock.localScale;
            totalVolume = s.x * s.y * s.z;
        }

        int count = Random.Range(4, 7);
        float[] fractions = new float[count];
        float sum = 0;
        for (int i = 0; i < count; i++)
        {
            fractions[i] = Random.Range(0.5f, 1.5f);
            sum += fractions[i];
        }
        float efficiency = 0.85f;
        const float minVolume = 0.008f;

        for (int i = 0; i < count; i++)
        {
            float volume = fractions[i] / sum * totalVolume * efficiency;
            if (volume < minVolume) continue;

            float s = Mathf.Pow(volume, 1f / 3f);
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "RockDebris";
            piece.transform.position = origin + Random.insideUnitSphere * s * 0.5f;
            piece.transform.localScale = Vector3.one * s;
            var r = piece.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.Lerp(Color.gray, Color.black, Random.value * 0.5f);
            var rb = piece.AddComponent<Rigidbody>();
            rb.mass = volume * 1000f;
            Vector3 vel = new Vector3(Random.Range(-2f, 2f), Random.Range(4f, 8f), Random.Range(-2f, 2f));
            rb.linearVelocity = vel;
            rb.angularVelocity = Random.insideUnitSphere * 5f;
        }
    }

    public void SmashDebris(GameObject piece)
    {
        if (piece == null) return;
        var s = piece.transform.localScale;
        float volume = s.x * s.y * s.z;
        const float minVolume = 0.008f;

        int splitCount = Random.Range(2, 4);
        float[] fractions = new float[splitCount];
        float sum = 0;
        for (int i = 0; i < splitCount; i++)
        {
            fractions[i] = Random.Range(0.3f, 0.7f);
            sum += fractions[i];
        }
        float efficiency = 0.7f;

        Vector3 pos = piece.transform.position;
        Destroy(piece);

        for (int i = 0; i < splitCount; i++)
        {
            float vol = fractions[i] / sum * volume * efficiency;
            if (vol < minVolume) continue;

            float side = Mathf.Pow(vol, 1f / 3f);
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "RockDebris";
            p.transform.position = pos + Random.insideUnitSphere * side * 0.3f;
            p.transform.localScale = Vector3.one * side;
            var r = p.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.Lerp(Color.gray, Color.black, Random.value * 0.5f);
            var rb = p.AddComponent<Rigidbody>();
            rb.mass = vol * 1000f;
            rb.linearVelocity = Random.insideUnitSphere * 3f + Vector3.up * 2f;
            rb.angularVelocity = Random.insideUnitSphere * 5f;
        }
    }

    public void SplitWoodDebris(GameObject debris)
    {
        if (debris == null) return;
        string partName = debris.name == "TreeFelled" ? "Trunk" : "BranchTopPart";
        var part = debris.transform.Find(partName);
        if (part == null) return;

        var s = part.localScale;
        float halfH = s.y * 0.5f;
        const float minH = 0.1f;

        if (halfH < minH)
        {
            Destroy(debris);
            return;
        }

        Vector3 worldPos = part.position;
        Vector3 up = part.up;
        var origMat = part.GetComponent<Renderer>()?.material;

        part.localScale = new Vector3(s.x, halfH, s.z);
        part.position = worldPos - up * halfH * 0.5f;

        var split = new GameObject(debris.name);
        split.transform.position = worldPos + up * halfH * 0.5f;
        split.transform.rotation = debris.transform.rotation;
        var rb = split.AddComponent<Rigidbody>();
        rb.mass = s.x * halfH * s.z * 500f;

        var splitPart = GameObject.CreatePrimitive(PrimitiveType.Cube);
        splitPart.name = partName;
        splitPart.transform.SetParent(split.transform);
        splitPart.transform.localScale = new Vector3(s.x, halfH, s.z);
        splitPart.transform.localPosition = Vector3.zero;
        splitPart.transform.localRotation = part.localRotation;
        var r = splitPart.GetComponent<Renderer>();
        if (r != null)
            r.material = origMat != null ? new Material(origMat) : CreateFallbackWoodMaterial();
    }

    private static Material CreateSafeLitMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        return new Material(shader);
    }

    private static Material CreateFallbackWoodMaterial()
    {
        var mat = CreateSafeLitMaterial();
        mat.color = new Color(0.36f, 0.23f, 0.12f);
        return mat;
    }

    private void GetFaceGeometry(int face, float w, float h, float d,
        out Vector3 centerOffset, out Vector3 tanU, out Vector3 tanV, out Vector3 normal, out float extU, out float extV)
    {
        switch (face)
        {
            case 0: centerOffset = new Vector3(0, 0, d * 0.5f); tanU = Vector3.right; tanV = Vector3.up; normal = Vector3.forward; extU = w; extV = h; break;
            case 1: centerOffset = new Vector3(0, 0, -d * 0.5f); tanU = Vector3.right; tanV = Vector3.up; normal = Vector3.back; extU = w; extV = h; break;
            case 2: centerOffset = new Vector3(w * 0.5f, 0, 0); tanU = Vector3.up; tanV = Vector3.forward; normal = Vector3.right; extU = h; extV = d; break;
            case 3: centerOffset = new Vector3(-w * 0.5f, 0, 0); tanU = Vector3.up; tanV = Vector3.forward; normal = Vector3.left; extU = h; extV = d; break;
            case 4: centerOffset = new Vector3(0, h * 0.5f, 0); tanU = Vector3.right; tanV = Vector3.forward; normal = Vector3.up; extU = w; extV = d; break;
            default: centerOffset = new Vector3(0, -h * 0.5f, 0); tanU = Vector3.right; tanV = Vector3.forward; normal = Vector3.down; extU = w; extV = d; break;
        }
    }

    private float RayRectIntersection(float originU, float originV, float dirU, float dirV, float halfU, float halfV)
    {
        float t = float.MaxValue;
        if (dirU > 0.001f) { float tu = (halfU - originU) / dirU; if (tu > 0 && tu < t) t = tu; }
        else if (dirU < -0.001f) { float tu = (-halfU - originU) / dirU; if (tu > 0 && tu < t) t = tu; }
        if (dirV > 0.001f) { float tv = (halfV - originV) / dirV; if (tv > 0 && tv < t) t = tv; }
        else if (dirV < -0.001f) { float tv = (-halfV - originV) / dirV; if (tv > 0 && tv < t) t = tv; }
        return t;
    }

    private GameObject BuildCrackPrimitive(GameObject parent)
    {
        var crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crack.name = "Crack";
        Destroy(crack.GetComponent<Collider>());
        crack.transform.SetParent(parent.transform);
        var r = crack.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.black;
        return crack;
    }

    private void ApplyCrackTransform(GameObject crack, RockCrackData data, float w, float h, float d, Vector3 rockPos)
    {
        GetFaceGeometry(data.Face, w, h, d, out Vector3 centerOff, out Vector3 tanU, out Vector3 tanV, out Vector3 normal, out float extU, out float extV);
        Vector3 pos3D = centerOff + data.PosU * tanU + data.PosV * tanV;
        crack.transform.localPosition = pos3D + rockPos;
        Vector3 longDir = (Mathf.Cos(data.Angle) * tanU + Mathf.Sin(data.Angle) * tanV).normalized;
        Vector3 zAxis = Vector3.Cross(longDir, normal).normalized;
        crack.transform.localRotation = Quaternion.LookRotation(zAxis, normal);
        crack.transform.localScale = new Vector3(data.Length, data.Thickness, data.Thickness);
    }

    public void AddCracksToBuildingPart(GameObject partEntity)
    {
        if (partEntity == null || _worldRoot == null) return;

        List<RockCrackData> existing;
        if (!_buildingPartCracks.TryGetValue(partEntity, out existing))
        {
            existing = new List<RockCrackData>();
            _buildingPartCracks[partEntity] = existing;
        }

        float w = partEntity.transform.localScale.x;
        float h = partEntity.transform.localScale.y;
        float d = partEntity.transform.localScale.z;
        float minDim = Mathf.Min(w, Mathf.Min(h, d));
        if (minDim < 0.01f) return;

        var partTransform = partEntity.transform;
        Quaternion partRot = partTransform.rotation;
        Vector3 partPos = partTransform.position;

        int cracksToAdd = existing.Count == 0 ? 6 : Mathf.Max(3, existing.Count / 2 + 2);
        for (int i = 0; i < cracksToAdd; i++)
        {
            var data = new RockCrackData();
            data.Face = Random.Range(0, 6);
            GetFaceGeometry(data.Face, w, h, d, out Vector3 centerOff, out Vector3 tanU, out Vector3 tanV, out Vector3 normal, out float extU, out float extV);
            data.Length = Random.Range(minDim * 0.2f, minDim * 0.4f);
            data.Thickness = Random.Range(0.025f, 0.045f);
            data.Angle = Random.Range(0f, Mathf.PI);
            float margin = Mathf.Min(0.02f, Mathf.Min(extU, extV) * 0.1f);
            float avail = Mathf.Min(extU, extV) * 0.5f - margin;
            float halfLen = Mathf.Min(data.Length * 0.5f, Mathf.Max(0.005f, avail * 0.9f));
            data.PosU = Random.Range(-extU * 0.5f + halfLen + margin, extU * 0.5f - halfLen - margin);
            data.PosV = Random.Range(-extV * 0.5f + halfLen + margin, extV * 0.5f - halfLen - margin);

            data.Obj = BuildCrackPrimitive(_worldRoot);

            Vector3 dimPos = centerOff + data.PosU * tanU + data.PosV * tanV;
            data.Obj.transform.position = partPos + partRot * dimPos;

            Vector3 longDir = (Mathf.Cos(data.Angle) * tanU + Mathf.Sin(data.Angle) * tanV).normalized;
            Vector3 worldNormal = (partRot * normal).normalized;
            Vector3 worldLong = (partRot * longDir).normalized;
            Vector3 worldZ = Vector3.Cross(worldLong, worldNormal).normalized;
            data.Obj.transform.rotation = Quaternion.LookRotation(worldZ, worldNormal);

            data.Obj.transform.localScale = new Vector3(data.Length, data.Thickness, data.Thickness);

            existing.Add(data);
        }
    }

    public void RemoveBuildingPartCracks(GameObject partEntity)
    {
        if (partEntity == null) return;
        if (_buildingPartCracks.TryGetValue(partEntity, out var cracks))
        {
            foreach (var c in cracks)
                if (c.Obj != null) Destroy(c.Obj);
            _buildingPartCracks.Remove(partEntity);
        }
    }

    public bool RemoveRock(GameObject rock)
    {
        if (rock == null)
            return false;
        if (_rocks.Contains(rock))
        {
            if (_rockCrackStates.TryGetValue(rock, out var state))
            {
                foreach (var crack in state.Cracks)
                {
                    if (crack.Obj != null) Destroy(crack.Obj);
                }
                _rockCrackStates.Remove(rock);
            }
            SpawnRockDebris(rock);
            Destroy(rock);
            _rocks.Remove(rock);
            return true;
        }
        return false;
    }

    public int CurrentBuildingIndex { get => _currentBuildingIndex; set { _currentBuildingIndex = value; UpdateBuildingPreview(); } }
}

