using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI; 
using System.IO;
using System.Text;
using System.Linq; 

public enum GameMode
{
    Idle,
    Training,
    Experiment,
    Rating
}

public enum ExperimentCondition
{
    None,
    Sound,
    Haptics,
    Both
}

// 1. Helper class to show Sequences in Inspector
[System.Serializable]
public class BoxSequence
{
    public string name = "Sequence";
    public List<BoxData> sequencePath; // Drag boxes here in order
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI References")]
    public GameObject menuCanvas;       
    public GameObject ratingCanvas;     
    public TextMeshProUGUI statusText; 
    public TextMeshProUGUI idText;     

    [Header("Rating UI")]
    public UnityEngine.UI.Slider ratingSlider;         
    public TMPro.TextMeshProUGUI ratingLabel; 

    [Header("Player References")]
    public Transform playerHead;
    
    [Header("Game Data")]
    public List<BoxData> allBoxes; // Still needed for Training randomness

    [Header("Experiment Sequences")]
    // 2. Define your 4 sequences here in the Inspector
    public List<BoxSequence> definedSequences; 

    [Header("Settings")]
    public float delayBetweenRounds = 1.0f; 
    public int trainingRoundsTotal = 5;     
    public int experimentRoundsPerPhase = 10; 

    // Internal State
    private GameMode currentMode = GameMode.Idle;
    
    // State Management for Conditions
    private ExperimentCondition currentCondition; 
    private List<ExperimentCondition> experimentPhases; 
    
    // Sequence Management
    private List<BoxSequence> shuffledSequences; // The order used for this session
    private int currentPhaseIndex = 0;

    private bool isSearching = false;
    private BoxData currentTargetBox;
    
    // Session Data
    private int sessionID;
    private string sessionTimestamp;
    private int currentRound = 0;
    
    // Data Storage
    private Dictionary<ExperimentCondition, List<float>> reactionTimes = new Dictionary<ExperimentCondition, List<float>>();
    private Dictionary<ExperimentCondition, int> ratings = new Dictionary<ExperimentCondition, int>();
    
    // Vibration Config
    private float maxAngle = 90f; 
    private float startTime; 

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        sessionID = Random.Range(10000, 100000);
        sessionTimestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (idText != null) idText.text = "ID: " + sessionID.ToString();

        // Auto-fill allBoxes if empty (for training randomness)
        if (allBoxes.Count == 0)
        {
            BoxData[] foundBoxes = FindObjectsByType<BoxData>(FindObjectsSortMode.None);
            allBoxes.AddRange(foundBoxes);
        }
        
        if(ratingCanvas != null) ratingCanvas.SetActive(false);
        if (ratingSlider != null) ratingSlider.onValueChanged.AddListener(OnSliderChanged);

        ShowMenu();
    }

    void Update()
    {
        bool allowHaptics = (currentCondition == ExperimentCondition.Haptics || currentCondition == ExperimentCondition.Both);

        // Safety check for null playerHead
        if (isSearching && allowHaptics && currentTargetBox != null && playerHead != null)
        {
            HandleDirectionalVibration();
        }
    }

    // --- MENU BUTTON FUNCTIONS ---

    public void StartTraining(bool enableSound)
    {
        currentMode = GameMode.Training;
        currentCondition = enableSound ? ExperimentCondition.Sound : ExperimentCondition.Haptics;
        currentRound = 1;
        menuCanvas.SetActive(false);
        StartNewRound();
    }

    public void StartExperiment()
    {
        // Validation check
        if (definedSequences.Count < 4)
        {
            Debug.LogError("Please assign at least 4 Sequences in the GameManager Inspector!");
            return;
        }

        currentMode = GameMode.Experiment;
        
        // Reset Data
        reactionTimes.Clear();
        ratings.Clear();
        reactionTimes.Add(ExperimentCondition.None, new List<float>());
        reactionTimes.Add(ExperimentCondition.Sound, new List<float>());
        reactionTimes.Add(ExperimentCondition.Haptics, new List<float>());
        reactionTimes.Add(ExperimentCondition.Both, new List<float>());

        // 1. Shuffle Phases
        experimentPhases = new List<ExperimentCondition> 
        { 
            ExperimentCondition.None, 
            ExperimentCondition.Sound, 
            ExperimentCondition.Haptics, 
            ExperimentCondition.Both 
        };
        ShuffleList(experimentPhases);

        // 2. Shuffle Sequences (So Sequence 1 is not always Phase 1)
        shuffledSequences = new List<BoxSequence>(definedSequences);
        ShuffleList(shuffledSequences);

        currentPhaseIndex = 0;
        currentCondition = experimentPhases[0]; 
        currentRound = 1;

        menuCanvas.SetActive(false);
        StartNewRound();
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    // --- GAME LOOP ---

    private void StartNewRound()
    {
        foreach (var box in allBoxes) box.ResetBox();

        // --- NEW TARGET SELECTION LOGIC ---
        if (currentMode == GameMode.Training)
        {
            // Random for Training
            int randomIndex = Random.Range(0, allBoxes.Count);
            currentTargetBox = allBoxes[randomIndex];
        }
        else // Experiment
        {
            // Sequential for Experiment
            // Get the sequence assigned to the current Phase Index
            BoxSequence activeSeq = shuffledSequences[currentPhaseIndex];
            
            // Get box based on current round (index 0 for round 1)
            // Using modulo (%) ensures we don't crash if rounds > sequence length
            int boxIndex = (currentRound - 1) % activeSeq.sequencePath.Count;
            currentTargetBox = activeSeq.sequencePath[boxIndex];
        }
        // ----------------------------------

        currentTargetBox.SetAsTarget(true);

        bool allowSound = (currentCondition == ExperimentCondition.Sound || currentCondition == ExperimentCondition.Both);
        if (allowSound) currentTargetBox.PlaySound();

        if(currentMode == GameMode.Experiment)
             UpdateStatusText($"Exp: {currentCondition} | Round {currentRound}");
        else
             UpdateStatusText($"Train: {currentCondition} | Round {currentRound}");

        startTime = Time.time;
        isSearching = true;
    }

    public void SubmitBoxSelection(BoxData selectedBox)
    {
        if (!isSearching) return;

        if (selectedBox == currentTargetBox)
        {
            float duration = Time.time - startTime;
            CompleteRound(duration);
        }
    }

    private void CompleteRound(float duration)
    {
        isSearching = false;
        currentTargetBox.SetAsTarget(false);
        selectedBoxData().StopSound();
        StopVibration();

        Debug.Log($"Round Finished. Time: {duration:F3}");

        if (currentMode == GameMode.Experiment)
        {
            reactionTimes[currentCondition].Add(duration);
        }

        StartCoroutine(WaitAndDecideNextStep());
    }

    private IEnumerator WaitAndDecideNextStep()
    {
        yield return new WaitForSeconds(delayBetweenRounds);

        if (currentMode == GameMode.Training) HandleTrainingProgress();
        else if (currentMode == GameMode.Experiment) HandleExperimentProgress();
    }

    private void HandleTrainingProgress()
    {
        if (currentRound < trainingRoundsTotal)
        {
            currentRound++;
            StartNewRound();
        }
        else
        {
            UpdateStatusText("Training Complete!");
            ShowMenu();
        }
    }

    private void HandleExperimentProgress()
    {
        if (currentRound < experimentRoundsPerPhase)
        {
            currentRound++;
            StartNewRound();
        }
        else
        {
            ShowRatingMenu();
        }
    }

    // --- RATING LOGIC ---

    private void ShowRatingMenu()
    {
        currentMode = GameMode.Rating;
        ratingCanvas.SetActive(true);
        UpdateStatusText($"Rate helpfulness: {currentCondition}");
        
        if (ratingSlider != null)
        {
            ratingSlider.value = 5;
            OnSliderChanged(5);
        }
    }

    public void OnSliderChanged(float val)
    {
        if (ratingLabel != null) ratingLabel.text = $"{val}/10";
    }

    public void SubmitRating()
    {
        int score = 0;
        if(ratingSlider != null) score = Mathf.RoundToInt(ratingSlider.value);

        if (ratings.ContainsKey(currentCondition)) ratings[currentCondition] = score;
        else ratings.Add(currentCondition, score);

        ratingCanvas.SetActive(false);

        currentPhaseIndex++;

        if (currentPhaseIndex < experimentPhases.Count)
        {
            currentCondition = experimentPhases[currentPhaseIndex];
            currentRound = 1;
            currentMode = GameMode.Experiment;
            StartNewRound();
        }
        else
        {
            FinishExperiment();
        }
    }

    // --- CSV & FINISH ---

    private void FinishExperiment()
    {
        SaveToCSV();
        UpdateStatusText($"<b>Done! (ID: {sessionID})</b>\nData saved.");
        ShowMenu();
    }

    private void SaveToCSV()
    {
        StringBuilder sb = new StringBuilder();
        sessionTimestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        // Added "SequenceName" to the CSV so you know which pattern was used
        sb.AppendLine("PhaseOrder,Condition,SequenceName,Round,Time,RatingForCondition");

        for(int i = 0; i < experimentPhases.Count; i++)
        {
            ExperimentCondition cond = experimentPhases[i];
            string seqName = shuffledSequences[i].name; // Get name of sequence used

            int rating = ratings.ContainsKey(cond) ? ratings[cond] : 0;
            List<float> times = reactionTimes[cond];

            for(int r = 0; r < times.Count; r++)
            {
                sb.AppendLine($"{i+1},{cond},{seqName},{r+1},{times[r]:F3},{rating}");
            }
        }

        string filename = $"{sessionTimestamp}_{sessionID}_experiment.csv";
        string path = Path.Combine(Application.persistentDataPath, filename);

        try
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"Saved CSV to: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save CSV: " + e.Message);
        }
    }

    // --- HELPER FUNCTIONS ---

    private void ShowMenu()
    {
        menuCanvas.SetActive(true);
        isSearching = false;
        currentMode = GameMode.Idle;
    }

    private void UpdateStatusText(string msg) { if(statusText != null) statusText.text = msg; }
    private BoxData selectedBoxData() { return currentTargetBox; }
    
    private void StopVibration()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }

    private void HandleDirectionalVibration()
    {
        // Added PlayerHead Check
        if (playerHead == null || currentTargetBox == null) return;

        Vector3 targetDir = currentTargetBox.transform.position - playerHead.position;
        targetDir.y = 0;
        Vector3 headForward = playerHead.forward;
        headForward.y = 0;
        float angle = Vector3.SignedAngle(headForward, targetDir, Vector3.up);
        float strength = Mathf.InverseLerp(0, maxAngle, Mathf.Abs(angle)); 

        if (Mathf.Abs(angle) < 5f) { 
            OVRInput.SetControllerVibration(1, 0, OVRInput.Controller.RTouch); 
            OVRInput.SetControllerVibration(1, 0, OVRInput.Controller.LTouch); 
        }
        else if (angle < 0) { 
            OVRInput.SetControllerVibration(1, strength, OVRInput.Controller.LTouch); 
            OVRInput.SetControllerVibration(1, 0, OVRInput.Controller.RTouch); 
        }
        else { 
            OVRInput.SetControllerVibration(1, 0, OVRInput.Controller.LTouch); 
            OVRInput.SetControllerVibration(1, strength, OVRInput.Controller.RTouch); 
        }
    }
}