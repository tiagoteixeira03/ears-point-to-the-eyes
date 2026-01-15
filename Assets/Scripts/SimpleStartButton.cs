using UnityEngine;

public class SimpleStartButton : MonoBehaviour
{
    // This function acts as a bridge.
    // Hook this up to the Button's "When Select" event.
    public void OnButtonPress()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartExperiment();
        }
        else
        {
            Debug.LogError("Game Manager not found!");
        }
    }
}