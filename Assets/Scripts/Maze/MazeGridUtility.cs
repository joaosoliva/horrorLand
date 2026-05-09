using UnityEngine;

public static class MazeGridUtility
{
    public static Vector3 GridToWorldCenter(Vector2Int cell, float cellSize, float floorY = 0f)
    {
        return new Vector3(cell.x * cellSize + cellSize * 0.5f, floorY, cell.y * cellSize + cellSize * 0.5f);
    }

    public static Vector2Int WorldToGrid(Vector3 worldPos, float cellSize)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x / cellSize), Mathf.FloorToInt(worldPos.z / cellSize));
    }

    public static Vector3 BoundaryCenterBetweenCells(Vector2Int a, Vector2Int b, float cellSize, float floorY = 0f)
    {
        Vector3 aw = GridToWorldCenter(a, cellSize, floorY);
        Vector3 bw = GridToWorldCenter(b, cellSize, floorY);
        return (aw + bw) * 0.5f;
    }

    public static Quaternion DoorRotationForBoundary(Vector2Int a, Vector2Int b)
    {
        Vector2Int delta = b - a;
        Vector3 facing = new Vector3(delta.x, 0f, delta.y).normalized;
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
        return Quaternion.LookRotation(facing);
    }
}
