using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "HorrorLand/Notes/Note Data", fileName = "NoteData")]
public class NoteData : ScriptableObject
{
    public string id;

    [TextArea(3, 10)]
    public string content;

    [Min(1)]
    public int tier = 1;
    public bool isBaseNote = true;
    public List<UnlockCondition> unlockConditions = new List<UnlockCondition>();
    public List<string> tags = new List<string>();
}
