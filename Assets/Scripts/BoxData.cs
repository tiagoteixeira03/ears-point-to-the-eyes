using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Renderer))]
public class BoxData : MonoBehaviour
{
    private Renderer myRenderer;
    private AudioSource myAudio;
    private Color originalColor;
    
    // We set this in Inspector or code (White)
    public Color targetColor = Color.white;

    void Awake()
    {
        myRenderer = GetComponent<Renderer>();
        myAudio = GetComponent<AudioSource>();
        
        // Save the gray color so we can reset later
        if(myRenderer != null)
            originalColor = myRenderer.material.color;
    }

    public void SetAsTarget(bool isTarget)
    {
        if (myRenderer != null)
        {
            myRenderer.material.color = isTarget ? targetColor : originalColor;
        }
    }

    public void PlaySound()
    {
        if (myAudio != null)
        {
            myAudio.Play();
        }
    }

    public void StopSound()
    {
        if (myAudio != null)
        {
            myAudio.Stop();
        }
    }

    public void ResetBox()
    {
        SetAsTarget(false);
        if (myAudio.isPlaying) myAudio.Stop();
    }

    // This function will be called by the Interaction SDK Event Wrapper
    public void OnBoxSelected()
    {
        // Tell the Game Manager "I was clicked"
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SubmitBoxSelection(this);
        }
    }
}