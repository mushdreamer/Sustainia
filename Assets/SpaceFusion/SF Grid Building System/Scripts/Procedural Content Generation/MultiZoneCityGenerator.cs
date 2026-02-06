using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

using CoreBuildingType = SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType;

public class MultiZoneCityGenerator : MonoBehaviour
{
    public static MultiZoneCityGenerator Instance;

    [System.Serializable]
    public class GenerationZone
    {
        public string zoneName = "Area 1";
        public Transform originPoint;
        public int width = 5;
        public int height = 5;
        public float visualHeightOffset = 0.0f;
        public bool isOccupied = false;

        // ½Ì³Ì¸ßÁÁÂß¼­
        [HideInInspector] public bool isTutorialHighlight = false;
        [HideInInspector] public Color customHighlightColor;

        [HideInInspector] public LineRenderer groundOutline;
        [HideInInspector] public TextMesh statusText;
        [HideInInspector] public Transform arrowObj;
        [HideInInspector] public Material instanceLineMat;

        public bool Contains(Vector3 worldPos, float cellSize)
        {
            if (originPoint == null) return false;
            float diffX = worldPos.x - originPoint.position.x;
            float diffZ = worldPos.z - originPoint.position.z;
            return diffX >= 0 && diffX < width * cellSize && diffZ >= 0 && diffZ < height * cellSize;
        }
    }

    [System.Serializable]
    public struct BuildingType { public string name; public GameObject prefab; public Placeable data; }

    [Header("Global Settings")]
    public float cellSize = 10.0f;
    public List<BuildingType> buildingOptions;
    public float buildingYOffset = 0.0f;

    [Header("Optimization Objectives")]
    public float targetCo2 = 50.0f;
    public float targetCost = 1000.0f;
    public float targetEnergy = 100.0f;
    public float weightCo2 = 1.0f;
    public float weightCost = 1.0f;
    public float weightEnergy = 1.5f;

    [Header("Visual Feedback")]
    public Color validColor = Color.green;
    public Color occupiedColor = new Color(1f, 0.6f, 0f);
    public float lineWidth = 0.05f;
    public float lineYOffset = 0.1f;
    public float baseUiHeight = 12.0f;
    public float animSpeed = 2.0f;
    public int circleSegments = 128;

    public bool autoStartGeneration = false;
    private string _csvFilePath;
    private float _currentCo2, _currentCost, _currentEnergy;
    private int _stepCount;

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    [Header("Special Buildings (Tutorial Only)")]
    [Tooltip("ÕâÐ©½¨Öþ²»»á³öÏÖÔÚÍæ¼Ò½¨ÔìÀ¸£¬½ö¹©½ÌÑ§ PCG Éú³É")]
    public List<BuildingType> specialBuildingOptions;

    private void Awake() { if (Instance != null) Destroy(gameObject); Instance = this; }

    IEnumerator Start()
    {
        InitZoneVisuals();
        yield return null;
        _csvFilePath = Path.Combine(Application.dataPath, $"TrainingData_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        InitCSV();
        if (autoStartGeneration) BeginGeneration();
    }

    public void BeginGeneration() { StopAllCoroutines(); StartCoroutine(GenerateZonesSequence()); }

    private void Update() => AnimateVisuals();

    public bool IsZoneValidAndEmpty(Vector3 worldPos) { var z = GetZoneAtPosition(worldPos); return z != null && !z.isOccupied; }
    public bool IsZoneAvailableForBuilding(Vector3 worldPos) => IsZoneValidAndEmpty(worldPos);
    public void SetZoneOccupiedStatus(Vector3 worldPos, bool occupied) { var z = GetZoneAtPosition(worldPos); if (z != null) z.isOccupied = occupied; }
    public void SetZoneOccupiedState(Vector3 worldPos, bool occupied) => SetZoneOccupiedStatus(worldPos, occupied);

    private GenerationZone GetZoneAtPosition(Vector3 worldPos)
    {
        foreach (var zone in zones) if (zone.Contains(worldPos, cellSize)) return zone;
        return null;
    }

    // --- ÐÞ¸ÄÖØµã£ºÈ·±£½¨ÖþÉú³ÉÔÚÖÐÐÄ²¢ÕýÈ·×¢²á ---
    public void ForceSpawnBuildingInZone(int zoneIndex, string buildingName)
    {
        if (zoneIndex < 0 || zoneIndex >= zones.Count) return;

        // ¸ÄÓÃÃû³Æ²éÕÒ
        var opt = buildingOptions.Find(b => b.data.name == buildingName);
        if (opt.prefab == null)
            opt = specialBuildingOptions.Find(b => b.data.name == buildingName);

        if (opt.prefab != null)
        {
            var zone = zones[zoneIndex];

            // 1. ÇåÀí¸ÃÇøÓòÔ­ÓÐµÄËùÓÐ½¨Öþ
            foreach (Transform child in zone.originPoint)
                if (child.name != "RingOutline" && child.name != "StatusLabel" && child.name != "ArrowIndicator")
                    Destroy(child.gameObject);

            // 2. ¼ÆËãÉú³ÉÎ»ÖÃ£º±ØÐëÊÇ OriginPoint µÄ±¾µØÖÐÐÄ£¬²¢¼ÓÉÏÆ«ÒÆ
            Vector3 spawnPos = GetZoneCenter(zone) + Vector3.up * buildingYOffset;
            GameObject b = Instantiate(opt.prefab, spawnPos, Quaternion.identity, zone.originPoint);

            // 3. ºËÐÄ³õÊ¼»¯£ºÓÉÓÚÊÇÇ¿ÖÆÉú³É£¬±ØÐëÊÖ¶¯µ÷ÓÃ³õÊ¼»¯
            AttachAndInitialize(b, opt.data, spawnPos);

            // 4. ÅÐ¶ÏÂß¼­½Å±¾¼¤»î
            var normalEffect = b.GetComponent<BuildingEffect>();
            var tutorialEffect = b.GetComponent<TutorialBuildingEffect>();

            if (normalEffect != null) normalEffect.ApplyEffect();
            if (tutorialEffect != null) tutorialEffect.ApplyTutorialEffect();

            zone.isOccupied = true;
            Debug.Log($"[PCG] ÒÑÔÚ {zone.zoneName} Éú³É½¨Öþ: {buildingName}");
        }
        else
        {
            Debug.LogError($"[PCG] ÎÞ·¨ÕÒµ½ÃûÎª {buildingName} µÄ½¨ÖþÅäÖÃ£¬Çë¼ì²é Inspector£¡");
        }
    }

    private void AnimateVisuals()
    {
        if (zones == null) return;
        float timeVar = Time.unscaledTime;
        float bounceY = Mathf.Sin(timeVar * animSpeed) * 1.5f;

        foreach (var zone in zones)
        {
            Color targetColor = zone.isTutorialHighlight ? zone.customHighlightColor : (zone.isOccupied ? occupiedColor : validColor);
            float finalHeight = baseUiHeight + zone.visualHeightOffset;
            Vector3 center = GetZoneCenter(zone);

            if (zone.arrowObj != null)
            {
                zone.arrowObj.position = center + Vector3.up * (finalHeight + bounceY);
                foreach (var r in zone.arrowObj.GetComponentsInChildren<Renderer>()) r.material.color = targetColor;
            }

            if (zone.statusText != null)
            {
                if (Camera.main != null) zone.statusText.transform.rotation = Quaternion.LookRotation(zone.statusText.transform.position - Camera.main.transform.position);
                zone.statusText.transform.position = center + Vector3.up * (finalHeight - 2.5f);
                zone.statusText.color = targetColor;
                zone.statusText.text = zone.isOccupied ? "FIXED" : "OPEN";
            }

            if (zone.instanceLineMat != null)
            {
                zone.instanceLineMat.color = targetColor;
                UpdateCirclePositions(zone);
            }
        }
    }

    private void UpdateCirclePositions(GenerationZone zone)
    {
        if (zone.groundOutline == null) return;
        zone.groundOutline.startWidth = lineWidth;
        zone.groundOutline.endWidth = lineWidth;
        float rx = zone.width * cellSize * 0.5f;
        float rz = zone.height * cellSize * 0.5f;
        zone.groundOutline.positionCount = circleSegments + 1;
        for (int i = 0; i <= circleSegments; i++)
        {
            float a = (float)i / circleSegments * Mathf.PI * 2f;
            zone.groundOutline.SetPosition(i, new Vector3(rx + Mathf.Cos(a) * rx, lineYOffset, rz + Mathf.Sin(a) * rz));
        }
    }

    private void InitZoneVisuals()
    {
        foreach (var zone in zones)
        {
            if (zone.originPoint == null) continue;
            GameObject container = new GameObject($"{zone.zoneName}_Visuals");
            container.transform.SetParent(this.transform);

            GameObject lineObj = new GameObject("RingOutline");
            lineObj.transform.SetParent(container.transform);
            lineObj.transform.position = zone.originPoint.position;
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            zone.instanceLineMat = lr.material; zone.groundOutline = lr;
            UpdateCirclePositions(zone);

            GameObject textObj = new GameObject("StatusLabel");
            textObj.transform.SetParent(container.transform);
            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter; tm.fontSize = 20;
            zone.statusText = tm;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "ArrowIndicator"; Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(container.transform);
            marker.transform.localScale = Vector3.one * 2f;
            zone.arrowObj = marker.transform;
            marker.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    private Vector3 GetZoneCenter(GenerationZone zone) => zone.originPoint.position + new Vector3(zone.width * cellSize / 2f, 0, zone.height * cellSize / 2f);

    public void ClearAndRestartGeneration()
    {
        foreach (var zone in zones) { zone.isOccupied = false; foreach (Transform child in zone.originPoint) Destroy(child.gameObject); }
        _currentCo2 = _currentCost = _currentEnergy = 0; _stepCount = 0;
        StartCoroutine(GenerateZonesSequence());
    }

    IEnumerator GenerateZonesSequence()
    {
        List<int> available = new List<int>();
        for (int i = 0; i < zones.Count; i++) available.Add(i);
        float currentError = CalculateWeightedError(_currentCo2, _currentCost, _currentEnergy);
        while (available.Count > 0)
        {
            int idx = available[Random.Range(0, available.Count)];
            var type = buildingOptions[Random.Range(0, buildingOptions.Count)];
            var stats = GetBuildingStats(type.prefab);
            if (CalculateWeightedError(_currentCo2 + stats.co2, _currentCost + stats.cost, _currentEnergy + stats.energy) < currentError)
            {
                GenerateOneZone(zones[idx], type);
                zones[idx].isOccupied = true;
                _currentCo2 += stats.co2; _currentCost += stats.cost; _currentEnergy += stats.energy;
                _stepCount++;
                available.Remove(idx);
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    private float CalculateWeightedError(float c, float m, float e) => (Mathf.Abs(c - targetCo2) * weightCo2) + (Mathf.Abs(m - targetCost) * weightCost) + (Mathf.Abs(e - targetEnergy) * weightEnergy);

    private void InitCSV() { if (!string.IsNullOrEmpty(_csvFilePath)) File.WriteAllText(_csvFilePath, "Step,Zone,Building,Co2,Cost,Energy\n"); }

    struct BuildingStats { public float co2, cost, energy; }
    private BuildingStats GetBuildingStats(GameObject p)
    {
        BuildingStats s = new BuildingStats();
        BuildingEffect e = p.GetComponent<BuildingEffect>();
        if (e != null)
        {
            // 读你新改的名字，确保 PCG 算法逻辑正确
            s.co2 = e.co2Change;
            s.energy = e.electricityChange;
            s.cost = 100f;
        }
        return s;
    }

    void GenerateOneZone(GenerationZone zone, BuildingType type)
    {
        Vector3 spawnPos = GetZoneCenter(zone) + Vector3.up * buildingYOffset;
        GameObject b = Instantiate(type.prefab, spawnPos, Quaternion.identity, zone.originPoint);
        AttachAndInitialize(b, type.data, spawnPos);
    }

    void AttachAndInitialize(GameObject building, Placeable data, Vector3 pos)
    {
        PlacedObject po = building.GetComponent<PlacedObject>() ?? building.AddComponent<PlacedObject>();
        if (GameManager.Instance != null && GameManager.Instance.PlacementGrid != null)
        {
            Vector3Int gPos = GameManager.Instance.PlacementGrid.WorldToCell(pos);
            po.Initialize(data, gPos);
            // ±ØÐëÏò PlacementSystem ×¢²á£¬·ñÔò½¨ÖþÂß¼­²»»áÉúÐ§
            PlacementSystem.Instance?.RegisterExternalObject(building, data, gPos);
        }
    }
}