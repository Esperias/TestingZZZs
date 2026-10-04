using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ThreatIndicatorManager : MonoBehaviour
{
    private class ThreatBlip
    {
        public Animal target;
        public GameObject uiObject;
        public RectTransform rect;
        public Image icon;
        public CanvasGroup group;
    }

    private RectTransform centerPoint;
    private List<ThreatBlip> blips = new List<ThreatBlip>();
    private float indicatorRadius = 150f;
    private float maxThreatDistance = 30f;

    // Cached references — never call Camera.main or FindObjects in Update
    private Camera cachedCam;
    private float scanInterval = 0.1f; // Scan for animals 10x/sec, not 60x/sec
    private float scanTimer = 0f;
    private Animal[] cachedAnimals = new Animal[0];

    private void Start()
    {
        cachedCam = Camera.main;

        GameObject canvasObj = GameObject.Find("HUD Canvas");
        if (canvasObj != null)
        {
            GameObject centerObj = new GameObject("Threat Center");
            centerObj.transform.SetParent(canvasObj.transform, false);
            centerPoint = centerObj.AddComponent<RectTransform>();
            centerPoint.anchoredPosition = Vector2.zero;
        }
    }

    private void Update()
    {
        if (centerPoint == null) return;
        if (cachedCam == null) cachedCam = Camera.main;

        // Only re-scan for animals every 100ms instead of every frame
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            cachedAnimals = FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        }

        HashSet<Animal> activeThreats = new HashSet<Animal>();

        foreach (Animal animal in cachedAnimals)
        {
            if (animal == null || animal.currentHealth <= 0) continue;

            float dist = Vector3.Distance(transform.position, animal.transform.position);

            if (dist < maxThreatDistance)
            {
                activeThreats.Add(animal);
                ThreatBlip blip = GetOrCreateBlip(animal);
                UpdateBlip(blip, animal, dist);
            }
        }

        // Clean up blips for animals that died or moved too far
        for (int i = blips.Count - 1; i >= 0; i--)
        {
            if (!activeThreats.Contains(blips[i].target))
            {
                Destroy(blips[i].uiObject);
                blips.RemoveAt(i);
            }
        }
    }

    private ThreatBlip GetOrCreateBlip(Animal animal)
    {
        foreach (var b in blips)
        {
            if (b.target == animal) return b;
        }

        ThreatBlip blip = new ThreatBlip();
        blip.target = animal;

        blip.uiObject = new GameObject("Threat Blip");
        blip.uiObject.transform.SetParent(centerPoint.transform, false);
        
        blip.rect = blip.uiObject.AddComponent<RectTransform>();
        blip.rect.sizeDelta = new Vector2(80, 40);
        
        Text txt = blip.uiObject.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 24;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.supportRichText = true;
        txt.text = "<color=#ff000088>(</color>  <color=#ffffff>!</color>  <color=#ff000088>)</color>";

        Outline outl = blip.uiObject.AddComponent<Outline>();
        outl.effectColor = new Color(0, 0, 0, 0.5f);
        outl.effectDistance = new Vector2(1, -1);

        blip.group = blip.uiObject.AddComponent<CanvasGroup>();

        blips.Add(blip);
        return blip;
    }

    private void UpdateBlip(ThreatBlip blip, Animal animal, float distance)
    {
        if (cachedCam == null) return;

        Vector3 dirToTarget = animal.transform.position - cachedCam.transform.position;
        dirToTarget.y = 0;
        dirToTarget.Normalize();

        Vector3 camForward = cachedCam.transform.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cachedCam.transform.right;
        camRight.y = 0;
        camRight.Normalize();

        float dotForward = Vector3.Dot(camForward, dirToTarget);
        float dotRight = Vector3.Dot(camRight, dirToTarget);
        float angleRad = Mathf.Atan2(dotRight, dotForward);

        float x = Mathf.Sin(angleRad) * indicatorRadius;
        float y = Mathf.Cos(angleRad) * indicatorRadius;

        blip.rect.anchoredPosition = new Vector2(x, y);

        float angleDeg = -angleRad * Mathf.Rad2Deg;
        blip.rect.localRotation = Quaternion.Euler(0, 0, angleDeg);

        float normalizedDist = 1f - (distance / maxThreatDistance);
        blip.group.alpha = Mathf.Clamp(normalizedDist * 2f, 0.1f, 1f);
    }
}




