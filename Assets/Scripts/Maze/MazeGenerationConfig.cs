using UnityEngine;

[CreateAssetMenu(fileName = "MazeGenerationConfig", menuName = "HorrorLand/Maze Generation Config")]
public class MazeGenerationConfig : ScriptableObject
{
    [Header("Grid")]
    public float cellSize = 2f;
    public float wallHeight = 3f;
    public float wallThickness = 0.2f;
    public float floorY = 0f;

    [Header("Door")]
    public float doorWidth = 3.95f;
    public float doorHeight = 2.7f;
    public float doorThickness = 0.1f;

    [Header("Materials")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material ceilingMaterial;
    public Material doorMaterial;
}
