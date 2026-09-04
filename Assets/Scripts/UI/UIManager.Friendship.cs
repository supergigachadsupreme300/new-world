using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public partial class UIManager
{
    private GameObject _friendPanelRoot;
    private TMP_Text _friendRows;
    private TMP_Text _friendGiftTitle;
    private TMP_Text _friendGiftList;
    private bool _friendPanelVisible;
    private int _friendSelected;
    private bool _friendGiftMode;
    private int _friendGiftPage;

    public bool FriendPanelVisible => _friendPanelVisible;

    public void ToggleFriendPanel()
    {
        if (_friendPanelVisible) HideFriendPanel();
        else ShowFriendPanel();
    }

    public void ShowFriendPanel()
    {
        if (_canvas == null) return;
        if (_friendPanelRoot == null) CreateFriendPanel();
        if (_friendPanelRoot == null) return;
        _friendPanelVisible = true;
        _friendGiftMode = false;
        _friendGiftPage = 0;
        _friendPanelRoot.SetActive(true);
        UpdateFriendPanel();
    }

    public void HideFriendPanel()
    {
        if (_friendPanelVisible && _friendPanelRoot != null)
            _friendPanelRoot.SetActive(false);
        _friendPanelVisible = false;
    }

    // Called each frame from PlayerController to handle friendship keys.
    public void HandleFriendPanelKeys()
    {
        if (!_friendPanelVisible || _friendPanelRoot == null) return;
        if (Keyboard.current == null) return;

        int selected = _friendSelected;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) selected = 0;
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) selected = 1;
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) selected = 2;
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) selected = 3;

        if (selected != _friendSelected)
        {
            _friendSelected = selected;
            _friendGiftMode = false;
            UpdateFriendPanel();
            return;
        }

        if (_friendGiftMode)
        {
            var gifts = OwnedGiftable();
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                _friendGiftMode = false;
                _friendGiftPage = 0;
                UpdateFriendPanel();
                return;
            }
            if (Keyboard.current.digit1Key.wasPressedThisFrame) GiveSelected(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) GiveSelected(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) GiveSelected(2);
            else if (Keyboard.current.digit4Key.wasPressedThisFrame) GiveSelected(3);
            else if (Keyboard.current.digit5Key.wasPressedThisFrame) GiveSelected(4);
            else if (Keyboard.current.digit6Key.wasPressedThisFrame) GiveSelected(5);
            else if (Keyboard.current.digit7Key.wasPressedThisFrame) GiveSelected(6);
            else if (Keyboard.current.digit8Key.wasPressedThisFrame) GiveSelected(7);
            else if (Keyboard.current.digit9Key.wasPressedThisFrame) GiveSelected(8);
            else if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _friendGiftPage++;
                UpdateFriendPanel();
            }
        }
        else
        {
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                _friendGiftMode = true;
                _friendGiftPage = 0;
                UpdateFriendPanel();
                return;
            }
            if (Keyboard.current.digit1Key.wasPressedThisFrame ||
                Keyboard.current.digit2Key.wasPressedThisFrame ||
                Keyboard.current.digit3Key.wasPressedThisFrame ||
                Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                _friendSelected = selected;
                UpdateFriendPanel();
            }
        }
    }

    private string[] OwnedGiftable()
    {
        var tm = ToolManager.Instance;
        if (tm == null) return new string[0];
        var all = FriendshipManager.AllGiftable();
        var owned = new System.Collections.Generic.List<string>();
        for (int i = 0; i < all.Length; i++)
            if (tm.CountItem(all[i]) > 0) owned.Add(all[i]);
        return owned.ToArray();
    }

    private void GiveSelected(int index)
    {
        var gifts = OwnedGiftable();
        if (_friendGiftPage < 0) _friendGiftPage = 0;
        int start = _friendGiftPage * 9;
        int actual = start + index;
        if (actual < 0 || actual >= gifts.Length) return;

        var fm = FriendshipManager.Instance;
        if (fm == null) return;
        string id = fm.NpcIds[_friendSelected];
        string item = gifts[actual];

        if (fm.GiveGift(id, item))
        {
            string name = Localization.ItemName(item);
            bool liked = fm.IsItemLiked(id, item);
            GameManager.Instance?.UIManager?.ShowMessage(
                Localization.F(liked ? "Tặng {0} cho {1}. Họ rất thích!" : "Tặng {0} cho {1}.", name, FriendshipManager.NpcDisplayName(id)), 2.5f);
            UpdateFriendPanel();
        }
        else
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Không thể tặng món này ngay bây giờ."), 2f);
        }
    }

    private void UpdateFriendPanel()
    {
        if (!_friendPanelVisible || _friendPanelRoot == null) return;
        var fm = FriendshipManager.Instance;
        if (fm == null) return;

        if (_friendGiftMode)
        {
            string id = fm.NpcIds[_friendSelected];
            _friendRows.text = Localization.T("Tặng quà cho") + ": " + FriendshipManager.NpcDisplayName(id);
            var gifts = OwnedGiftable();
            int start = _friendGiftPage * 9;
            var sb = new System.Text.StringBuilder();
            int shown = 0;
            for (int i = start; i < gifts.Length && shown < 9; i++, shown++)
            {
                int count = ToolManager.Instance != null ? ToolManager.Instance.CountItem(gifts[i]) : 0;
                string mark = fm.IsItemLiked(id, gifts[i]) ? "♥" : "";
                sb.AppendLine(Localization.F(" {0}. {1} x{2} {3}", (shown + 1), Localization.ItemName(gifts[i]), count, mark));
            }
            if (shown == 0)
                sb.AppendLine(Localization.T("Túi đồ không có món quà nào."));
            _friendGiftTitle.text = Localization.T("Nhấn số để tặng, G để đóng, Space để đổi trang");
            _friendGiftList.text = sb.ToString();
            _friendGiftList.gameObject.SetActive(true);
            _friendGiftTitle.gameObject.SetActive(true);
            _friendRows.gameObject.SetActive(false);
            return;
        }

        var rows = new System.Text.StringBuilder();
        for (int i = 0; i < fm.NpcIds.Length; i++)
        {
            string id = fm.NpcIds[i];
            int lv = fm.HeartLevel(id);
            int points = (int)fm.HeartPoints(id);
            string marker = i == _friendSelected ? "▶" : " ";
            string bar = FriendshipManager.HeartEmoji(lv);
            string pct = lv >= 5 ? "" : Localization.F(" ({0}/{1})", points, (lv + 1) * FriendshipManager.PointsPerHeart);
            rows.AppendLine(Localization.F("{0} {1}. {2}  {3}{4}", marker, i + 1, FriendshipManager.NpcDisplayName(id), bar, pct));
        }
        _friendRows.gameObject.SetActive(true);
        _friendRows.text = rows.ToString();
        _friendGiftList.gameObject.SetActive(false);
        _friendGiftTitle.gameObject.SetActive(false);
    }

    private void CreateFriendPanel()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        float panelW = sw * 0.3f;
        float panelH = sh * 0.42f;

        _friendPanelRoot = new GameObject("FriendPanelRoot");
        _friendPanelRoot.transform.SetParent(_canvas.transform, false);

        var rootRect = _friendPanelRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0.5f);
        rootRect.anchorMax = new Vector2(0f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = new Vector2(sw * 0.02f, -sh * 0.05f);
        rootRect.sizeDelta = new Vector2(panelW, panelH);

        var bg = new GameObject("FriendPanelBg");
        bg.transform.SetParent(_friendPanelRoot.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 1f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.78f);
        bgImg.raycastTarget = false;

        EnsureText("FriendPanelTitle", new Vector2(0f, panelH * 0.5f - 22f), Localization.T("Tình Bạn"), 16,
            _friendPanelRoot.transform, TextAlignmentOptions.Center, false, new Vector2(panelW, 22f));

        _friendRows = EnsureText("FriendRows", new Vector2(0f, panelH * 0.1f), "", 14,
            _friendPanelRoot.transform, TextAlignmentOptions.Center, false, new Vector2(panelW - 12f, panelH * 0.72f));

        _friendGiftTitle = EnsureText("FriendGiftTitle", new Vector2(0f, panelH * 0.5f - 22f), "", 13,
            _friendPanelRoot.transform, TextAlignmentOptions.Center, false, new Vector2(panelW, 22f));
        _friendGiftTitle.gameObject.SetActive(false);

        _friendGiftList = EnsureText("FriendGiftList", new Vector2(0f, -8f), "", 13,
            _friendPanelRoot.transform, TextAlignmentOptions.Center, false, new Vector2(panelW - 12f, panelH * 0.72f));
        _friendGiftList.gameObject.SetActive(false);

        _friendRows.text = "";
        _friendPanelRoot.SetActive(false);
    }
}
