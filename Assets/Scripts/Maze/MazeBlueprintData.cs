using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MazeBlueprintData
{
    [System.Serializable]
    public class CellData
    {
        public Vector2Int cell;
        public string stageTag;
    }

    [System.Serializable]
    public class EdgeData
    {
        public Vector2Int a;
        public Vector2Int b;
        public bool requiresDoor;
        public string doorId;
    }

    public int width;
    public int height;
    public List<CellData> cells = new List<CellData>();
    public List<EdgeData> edges = new List<EdgeData>();
}
