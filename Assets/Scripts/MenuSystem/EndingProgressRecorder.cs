using UnityEngine;

namespace HorrorLand.MenuSystem
{
    public class EndingProgressRecorder : MonoBehaviour
    {
        public void RecordEnding(EndingData endingData)
        {
            if (endingData == null || string.IsNullOrWhiteSpace(endingData.id))
            {
                return;
            }

            EndingProgressService.MarkCompleted(endingData.id);
        }
    }
}
