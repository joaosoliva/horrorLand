using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class UnlocksMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Button closeButton;
        [SerializeField] private EndingUnlockCatalog endingCatalog;
        [SerializeField] private EndingEntryView endingEntryPrefab;
        [SerializeField] private Transform listParent;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            rootPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            rootPanel.SetActive(true);
            RebuildList();
        }

        public void Close()
        {
            rootPanel.SetActive(false);
        }

        private void RebuildList()
        {
            for (int i = listParent.childCount - 1; i >= 0; i--)
            {
                Destroy(listParent.GetChild(i).gameObject);
            }

            var entries = endingCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var item = Instantiate(endingEntryPrefab, listParent);

                string resolvedName = string.IsNullOrWhiteSpace(entry.endingName) ? "Unknown Ending" : entry.endingName;
                bool isUnlocked = EndingProgressService.IsCompleted(entry.ResolvedId);
                item.Bind(entry.endingNumber, resolvedName, entry.unlockHint, isUnlocked);
            }
        }
    }
}
