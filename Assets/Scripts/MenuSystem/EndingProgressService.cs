using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HorrorLand.MenuSystem
{
    public static class EndingProgressService
    {
        private const string CompletedEndingsKey = "Menu.CompletedEndingIds";

        public static void MarkCompleted(string endingId)
        {
            if (string.IsNullOrWhiteSpace(endingId))
            {
                return;
            }

            var ids = GetCompletedEndingIds();
            if (!ids.Add(endingId))
            {
                return;
            }

            Save(ids);
        }

        public static bool IsCompleted(string endingId)
        {
            return !string.IsNullOrWhiteSpace(endingId) && GetCompletedEndingIds().Contains(endingId);
        }

        public static HashSet<string> GetCompletedEndingIds()
        {
            string raw = PlayerPrefs.GetString(CompletedEndingsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new HashSet<string>();
            }

            return new HashSet<string>(raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static void Save(HashSet<string> completedIds)
        {
            string serialized = string.Join("|", completedIds.OrderBy(id => id));
            PlayerPrefs.SetString(CompletedEndingsKey, serialized);
            PlayerPrefs.Save();
        }
    }
}
