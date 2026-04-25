using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class EndingEntryView : MonoBehaviour
    {
        [SerializeField] private Text endingTitleText;
        [SerializeField] private Text hintText;

        public void Bind(int endingNumber, string endingName, string unlockHint, bool isUnlocked)
        {
            endingTitleText.text = $"Ending {endingNumber:00} - {endingName}";
            hintText.text = unlockHint;
            hintText.gameObject.SetActive(!isUnlocked && !string.IsNullOrWhiteSpace(unlockHint));
        }
    }
}
