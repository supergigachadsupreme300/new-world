/// <summary>
/// The 21 gear slots (game-design §5.4): 5 armor / 2 weapon / 14 accessory.
/// Order matters for display: Head, Body, Glove, Legging, Feet, LeftHand, RightHand,
/// then 10 Fingers, Necklace, 2 Ears, Belt.
/// </summary>
public enum EquipSlot
{
    Head = 0,
    Body = 1,
    Glove = 2,
    Legging = 3,
    Feet = 4,
    LeftHand = 5,
    RightHand = 6,
    Finger1 = 7,
    Finger2 = 8,
    Finger3 = 9,
    Finger4 = 10,
    Finger5 = 11,
    Finger6 = 12,
    Finger7 = 13,
    Finger8 = 14,
    Finger9 = 15,
    Finger10 = 16,
    Necklace = 17,
    Ear1 = 18,
    Ear2 = 19,
    Belt = 20,
}

/// <summary>Genre grouping over the 21 slots (§5.4).</summary>
public enum EquipGenre
{
    Armor = 0,
    Weapon = 1,
    Accessory = 2,
}

/// <summary>
/// A single gear definition (game-design §5.4). Data-only: armor pieces provide physical DR
/// and per-type elemental/magic resistance; accessories carry passive stat bonuses; weapons
/// provide damage. Programmatic roster lives in <see cref="GearCatalog"/>.
/// </summary>
public sealed class GearDef
{
    public string id;
    public string displayName;
    public EquipSlot Slot;
    public EquipGenre Genre;
    public float Weight;
    public float Defense;
    public float[] Resist = new float[10];
    public float[] StatBonus = new float[11];
    public float DamageBonus;
    public int BaseValue;
}