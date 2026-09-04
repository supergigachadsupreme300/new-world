using System.Collections.Generic;
using UnityEngine;

public class GoblinPet : MonoBehaviour
{
    public enum CommandMode { Follow, Stay, GoHome }

    public static GoblinPet Instance;

    public float FollowSpeed = 2.2f;
    public float FollowDistance = 2.4f;
    public float LateralOffset = 1.2f;
    public float PlantReach = 1.6f;
    public int MaxHealth = 30;
    public float RetryDelay = 4f;
    public float PlantSearchRadius = 25f;

    [SerializeField] private Transform _player;
    private GameManager _gm;
    private Transform _modelRoot;
    private Transform _upperTorso;
    private Transform _midTorso;
    private Transform _lowerTorso;
    private Transform _armL;
    private Transform _armR;
    private Transform _legL;
    private Transform _legR;
    private Transform _kneeL;
    private Transform _kneeR;
    private SphereCollider _collider;
    private Rigidbody _rb;

    private int _health;
    private bool _isDead;
    private bool _isHiding;
    private bool _isMoving;
    private float _retryTimer;
    private float _bobTimer;
    private float _walkCycle;

    private string _heldSeedType;
    private string _carriedCrop;
    private readonly Dictionary<string, int> _storage = new Dictionary<string, int>();

    private enum GoblinTask { None, Plant, Water, Harvest, Deposit }
    private GoblinTask _task = GoblinTask.None;
    private WorldBuilder.FieldState _taskField;
    private Transform _chestTransform;
    private float _workRetry;

    private readonly List<Vector3> _waypoints = new List<Vector3>();
    private int _waypointIndex;
    private bool _pathBuilt;
    private Vector3 _pathDest;

    private const float ArriveDist = 0.45f;
    private const float WorkReach = 1.7f;
    private const float DepositReach = 2.0f;

    [System.Serializable]
    public class GoblinStorageSaveItem
    {
        public string type;
        public int count;
    }

    public bool IsDead => _isDead;
    public bool IsHiddenInHut => _isHiding;
    public bool IsHoldingSeed => !string.IsNullOrEmpty(_heldSeedType);
    public bool IsCarryingCrop => !string.IsNullOrEmpty(_carriedCrop);
    public bool CanAcceptSeed => !_isDead && string.IsNullOrEmpty(_heldSeedType) && string.IsNullOrEmpty(_carriedCrop);
    public int Health => _health;
    public string HeldSeedType => _heldSeedType;
    public string CarriedCrop => _carriedCrop;
    public CommandMode Command { get; private set; } = CommandMode.Follow;

    public void SetCommand(CommandMode mode)
    {
        Command = mode;
        _plantTarget = null;
        _task = GoblinTask.None;
        _taskField = null;
        _pathBuilt = false;
    }

    private WorldBuilder.FieldState _plantTarget;

    private void Awake()
    {
        Instance = this;
        if (_player == null)
            _player = Object.FindAnyObjectByType<PlayerController>()?.transform;
        _health = MaxHealth;

        _modelRoot = GoblinModelBuilder.BuildGoblin(transform);
        _upperTorso = _modelRoot.Find("UpperTorso");
        _midTorso = _modelRoot.Find("MidTorso");
        _lowerTorso = _modelRoot.Find("LowerTorso");
        _armL = _modelRoot.Find("ArmL");
        _armR = _modelRoot.Find("ArmR");
        _legL = _modelRoot.Find("LegL");
        _legR = _modelRoot.Find("LegR");
        _kneeL = _legL != null ? _legL.Find("KneeL") : null;
        _kneeR = _legR != null ? _legR.Find("KneeR") : null;

        _collider = gameObject.AddComponent<SphereCollider>();
        _collider.radius = 0.4f;
        _collider.center = new Vector3(0f, 0.4f, 0f);

        _rb = gameObject.AddComponent<Rigidbody>();
        _rb.useGravity = true;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        var pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc != null && pc.GetComponent<CharacterController>() != null)
            Physics.IgnoreCollision(_collider, pc.GetComponent<CharacterController>());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool GiveSeed(string cropType)
    {
        if (string.IsNullOrEmpty(cropType)) return false;
        if (_isDead) return false;
        if (!string.IsNullOrEmpty(_heldSeedType)) return false;
        if (!string.IsNullOrEmpty(_carriedCrop)) return false;

        _heldSeedType = cropType;
        _retryTimer = 0f;
        _plantTarget = null;
        _task = GoblinTask.None;
        _taskField = null;
        _pathBuilt = false;
        return true;
    }

    // ── Storage (the chest in front of the goblin hut) ──
    public int StorageCount(string cropType)
    {
        if (string.IsNullOrEmpty(cropType)) return 0;
        return _storage.TryGetValue(cropType, out int count) ? count : 0;
    }

    public bool AddToStorage(string cropType, int amount)
    {
        if (string.IsNullOrEmpty(cropType) || amount <= 0)
            return false;
        _storage[cropType] = StorageCount(cropType) + amount;
        return true;
    }

    public bool RemoveFromStorage(string cropType, int amount)
    {
        if (string.IsNullOrEmpty(cropType) || amount <= 0)
            return false;
        if (!_storage.ContainsKey(cropType))
            return false;
        int remaining = _storage[cropType] - amount;
        if (remaining <= 0)
            _storage.Remove(cropType);
        else
            _storage[cropType] = remaining;
        return true;
    }

    public IEnumerable<KeyValuePair<string, int>> GetAllStored()
        => _storage;

    public List<GoblinStorageSaveItem> GetStorageSaveItems()
    {
        var result = new List<GoblinStorageSaveItem>();
        foreach (var kv in _storage)
            result.Add(new GoblinStorageSaveItem { type = kv.Key, count = kv.Value });
        return result;
    }

    public void LoadStorageSaveItems(List<GoblinStorageSaveItem> items)
    {
        _storage.Clear();
        if (items == null)
            return;
        foreach (var it in items)
        {
            if (it != null && !string.IsNullOrEmpty(it.type) && it.count > 0)
                _storage[it.type] = it.count;
        }
    }

    public void LoadHeldSave(string seed, string carried)
    {
        _heldSeedType = seed;
        _carriedCrop = carried;
    }

    public void TakeDamage(int amount)
    {
        if (_isDead) return;

        _health -= amount;
        if (_health <= 0)
            Die();
    }

    private void Die()
    {
        _isDead = true;
        _isHiding = false;
        _isMoving = false;
        _heldSeedType = null;
        _carriedCrop = null;
        _plantTarget = null;
        _task = GoblinTask.None;
        _taskField = null;
        _pathBuilt = false;

        if (_collider != null)
            _collider.enabled = false;
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
        }
        if (_modelRoot != null)
        {
            _modelRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _modelRoot.localPosition = new Vector3(0f, -0.15f, 0f);
        }
    }

    private void Revive()
    {
        _isDead = false;
        _health = MaxHealth;

        if (_collider != null)
            _collider.enabled = true;
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
        }
        if (_modelRoot != null)
        {
            _modelRoot.localRotation = Quaternion.identity;
            _modelRoot.localPosition = Vector3.zero;
        }

        _retryTimer = 0f;
        _task = GoblinTask.None;
        _taskField = null;
        _pathBuilt = false;
    }

    private static bool IsNight()
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        float hour = gm.TimeOfDay;
        return hour >= 18f || hour < 6f;
    }

    private void Update()
    {
        if (_gm == null) _gm = GameManager.Instance;
        if (_gm != null && _gm.GamePaused) return;

        if (_player == null)
        {
            var pc = GameManager.Instance?.Player;
            _player = pc != null ? pc.transform : Object.FindAnyObjectByType<PlayerController>()?.transform;
        }
        if (_player == null)
            return;

        _isMoving = false;

        if (_isDead)
        {
            if (!IsNight())
                Revive();
            return;
        }

        if (IsNight())
        {
            HandleHide();
        }
        else
        {
            if (_isHiding)
                _isHiding = false;
            HandleDayCommand();
        }

        if (!_isMoving && _rb != null)
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);

        Animate();
    }

    private void HandleHide()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null)
        {
            _isHiding = false;
            HandleFollow();
            return;
        }

        Vector3? hutPos = FindHutPosition(wb);
        if (!hutPos.HasValue)
        {
            _isHiding = false;
            HandleFollow();
            return;
        }

        Vector3 target = hutPos.Value + new Vector3(0f, 0f, 2.2f);
        target.y = transform.position.y;

        if (Vector3.Distance(transform.position, target) <= 0.7f
            || (_waypoints.Count > 0
                && Vector3.Distance(transform.position, _waypoints[_waypoints.Count - 1]) <= ArriveDist))
        {
            _isHiding = true;
            _pathBuilt = false;
            return;
        }

        GoToWaypoint(target);
    }

    private Vector3? FindHutPosition(WorldBuilder wb)
    {
        Vector3? best = null;
        float bestDist = float.MaxValue;
        foreach (var b in wb.GetAllBuildings())
        {
            if (b == null) continue;
            if (b.Type != "goblin_hut") continue;
            if (b.Entity == null) continue;
            float d = Vector3.Distance(transform.position, b.Position);
            if (d < bestDist)
            {
                bestDist = d;
                best = b.Position;
            }
        }
        return best;
    }

    private void HandlePlant()
    {
        if (_plantTarget == null || !IsUsableField(_plantTarget))
        {
            _plantTarget = null;
            _retryTimer -= Time.deltaTime;
            if (_retryTimer <= 0f)
            {
                _plantTarget = FindNearestEmptyField();
                _retryTimer = RetryDelay;
            }
            if (_plantTarget == null)
            {
                HandleFollow();
                return;
            }
        }

        Vector3 fieldPos = _plantTarget.FieldObject != null
            ? _plantTarget.FieldObject.transform.position
            : transform.position;
        fieldPos.y = transform.position.y;

        if (HorizontalDistance(transform.position, fieldPos) <= PlantReach)
        {
            var wb = WorldBuilder.Instance;
            bool planted = wb != null && wb.PlantCrop(_plantTarget, _heldSeedType);
            if (planted)
            {
                _heldSeedType = null;
                _plantTarget = null;
                _retryTimer = 0f;
                _pathBuilt = false;
                _workRetry = 2f;
                GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Goblin đã gieo hạt giống giúp bạn!"), 2f);
                HandleFollow();
                return;
            }

            _plantTarget = null;
            return;
        }

        GoToWaypoint(fieldPos);
    }

    private static bool IsUsableField(WorldBuilder.FieldState field)
    {
        if (field == null) return false;
        if (field.FieldObject == null) return false;
        return field.Tilled && !field.HasCrop;
    }

    private WorldBuilder.FieldState FindNearestEmptyField()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return null;

        WorldBuilder.FieldState best = null;
        float bestDist = float.MaxValue;
        foreach (var field in wb.GetAllFields())
        {
            if (!IsUsableField(field)) continue;
            Vector3 p = field.FieldObject.transform.position;
            float d = Vector3.Distance(transform.position, p);
            if (d > PlantSearchRadius) continue;
            if (d < bestDist)
            {
                bestDist = d;
                best = field;
            }
        }
        return best;
    }

    private void HandleDayCommand()
    {
        if (!string.IsNullOrEmpty(_carriedCrop))
        {
            HandleDeposit();
            return;
        }
        if (!string.IsNullOrEmpty(_heldSeedType))
        {
            HandlePlant();
            return;
        }
        switch (Command)
        {
            case CommandMode.GoHome:
                HandleGoHome();
                return;
            case CommandMode.Stay:
                if (_task == GoblinTask.Harvest || _task == GoblinTask.Water)
                    HandleFarmTask();
                return;
            default:
                if (_task == GoblinTask.Harvest || _task == GoblinTask.Water)
                    HandleFarmTask();
                else
                    HandleWorkSearch();
                return;
        }
    }

    private void HandleGoHome()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null)
        {
            HandleFollow();
            return;
        }

        Vector3? hutPos = FindHutPosition(wb);
        if (!hutPos.HasValue)
        {
            HandleFollow();
            return;
        }

        Vector3 target = hutPos.Value + new Vector3(0f, 0f, 2.2f);
        target.y = transform.position.y;

        if (Vector3.Distance(transform.position, target) <= 0.7f
            || (_waypoints.Count > 0
                && Vector3.Distance(transform.position, _waypoints[_waypoints.Count - 1]) <= ArriveDist))
        {
            _pathBuilt = false;
            return;
        }

        GoToWaypoint(target);
    }

    private void HandleWorkSearch()
    {
        if (_workRetry > 0f)
        {
            _workRetry -= Time.deltaTime;
            HandleFollow();
            return;
        }

        var wb = WorldBuilder.Instance;
        if (wb == null)
        {
            HandleFollow();
            return;
        }

        var harvestField = FindNearestHarvestField();
        if (harvestField != null)
        {
            _task = GoblinTask.Harvest;
            _taskField = harvestField;
            _pathBuilt = false;
            return;
        }

        var waterField = FindNearestWaterField();
        if (waterField != null)
        {
            _task = GoblinTask.Water;
            _taskField = waterField;
            _pathBuilt = false;
            return;
        }

        HandleFollow();
    }

    private void HandleFarmTask()
    {
        if (_taskField == null || _taskField.FieldObject == null || _taskField.IsHarvested)
        {
            _task = GoblinTask.None;
            _taskField = null;
            _pathBuilt = false;
            return;
        }

        Vector3 fieldPos = _taskField.FieldObject.transform.position;
        if (HorizontalDistance(transform.position, fieldPos) <= WorkReach)
        {
            bool ok = _task == GoblinTask.Harvest ? TryHarvest() : TryWater();
            _task = GoblinTask.None;
            _taskField = null;
            _pathBuilt = false;
            if (ok)
                _workRetry = 2f;
            return;
        }

        GoToWaypoint(fieldPos);
    }

    private bool TryHarvest()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null || _taskField == null)
            return false;
        if (wb.HarvestField(_taskField, out var crop))
        {
            _carriedCrop = crop;
            GameManager.Instance?.UIManager?.ShowMessage(
                Localization.F("Goblin đã thu hoạch {0}!", Localization.ItemName(crop)), 2f);
            return true;
        }
        return false;
    }

    private bool TryWater()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null || _taskField == null || _taskField.FieldObject == null)
            return false;
        if (wb.WaterField(_taskField.FieldObject.transform.position))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Goblin đã tưới cây!"), 2f);
            return true;
        }
        return false;
    }

    private void HandleDeposit()
    {
        var chest = FindChest();
        if (chest == null)
        {
            _workRetry -= Time.deltaTime;
            HandleFollow();
            return;
        }

        float d = HorizontalDistance(transform.position, chest.position);
        if (d <= DepositReach)
        {
            string crop = _carriedCrop;
            if (AddToStorage(crop, 1))
            {
                _carriedCrop = null;
                _pathBuilt = false;
                _workRetry = 2.5f;
                GameManager.Instance?.UIManager?.ShowMessage(
                    Localization.F("Goblin đã bỏ {0} vào kho.", Localization.ItemName(crop)), 2f);
            }
            return;
        }

        GoToWaypoint(chest.position);
    }

    private Transform FindChest()
    {
        if (_chestTransform == null)
        {
            var go = GameObject.Find("GoblinChest");
            if (go != null)
                _chestTransform = go.transform;
        }
        return _chestTransform;
    }

    private WorldBuilder.FieldState FindNearestHarvestField()
    {
        return FindNearestUsableField(f => f.HasCrop && !f.IsHarvested && f.Stage >= 4);
    }

    private WorldBuilder.FieldState FindNearestWaterField()
    {
        return FindNearestUsableField(f => f.HasCrop && !f.IsHarvested && f.Stage < 4 && !f.Watered);
    }

    private WorldBuilder.FieldState FindNearestUsableField(System.Func<WorldBuilder.FieldState, bool> predicate)
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return null;

        WorldBuilder.FieldState best = null;
        float bestDist = float.MaxValue;
        foreach (var field in wb.GetAllFields())
        {
            if (field == null || field.FieldObject == null) continue;
            if (!field.Tilled) continue;
            if (!predicate(field)) continue;
            Vector3 p = field.FieldObject.transform.position;
            float d = Vector3.Distance(transform.position, p);
            if (d > PlantSearchRadius) continue;
            if (d < bestDist)
            {
                bestDist = d;
                best = field;
            }
        }
        return best;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 da = new Vector3(a.x, 0f, a.z);
        Vector3 db = new Vector3(b.x, 0f, b.z);
        return Vector3.Distance(da, db);
    }

    private void BuildGoblinPath(Vector3 dest)
    {
        _waypoints.Clear();
        _waypointIndex = 0;
        _pathBuilt = true;
        _pathDest = dest;

        if (NavGrid.Instance == null)
        {
            _waypoints.Add(dest);
            return;
        }
        if (!NavGrid.Instance.FindPath(transform.position, dest, _waypoints))
        {
            if (NavGrid.Instance.NearestWalkable(dest, out Vector3 snapped)
                && NavGrid.Instance.FindPath(transform.position, snapped, _waypoints))
                return;
            _waypoints.Clear();
            _waypoints.Add(dest);
        }
        if (_waypoints.Count == 0)
            _waypoints.Add(dest);
    }

    private bool MoveAlongPath(float speed)
    {
        if (_waypointIndex >= _waypoints.Count)
            return true;

        Vector3 target = _waypoints[_waypointIndex];
        Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);
        Vector3 to = flatTarget - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist <= ArriveDist)
        {
            _waypointIndex++;
            return _waypointIndex >= _waypoints.Count;
        }
        if (dist < 0.0001f)
            return true;

        _rb.linearVelocity = new Vector3(to.normalized.x * speed, _rb.linearVelocity.y, to.normalized.z * speed);
        transform.rotation = Quaternion.LookRotation(to.normalized);
        _isMoving = true;
        return false;
    }

    private void GoToWaypoint(Vector3 dest)
    {
        if (!_pathBuilt || HorizontalDistance(_pathDest, dest) > 0.01f)
        {
            BuildGoblinPath(dest);
            return;
        }
        if (MoveAlongPath(FollowSpeed))
            _pathBuilt = false;
    }

    private void HandleFollow()
    {
        Vector3 targetPos = _player.position - _player.forward * FollowDistance + _player.right * LateralOffset;
        targetPos.y = transform.position.y;

        if (Vector3.Distance(transform.position, targetPos) <= 0.2f)
            return;

        MoveToward(targetPos);
    }

    private void MoveToward(Vector3 target)
    {
        Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);
        Vector3 dir = flatTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        _rb.linearVelocity = new Vector3(dir.normalized.x * FollowSpeed, _rb.linearVelocity.y, dir.normalized.z * FollowSpeed);
        transform.rotation = Quaternion.LookRotation(dir.normalized);
        _isMoving = true;
    }

    private void Animate()
    {
        if (_modelRoot == null) return;

        if (_isMoving)
        {
            _walkCycle += Time.deltaTime * FollowSpeed * 7f;
            float sin = Mathf.Sin(_walkCycle);
            float cos = Mathf.Cos(_walkCycle);

            if (_legL != null) _legL.localRotation = Quaternion.Euler(sin * 32f, 0f, 0f);
            if (_legR != null) _legR.localRotation = Quaternion.Euler(-sin * 32f, 0f, 0f);

            float kneeBendL = Mathf.Max(0f, -sin) * 38f;
            float kneeBendR = Mathf.Max(0f, sin) * 38f;
            if (_kneeL != null) _kneeL.localRotation = Quaternion.Euler(-kneeBendL, 0f, 0f);
            if (_kneeR != null) _kneeR.localRotation = Quaternion.Euler(-kneeBendR, 0f, 0f);

            float armSwing = sin * 24f;
            if (_armL != null) _armL.localRotation = Quaternion.Euler(-armSwing, 0f, 0f);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(armSwing, 0f, 0f);

            _bobTimer += Time.deltaTime * FollowSpeed * 5f;
            float bob = Mathf.Sin(_bobTimer * 2f) * 0.015f;
            if (_upperTorso != null) _upperTorso.localRotation = Quaternion.Euler(12f, 0f, 0f);
            if (_midTorso != null) _midTorso.localRotation = Quaternion.Euler(8f, 0f, 0f);
            if (_lowerTorso != null) _lowerTorso.localRotation = Quaternion.Euler(5f, 0f, 0f);
            _modelRoot.localPosition = new Vector3(0f, bob, 0f);
            _modelRoot.localRotation = Quaternion.identity;
        }
        else
        {
            _bobTimer += Time.deltaTime * 1.5f;
            float bob = Mathf.Sin(_bobTimer) * 0.008f;
            float breath = Mathf.Sin(_bobTimer * 0.8f) * 1.5f;
            if (_upperTorso != null) _upperTorso.localRotation = Quaternion.Euler(12f + breath, 0f, 0f);
            if (_midTorso != null) _midTorso.localRotation = Quaternion.Euler(8f + breath * 0.6f, 0f, 0f);
            if (_lowerTorso != null) _lowerTorso.localRotation = Quaternion.Euler(5f + breath * 0.3f, 0f, 0f);
            _modelRoot.localPosition = new Vector3(0f, bob, 0f);
            _modelRoot.localRotation = Quaternion.identity;

            ResetJoints();
        }
    }

    private void ResetJoints()
    {
        if (_legL != null) _legL.localRotation = Quaternion.identity;
        if (_legR != null) _legR.localRotation = Quaternion.identity;
        if (_kneeL != null) _kneeL.localRotation = Quaternion.identity;
        if (_kneeR != null) _kneeR.localRotation = Quaternion.identity;
        if (_armL != null) _armL.localRotation = Quaternion.identity;
        if (_armR != null) _armR.localRotation = Quaternion.identity;
    }
}
