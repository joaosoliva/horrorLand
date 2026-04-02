using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "HorrorLand/Endings/Ending Data", fileName = "EndingData")]
public class EndingData : ScriptableObject
{
    public string id;
    public string resultMessage;
    public int priority = 0;
    public List<EndingCondition> conditions = new List<EndingCondition>();
}
