using System;
using UnityEngine;

public enum HorrorPhase
{
	Calm,
	Build,
	Threat,
	Peak,
	Relief,
	Finale
}

public enum ScareType
{
	MinorPsychological,
	PresenceCue,
	Fakeout,
	MajorJumpscare,
	ChaseTrigger,
	ReliefBeat,
	RoutePressure
}

public enum EnemyDistanceBand
{
	Far,
	Near,
	Danger,
	Immediate
}

public static class HorrorEvents
{
	public static event Action<float> OnTensionChanged;
	public static event Action<HorrorPhase> OnPhaseChanged;
	public static event Action<EnemyDistanceBand> OnThreatBandChanged;
	public static event Action<ScareType> OnScareTriggered;
	public static event Action OnMajorPeakStarted;
	public static event Action OnMajorPeakEnded;
	public static event Action OnFinaleStarted;
	public static event Action OnChaseStarted;
	public static event Action OnChaseEnded;
	public static event Action<string, float> OnSoundboardPlayed;
	public static event Action OnJumpscareTriggered;
	public static event Action<float, float, float> OnSanityChanged;
	public static event Action<float, float> OnCorruptionChanged;
	public static event Action<string, float> OnCorruptionEventTriggered;

	public static event Action OnTutorialStarted;
	public static event Action OnTutorialCompleted;
	public static event Action OnSoundboardCollected;
	public static event Action OnSoundboardUsed;
	public static event Action OnSanityLow;
	public static event Action OnSanityCritical;
	public static event Action OnCorruptionHigh;
	public static event Action OnLightSpotEntered;
	public static event Action OnLightSpotUsed;
	public static event Action OnLightSpotExpired;
	public static event Action OnSprintStarted;
	public static event Action OnSprintStopped;
	public static event Action<float, string> OnNoiseCreated;
	public static event Action OnMonsterChaseStarted;
	public static event Action OnMonsterLostPlayer;
	public static event Action<string> OnPlayerDeath;
	public static event Action<string> OnExitInteractionFailed;
	public static event Action OnExitUnlocked;

	public static void RaiseTensionChanged(float tension)
	{
		OnTensionChanged?.Invoke(Mathf.Clamp01(tension));
	}

	public static void RaisePhaseChanged(HorrorPhase phase)
	{
		OnPhaseChanged?.Invoke(phase);
	}

	public static void RaiseThreatBandChanged(EnemyDistanceBand band)
	{
		OnThreatBandChanged?.Invoke(band);
	}

	public static void RaiseScareTriggered(ScareType scareType)
	{
		OnScareTriggered?.Invoke(scareType);
	}

	public static void RaiseMajorPeakStarted()
	{
		OnMajorPeakStarted?.Invoke();
	}

	public static void RaiseMajorPeakEnded()
	{
		OnMajorPeakEnded?.Invoke();
	}

	public static void RaiseFinaleStarted()
	{
		OnFinaleStarted?.Invoke();
	}

	public static void RaiseChaseStarted()
	{
		OnChaseStarted?.Invoke();
		OnMonsterChaseStarted?.Invoke();
	}

	public static void RaiseChaseEnded()
	{
		OnChaseEnded?.Invoke();
		OnMonsterLostPlayer?.Invoke();
	}

	public static void RaiseSoundboardPlayed(string soundTag, float loudness)
	{
		OnSoundboardPlayed?.Invoke(soundTag, Mathf.Clamp01(loudness));
		OnSoundboardUsed?.Invoke();
		RaiseNoiseCreated(loudness, "Soundboard:" + soundTag);
	}

	public static void RaiseJumpscareTriggered()
	{
		OnJumpscareTriggered?.Invoke();
	}

	public static void RaiseSanityChanged(float currentSanity, float normalizedSanity, float stress01)
	{
		OnSanityChanged?.Invoke(currentSanity, Mathf.Clamp01(normalizedSanity), Mathf.Clamp01(stress01));
	}

	public static void RaiseCorruptionChanged(float currentCorruption, float normalizedCorruption)
	{
		OnCorruptionChanged?.Invoke(Mathf.Max(0f, currentCorruption), Mathf.Clamp01(normalizedCorruption));
	}

	public static void RaiseCorruptionEventTriggered(string eventId, float corruptionLevel)
	{
		OnCorruptionEventTriggered?.Invoke(eventId, Mathf.Clamp01(corruptionLevel));
	}

	public static void RaiseTutorialStarted() => OnTutorialStarted?.Invoke();
	public static void RaiseTutorialCompleted() => OnTutorialCompleted?.Invoke();
	public static void RaiseSoundboardCollected() => OnSoundboardCollected?.Invoke();
	public static void RaiseSanityLow() => OnSanityLow?.Invoke();
	public static void RaiseSanityCritical() => OnSanityCritical?.Invoke();
	public static void RaiseCorruptionHigh() => OnCorruptionHigh?.Invoke();
	public static void RaiseLightSpotEntered() => OnLightSpotEntered?.Invoke();
	public static void RaiseLightSpotUsed() => OnLightSpotUsed?.Invoke();
	public static void RaiseLightSpotExpired() => OnLightSpotExpired?.Invoke();
	public static void RaiseSprintStarted() => OnSprintStarted?.Invoke();
	public static void RaiseSprintStopped() => OnSprintStopped?.Invoke();
	public static void RaiseNoiseCreated(float loudness, string sourceTag) => OnNoiseCreated?.Invoke(Mathf.Clamp01(loudness), sourceTag ?? "Unknown");
	public static void RaisePlayerDeath(string cause) => OnPlayerDeath?.Invoke(string.IsNullOrWhiteSpace(cause) ? "Unknown" : cause);
	public static void RaiseExitInteractionFailed(string reason) => OnExitInteractionFailed?.Invoke(string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason);
	public static void RaiseExitUnlocked() => OnExitUnlocked?.Invoke();
}
