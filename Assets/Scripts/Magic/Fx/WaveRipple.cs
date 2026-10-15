using UnityEngine;

/// <summary>
/// 1l85: the traveling ripple for the Wave floor body. The floor is a flat lattice of
/// small cubes (see <c>MagicProjectileModelBuilder.Wave</c>); this component holds those cells and
/// their authored resting heights, and <see cref="Apply"/> offsets each cell along local Y by a sine
/// that advances with time — so a crest travels along local +Z while the wave flies, reading as a wave
/// rolling across the ground instead of a frozen silhouette. The same sine brightens the crest, because
/// a vertical offset alone is nearly invisible on flat-topped cubes whose normals never change.
/// <para><b>No <c>Update</c> of its own, on purpose.</b> The per-frame write is driven from
/// <c>SpellWave</c>'s <c>Update</c> — the loop that runs while the thing MOVES
/// (rule 19: a per-frame effect belongs there, never in the shape factory). The factory also draws
/// MOTIONLESS QA pedestals for every castable spell through <c>CreateProjectileDisplay</c>; a
/// self-updating component would ripple on every one of them, which is the same pedestal artifact the
/// in-flight trail was moved out of the builder to avoid.</para>
/// <para><b>Offsets are recomputed from the authored rest, never accumulated.</b> <see cref="Apply"/>
/// writes <c>_restY[i] + sin(...)</c> fresh every frame, so it cannot drift the way a
/// read-the-live-value-and-multiply integrator would — the Flamethrower's compounding-scale bug fixed
/// in the same task (1l85 task 1) is that same failure in another costume.</para>
/// </summary>
public sealed class WaveRipple : MonoBehaviour
{
    private Transform[] _cells;
    private float[] _restY;
    private Material[] _mats;
    private Color _trough;
    private Color _crest;
    private float _amplitude;
    private float _speed;
    private float _waveNumber;

    /// <summary>Hand the component its cells, their authored rest heights, the per-cell materials and
    /// the two colours the ripple lerps between. Called once by the builder right after the floor is
    /// laid, so the cells are already parented and their rest positions set.</summary>
    public void Configure(Transform[] cells, float[] restY, Material[] mats,
        Color trough, Color crest, float amplitude, float speed, float wavelength)
    {
        _cells = cells;
        _restY = restY;
        _mats = mats;
        _trough = trough;
        _crest = crest;
        _amplitude = amplitude;
        _speed = speed;
        // Guard the divisor: a zero wavelength would make the phase NaN and blank the floor.
        _waveNumber = 2f * Mathf.PI / Mathf.Max(0.01f, wavelength);
    }

    /// <summary>Offset and tint every cell by a traveling sine. <paramref name="time"/> is passed in
    /// rather than read from <c>Time</c> here, so the caller owns the clock and the component is a pure
    /// function of (time, cell).</summary>
    public void Apply(float time)
    {
        if (_cells == null || _restY == null) return;
        int n = Mathf.Min(_cells.Length, _restY.Length);
        for (int i = 0; i < n; i++)
        {
            Transform c = _cells[i];
            if (c == null) continue;
            Vector3 p = c.localPosition;
            float s = Mathf.Sin(time * _speed - p.z * _waveNumber);
            c.localPosition = new Vector3(p.x, _restY[i] + s * _amplitude, p.z);
            if (_mats != null && i < _mats.Length && _mats[i] != null)
            {
                // Square the 0..1 crest factor so only cells near the top of the ripple brighten; a
                // linear ramp would wash the whole floor toward the crest colour.
                float k = s * 0.5f + 0.5f;
                _mats[i].color = Color.Lerp(_trough, _crest, k * k);
            }
        }
    }
}
