using UnityEngine;

public class PlaySound : MonoBehaviour
{
	public AudioClip[] soundClips;
	public bool raiseHorrorSoundboardEvent = true;
	public string soundboardTag = "Generic";
	[Range(0f, 1f)] public float eventLoudness = 0.6f;

	private AudioSource audioSource;

	void Start()
	{
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null)
		{
			audioSource = gameObject.AddComponent<AudioSource>();
		}
	}

	void Update()
	{
		if (Input.GetMouseButtonDown(1) && soundClips.Length > 0)
		{
			int randomIndex = Random.Range(0, soundClips.Length);
			audioSource.Stop();
			audioSource.clip = soundClips[randomIndex];
			audioSource.Play();

			if (raiseHorrorSoundboardEvent)
			{
				HorrorEvents.RaiseSoundboardPlayed(soundboardTag, eventLoudness);
			}
		}
	}
}
