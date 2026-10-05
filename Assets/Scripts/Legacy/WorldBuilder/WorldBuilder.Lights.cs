/// WorldBuilder lights group: street-light management helpers.
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;
using CountryLife.Helpers;

public partial class WorldBuilder
{
    private void SetStreetLights(bool on)
    {
        for (int i = _streetLights.Count - 1; i >= 0; i--)
        {
            var l = _streetLights[i];
            if (l == null)
            {
                _streetLights.RemoveAt(i);
                continue;
            }
            l.enabled = on;
        }
    }
}