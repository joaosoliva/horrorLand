using UnityEngine;

public static class TutorialMazeBlueprintFactory
{
    public static MazeBlueprintData CreateDefaultTutorialBlueprint()
    {
        MazeBlueprintData bp = new MazeBlueprintData();
        bp.width = 48;
        bp.height = 12;

        AddRect(bp, 4, 0, 6, 3, "TutorialStage_SoundboardPickup");
        AddRect(bp, 7, 1, 7, 2, "TutorialStage_SoundboardUse");
        AddRect(bp, 8, 1, 12, 1, "TutorialStage_CorruptionDemo");
        AddRect(bp, 13, 0, 16, 3, "TutorialStage_LightSpot");
        AddRect(bp, 17, 1, 24, 1, "TutorialStage_MonsterReveal");
        AddRect(bp, 25, 0, 28, 4, "TutorialStage_HideFromMonster");
        AddRect(bp, 29, 2, 34, 2, "TutorialStage_SprintRisk");
        AddRect(bp, 35, 1, 36, 3, "TutorialStage_Exit");
        AddRect(bp, 37, 2, 38, 2, "TutorialStage_MainMazeConnector");

        AddDoorEdge(bp, new Vector2Int(6, 2), new Vector2Int(7, 2), "Door_SoundboardPickupExit");
        AddDoorEdge(bp, new Vector2Int(7, 1), new Vector2Int(8, 1), "Door_SoundboardUseExit");
        AddDoorEdge(bp, new Vector2Int(12, 1), new Vector2Int(13, 1), "Door_CorruptionExit");
        AddDoorEdge(bp, new Vector2Int(16, 1), new Vector2Int(17, 1), "Door_LightSpotExit");
        AddDoorEdge(bp, new Vector2Int(28, 2), new Vector2Int(29, 2), "Door_HideChamberExit");
        AddDoorEdge(bp, new Vector2Int(34, 2), new Vector2Int(35, 2), "Door_SprintExit");
        AddDoorEdge(bp, new Vector2Int(36, 2), new Vector2Int(37, 2), "Door_TutorialFinalExit");

        return bp;
    }

    static void AddRect(MazeBlueprintData bp, int minX, int minY, int maxX, int maxY, string stageTag)
    {
        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                bp.cells.Add(new MazeBlueprintData.CellData { cell = new Vector2Int(x, y), stageTag = stageTag });
    }

    static void AddDoorEdge(MazeBlueprintData bp, Vector2Int a, Vector2Int b, string doorId)
    {
        bp.edges.Add(new MazeBlueprintData.EdgeData { a = a, b = b, requiresDoor = true, doorId = doorId });
    }
}
