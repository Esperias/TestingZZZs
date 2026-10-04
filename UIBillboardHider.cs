using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hides all world-space billboard canvases (pickup prompts, cave signs, resource labels)
/// while a UI panel (inventory, map) is open, then restores them on close.
/// Call SetHidden(true) on open and SetHidden(false) on close.
/// </summary>
public static class UIBillboardHider
{
    private static readonly List<GameObject> hiddenCanvases = new List<GameObject>();
    private static readonly List<PickupBillboard> hiddenPickups = new List<PickupBillboard>();
    private static readonly List<CaveBillboard> hiddenCaves = new List<CaveBillboard>();
    private static readonly List<ResourceBillboard> hiddenResources = new List<ResourceBillboard>();
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStaticsForFastPlayMode() { hiddenCanvases.Clear(); hiddenPickups.Clear(); hiddenCaves.Clear(); hiddenResources.Clear(); }


    public static void SetHidden(bool hide)
    {
        if (hide)
        {
            hiddenCanvases.Clear();
            hiddenPickups.Clear();
            hiddenCaves.Clear();
            hiddenResources.Clear();

            // Hide PickupBillboard components (suppresses LateUpdate re-show logic)
            PickupBillboard[] pickups = Object.FindObjectsByType<PickupBillboard>(FindObjectsInactive.Exclude);
            foreach (var pb in pickups)
            {
                pb.HideBillboard();
                hiddenPickups.Add(pb);
            }

            // Hide CaveBillboard components
            CaveBillboard[] caves = Object.FindObjectsByType<CaveBillboard>(FindObjectsInactive.Exclude);
            foreach (var cb in caves)
            {
                cb.HideBillboard();
                hiddenCaves.Add(cb);
            }


            // Hide ResourceBillboard components
            ResourceBillboard[] resources = Object.FindObjectsByType<ResourceBillboard>(FindObjectsInactive.Exclude);
            foreach (var rb in resources)
            {
                rb.HideBillboard();
                hiddenResources.Add(rb);
            }

            // Also hide any WorldSpace billboard canvases by name (belt-and-suspenders)
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (var canvas in allCanvases)
            {
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                string n = canvas.gameObject.name;
                if (n.Contains("Billboard") || n.Contains("billboard"))
                {
                    hiddenCanvases.Add(canvas.gameObject);
                    canvas.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // Restore PickupBillboards (LateUpdate will re-evaluate distance)
            foreach (var pb in hiddenPickups)
                if (pb != null) pb.ShowBillboard();

            // Restore CaveBillboards
            foreach (var cb in hiddenCaves)
                if (cb != null) cb.ShowBillboard();


            // Restore ResourceBillboards
            foreach (var rb in hiddenResources)
                if (rb != null) rb.ShowBillboard();

            // Restore any extra hidden canvases
            foreach (var go in hiddenCanvases)
                if (go != null) go.SetActive(true);

            hiddenCanvases.Clear();
            hiddenPickups.Clear();
            hiddenCaves.Clear();
            hiddenResources.Clear();
        }
    }
}




