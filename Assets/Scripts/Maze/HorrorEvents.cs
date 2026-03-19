using System;
using UnityEngine;

public static class HorrorEvents
{
	public static event Action<float> OnTensionChanged;
	public static event Action OnChaseStarted;
	public static event Action OnChaseEnded;
	public static event Action<string, float> OnSoundboardPlayed;

	public static void RaiseTensionChanged(float tension)
	{
		OnTensionChanged?.Invoke(Mathf.Clamp01(tension));
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
}
