/// WorldBuilder environment group: sky, sun, day/night cycle, fog and cloud handling.
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;
using CountryLife.Helpers;

public partial class WorldBuilder
{
    private void SpawnInitialClouds()
    {
        int half = Mathf.FloorToInt(GroundSize.x * 0.5f) - 20;
        for (int i = 0; i < 8; i++)
        {
            Vector3 pos = new Vector3(
                UnityEngine.Random.Range(-half, half + 1),
                UnityEngine.Random.Range(60f, 80f),
                UnityEngine.Random.Range(-half, half + 1));
            float scale = UnityEngine.Random.Range(1.5f, 3f);
            var cloud = MapBuilder.BuildCloud(_worldRoot.transform, pos, scale);
            cloud.AddComponent<CloudBehavior>();
            _clouds.Add(cloud);
        }
    }

    public void SetDayNight(float hour)
    {
        if (!EnableLegacyGeneration)
            return;

bool isNight = hour >= 18f || hour < 6f;
        if (isNight && !_wasNight)
        {
            _wasNight = true;
            CloseAllDoors();
            SetStreetLights(true);
        }
        else if (!isNight && _wasNight)
        {
            _wasNight = false;
            SetStreetLights(false);
        }

        if (SunLight == null)
            return;

        float t = hour / 24f;
        float elevation = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f) * 80f;
        float sunY = Mathf.Lerp(-180f, 180f, t);
        SunLight.transform.rotation = Quaternion.Euler(elevation, sunY, 0f);

        Color sunColor;
        float sunIntensity;
        Color ambient;
        float ambientIntensity;
        bool fog;
        Color fogColor = default;
        float fogDensity = 0f;

        if (hour >= 6f && hour < 17f)
        {
            sunIntensity = 2f;
            sunColor = new Color(1f, 0.925f, 0.77f);
            ambient = new Color(0.5f, 0.7f, 1f);
            ambientIntensity = 0.8f;
            fog = false;
        }
        else
        {
            float dayFactor = Mathf.Clamp01((elevation + 10f) / 90f);
            sunIntensity = Mathf.Lerp(0.05f, 2f, dayFactor);

            float warmFactor = 0f;
            if (hour >= 5f && hour < 6f)
                warmFactor = Mathf.InverseLerp(5f, 6f, hour);
            else if (hour >= 17f && hour < 18f)
                warmFactor = 1f - Mathf.InverseLerp(17f, 18f, hour);
            Color baseSunColor = Color.Lerp(
                new Color(1f, 0.925f, 0.77f),
                new Color(1f, 0.5f, 0.15f),
                warmFactor);
            if (elevation < -5f)
            {
                float nightFactor = Mathf.InverseLerp(-5f, -30f, elevation);
                baseSunColor = Color.Lerp(baseSunColor, new Color(0.1f, 0.1f, 0.3f), nightFactor);
            }
            sunColor = baseSunColor;

            Color skyColor;
            if (elevation > 15f)
            {
                skyColor = new Color(0.5f, 0.7f, 1f);
            }
            else if (elevation > -5f)
            {
                float sunriseT = Mathf.InverseLerp(-5f, 15f, elevation);
                skyColor = Color.Lerp(new Color(0.8f, 0.3f, 0.1f), new Color(0.5f, 0.7f, 1f), sunriseT);
            }
            else
            {
                float nightT = Mathf.InverseLerp(-5f, -30f, elevation);
                skyColor = Color.Lerp(new Color(0.09f, 0.09f, 0.15f), new Color(0.06f, 0.06f, 0.12f), nightT);
            }

            ambient = skyColor;
            ambientIntensity = Mathf.Lerp(0.3f, 0.8f, dayFactor);

            float fogFactor = 1f - Mathf.Abs(elevation - 10f) / 25f;
            fogFactor = Mathf.Clamp01(fogFactor);
            if (fogFactor > 0.01f)
            {
                fog = true;
                fogColor = Color.Lerp(skyColor, new Color(1f, 0.6f, 0.3f), elevation > 0f ? 0.3f : 0.5f);
                fogDensity = fogFactor * 0.015f;
            }
            else
            {
                fog = false;
            }
        }

        if (SunLight.color != sunColor)
            SunLight.color = sunColor;
        if (SunLight.intensity != sunIntensity)
            SunLight.intensity = sunIntensity;
        if (RenderSettings.ambientLight != ambient)
            RenderSettings.ambientLight = ambient;
        if (RenderSettings.ambientIntensity != ambientIntensity)
            RenderSettings.ambientIntensity = ambientIntensity;
        if (RenderSettings.fog != fog)
            RenderSettings.fog = fog;
        if (fog)
        {
            if (RenderSettings.fogColor != fogColor)
                RenderSettings.fogColor = fogColor;
            if (RenderSettings.fogDensity != fogDensity)
                RenderSettings.fogDensity = fogDensity;
        }
    }
}