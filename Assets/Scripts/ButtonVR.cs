using UnityEngine;
using UnityEngine.Events;

public class ButtonVR : MonoBehaviour
{
    public GameObject button;
    public UnityEvent onPress;
    public UnityEvent onRelease;
    GameObject presser;
    AudioSource sound;
    bool isPressed;
    private Vector3 initialPosition;
    public Vector3 pressedOffset = new Vector3(0, -0.012f, 0);
    public Vector3 releasedOffset = Vector3.zero;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sound = GetComponent<AudioSource>();
        isPressed = false;
        initialPosition = button.transform.localPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!isPressed)
        {
            button.transform.localPosition = initialPosition + pressedOffset;
            presser = other.gameObject;
            onPress.Invoke();
            sound.Play();
            isPressed = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == presser)
        {
            button.transform.localPosition = initialPosition + releasedOffset;
            isPressed = false;
        }
    }

    public void SpawnSphere()
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.localScale = Vector3.one * 0.5f;
        sphere.transform.position = transform.position + Vector3.up * 1f + Vector3.left * 1f;
        sphere.AddComponent<Rigidbody>();

        Debug.Log("Sphere spawned!");
    }
}
