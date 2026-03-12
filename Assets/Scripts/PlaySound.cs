using UnityEngine;

public class PlaySound : MonoBehaviour
{
   public AudioClip[] soundClips;   // Assign multiple sounds in Inspector
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1) && soundClips.Length > 0)
        {
            int randomIndex = Random.Range(0, soundClips.Length);
            audioSource.Stop(); // Stop any currently playing sound
            audioSource.clip = soundClips[randomIndex];
            audioSource.Play();
        }
    }
}