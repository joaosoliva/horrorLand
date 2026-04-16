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
	}

	public static void RaiseChaseEnded()
	{
		OnChaseEnded?.Invoke();
	}

	public static void RaiseSoundboardPlayed(string soundTag, float loudness)
	{
		OnSoundboardPlayed?.Invoke(soundTag, Mathf.Clamp01(loudness));
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
}
