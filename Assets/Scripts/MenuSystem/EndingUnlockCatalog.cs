using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorLand.MenuSystem
{
    [CreateAssetMenu(menuName = "HorrorLand/Endings/Ending Unlock Catalog", fileName = "EndingUnlockCatalog")]
    public class EndingUnlockCatalog : ScriptableObject
    {
        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        [Serializable]
        public class Entry
        {
            public EndingData endingData;
            public string endingIdOverride;
            public int endingNumber;
            public string endingName;
            [TextArea] public string unlockHint;

            public string ResolvedId
            {
                get
                {
                    if (endingData != null && !string.IsNullOrWhiteSpace(endingData.id))
                    {
                        return endingData.id;
                    }

                    return endingIdOverride;
                }
            }
        }
    }
}
