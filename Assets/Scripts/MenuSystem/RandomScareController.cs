using System.Collections;
using UnityEngine;

namespace HorrorLand.MenuSystem
{
    public class RandomScareController : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField, Range(0f, 1f)] private float scareChance = 0.25f;
        [SerializeField] private float jumpscareDuration = 0.75f;

        [Header("Audio")]
        [SerializeField] private AudioSource screamAudioSource;
        [SerializeField] private AudioClip[] screamClips;

        [Header("UI")]
        [SerializeField] private GameObject jumpscareOverlay;

        private bool randomScaresEnabled;
        private bool jumpscarePlaying;

        private void Awake()
        {
            if (jumpscareOverlay != null)
            {
                jumpscareOverlay.SetActive(false);
            }
        }

        public void SetEnabledState(bool enabled)
        {
            randomScaresEnabled = enabled;
        }

        public void TryTriggerScareFromInteraction()
        {
            if (!randomScaresEnabled)
            {
                return;
            }

            if (Random.value > scareChance)
            {
                return;
            }

            int outcome = Random.Range(0, 3);
            switch (outcome)
            {
                case 0:
                    PlayRandomScream();
                    break;
                case 1:
                    TryPlayJumpscareOverlay();
                    break;
                default:
                    break;
            }
        }

        private void PlayRandomScream()
        {
            if (screamAudioSource == null || screamClips == null || screamClips.Length == 0)
            {
                return;
            }

            var clip = screamClips[Random.Range(0, screamClips.Length)];
            screamAudioSource.PlayOneShot(clip);
        }

        private void TryPlayJumpscareOverlay()
        {
            if (jumpscareOverlay == null || jumpscarePlaying)
            {
                return;
            }

            StartCoroutine(PlayJumpscareOverlayRoutine());
        }

        private IEnumerator PlayJumpscareOverlayRoutine()
        {
            jumpscarePlaying = true;
            jumpscareOverlay.SetActive(true);
            yield return new WaitForSeconds(jumpscareDuration);
            jumpscareOverlay.SetActive(false);
            jumpscarePlaying = false;
        }
    }
}
