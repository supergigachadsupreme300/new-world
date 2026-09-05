using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.2): World Map UI. A modal panel that lists every <see cref="POIRegistry"/>
/// point of interest with its kind/biome, the player's current world + chunk coordinates
/// (<see cref="ChunkData"/>/<see cref="ChunkCoord"/>), and fast-travel markers. Composes the
/// existing POI registry without rewriting it.
/// </summary>
public sealed class WorldMapUI : MenuPanelBase
{
    private TMP_Text _poiLine;
    private TMP_Text _coordLine;

    private void OnEnable()
    {
        Build(Localization.T("WORLD MAP"));
        _poiLine = MakeBodyText(BodyRow, "PoiList", new Vector2(-220f, 150f));
        _coordLine = MakeBodyText(BodyRow, "Coords", new Vector2(40f, 150f));
    }

    private TMP_Text MakeBodyText(RectTransform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(520f, 260f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(14f, Screen.height / 48f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        return tmp;
    }

    protected override void Refresh()
    {
        // POI list.
        var pois = POIRegistry.All;
        StringBuilder sb = new StringBuilder();
        foreach (var p in pois)
        {
            sb.Append(p.DisplayName != null ? p.DisplayName : p.Id)
              .Append("  [").Append(KindName(p.Kind)).Append("]");
            if (p.IsFastTravelPoint) sb.Append("  ✈");
            sb.Append("\n");
        }
        _poiLine.text = sb.Length == 0 ? Localization.T("No points of interest.") : sb.ToString();

        // Coordinates.
        var gm = GameManager.Instance;
        Vector3 pos = gm != null && gm.Player != null ? gm.Player.transform.position : Vector3.zero;
        int cx = Mathf.FloorToInt(pos.x / ChunkData.Size);
        int cz = Mathf.FloorToInt(pos.z / ChunkData.Size);
        _coordLine.text = Localization.F("Vị ži: ({0}, {1}, {2})", (int)pos.x, (int)pos.y, (int)pos.z)
            + "\n" + Localization.F("Tile: ({0}, {1})", cx, cz);
    }

    private static string KindName(PoiKind kind)
    {
        switch (kind)
        {
            case PoiKind.Town: return "Town";
            case PoiKind.Dungeon: return "Dungeon";
            case PoiKind.BossArena: return "Boss";
            case PoiKind.Fishing: return "Fishing";
            case PoiKind.FastTravel: return "FT";
            case PoiKind.Farming: return "Farm";
            case PoiKind.HiddenCave: return "Cave";
            case PoiKind.SkillBook: return "Skill";
            default: return "?";
        }
    }
}