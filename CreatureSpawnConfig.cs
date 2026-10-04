using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "SpawnConfig", menuName = "Creature System/Spawn Config")]
public class CreatureSpawnConfig : ScriptableObject
{
    public string speciesName;
    public GameObject prefab;
    public int globalCap = 50;
    public int perChunkCap = 3;
    public float spawnWeight = 1f;
    public bool isFlying = false; // For ravens
}




