using System;
using System.Collections.Generic;
using UnityEngine;

public class RunGameState : MonoBehaviour
{
    public float TimeSurvived => timeSurvived;
    public bool WasChased => wasChased;
    public bool EnteredSecretRoom => enteredSecretRoom;
    public float CurrentSanity => currentSanity;

    public event Action OnStateChanged;

    private readonly HashSet<string> collectedNoteIds = new HashSet<string>();
    private readonly HashSet<string> collectedTags = new HashSet<string>();
    private readonly HashSet<string> visitedAreas = new HashSet<string>();
    private readonly HashSet<string> triggeredEvents = new HashSet<string>();

    private float timeSurvived;
    private bool wasChased;
    private bool enteredSecretRoom;
    private float currentSanity = 100f;
    private float nextTimeBroadcast;

    void OnEnable()
    {
        HorrorEvents.OnChaseStarted += HandleChaseStarted;
        HorrorEvents.OnSanityChanged += HandleSanityChanged;
    }

    void OnDisable()
    {
        HorrorEvents.OnChaseStarted -= HandleChaseStarted;
        HorrorEvents.OnSanityChanged -= HandleSanityChanged;
    }

    void Update()
    {
        timeSurvived += Time.deltaTime;

        if (Time.time >= nextTimeBroadcast)
        {
            nextTimeBroadcast = Time.time + 1f;
            RaiseStateChanged();
        }
    }

    public void RegisterCollectedNote(NoteData noteData)
    {
        if (noteData == null || string.IsNullOrEmpty(noteData.id)) return;

        if (collectedNoteIds.Add(noteData.id))
        {
            for (int i = 0; i < noteData.tags.Count; i++)
            {
                if (!string.IsNullOrEmpty(noteData.tags[i]))
                {
                    collectedTags.Add(noteData.tags[i]);
                }
            }

            RaiseStateChanged();
        }
    }

    public void MarkVisitedArea(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return;
        if (visitedAreas.Add(areaId))
        {
            if (areaId == "secret_room")
            {
                enteredSecretRoom = true;
            }
            RaiseStateChanged();
        }
    }

    public void MarkTriggeredEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        if (triggeredEvents.Add(eventId))
        {
            RaiseStateChanged();
        }
    }

    public bool HasCollectedNote(string noteId) => collectedNoteIds.Contains(noteId);
    public bool HasCollectedTag(string tag) => collectedTags.Contains(tag);
    public bool HasVisitedArea(string areaId) => visitedAreas.Contains(areaId);
    public bool HasTriggeredEvent(string eventId) => triggeredEvents.Contains(eventId);

    private void HandleChaseStarted()
    {
        wasChased = true;
        RaiseStateChanged();
    }

    private void HandleSanityChanged(float sanity, float normalizedSanity, float stress)
    {
        currentSanity = sanity;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
