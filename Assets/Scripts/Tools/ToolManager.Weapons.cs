/// Melee/club weapon swing animation (club hit logic lives in UseSelectedItem).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class ToolManager
{
    private void PlaySwing()
    {
        if (_isSwinging) return;
        StartCoroutine(SwingAnimation());
    }

    private IEnumerator SwingAnimation()
    {
        _isSwinging = true;

        var itemType = GetSelectedItemType();
        if (itemType != null)
        {
            var sound = itemType switch
            {
                "scythe" => "sickle",
                _ => itemType
            };
            SoundManager.Instance?.Play(sound);
        }

        var tool = GetActiveToolModel();
        if (tool != null)
        {
            float dur = 0.12f;
            float elapsed = 0f;
            Quaternion start = tool.transform.localRotation;
            Quaternion swing = start * Quaternion.Euler(50f, 0f, 0f);

            while (elapsed < dur)
            {
                tool.transform.localRotation = Quaternion.Slerp(start, swing, elapsed / dur);
                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < dur)
            {
                tool.transform.localRotation = Quaternion.Slerp(swing, start, elapsed / dur);
                elapsed += Time.deltaTime;
                yield return null;
            }

            tool.transform.localRotation = start;
        }
        _isSwinging = false;
    }
}