using System.Collections.Generic;
using UnityEngine;

public enum TutorialZoneType
{
    TutorialSafeZone,
    EncounterDisabledZone,
    MonsterDisabledZone,
    SanityDrainZone,
    CorruptionDemoZone,
    LightSpotZone,
    MonsterRevealZone,
    SprintDemoZone,
    TutorialExitZone
}

public class TutorialZoneMarker : MonoBehaviour
{
    public TutorialZoneType zoneType;
    public Bounds worldBounds;
}

public class TutorialLayoutContext
{
    public Transform playerSpawnPoint;
    public SoundboardPickup soundboardPickup;
    public GameObject soundboardGate;
    public GameObject soundboardUseDoor;
    public GameObject corruptionDoor;
    public GameObject lightGate;
    public GameObject chaseGate;
    public GameObject sprintDoor;
    public GameObject corruptionDemoTrigger;
    public SafeSpaceZone firstLightSpot;
    public Transform monsterRevealPoint;
    public Transform monsterSpawnPoint;
    public SafeSpaceZone hideLightSpot;
    public GameObject sprintRiskTrigger;
    public GameObject tutorialExitGate;
    public Transform mainMazeConnector;
    public readonly List<Vector3> generatedCells = new List<Vector3>();
    public readonly List<GameObject> generatedRooms = new List<GameObject>();
    public readonly List<TutorialZoneMarker> zoneMarkers = new List<TutorialZoneMarker>();

    public bool IsValid()
    {
        return playerSpawnPoint != null &&
            soundboardPickup != null &&
            soundboardGate != null &&
            soundboardUseDoor != null &&
            corruptionDoor != null &&
            lightGate != null &&
            chaseGate != null &&
            sprintDoor != null &&
            firstLightSpot != null &&
            hideLightSpot != null &&
            monsterRevealPoint != null &&
            monsterSpawnPoint != null &&
            sprintRiskTrigger != null &&
            tutorialExitGate != null &&
            mainMazeConnector != null;
    }
}
