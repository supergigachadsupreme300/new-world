using UnityEngine;

public static class AssetLoader
{
    public static T LoadAsset<T>(string relativePath) where T : Object
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        string normalizedPath = relativePath.Replace("Assets/Resources/", string.Empty).Replace("Resources/", string.Empty);
        normalizedPath = normalizedPath.Replace(".prefab", string.Empty).Replace(".png", string.Empty).Replace(".jpg", string.Empty).Replace(".wav", string.Empty).Replace(".mp3", string.Empty);

        T asset = Resources.Load<T>(normalizedPath);
        if (asset != null)
            return asset;

        Debug.LogWarning($"[AssetLoader] Không tìm thấy asset: {relativePath} (đã thử: {normalizedPath})");
        return null;
    }

    public static GameObject LoadModel(string relativePath)
    {
        return LoadAsset<GameObject>(relativePath);
    }

    public static Texture2D LoadTexture(string relativePath)
    {
        return LoadAsset<Texture2D>(relativePath);
    }

    public static AudioClip LoadAudioClip(string relativePath)
    {
        return LoadAsset<AudioClip>(relativePath);
    }
}
