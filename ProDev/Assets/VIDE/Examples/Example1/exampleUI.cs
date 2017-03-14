using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.UI;

public class exampleUI : MonoBehaviour
{
    //This script will handle everything related to dialogue interface
    //It will use a VIDE_Data component to load dialogues and retrieve node data
    //to draw the text for the dialogue

    [System.NonSerialized]
    public VIDE_Data dialogue; //We'll be sending and retrieving all data from VIDE_Data

    //These are just references to UI components and objects in the scene
    public Text npcText;
    public Text npcName;
    public Text playerText;
    public GameObject itemPopUp;
    public GameObject uiContainer;

    //We'll use these later
    bool dialoguePaused = false;
    bool animatingText = false;

    //Demo variables
    public List<string> example_Items = new List<string>();
    public List<string> example_ItemInventory = new List<string>();

    //We'll be using this to store the current player dialogue options
    private List<Text> currentOptions = new List<Text>();

    //Here I'm assigning the variable a new component of its required type
    void Start()
    {
        dialogue = gameObject.AddComponent<VIDE_Data>(); //Automatically adding the VIDE_Data component
        dialogue.OnActionNode += ActionHandler; //Subscribe to listen to triggered actions
        dialogue.OnLoaded += OnLoadedAction; //Subscribe
        dialogue.LoadDialogues(); //Load all dialogues to memory so that we dont spend time doing so later

        //Remember you can also manually add the VIDE_Data script as a component in the Inspector, 
        //then drag&drop it on your 'dialogue' variable slot
    }

    //Just so we know when we finished loading all dialogues, then we unsubscribe
    void OnLoadedAction()
    {
        Debug.Log("Finished loading all dialogues");
        dialogue.OnLoaded -= OnLoadedAction;
    }

    void OnDisable()
    {
        dialogue.OnActionNode -= ActionHandler;
    }

    //This begins the conversation (Called by examplePlayer script)
    public void Begin(VIDE_Assign diagToLoad)
    {
        //Let's clean the NPC text variables
        npcText.text = "";
        npcName.text = "";

        //First step is to call BeginDialogue, passing the required VIDE_Assign component 
        //This will store the first Node data in dialogue.nodeData
        //But before we do so, let's subscribe to certain events that will allow us to easily
        //Handle the node-changes
        dialogue.OnActionNode += ActionHandler;
        dialogue.OnNodeChange += NodeChangeAction;
        dialogue.OnEnd += EndDialogue;

        SpecialStartNodeOverrides(diagToLoad); //This one checks for special cases when overrideStartNode could change right before starting a conversation

        dialogue.BeginDialogue(diagToLoad); //Begins conversation
        uiContainer.SetActive(true);
    }

    //Demo on yet another way to modify the flow of the conversation
    void SpecialStartNodeOverrides(VIDE_Assign diagToLoad)
    {
        //Get the item from CrazyCap to trigger this one on Charlie
        if (diagToLoad.dialogueName == "Charlie")
        {
            if (example_ItemInventory.Count > 0 && diagToLoad.overrideStartNode == -1)
                diagToLoad.overrideStartNode = 16;
        }
    }

    //Input related stuff (scroll through player choices and update highlight)
    void Update()
    {
        //Lets just store the Node Data variable for the sake of fewer words
        var data = dialogue.nodeData;

        if (dialogue.isLoaded) //Only if
        {
            //Scroll through Player dialogue options
            if (!data.pausedAction)
            {
                if (Input.GetKeyDown(KeyCode.S))
                {
                    if (data.selectedOption < currentOptions.Count - 1)
                        data.selectedOption++;
                }
                if (Input.GetKeyDown(KeyCode.W))
                {
                    if (data.selectedOption > 0)
                        data.selectedOption--;
                }

                //Color the Player options. Blue for the selected one
                for (int i = 0; i < currentOptions.Count; i++)
                {
                    currentOptions[i].color = Color.black;
                    if (i == data.selectedOption) currentOptions[i].color = Color.blue;
                }
            }
        }
    }

    //examplePlayer.cs calls this one to move forward in the conversation
    public void CallNext()
    {
        //Let's not go forward if text is currently being animated, but let's speed it up.
        if (animatingText) { animatingText = false; return; }

        if (!dialoguePaused) //Only if
        {
            //We check for current extraData before moving forward to do special actions
            //ExtraDataLookUp returns true if an action requires to skip dialogue.Next()
            if (ExtraDataLookUp(dialogue.nodeData)) return;

            dialogue.Next(); //We call the next node and populate nodeData with new data
            return;
        }
        //This will just disable the item popup if it is enabled
        if (itemPopUp.activeSelf)
        {
            dialoguePaused = false;
            itemPopUp.SetActive(false);
        }
    }       

    //Another way to handle Action Nodes is to listen to the OnActionNode event, which sends the ID of the action node
    void ActionHandler(int action)
    {
        Debug.Log("ACTION TRIGGERED: " + action.ToString());
    }

    //We listen to OnNodeChange to update our UI with each new nodeData
    //This should happen right after calling VIDE_Data.Next()
    void NodeChangeAction(VIDE_Data.NodeData data)
    {
        //Reset some variables
        npcText.text = "";
        npcText.transform.parent.gameObject.SetActive(false);
        playerText.transform.parent.gameObject.SetActive(false);

        //Look for dynamic text change in extraData
        if (data.extraData == "nameLookUp")
            nameLookUp(data);

        //If this new Node is a Player Node, set the player choices offered by the node
        if (data.currentIsPlayer)
        {
            SetOptions(data.playerComments);
            playerText.transform.parent.gameObject.SetActive(true);
        }
        else  //If it's an NPC Node, let's just update NPC's text
        {
            StartCoroutine(AnimateText(data));

            //If it has a tag, show it, otherwise show the dialogueName
            if (data.tag.Length > 0)
                npcName.text = data.tag;
            else
                npcName.text = dialogue.assigned.dialogueName;

            npcText.transform.parent.gameObject.SetActive(true);
        }
    }

    //Check to see if there's extraData and if so, we do stuff
    bool ExtraDataLookUp(VIDE_Data.NodeData data)
    {
        if (!data.currentIsPlayer && !data.pausedAction) {
            //This whole chunk will read the X_item_X nomenclature, parse it, and use the information
            if (data.extraData.Contains("_item_")) 
            {
                int[] lineAndIndex = ParseItem(data.extraData);
                if (data.npcCommentIndex == lineAndIndex[0] )
                {
                    if (!example_ItemInventory.Contains(example_Items[lineAndIndex[1]]))
                    {
                        GiveItem(lineAndIndex[1]);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    //Just a parsing method
    int[] ParseItem(string itemString)
    {
        int[] parsed = new int[2];
        string[] s = itemString.Split('_');
        System.Int32.TryParse(s[0], out parsed[0]);
        System.Int32.TryParse(s[2], out parsed[1]);
        return parsed;
    }

    //Adds item to demo inventory, shows item popup, and pauses dialogue
    void GiveItem(int itemIndex)
    {
        example_ItemInventory.Add(example_Items[itemIndex]);
        itemPopUp.SetActive(true);
        string text = "You've got a <color=blue>" + example_Items[itemIndex] + "</color>!";
        itemPopUp.transform.GetChild(0).GetComponent<Text>().text = text;
        dialoguePaused = true;
    }

    //This uses the returned string[] from nodeData.playerComments to create the UIs for each comment
    //It first cleans, then it instantiates new options
    //This is for demo only, you shouldn´t instantiate/destroy so constantly
    public void SetOptions(string[] opts)
    {
        //Destroy the current options
        foreach (UnityEngine.UI.Text op in currentOptions)
            Destroy(op.gameObject);

        //Clean the variable
        currentOptions = new List<UnityEngine.UI.Text>();

        //Create the options
        for (int i = 0; i < opts.Length; i++)
        {
            GameObject newOp = Instantiate(playerText.gameObject, playerText.transform.position, Quaternion.identity) as GameObject;
            newOp.SetActive(true);
            newOp.transform.SetParent(playerText.transform.parent, true);
            newOp.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 20 - (20 * i));
            newOp.GetComponent<UnityEngine.UI.Text>().text = opts[i];
            currentOptions.Add(newOp.GetComponent<UnityEngine.UI.Text>());
        }
    }

    //This will replace any "[NAME]" with the name of the gameobject holding the VIDE_Assign
    //Will also replace [WEAPON] with a different variable
    void nameLookUp(VIDE_Data.NodeData data)
    {
        if (data.npcComment[data.npcCommentIndex].Contains("[NAME]"))
        data.npcComment[data.npcCommentIndex] = data.npcComment[data.npcCommentIndex].Replace("[NAME]", dialogue.assigned.gameObject.name);

        if (data.npcComment[data.npcCommentIndex].Contains("[WEAPON]"))
        data.npcComment[data.npcCommentIndex] = data.npcComment[data.npcCommentIndex].Replace("[WEAPON]", example_ItemInventory[0]);
    }

    //Very simple text animation usin StringBuilder
    public IEnumerator AnimateText(VIDE_Data.NodeData data)
    {
        animatingText = true;
        string text = data.npcComment[data.npcCommentIndex];

        if (!data.currentIsPlayer)
        {
            StringBuilder builder = new StringBuilder();
            int charIndex = 0;
            while (npcText.text != text)
            {
                if (!animatingText) break; //CallNext() makes this possible to speed things up

                builder.Append(text[charIndex]);
                charIndex++;
                npcText.text = builder.ToString();
                yield return new WaitForSeconds(0.02f);
            }
        }

        npcText.text = data.npcComment[data.npcCommentIndex]; //Now just copy full text		
        animatingText = false;
    }

    //Unsuscribe from everything, disable UI, and end dialogue
    void EndDialogue(VIDE_Data.NodeData data)
    {
        dialogue.OnActionNode -= ActionHandler;
        dialogue.OnNodeChange -= NodeChangeAction;
        dialogue.OnEnd -= EndDialogue;
        uiContainer.SetActive(false);
        dialogue.EndDialogue();
    }

    //Example method called by an Action Node
    public void ActionGiveItem(int item)
    {
        //Do something here
    }
}
