using UnityEngine;
using UnityEngine.UI;

public class SurveyPopupController : MonoBehaviour
{
    [Tooltip("Drag your popup panel here")]
    public GameObject popupPanel;
    [Tooltip("Drag the 'Open Survey' Button here")]
    public Button openSurveyButton;
    [Tooltip("Drag the 'Continue' Button here")]
    public Button continueButton;

    // URL set dynamically by GameManager
    [HideInInspector]
    public string surveyURL;
    // Flag to indicate post-game survey
    [HideInInspector]
    public bool isFinalSurvey = false;

    void Awake()
    {
        popupPanel.SetActive(false);
        openSurveyButton.onClick.AddListener(OnOpenSurvey);
        continueButton.onClick.AddListener(OnContinue);
    }

    /// <summary>Show the popup and pause the game.</summary>
    public void Show()
    {
        Time.timeScale = 0f;
        Player.inst.canMove = false;
        Player.inst.canSwap = false;  
        Player.inst.canAttack = false;
        popupPanel.SetActive(true);
    }

    /// <summary>Opens the Qualtrics tab.</summary>
    private void OnOpenSurvey()
    {
        Application.OpenURL(surveyURL);
    }

    /// <summary>Closes the popup, resumes game, notifies GameManager.</summary>
    private void OnContinue()
    {
        popupPanel.SetActive(false);
        Time.timeScale = 1f;
        if (isFinalSurvey)
            GameManager.inst.OnFinalSurveyContinue();
        else
            GameManager.inst.OnSurveyContinue();
    }
}
