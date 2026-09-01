using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LibrarianNPC : MonoSingleton<LibrarianNPC>
{
private enum Phase { Intro, Menu, Done }

    private Transform _myTransform;
    private Transform _playerTransform;
    private Quaternion _originalRotation = Quaternion.identity;

    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private bool _dialogActive;
    private bool _researchShowing;
    private Phase _phase;
    private readonly Queue<string> _dialogQueue = new Queue<string>();

    private readonly string[] _lines =
    {
        "Chào mừng đến Thư Viện làng. Nơi đây cất giữ mọi bản thiết kế của vùng quê.",
        "Muốn xây dựng điều gì mới, con cần học hỏi từ những trang sách cũ.",
        "Với chút vàng bạc, ta có thể truyền thụ tri thức về những công trình chưa từng được xây trong làng.",
        "Con cứ tự do tham khảo sách. Khi đã sẵn sàng học điều mới, hãy gọi ta."
    };

    private const string _menuTitle = "CON MUỐN HỌC BẢN THIẾT KẾ NÀO?";

    public bool IsDialogActive => _dialogActive;
    public bool IsResearchShown => _researchShowing;

    void Start()
    {
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;
        if (_myTransform != null)
            _originalRotation = _myTransform.rotation;
    }
    public void Interact()
    {
        if (gameObject == null || !gameObject.activeInHierarchy)
            return;
        InitializeDialog();
        if (_panel == null)
            return;

        FriendshipManager.Instance?.GrantTalk("librarian");
        FacePlayer();
        _dialogActive = true;
        _panel.SetActive(true);
        _nameText.text = Localization.T("Thủ Thư");
        _dialogQueue.Clear();
        _researchShowing = false;
        _phase = Phase.Intro;
        for (int i = 0; i < _lines.Length; i++)
            _dialogQueue.Enqueue(_lines[i]);
        Advance();
    }
    public void Advance()
    {
        if (_panel == null)
            return;
        if (_researchShowing)
        {
            _researchShowing = false;
            _phase = Phase.Done;
            _dialogQueue.Enqueue("Khi nào con muốn học thêm, hãy quay lại đây nhé.");
        }
        if (_dialogQueue.Count == 0)
        {
            if (_phase == Phase.Intro)
            {
                var wb = WorldBuilder.Instance;
                var list = wb != null ? wb.GetResearchableBlueprints() : new List<(string Name, int Cost)>();
                if (list.Count > 0)
                {
                    ShowResearchMenu(list);
                    return;
                }
                _phase = Phase.Done;
                _dialogQueue.Enqueue("Con đã nắm được mọi tri thức ở thư viện này rồi. Hãy truyền lại cho thế hệ sau nhé.");
            }
            else
            {
                Hide();
                return;
            }
        }
        _dialogText.text = Localization.T(_dialogQueue.Dequeue());
        _promptText.text = _dialogQueue.Count > 0
            ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
    }
    public void ChooseResearch(int index)
    {
        if (!_researchShowing)
            return;
        var wb = WorldBuilder.Instance;
        var player = GameManager.Instance?.Player;
        if (wb == null || player == null)
            return;

        var list = wb.GetResearchableBlueprints();
        if (index < 0 || index >= list.Count)
            return;

        var item = list[index];
        _researchShowing = false;
        _phase = Phase.Done;
        _dialogQueue.Clear();

        if (player.Money < item.Cost)
        {
            _dialogQueue.Enqueue(Localization.F("Con chưa đủ {0}🪙 để học. Hãy quay lại khi có đủ vàng nhé.", item.Cost));
        }
        else
        {
            player.Money -= item.Cost;
            wb.UnlockBlueprint(item.Name);
            GameManager.Instance?.UIManager?.UpdatePlayerHud(player.HP, player.MaxHP, player.Stamina, player.MaxStamina, player.Money);
            _dialogQueue.Enqueue(Localization.F("Con đã học được bản thiết kế {0}! Giờ con có thể xây nó ở bất cứ đâu.", Localization.BuildingName(item.Name)));
            GameManager.Instance?.UIManager?.ShowMessage(Localization.F("Đã mở khóa: {0}", Localization.BuildingName(item.Name)), 2f);
        }

        Advance();
    }
    public void Hide()
    {
        _dialogActive = false;
        _researchShowing = false;
        _phase = Phase.Done;
        if (_panel != null)
            _panel.SetActive(false);
        if (_myTransform != null)
            _myTransform.rotation = _originalRotation;
    }
    private void ShowResearchMenu(List<(string Name, int Cost)> list)
    {
        _researchShowing = true;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Localization.T(_menuTitle));
        for (int i = 0; i < list.Count; i++)
        {
            if (i < 9)
                sb.AppendLine(Localization.F("  {0}. {1} - {2}🪙", (i + 1), Localization.BuildingName(list[i].Name), list[i].Cost));
            else
                sb.AppendLine(Localization.F("  • {0} - {1}🪙", Localization.BuildingName(list[i].Name), list[i].Cost));
        }
        _dialogText.text = sb.ToString();
        _promptText.text = GameInput.IsMobile
            ? Localization.T("Chạm để đóng")
            : Localization.T("Chọn 1-9 để học, nhấn E để đóng");
    }
    private void FacePlayer()
    {
        if (_myTransform == null || _playerTransform == null)
            return;
        Vector3 to = _myTransform.position - _playerTransform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.001f)
            _myTransform.rotation = Quaternion.LookRotation(to.normalized);
    }
    private void InitializeDialog()
    {
        if (_canvas != null)
            return;
        var hudGo = GameObject.Find("HUD_Canvas");
        _canvas = hudGo != null ? hudGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
            return;
        CreatePanel();
    }
    private void CreatePanel()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("LibrarianDialogPanel");
        _panel.transform.SetParent(_canvas.transform, false);

        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, sh * 0.15f);
        rt.sizeDelta = new Vector2(sw * 0.6f, sh * 0.2f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        var btn = _panel.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Advance);

        float panelW = sw * 0.6f;
        float panelH = sh * 0.2f;

        _nameText = MakeText("LibrarianDialogName", rt, new Vector2(0f, panelH * 0.36f),
            Localization.T("Thủ Thư"), 24, new Color(1f, 0.82f, 0.4f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("LibrarianDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("LibrarianDialogPrompt", rt, new Vector2(0f, -panelH * 0.36f),
            "", 16, new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 25f));

        _panel.SetActive(false);
    }
    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}
