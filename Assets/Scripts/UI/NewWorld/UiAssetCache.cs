using TMPro;
using UnityEngine;

/// <summary>
/// Lazy one-time caches for UI assets that are re-loaded via <see cref="Resources.Load"/> at many
/// call sites (the "menu" backdrop texture, the default VietPixel font). Keeps a single strong
/// reference so the assets stay alive and no repeated disk reads/allocations happen during menu
/// construction, HUD build-out, or chunk finalization.
/// </summary>
public static class UiAssetCache
{
    private static Texture2D _menuTexture;
    private static TMP_FontAsset _defaultFont;

    /// <summary>The shared "menu" panel/backdrop texture.</summary>
    public static Texture2D MenuTexture
    {
        get
        {
            if (_menuTexture == null)
                _menuTexture = Resources.Load<Texture2D>("menu");
            return _menuTexture;
        }
    }

    /// <summary>The shared default UI font (VietPixel).</summary>
    public static TMP_FontAsset DefaultFont
    {
        get
        {
            if (_defaultFont == null)
                _defaultFont = Resources.Load<TMP_FontAsset>("VietPixel");
            return _defaultFont;
        }
    }
}