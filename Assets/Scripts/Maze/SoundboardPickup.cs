using UnityEngine;

public class SoundboardPickup : MonoBehaviour
{
    public GameObject soundboardRuntimeObject;
    public KeyCode interactKey = KeyCode.E;
    public string prompt = "Take the Soundboard";

    private bool playerInRange;
    private bool pickedUp;
    private GUIStyle style;

    void Start()
    {
        if (soundboardRuntimeObject != null)
        {
            soundboardRuntimeObject.SetActive(false);
        }
    }

    void Update()
    {
        if (pickedUp || !playerInRange)
        {
            return;
        }

        if (Input.GetKeyDown(interactKey))
        {
            pickedUp = true;
            if (soundboardRuntimeObject != null)
            {
                soundboardRuntimeObject.SetActive(true);
            }
            HorrorEvents.RaiseSoundboardCollected();
            GameplayHintController.PushGlobalHint("The Soundboard restores sanity, but corrupts the maze.", 4f, HintPriority.High);
            gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    void OnGUI()
    {
        if (!playerInRange || pickedUp)
        {
            return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box);
            style.fontSize = 22;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
        }

        float width = 320f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height * 0.82f;
        GUI.Box(new Rect(x, y, width, 42f), prompt + " [E]", style);
    }
}
