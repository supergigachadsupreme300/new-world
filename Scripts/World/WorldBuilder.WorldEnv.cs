using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class WorldBuilder
{
    private void CreateSkyAndLight()
    {
        var sky = Object.FindAnyObjectByType<Light>();
        if (sky == null)
        {
            var sunObject = new GameObject("SunLight");
            SunLight = sunObject.AddComponent<Light>();
            SunLight.type = LightType.Directional;
            SunLight.color = new Color(1f, 0.98f, 0.92f);
            SunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sunObject.transform.SetParent(_worldRoot.transform);
        }
        else
        {
            SunLight = sky;
        }
    }

    private void CreateGround()
    {
        GroundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        GroundObject.name = "Ground";
        GroundObject.transform.SetParent(_worldRoot.transform);
        GroundObject.transform.localScale = new Vector3(GroundSize.x / 10f, 1f, GroundSize.z / 10f);
        GroundObject.transform.position = Vector3.zero;

        var renderer = GroundObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (GroundMaterial != null)
            {
                renderer.material = GroundMaterial;
            }
            else
            {
                if (GroundTexture != null)
                {
                    renderer.material = new Material(Shader.Find("Standard"));
                    renderer.material.mainTexture = GroundTexture;
                }
                else
                {
                    var urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    var groundMat = new Material(urpShader != null ? urpShader : Shader.Find("Standard"));
                    var texture = Resources.Load<Texture2D>("texture/grass_blade");
                    if (texture != null)
                    {
                        groundMat.mainTexture = texture;
                        groundMat.mainTextureScale = new Vector2(GroundSize.x / 5f, GroundSize.z / 5f);
                    }
                    else
                    {
                        groundMat.color = ColorPalette.GrassGreen;
                    }
                    renderer.material = groundMat;
                }
            }
        }
    }

    private GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false, bool addCollider = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        var rend = go.GetComponent<Renderer>();
        MapBuilder.ApplyBlockColor(rend, color);
        if (removeCollider) Destroy(go.GetComponent<Collider>());
        if (addCollider && go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
        return go;
    }

    private void BuildRoad()
    {
        float roadCx = 14f;
        float roadHw = 3.8f;
        float roadTurnZ = 90f;
        float roadEndX = 280f;
        float nsZStart = -300f;
        float nsZEnd = 300f; // north border â€” N-S road runs the full map height
        float nsLen = nsZEnd - nsZStart;
        float nsZc = (nsZStart + nsZEnd) * 0.5f;
        float ewLen = roadEndX - roadCx;
        float ewXc = (roadEndX + roadCx) * 0.5f;

        Color curbC = new Color(0.46f, 0.45f, 0.42f);
        Color whiteC = Color.white;
        Color yellowC = new Color(0.92f, 0.80f, 0.18f);
        Color asphaltC = new Color(0.235f, 0.243f, 0.275f);

        // North-south leg (runs z: nsZStart -> nsZEnd)
        RoadObject = MakeBlock("Road", _worldRoot.transform,
            new Vector3(roadHw * 2f, 0.06f, nsLen),
            new Vector3(roadCx, 0.03f, nsZc), asphaltC, false, true);

        // Kerbs â€” split around all 3 junctions (south z=-50, mid z=90, north z=180)
        float jSouthZ = -50f;
        float jMidZ = roadTurnZ; // 90
        float jNorthZ = 180f;

        // West kerb: 4 segments avoiding all 3 junctions
        float wKerbX = roadCx - (roadHw + 0.35f);
        float wKerbW = 0.55f;
        float wKerbH = 0.22f;
        MakeBlock("Kerb", _worldRoot.transform, new Vector3(wKerbW, wKerbH, (jSouthZ - roadHw) - nsZStart),
            new Vector3(wKerbX, 0.11f, (nsZStart + (jSouthZ - roadHw)) * 0.5f), curbC, true);
        MakeBlock("Kerb", _worldRoot.transform, new Vector3(wKerbW, wKerbH, (jMidZ - roadHw) - (jSouthZ + roadHw)),
            new Vector3(wKerbX, 0.11f, ((jSouthZ + roadHw) + (jMidZ - roadHw)) * 0.5f), curbC, true);
        MakeBlock("Kerb", _worldRoot.transform, new Vector3(wKerbW, wKerbH, (jNorthZ - roadHw) - (jMidZ + roadHw)),
            new Vector3(wKerbX, 0.11f, ((jMidZ + roadHw) + (jNorthZ - roadHw)) * 0.5f), curbC, true);
        MakeBlock("Kerb", _worldRoot.transform, new Vector3(wKerbW, wKerbH, nsZEnd - (jNorthZ + roadHw)),
            new Vector3(wKerbX, 0.11f, ((jNorthZ + roadHw) + nsZEnd) * 0.5f), curbC, true);

        // East kerb: 4 segments avoiding all 3 junctions
        float eKerbX = roadCx + (roadHw + 0.35f);
        MakeBlock("Kerb", _worldRoot.transform, new Vector3(wKerbW, wKerbH, (jSouthZ - roadHw) - nsZStart),
            new Vector3(eKerbX, 0.11f, (nsZStart + (jSouthZ - roadHw)) * 0.5f), curbC, true);
        MakeBlock("Kerb", _worldRoot.transform, new Vector3(wKerbW, wKerbH, (jMidZ - roadHw) - (jSouthZ + roadHw)),
            new Vector3(eKerbX, 0.11f, ((jSouthZ + roadHw) + (jMidZ - roadHw)) * 0.5f), curbC, true);
        MakeBlock("Kerb", _worldRoot.transform, new Vector3(wKerbW, wKerbH, (jNorthZ - roadHw) - (jMidZ + roadHw)),
            new Vector3(eKerbX, 0.11f, ((jMidZ + roadHw) + (jNorthZ - roadHw)) * 0.5f), curbC, true);
        MakeBlock("Kerb", _worldRoot.transform, new Vector3(wKerbW, wKerbH, nsZEnd - (jNorthZ + roadHw)),
            new Vector3(eKerbX, 0.11f, ((jNorthZ + roadHw) + nsZEnd) * 0.5f), curbC, true);

        // White edge lines â€” same 4-segment split on each side
        float nsMidZ = (nsZStart + nsZEnd) * 0.5f;
        MakeBlock("EdgeLine", _worldRoot.transform, new Vector3(0.18f, 0.03f, nsLen),
            new Vector3(roadCx - (roadHw - 0.22f), 0.03f, nsMidZ), whiteC, true);
        MakeBlock("EdgeLine", _worldRoot.transform, new Vector3(0.18f, 0.03f, (jSouthZ - roadHw) - nsZStart),
            new Vector3(roadCx + (roadHw - 0.22f), 0.03f, (nsZStart + (jSouthZ - roadHw)) * 0.5f), whiteC, true);
        MakeBlock("EdgeLine", _worldRoot.transform, new Vector3(0.18f, 0.03f, (jMidZ - roadHw) - (jSouthZ + roadHw)),
            new Vector3(roadCx + (roadHw - 0.22f), 0.03f, ((jSouthZ + roadHw) + (jMidZ - roadHw)) * 0.5f), whiteC, true);
        MakeBlock("EdgeLine", _worldRoot.transform, new Vector3(0.18f, 0.03f, (jNorthZ - roadHw) - (jMidZ + roadHw)),
            new Vector3(roadCx + (roadHw - 0.22f), 0.03f, ((jMidZ + roadHw) + (jNorthZ - roadHw)) * 0.5f), whiteC, true);
        MakeBlock("EdgeLine", _worldRoot.transform, new Vector3(0.18f, 0.03f, nsZEnd - (jNorthZ + roadHw)),
            new Vector3(roadCx + (roadHw - 0.22f), 0.03f, ((jNorthZ + roadHw) + nsZEnd) * 0.5f), whiteC, true);

        // Yellow dashed center line
        float dashLen = 2.8f;
        float dashGap = 2.2f;
        float dashStep = dashLen + dashGap;
        float zStart = nsZStart + dashLen / 2f;
        int numDashes = Mathf.FloorToInt(nsLen / dashStep);
        for (int i = 0; i < numDashes; i++)
        {
            MakeBlock("CenterDash", _worldRoot.transform,
                new Vector3(0.18f, 0.03f, dashLen),
                new Vector3(roadCx, 0.03f, zStart + i * dashStep), yellowC, true);
        }

        // Corner patch at the junction
        MakeBlock("RoadCorner", _worldRoot.transform,
            new Vector3(roadHw * 2f, 0.06f, roadHw * 2f),
            new Vector3(roadCx, 0.03f, roadTurnZ), asphaltC, false, true);

        // East-west leg at the north turn (runs x: roadCx -> roadEndX at z = roadTurnZ)
        MakeBlock("RoadTurn", _worldRoot.transform,
            new Vector3(ewLen, 0.06f, roadHw * 2f),
            new Vector3(ewXc, 0.03f, roadTurnZ), asphaltC, false, true);

        // E-W kerbs start just east of the N-S road so they don't cross it
        foreach (int side in new[] { -1, 1 })
        {
            MakeBlock("Kerb", _worldRoot.transform, new Vector3(ewLen - roadHw, 0.22f, 0.55f),
                new Vector3((roadCx + roadHw + roadEndX) * 0.5f, 0.11f, roadTurnZ + side * (roadHw + 0.35f)), curbC, true);
        }

        foreach (int side in new[] { -1, 1 })
        {
            MakeBlock("EdgeLine", _worldRoot.transform, new Vector3(ewLen - roadHw, 0.03f, 0.18f),
                new Vector3((roadCx + roadHw + roadEndX) * 0.5f, 0.03f, roadTurnZ + side * (roadHw - 0.22f)), whiteC, true);
        }

        float xStart = roadCx + dashLen / 2f;
        int ewDashes = Mathf.FloorToInt(ewLen / dashStep);
        for (int i = 0; i < ewDashes; i++)
        {
            MakeBlock("CenterDash", _worldRoot.transform,
                new Vector3(dashLen, 0.03f, 0.18f),
                new Vector3(xStart + i * dashStep, 0.03f, roadTurnZ), yellowC, true);
        }

        // â”€â”€ South branch: E-W road at z = -50 going WEST (x: roadCx -> -120) â”€â”€
        float southTurnZ = -50f;
        float southEndX = -120f;
        float southEwLen = Mathf.Abs(southEndX - roadCx);
        float southEwXc = (roadCx + southEndX) * 0.5f;

        MakeBlock("RoadCorner", _worldRoot.transform,
            new Vector3(roadHw * 2f, 0.06f, roadHw * 2f),
            new Vector3(roadCx, 0.03f, southTurnZ), asphaltC, false, true);

        MakeBlock("RoadTurn", _worldRoot.transform,
            new Vector3(southEwLen, 0.06f, roadHw * 2f),
            new Vector3(southEwXc, 0.03f, southTurnZ), asphaltC, false, true);

        // West-going kerbs: start from west end, stop before N-S road
        foreach (int side in new[] { -1, 1 })
        {
            MakeBlock("Kerb", _worldRoot.transform, new Vector3(southEwLen - roadHw, 0.22f, 0.55f),
                new Vector3((southEndX + roadCx - roadHw) * 0.5f, 0.11f, southTurnZ + side * (roadHw + 0.35f)), curbC, true);
        }
        foreach (int side in new[] { -1, 1 })
        {
            MakeBlock("EdgeLine", _worldRoot.transform, new Vector3(southEwLen - roadHw, 0.03f, 0.18f),
                new Vector3((southEndX + roadCx - roadHw) * 0.5f, 0.03f, southTurnZ + side * (roadHw - 0.22f)), whiteC, true);
        }

        float southXStart = southEndX + dashLen / 2f;
        if (southEndX > roadCx) southXStart = roadCx + dashLen / 2f;
        int southDashes = Mathf.FloorToInt(southEwLen / dashStep);
        for (int i = 0; i < southDashes; i++)
        {
            MakeBlock("CenterDash", _worldRoot.transform,
                new Vector3(dashLen, 0.03f, 0.18f),
                new Vector3(southXStart + i * dashStep, 0.03f, southTurnZ), yellowC, true);
        }

        // â”€â”€ North branch: E-W road at z = 180 going EAST (x: roadCx -> 150) â”€â”€
        float northTurnZ = 180f;
        float northEndX = 150f;
        float northEwLen = northEndX - roadCx;
        float northEwXc = (roadCx + northEndX) * 0.5f;

        MakeBlock("RoadCorner", _worldRoot.transform,
            new Vector3(roadHw * 2f, 0.06f, roadHw * 2f),
            new Vector3(roadCx, 0.03f, northTurnZ), asphaltC, false, true);

        MakeBlock("RoadTurn", _worldRoot.transform,
            new Vector3(northEwLen, 0.06f, roadHw * 2f),
            new Vector3(northEwXc, 0.03f, northTurnZ), asphaltC, false, true);

        foreach (int side in new[] { -1, 1 })
        {
            MakeBlock("Kerb", _worldRoot.transform, new Vector3(northEwLen - roadHw, 0.22f, 0.55f),
                new Vector3((roadCx + roadHw + northEndX) * 0.5f, 0.11f, northTurnZ + side * (roadHw + 0.35f)), curbC, true);
        }
        foreach (int side in new[] { -1, 1 })
        {
            MakeBlock("EdgeLine", _worldRoot.transform, new Vector3(northEwLen - roadHw, 0.03f, 0.18f),
                new Vector3((roadCx + roadHw + northEndX) * 0.5f, 0.03f, northTurnZ + side * (roadHw - 0.22f)), whiteC, true);
        }

        float northXStart = roadCx + dashLen / 2f;
        int northDashes = Mathf.FloorToInt(northEwLen / dashStep);
        for (int i = 0; i < northDashes; i++)
        {
            MakeBlock("CenterDash", _worldRoot.transform,
                new Vector3(dashLen, 0.03f, 0.18f),
                new Vector3(northXStart + i * dashStep, 0.03f, northTurnZ), yellowC, true);
        }

        // Publish bounds
        _roadCenterX = roadCx;
        _roadHalfWidth = roadHw;
        _roadZStart = nsZStart;
        _roadZEnd = nsZEnd;
        _roadTurnZ = roadTurnZ;
        _roadXEnd = roadEndX;
}

    private void PlaceStreetLights()
    {
        float lampOff = 1.1f;
        float eastX = _roadCenterX + _roadHalfWidth + lampOff;

        // N-S road, east side — skip junction bands and the fishing shop / convenience / police frontage
        for (float z = -260f; z <= 260f; z += 40f)
        {
            if (z > 34f && z < 98f) continue;
            if (Mathf.Abs(z - -50f) < 7f) continue;
            if (Mathf.Abs(z - 90f) < 7f) continue;
            if (Mathf.Abs(z - 180f) < 7f) continue;
            RegisterStreetLamp(MapBuilder.BuildStreetLamp(_worldRoot.transform, new Vector3(eastX, 0f, z)));
        }

        // E-W turn road (z=90), north edge — fronts the village plots
        float northEdgeZ = _roadTurnZ + _roadHalfWidth + lampOff;
        for (float x = 30f; x <= _roadXEnd - 10f; x += 40f)
        {
            if (Mathf.Abs(x - _roadCenterX) < 8f) continue;
            RegisterStreetLamp(MapBuilder.BuildStreetLamp(_worldRoot.transform, new Vector3(x, 0f, northEdgeZ)));
        }

        // South branch road (z=-50, going west), north edge
        float southEdgeZ = -50f + _roadHalfWidth + lampOff;
        for (float x = -110f; x <= 0f; x += 25f)
        {
            if (Mathf.Abs(x - _roadCenterX) < 8f) continue;
            RegisterStreetLamp(MapBuilder.BuildStreetLamp(_worldRoot.transform, new Vector3(x, 0f, southEdgeZ)));
        }

        // North branch road (z=180, going east), north edge
        float northBranchEdgeZ = 180f + _roadHalfWidth + lampOff;
        for (float x = 30f; x <= 130f; x += 25f)
        {
            if (Mathf.Abs(x - _roadCenterX) < 8f) continue;
            RegisterStreetLamp(MapBuilder.BuildStreetLamp(_worldRoot.transform, new Vector3(x, 0f, northBranchEdgeZ)));
        }
    }

    private void RegisterStreetLamp(Light lamp)
    {
        if (lamp != null)
            _streetLights.Add(lamp);
    }

    private void BuildRockyBorder()
    {
        float half = GroundSize.x * 0.5f;
        float spacing = 2.5f;
        float westX = -400f;

        void SpawnBorderSegment(Vector3 pos, float scale)
        {
            var rock = MapBuilder.BuildBorderRock(_worldRoot.transform, pos, scale);
            rock.name = "BorderRock";
        }

        for (float x = westX; x <= half; x += spacing)
        {
            SpawnBorderSegment(new Vector3(x, 0f, half), Random.Range(0.8f, 1.2f));
        }

        for (float x = westX; x <= half; x += spacing)
        {
            if (x > 8f && x < 20f) continue;
            SpawnBorderSegment(new Vector3(x, 0f, -half), Random.Range(0.8f, 1.2f));
        }

        for (float z = -half; z <= half; z += spacing)
        {
            SpawnBorderSegment(new Vector3(half, 0f, z), Random.Range(0.8f, 1.2f));
        }

        for (float z = -half; z <= half; z += spacing)
        {
            SpawnBorderSegment(new Vector3(westX, 0f, z), Random.Range(0.8f, 1.2f));
        }
    }

    public void CloseBorderGap()
    {
        float half = GroundSize.x * 0.5f;
        float spacing = 2.5f;

        for (float x = 8f; x <= 20f; x += spacing)
        {
            var rock = MapBuilder.BuildBorderRock(_worldRoot.transform, new Vector3(x, 0f, -half), Random.Range(0.8f, 1.2f));
            rock.name = "BorderRock";
        }
    }

    private void LoadTreeTextures()
    {
        var woodTex = Resources.Load<Texture2D>("texture/wood_texture");
        var leafTex = Resources.Load<Texture2D>("texture/leaves_texture");
        if (woodTex != null || leafTex != null)
        {
            MapBuilder.SetTreeTextures(woodTex, leafTex);
            Debug.Log("[WorldBuilder] Tree textures loaded.");
        }
        else
        {
            Debug.Log("[WorldBuilder] No tree textures found in Resources/texture/. Using flat colors.");
        }
    }

    private void SpawnTrees(int count)
    {
        int half = Mathf.FloorToInt(GroundSize.x * 0.5f) - 5;
        for (int i = 0; i < count; i++)
        {
            int x, z;
            while (true)
            {
                x = Random.Range(-half, half + 1);
                z = Random.Range(-half, half + 1);
                if (x > -145 && !IsReservedSpawnLocation(x, z))
                    break;
            }

GameObject treeRoot;
        if (TreePrefab != null)
        {
            treeRoot = Instantiate(TreePrefab, _worldRoot.transform);
            treeRoot.name = "Tree" + i;
            treeRoot.transform.position = new Vector3(x, 0f, z);
        }
        else
        {
            treeRoot = MapBuilder.BuildTree(_worldRoot.transform, new Vector3(x, 0f, z));
            treeRoot.name = "Tree" + i;
        }

            _trees.Add(treeRoot);
        }
    }

    private void SpawnRocks(int count)
    {
        int half = Mathf.FloorToInt(GroundSize.x * 0.5f) - 5;
        for (int i = 0; i < count; i++)
        {
            int x, z;
            while (true)
            {
                x = Random.Range(-half, half + 1);
                z = Random.Range(-half, half + 1);
            if (x > -145 && !IsReservedSpawnLocation(x, z))
                    break;
            }

            GameObject rock;
            if (RockPrefab != null)
            {
                rock = Instantiate(RockPrefab, _worldRoot.transform);
                rock.name = "Rock" + i;
                rock.transform.position = new Vector3(x, 0f, z);
            }
            else
            {
                rock = MapBuilder.BuildStone(_worldRoot.transform, new Vector3(x, 0f, z));
                rock.name = "Rock" + i;
            }
            _rocks.Add(rock);
        }
    }

    private void RespawnResources()
    {
        if (_trees.Count < MaxTrees)
            SpawnSingleTree();
        if (_rocks.Count < MaxRocks)
            SpawnSingleRock();
    }

    private void SpawnSingleTree()
    {
        int half = Mathf.FloorToInt(GroundSize.x * 0.5f) - 5;
        int x, z;
        int attempts = 0;
        while (attempts < 50)
        {
            x = Random.Range(-half, half + 1);
            z = Random.Range(-half, half + 1);
            if (x > -215 && !IsReservedSpawnLocation(x, z))
            {
                GameObject treeRoot;
                if (TreePrefab != null)
                {
                    treeRoot = Instantiate(TreePrefab, _worldRoot.transform);
                    treeRoot.name = "Tree" + _treeNameCounter++;
                    treeRoot.transform.position = new Vector3(x, 0f, z);
                }
                else
                {
                    treeRoot = MapBuilder.BuildTree(_worldRoot.transform, new Vector3(x, 0f, z));
                    treeRoot.name = "Tree" + _treeNameCounter++;
                }
                _trees.Add(treeRoot);
                return;
            }
            attempts++;
        }
    }

    private void SpawnSingleRock()
    {
        int half = Mathf.FloorToInt(GroundSize.x * 0.5f) - 5;
        int x, z;
        int attempts = 0;
        while (attempts < 50)
        {
            x = Random.Range(-half, half + 1);
            z = Random.Range(-half, half + 1);
            if (x > -215 && !IsReservedSpawnLocation(x, z))
            {
                GameObject rock;
                if (RockPrefab != null)
                {
                    rock = Instantiate(RockPrefab, _worldRoot.transform);
                    rock.name = "Rock" + _rockNameCounter++;
                    rock.transform.position = new Vector3(x, 0f, z);
                }
                else
                {
                    rock = MapBuilder.BuildStone(_worldRoot.transform, new Vector3(x, 0f, z));
                    rock.name = "Rock" + _rockNameCounter++;
                }
                _rocks.Add(rock);
                return;
            }
            attempts++;
        }
    }

}

