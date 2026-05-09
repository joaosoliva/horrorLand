using UnityEngine;

public class TutorialStageDoor : MonoBehaviour
{
    public string doorId;
    public DoorTrigger doorTrigger;
    public AudioSource audioSource;
    public AudioClip unlockClip;
    public GameObject lockVisual;
    public bool startsLocked = true;

    private Rigidbody lockBody;
    private bool isUnlocked;

    public bool IsUnlocked => isUnlocked;

    void Awake()
    {
        if (doorTrigger == null)
        {
            doorTrigger = GetComponent<DoorTrigger>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (lockVisual == null)
        {
            lockVisual = CreateLockVisual();
        }

        if (lockVisual != null)
        {
            lockBody = lockVisual.GetComponent<Rigidbody>();
            if (lockBody == null)
            {
                lockBody = lockVisual.AddComponent<Rigidbody>();
            }
            lockBody.isKinematic = true;
            lockBody.useGravity = false;
        }

        if (startsLocked)
        {
            SetLocked(true);
            isUnlocked = false;
        }
        else
        {
            isUnlocked = true;
        }
    }

    public void SetLocked(bool locked)
    {
        if (doorTrigger != null)
        {
            doorTrigger.SetLockedState(locked);
        }

        if (lockVisual != null)
        {
            lockVisual.SetActive(locked);
            if (locked && lockBody != null)
            {
                lockBody.isKinematic = true;
                lockBody.useGravity = false;
            }
        }

        isUnlocked = !locked;
    }

    public void Unlock(string reason)
    {
        if (isUnlocked)
        {
            return;
        }

        SetLocked(false);
        DropLock();

        if (unlockClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(unlockClip);
            Debug.Log("Unlock sound played: " + doorId);
        }
        else
        {
            Debug.LogWarning("[TutorialStageDoor] Missing unlock audio for " + doorId);
        }

        StartCoroutine(UnlockFlashRoutine());
        Debug.Log("TutorialStageDoor unlocked: " + reason + ". Door=" + doorId);
    }


    private System.Collections.IEnumerator UnlockFlashRoutine()
    {
        Transform target = lockVisual != null ? lockVisual.transform : transform;
        Vector3 baseScale = target.localScale;
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float pulse = 1f + Mathf.Sin((t / 0.12f) * Mathf.PI) * 0.12f;
            target.localScale = baseScale * pulse;
            yield return null;
        }
        target.localScale = baseScale;
    }

    private void DropLock()
    {
        if (lockVisual == null || lockBody == null)
        {
            return;
        }

        lockVisual.transform.SetParent(null, true);
        lockBody.isKinematic = false;
        lockBody.useGravity = true;
        Debug.Log("Lock dropped for door: " + doorId);
    }

    private GameObject CreateLockVisual()
    {
        GameObject lockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lockObj.name = "Lock_" + doorId;
        lockObj.transform.SetParent(transform);
        lockObj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        lockObj.transform.localPosition = new Vector3(0f, 1.35f, -0.08f);

        Renderer renderer = lockObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.black;
        }

        Collider c = lockObj.GetComponent<Collider>();
        if (c != null)
        {
            c.isTrigger = true;
        }

        return lockObj;
    }
}
