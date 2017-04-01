using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConversationUIManager : MonoBehaviour
{
    static ConversationUIManager _instance;
    public static ConversationUIManager Instance() { return _instance; }
    public GameObject player;

    public GameObject UIRoot;
    public Text SpeakerNameField;
    public Text ConversationBox;
    public Button[] answers;
    public string PlayerName = "ShitDev";

    private VIDE_Data _currentDialogue;

    void Awake()
    {
        if (_instance)
        {
            Debug.LogError("Should not be 2 conversation ui managers in scene");
        }
        _instance = this;

        _currentDialogue = gameObject.AddComponent<VIDE_Data>();
        _currentDialogue.LoadDialogues();
    }

    void Start()
    {
        SetPlayerControllerActive(true);
        UIRoot.SetActive(false);
    }

    void SetPlayerControllerActive(bool toSet)
    {
        player.GetComponent<CustomCharacterController>().enabled = toSet;
        foreach( var comp in player.GetComponentsInChildren<RotateWithMouse>() )
        {
            Cursor.lockState = toSet ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !toSet;
            comp.enabled = toSet;
        }
    }

    public void StartConversation(VIDE_Assign diagToLoad)
    {
        _currentDialogue.OnActionNode += ActionHandler;
        _currentDialogue.OnNodeChange += NodeChangeAction;
        _currentDialogue.OnEnd += EndConversation;

        SetPlayerControllerActive(false);
        UIRoot.SetActive(true);
        _currentDialogue.BeginDialogue( diagToLoad );
    }

    public void ActionHandler(int action)
    {
        Debug.Log("ACTION TRIGGERED: " + action.ToString());
    }

    void NodeChangeAction(VIDE_Data.NodeData data)
    {
        if (data.currentIsPlayer)
        {
            //SetOptions(data.playerComments);
            SpeakerNameField.text = PlayerName;
        }
        else
        {
            SpeakerNameField.text = _currentDialogue.assigned.dialogueName;
        }
        ConversationBox.text = data.npcComment[data.npcCommentIndex];
    }

    public void EndConversation(VIDE_Data.NodeData data )
    {
        _currentDialogue.OnActionNode -= ActionHandler;
        _currentDialogue.OnNodeChange -= NodeChangeAction;
        _currentDialogue.OnEnd -= EndConversation;

        SetPlayerControllerActive(true);
        _currentDialogue.EndDialogue();
        UIRoot.SetActive(false);
    }

    void Update()
    {

    }
}
