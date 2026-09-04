using UnityEngine;

public partial class ToolManager
{
    private void LaunchPalmProjectile(Camera cam)
    {
        var palmGo = new GameObject("PalmProjectile");
        palmGo.transform.position = cam.transform.position + cam.transform.forward * 0.8f;
        palmGo.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
        ItemBuilder.BuildPalm(palmGo.transform);

        var col = palmGo.AddComponent<BoxCollider>();
        col.size = new Vector3(0.8f, 0.6f, 0.3f);

        var rb = palmGo.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.mass = 0.2f;
        rb.linearVelocity = cam.transform.forward * PalmProjectileSpeed;

        palmGo.AddComponent<PalmProjectile>();

        SoundManager.Instance?.Play("pop");
    }

    private bool TryPlantSeed(string itemType, Vector3 hitPoint)
    {
        var seedToCrop = new System.Collections.Generic.Dictionary<string, string>
        {
            { "wheat_seed", "wheat" },
            { "corn_seed", "corn" },
            { "potato_seed", "potato" },
            { "carrot_seed", "carrot" },
            { "tomato_seed", "tomato" },
            { "strawberry_seed", "strawberry" },
            { "pumpkin_seed", "pumpkin" },
            { "onion_seed", "onion" },
            { "sugarcane_seed", "sugarcane" },
            { "rice_seed", "rice" },
            { "wheat", "wheat" },
            { "corn", "corn" },
            { "potato", "potato" },
            { "carrot", "carrot" },
            { "tomato", "tomato" },
            { "strawberry", "strawberry" },
            { "pumpkin", "pumpkin" },
            { "onion", "onion" },
            { "sugarcane", "sugarcane" },
            { "rice", "rice" },
        };

        if (!seedToCrop.TryGetValue(itemType, out var cropType))
            return false;

        var field = _worldBuilder.GetFieldAt(hitPoint);
        if (field != null && field.Tilled && !field.HasCrop)
        {
            if (_worldBuilder.PlantCrop(field, cropType))
            {
                RemoveItem(_selectedSlot, 1);
                SoundManager.Instance?.Play("pop");
                _uiManager.ShowMessage(Localization.F("Đã gieo {0}.", Localization.ItemName(cropType)), 1.5f);
            }
        }
        else
        {
            _uiManager.ShowMessage(Localization.T("Dùng hạt giống trên đất đã cày."), 1.5f);
        }
        return true;
    }
}
