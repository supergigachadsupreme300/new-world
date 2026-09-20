/// <summary>Character Info - Map tab placeholder summary refresh (the dedicated WorldMapUI is separate).</summary>
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public sealed partial class CharacterInfoUI
{

    private void RefreshMap()
    {
        _mapLine.text = Localization.T("World Map — see the dedicated Map menu.\nChar Info Map is a placeholder summary.");
    }
}