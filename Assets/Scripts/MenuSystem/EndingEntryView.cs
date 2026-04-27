using TMPro;
using UnityEngine;

namespace HorrorLand.MenuSystem
{
    public class EndingEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text endingTitleText;
        [SerializeField] private TMP_Text hintText;

        public void Bind(int endingNumber, string endingName, string unlockHint, bool isUnlocked)
        {
            endingTitleText.text = $"Ending {endingNumber:00} - {endingName}";
            hintText.text = unlockHint;
            hintText.gameObject.SetActive(!isUnlocked && !string.IsNullOrWhiteSpace(unlockHint));
        }
    }
}
