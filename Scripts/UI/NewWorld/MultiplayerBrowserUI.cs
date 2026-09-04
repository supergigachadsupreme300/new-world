using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.2): Multiplayer Browser / Party UI. A modal panel that lists the active
/// server ('<see cref="GameServer"/>' via '<see cref="NetServerHost"/>'), every connected
/// sesscould client, and the currently active <see cref="NetLobby"/>s with their mode + fill.
/// Composes the Phase 7 networking layer for browsing and party entry.
/// </summary>
public sealed class MultiplayerBrowserUI : MenuPanelBase
{
    private TMP_Text _serverLine;
    private TMP_Text _partyLine;
    private NetServerHost _host;

    private void OnEnable()
    {
        Build(Localization.T("MULTIPLAYER BROWSER"));
        _serverLine = MakeBodyText(BodyRow.transform, "Server", new Vector2(-220f, 150f));
        _partyLine = MakeBodyText(BodyRow.transform, "Parties", new Vector2(-220f, 20f));
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
        rt.sizeDelta = new Vector2(520f, 120f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(12f, Screen.height / 60f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        return tmp;
    }

    protected override void Refresh()
    {
        _host = Object.FindAnyObjectByType<NetServerHost>();
        GameServer server = _host != null && _host.IsRunning ? _host.Server : null;

        if (server != null)
        {
            int sessions = server.Sessions.Count;
            StringBuilder s = new StringBuilder();
            s.Append("Status: ").Append(Localization.T("Running")).Append(" (")
             .Append(sessions).Append(" ").Append(Localization.T("Players")).Append(")");
            foreach (var sess in server.Sessions)
                s.Append("\n  • ").Append(sess.PlayerName);
            _serverLine.text = s.ToString();
        }
        else
        {
            _serverLine.text = "Server: " + Localization.T("Offline");
        }

        // Part/lobby summary.
        var lobbies = FindLobbies();
        StringBuilder p = new StringBuilder();
        foreach (var l in lobbies)
        {
            string name = l.Mode != null ? l.Mode.DisplayName : "?";
            p.Append("• ").Append(name).Append("  ").Append(l.MemberCount)
             .Append("/").Append(l.Mode != null ? l.Mode.MaxPlayers : 0)
             .Append(l.IsOpenToJoin ? "  [open]" : "  [invite]").Append("\n");
        }
        _partyLine.text = p.Length == 0 ? Localization.T("No active parties.") : p.ToString();
    }

    private System.Collections.Generic.List<NetLobby> FindLobbies()
    {
        if (_host != null && _host.IsRunning)
            return _host.Lobbies;
        return new System.Collections.Generic.List<NetLobby>();
    }
}