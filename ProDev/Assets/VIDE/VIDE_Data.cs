/*
 * Copyright (c) 2017 Christian Henderson
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
 * CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
 * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

public class VIDE_Data : MonoBehaviour
{
    /*
     * This component is the source of all data you'll be needing to populate your dialogue interface.
     * It will manage the flow of the conversation based on the current node data stored in a variable called nodeData.
     * All you need is to attach this component to the game object that will manage your dialogue UI.
     * Then call BeginDialogue() on it to begin the conversation with an NPC. 
     * The rest is up to the Next() method to advance in the conversation up until you call EndDialogue()
     * Check exampleUI.cs for an already-setup UI manager example.
     */
    public int assignedIndex = 0;
    public static VIDE_Data instance;

    public class Diags
    {
        public string name = string.Empty;
        public bool loaded = false;
        public int start = -1;
        public string loadTag;
        public List<CommentSet> playerNodes = new List<CommentSet>();
        public List<Answer> npcNodes = new List<Answer>();
        public List<ActionNode> actionNodes = new List<ActionNode>();

        public Diags(string s, string t)
        {
            name = s;
            loadTag = t;
        }
    }

    public List<Diags> diags = new List<Diags>();
    public int currentDiag = -1;

    private Answer currentNPCStep;
    private CommentSet currentPlayerStep;
    private ActionNode currentActionNode;
    private ActionNode lastActionNode;
    private bool jumped = false;
    private int startPoint = -1;

    public VIDE_Assign assigned;
    public bool isLoaded;
    public NodeData nodeData;

    /* Events */

    public delegate void ActionEvent(int nodeID);
    public event ActionEvent OnActionNode;

    public delegate void NodeChange(NodeData data);
    public event NodeChange OnNodeChange;
    public event NodeChange OnEnd;

    public delegate void LoadUnload();
    public event LoadUnload OnLoaded;
    public event LoadUnload OnUnloaded;

    void Awake()
    {
        instance = this;
        TextAsset[] files = Resources.LoadAll<TextAsset>("Dialogues");
        List<string> names = new List<string>();
        for (int i = 0; i < files.Length; i++)
            names.Add(files[i].name);

        names.Sort();

        for (int i = 0; i < names.Count; i++)
        {
            string ttag = "";

            if (Resources.Load("Dialogues/" + names[i]) == null) break;
            Dictionary<string, object> dict = SerializeHelper.ReadFromFile(names[i]) as Dictionary<string, object>;
            if (dict.ContainsKey("loadTag"))
            {
                ttag = (string)dict["loadTag"];
            }

            diags.Add(new Diags(names[i], ttag));
        }
        //Debug.Log("Found: " + diags.Count.ToString() + " dialogues");
    }

    //The class that contains all of the node variables
    public class NodeData
    {
        public bool currentIsPlayer;
        public bool pausedAction;
        public bool isEnd;
        public int nodeID;
        public string[] playerComments;
        public string[] playerCommentExtraData;
        public string[] npcComment;
        public int npcCommentIndex;
        public int selectedOption;
        public string extraData;
        public string tag;
        public string playerTag;

        public NodeData(bool isP, bool isE, int id, string[] pt, string[] pcExtraD, string[] npt, string exData, string tagt, string ptag)
        {
            currentIsPlayer = isP;
            isEnd = isE;
            nodeID = id;
            playerComments = pt;
            playerCommentExtraData = pcExtraD;
            npcComment = npt;
            npcCommentIndex = 0;
            selectedOption = 0;
            extraData = exData;
            tag = tagt;
            playerTag = ptag;
            pausedAction = false;

        }

        public NodeData()
        {
            currentIsPlayer = true;
            isEnd = false;
            nodeID = -1;
            selectedOption = 0;
        }

    }

    /// <summary>
    /// Ignores current nodeData state and jumps directly to the specified node.
    /// </summary>
    /// <returns>
    /// The node.
    /// </returns>
    /// <param name='id'>
    /// The ID of your Node. Get it from the Dialogue Editor.
    /// </param>
    public NodeData SetNode(int id)
    {
        if (!isLoaded)
        {
            Debug.LogError("You must call the 'BeginDialogue()' method before calling the 'Next()' method!");
            return null;
        }

        //Look for Node with given ID
        bool foundID = false;
        bool isPl = false;
        bool isAct = false;
        for (int i = 0; i < diags[currentDiag].playerNodes.Count; i++)
        {
            if (diags[currentDiag].playerNodes[i].ID == id)
            {
                currentPlayerStep = diags[currentDiag].playerNodes[i];
                isPl = true;
                foundID = true;
            }
        }
        if (!foundID)
        {
            for (int i = 0; i < diags[currentDiag].npcNodes.Count; i++)
            {
                if (diags[currentDiag].npcNodes[i].ID == id)
                {
                    currentNPCStep = diags[currentDiag].npcNodes[i];
                    foundID = true;
                }
            }
        }
        if (!foundID)
        {
            for (int i = 0; i < diags[currentDiag].actionNodes.Count; i++)
            {
                if (diags[currentDiag].actionNodes[i].ID == id)
                {
                    currentActionNode = diags[currentDiag].actionNodes[i];
                    foundID = true;
                    isAct = true;
                }
            }
        }
        if (!foundID)
        {
            Debug.LogError("Could not find a Node with ID " + id.ToString());
            return null;
        }

        /* Action node */

        if (isAct)
        {
            lastActionNode = currentActionNode;
            nodeData = new NodeData();
            DoAction();
            return nodeData;
        }

        /* Action end */

        if (isPl)
        {
            nodeData = new NodeData(true, false, currentPlayerStep.ID, GetOptions(), GetExtraData(), null, null, null, currentPlayerStep.playerTag);
            if (OnNodeChange != null) OnNodeChange(nodeData);
            jumped = true;
            return nodeData;
        }
        else
        {
            List<string> ns = new List<string>();

            string[] rawSplit = Regex.Split(currentNPCStep.text, "<br>");
            foreach (string s in rawSplit)
            {
                if (s != "" && s != " ") ns.Add(s.Trim());
            }

            nodeData = new NodeData(isPl, false, id, null, null, ns.ToArray(), currentNPCStep.extraData, currentNPCStep.tag, "");
            if (OnNodeChange != null) OnNodeChange(nodeData);
            return nodeData;
        }
    }
    /// <summary>
    /// Populates nodeData with the data from next Node based on the current nodeData.
    /// </summary>
    /// <returns></returns>
    public NodeData Next()
    {
        if (!isLoaded)
        {
            Debug.LogError("You must call the 'BeginDialogue()' method before calling the 'Next()' method!");
            return null;
        }

        int option = 0;
        bool nextIsPlayer = true;

        if (nodeData != null)
            option = nodeData.selectedOption;

        if (!jumped && nodeData != null) //Here's where we check if we end
        {
            if (!nodeData.currentIsPlayer && currentNPCStep != null)
            {
                if (currentNPCStep.outputNPC == null && currentNPCStep.outputSet == null && currentNPCStep.outAction == null && nodeData.npcCommentIndex == nodeData.npcComment.Length - 1)
                {
                    nodeData.isEnd = true;
                    if (OnEnd!= null) OnEnd(nodeData);
                    return nodeData;
                }
                else if (currentNPCStep.outputNPC == null && currentNPCStep.outputSet == null && currentNPCStep.outAction == null && nodeData.npcComment.Length < 1)
                {
                    nodeData.isEnd = true;
                    if (OnEnd!= null) OnEnd(nodeData);
                    return nodeData;
                }
            }
        }

        if (nodeData != null)
            if (nodeData.currentIsPlayer)
            {
                //Mark end of conversation for player node
                if (currentPlayerStep != null)
                    if (currentPlayerStep.comment[option].outputAnswer == null && currentPlayerStep.comment[option].outAction == null)
                    {
                        nodeData.isEnd = true;
                        if (OnEnd!= null) OnEnd(nodeData);
                        return nodeData;
                    }
            }
        //If action node is connected to nothing, then it's the end
        if (lastActionNode != null)
        {
            if (lastActionNode.outPlayer == null && lastActionNode.outNPC == null && lastActionNode.outAction == null)
            {
                nodeData.isEnd = true;
                if (OnEnd!= null) OnEnd(nodeData);
                return nodeData;
            }
        }

        jumped = false;

        /* Action Node? */

        if (currentActionNode == null)
        {
            if (nodeData.currentIsPlayer)
            {
                currentActionNode = currentPlayerStep.comment[option].outAction;
            }
            else
            {
                if (nodeData.npcCommentIndex == nodeData.npcComment.Length-1) //Fix 
                currentActionNode = currentNPCStep.outAction;
            }
        }
        else
        {
            currentActionNode = currentActionNode.outAction;
        }

        //If we found actio node, let's go to it.
        if (currentActionNode != null)
        {
            lastActionNode = currentActionNode;
            DoAction();
            return nodeData;
        }
        else if (lastActionNode != null)
        {
            if (lastActionNode.outNPC != null)
            {
            }
        }

        /* END Action Node */

        if (!nodeData.currentIsPlayer)
        {
            nextIsPlayer = true;
            if (currentNPCStep.outputSet == null)
            {
                nextIsPlayer = false;
            }
        }
        else
        {
            nextIsPlayer = false;
        }

        if (!nodeData.currentIsPlayer) // WE ARE CURRENTLY NPC NODE
        {
            //Let's scroll through split comments first
            if (nodeData.npcComment.Length > 0)
            {
                if (nodeData.npcCommentIndex != nodeData.npcComment.Length - 1)
                {
                    nodeData.npcCommentIndex++;
                    lastActionNode = null;
                    if (OnNodeChange != null) OnNodeChange(nodeData);
                    return nodeData;
                }
            }

            if (lastActionNode != null)
                if (lastActionNode.outNPC != null)
                {
                    currentNPCStep = lastActionNode.outNPC;

                    List<string> ns = new List<string>();
                    string[] rawSplit = Regex.Split(currentNPCStep.text, "<br>");
                    foreach (string s in rawSplit)
                    { if (s != "" && s != " ") ns.Add(s.Trim()); }

                    lastActionNode = null;
                    nodeData = new NodeData(false, false, currentNPCStep.ID, null, null, ns.ToArray(), currentNPCStep.extraData, currentNPCStep.tag, "");
                    if (OnNodeChange != null) OnNodeChange(nodeData);
                    return nodeData;
                }

            if (lastActionNode != null)
                if (lastActionNode.outPlayer != null)
                {
                    currentPlayerStep = lastActionNode.outPlayer;
                    lastActionNode = null;
                    nodeData = new NodeData(true, false, currentPlayerStep.ID, GetOptions(), GetExtraData(), null, null, null, currentPlayerStep.playerTag);
                    if (OnNodeChange != null) OnNodeChange(nodeData);
                    return nodeData;
                }


            if (nextIsPlayer) // NEXT IS PLAYER
            {
                lastActionNode = null;
                currentPlayerStep = currentNPCStep.outputSet;
                nodeData = new NodeData(true, false, currentPlayerStep.ID, GetOptions(), GetExtraData(), null, null, null, currentPlayerStep.playerTag);
                if (OnNodeChange != null) OnNodeChange(nodeData);
            }
            else // NEXT IS ANOTHER NPC NODE
            {
                currentNPCStep = currentNPCStep.outputNPC;
                List<string> ns = new List<string>();

                string[] rawSplit = Regex.Split(currentNPCStep.text, "<br>");
                foreach (string s in rawSplit)
                { if (s != "" && s != " ") ns.Add(s.Trim()); }

                lastActionNode = null;
                nodeData = new NodeData(false, false, currentNPCStep.ID, null, null, ns.ToArray(), currentNPCStep.extraData, currentNPCStep.tag, "");
                if (OnNodeChange != null) OnNodeChange(nodeData);
            }

            return nodeData;
        }
        else // WE ARE CURRENTLY PLAYER NODE
        {
            //Pick next NPC node based on player choice OR next Player Node if there was an Action Node beforehand
            if (lastActionNode == null)
            {
                currentNPCStep = currentPlayerStep.comment[option].outputAnswer;
            }
            else
            {
                if (lastActionNode.outNPC != null) currentNPCStep = lastActionNode.outNPC;
                if (lastActionNode.outPlayer != null)
                {
                    currentPlayerStep = lastActionNode.outPlayer;
                    lastActionNode = null;
                    nodeData = new NodeData(true, false, currentPlayerStep.ID, GetOptions(), GetExtraData(), null, null, null, currentPlayerStep.playerTag);
                    if (OnNodeChange != null) OnNodeChange(nodeData);
                    return nodeData;
                }
            }

            List<string> ns = new List<string>();

            string[] rawSplit = Regex.Split(currentNPCStep.text, "<br>");
            foreach (string s in rawSplit)
            { if (s != "" && s != " ") ns.Add(s.Trim()); }

            lastActionNode = null;
            nodeData = new NodeData(false, false, currentNPCStep.ID, null, null, ns.ToArray(), currentNPCStep.extraData, currentNPCStep.tag, "");
            if (OnNodeChange != null) OnNodeChange(nodeData);
            return nodeData;
        }
    }

    void DoAction()
    {
        if (OnActionNode != null)
            OnActionNode(lastActionNode.ID);

        //Do predefined actions
        if (lastActionNode.ovrStartNode > -1)
            assigned.overrideStartNode = lastActionNode.ovrStartNode;
        if (lastActionNode.renameDialogue.Length > 0)
            assigned.dialogueName = lastActionNode.renameDialogue;


        var objects = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.name == currentActionNode.gameObjectName);

        foreach (GameObject g in objects)
        {
            if (currentActionNode.paramType == 0)
                g.SendMessage(currentActionNode.methodName, SendMessageOptions.DontRequireReceiver);
            if (currentActionNode.paramType == 1)
                g.SendMessage(currentActionNode.methodName, currentActionNode.param_bool, SendMessageOptions.DontRequireReceiver);
            if (currentActionNode.paramType == 2)
                g.SendMessage(currentActionNode.methodName, currentActionNode.param_string, SendMessageOptions.DontRequireReceiver);
            if (currentActionNode.paramType == 3)
                g.SendMessage(currentActionNode.methodName, currentActionNode.param_int, SendMessageOptions.DontRequireReceiver);
            if (currentActionNode.paramType == 4)
                g.SendMessage(currentActionNode.methodName, currentActionNode.param_float, SendMessageOptions.DontRequireReceiver);
        }


        if (!currentActionNode.pauseHere)
        {
            Next();
        }
        else
        {
            nodeData.pausedAction = true;
        }

    }

    /// <summary>
    /// Loads up the dialogue just sent. Populates the nodeData variable with the first Node based on the Start Node. Also returns the current NodeData package.
    /// </summary>
    /// <param name="diagToLoad"></param>
    /// <returns>NodeData</returns>
    public NodeData BeginDialogue(VIDE_Assign diagToLoad)
    {
        if (diagToLoad.assignedIndex < 0 || diagToLoad.assignedIndex > diagToLoad.diags.Count - 1)
        {
            Debug.LogError("No dialogue assigned to VIDE_Assign!");
            return null;
        }

        //Check if the dialogue is already loaded
        currentDiag = diagToLoad.assignedIndex; //assign current dialogue index
        if (!diags[currentDiag].loaded)
        {
            //Let's load the dialogue 
            if (Load(diagToLoad.diags[diagToLoad.assignedIndex]))
            {
                isLoaded = true;
                //Debug.Log("Loaded: " + diagToLoad.diags[diagToLoad.assignedIndex]);
            }
            else
            {
                isLoaded = false;
                currentDiag = -1;
                Debug.LogError("Failed to load '" + diagToLoad.diags[diagToLoad.assignedIndex] + "'");
                return null;
            }
        }
        else
        {
            isLoaded = true;
            //Debug.Log(diagToLoad.diags[diagToLoad.assignedIndex] + " was already loaded");
            //Debug.Log(diags[currentDiag].npcNodes.Count);
        }

        //Make sure that variables were correctly reset after last conversation
        if (nodeData != null)
        {
            Debug.LogError("You forgot to call 'EndDialogue()' on last conversation!");
            return null;
        }

        assigned = diagToLoad;
        startPoint = diags[currentDiag].start;

        if (assigned.overrideStartNode != -1)
            startPoint = assigned.overrideStartNode;

        int startIndex = -1;
        bool isPlayer = false;
        bool isAct = false;

        for (int i = 0; i < diags[currentDiag].npcNodes.Count; i++)
            if (startPoint == diags[currentDiag].npcNodes[i].ID) { startIndex = i; isPlayer = false; break; }
        for (int i = 0; i < diags[currentDiag].playerNodes.Count; i++)
            if (startPoint == diags[currentDiag].playerNodes[i].ID) { startIndex = i; isPlayer = true; break; }
        for (int i = 0; i < diags[currentDiag].actionNodes.Count; i++)
            if (startPoint == diags[currentDiag].actionNodes[i].ID)
            {
                startIndex = i;
                currentActionNode = diags[currentDiag].actionNodes[i]; isPlayer = true; isAct = true; break;
            }

        /* Action node */

        if (isAct)
        {
            lastActionNode = currentActionNode;
            nodeData = new NodeData();
            DoAction();
            return nodeData;
        }

        /* Action end */

        if (startIndex == -1)
        {
            Debug.LogError("Start point not found! Check your IDs!");
            return null;
        }

        if (isPlayer)
        {
            currentPlayerStep = diags[currentDiag].playerNodes[startIndex];

            lastActionNode = null;
            nodeData = new NodeData(true, false, currentPlayerStep.ID, GetOptions(), GetExtraData(), null, null, null, currentPlayerStep.playerTag);
            if (OnNodeChange != null) OnNodeChange(nodeData);
            return nodeData;
        }
        else
        {
            currentNPCStep = diags[currentDiag].npcNodes[startIndex];

            List<string> ns = new List<string>();

            string[] rawSplit = Regex.Split(currentNPCStep.text, "<br>");
            foreach (string s in rawSplit)
            { if (s != "" && s != " ") ns.Add(s.Trim()); }

            lastActionNode = null;
            nodeData = new NodeData(false, false, currentNPCStep.ID, null, null, ns.ToArray(), currentNPCStep.extraData, currentNPCStep.tag, "");
            if (OnNodeChange != null) OnNodeChange(nodeData);
            return nodeData;
        }


    }
    /// <summary>
    /// Wipes out all data and unloads the current VIDE_Assign, raising its interactionCount.
    /// </summary>
    public void EndDialogue()
    {
        nodeData = null;
        if (assigned != null)
            assigned.interactionCount++;
        assigned = null;
        startPoint = -1;
        isLoaded = false;
        currentDiag = -1;
        currentNPCStep = null;
        currentPlayerStep = null;
        currentActionNode = null;
        lastActionNode = null;
    }

    private string[] GetOptions()
    {
        List<string> op = new List<string>();

        if (currentPlayerStep == null)
        {
            return op.ToArray();
        }

        for (int i = 0; i < currentPlayerStep.comment.Count; i++)
        {
            op.Add(currentPlayerStep.comment[i].text);
        }

        return op.ToArray();
    }

    private string[] GetExtraData()
    {
        List<string> op = new List<string>();

        if (currentPlayerStep == null)
        {
            return op.ToArray();
        }

        for (int i = 0; i < currentPlayerStep.comment.Count; i++)
        {
            if (currentPlayerStep.comment[i].extraData.Length > 0)
                op.Add(currentPlayerStep.comment[i].extraData);
            else
                op.Add(string.Empty);
        }

        return op.ToArray();
    }

    //The following are all of the classes and methods we need for constructing the nodes
    class SerializeHelper
    {
        //static string fileDataPath = Application.dataPath + "/VIDE/dialogues/";
        public static object ReadFromFile(string filename)
        {
            string jsonString = Resources.Load<TextAsset>("Dialogues/" + filename).text;
            return MiniJSON_VIDE.DiagJson.Deserialize(jsonString);
        }
    }

    public class CommentSet
    {
        public List<Comment> comment;
        public int ID;
        public string playerTag;
        public bool endConversation = false;

        public CommentSet(int comSize, int id, string tag)
        {
            comment = new List<Comment>();
            ID = id;
            playerTag = tag;
            for (int i = 0; i < comSize; i++)
                comment.Add(new Comment());
        }
    }

    public class Comment
    {
        public string text;
        public string extraData;
        public CommentSet inputSet;
        public ActionNode outAction;
        public Answer outputAnswer;

        public Comment()
        {
            text = "";
            extraData = "";
        }
        public Comment(CommentSet id)
        {
            outputAnswer = null;
            inputSet = id;
            text = "Comment...";
            extraData = "ExtraData...";
        }
    }

    public class Answer
    {
        public string text;
        public CommentSet outputSet;
        public Answer outputNPC;
        public ActionNode outAction;

        public string extraData;
        public string tag;

        public int ID;

        public Answer(string t, int id, string exD, string tagt)
        {
            text = t;
            outputSet = null;
            outputNPC = null;
            extraData = exD;
            tag = tagt;
            ID = id;
        }

    }

    public class ActionNode
    {
        public bool pauseHere = false;
        public string gameObjectName;
        public string methodName;
        public int paramType;

        public bool param_bool;
        public string param_string;
        public int param_int;
        public float param_float;

        public int ID;
        public CommentSet outPlayer;
        public Answer outNPC;
        public ActionNode outAction;

        public int ovrStartNode = -1;
        public string renameDialogue = string.Empty;

        public ActionNode(int id, string meth, string goMeth, bool pau, bool pb, string ps, int pi, float pf)
        {
            pauseHere = pau;
            methodName = meth;
            gameObjectName = goMeth;

            param_bool = pb;
            param_string = ps;
            param_int = pi;
            param_float = pf;

            outPlayer = null;
            outNPC = null;
            outAction = null;
            ID = id;
        }

    }

    void addComment(CommentSet id)
    {
        id.comment.Add(new Comment(id));
    }

    void addAnswer(string t, int id, string exD, string tagt)
    {
        diags[currentDiag].npcNodes.Add(new Answer(t, id, exD, tagt));
    }

    void addSet(int cSize, int id, string tag)
    {
        diags[currentDiag].playerNodes.Add(new CommentSet(cSize, id, tag));
    }

    //This method will load the dialogue from the DialogueAssign component sent.
    bool Load(string dName)
    {

        diags[currentDiag] = new Diags(diags[currentDiag].name, diags[currentDiag].loadTag);

        if (Resources.Load("Dialogues/" + dName) == null) return false;

        Dictionary<string, object> dict = SerializeHelper.ReadFromFile(dName) as Dictionary<string, object>;

        int pDiags = (int)((long)dict["playerDiags"]);
        int nDiags = (int)((long)dict["npcDiags"]);
        int aDiags = 0;
        if (dict.ContainsKey("actionNodes")) aDiags = (int)((long)dict["actionNodes"]);

        diags[currentDiag].start = (int)((long)dict["startPoint"]);

        if (dict.ContainsKey("loadTag"))
        {
            diags[currentDiag].loadTag = (string)dict["loadTag"];

        }

        //Create first...
        for (int i = 0; i < pDiags; i++)
        {
            string tagt = "";

            if (dict.ContainsKey("pd_pTag_" + i.ToString()))
                tagt = (string)dict["pd_pTag_" + i.ToString()];


            addSet(
                (int)((long)dict["pd_comSize_" + i.ToString()]),
                (int)((long)dict["pd_ID_" + i.ToString()]),
                tagt
                );
        }

        for (int i = 0; i < nDiags; i++)
        {
            string tagt = "";

            if (dict.ContainsKey("nd_tag_" + i.ToString()))
                tagt = (string)dict["nd_tag_" + i.ToString()];

            addAnswer(
                (string)dict["nd_text_" + i.ToString()],
                (int)((long)dict["nd_ID_" + i.ToString()]),
                (string)dict["nd_extraData_" + i.ToString()],
                tagt
                );
        }
        for (int i = 0; i < aDiags; i++)
        {
            float pFloat;
            var pfl = dict["ac_pFloat_" + i.ToString()];
            if (pfl.GetType() == typeof(System.Double))
                pFloat = System.Convert.ToSingle(pfl);
            else
                pFloat = (float)(long)pfl;


            diags[currentDiag].actionNodes.Add(new ActionNode(
                (int)((long)dict["ac_ID_" + i.ToString()]),
                (string)dict["ac_meth_" + i.ToString()],
                (string)dict["ac_goName_" + i.ToString()],
                (bool)dict["ac_pause_" + i.ToString()],
                (bool)dict["ac_pBool_" + i.ToString()],
                (string)dict["ac_pString_" + i.ToString()],
                (int)((long)dict["ac_pInt_" + i.ToString()]),
                pFloat
                ));

            if (dict.ContainsKey("ac_ovrStartNode_" + i.ToString()))
                diags[currentDiag].actionNodes[diags[currentDiag].actionNodes.Count - 1].ovrStartNode = (int)((long)dict["ac_ovrStartNode_" + i.ToString()]);

            if (dict.ContainsKey("ac_renameDialogue_" + i.ToString()))
                diags[currentDiag].actionNodes[diags[currentDiag].actionNodes.Count - 1].renameDialogue = (string)dict["ac_renameDialogue_" + i.ToString()];
        }

        //Connect now...
        for (int i = 0; i < diags[currentDiag].playerNodes.Count; i++)
        {
            for (int ii = 0; ii < diags[currentDiag].playerNodes[i].comment.Count; ii++)
            {
                diags[currentDiag].playerNodes[i].comment[ii].text = (string)dict["pd_" + i.ToString() + "_com_" + ii.ToString() + "text"];

                if (dict.ContainsKey("pd_" + i.ToString() + "_com_" + ii.ToString() + "extraD"))
                    diags[currentDiag].playerNodes[i].comment[ii].extraData = (string)dict["pd_" + i.ToString() + "_com_" + ii.ToString() + "extraD"];

                int index = (int)((long)dict["pd_" + i.ToString() + "_com_" + ii.ToString() + "iSet"]);

                if (index != -1)
                    diags[currentDiag].playerNodes[i].comment[ii].inputSet = diags[currentDiag].playerNodes[index];

                index = (int)((long)dict["pd_" + i.ToString() + "_com_" + ii.ToString() + "oAns"]);

                if (index != -1)
                    diags[currentDiag].playerNodes[i].comment[ii].outputAnswer = diags[currentDiag].npcNodes[index];

                index = -1;
                if (dict.ContainsKey("pd_" + i.ToString() + "_com_" + ii.ToString() + "oAct"))
                    index = (int)((long)dict["pd_" + i.ToString() + "_com_" + ii.ToString() + "oAct"]);

                if (index != -1)
                    diags[currentDiag].playerNodes[i].comment[ii].outAction = diags[currentDiag].actionNodes[index];
            }
        }

        for (int i = 0; i < diags[currentDiag].npcNodes.Count; i++)
        {
            int index = (int)((long)dict["nd_oSet_" + i.ToString()]);
            if (index != -1)
                diags[currentDiag].npcNodes[i].outputSet = diags[currentDiag].playerNodes[index];

            if (dict.ContainsKey("nd_oNPC_" + i.ToString()))
            {
                int index2 = (int)((long)dict["nd_oNPC_" + i.ToString()]);
                if (index2 != -1)
                    diags[currentDiag].npcNodes[i].outputNPC = diags[currentDiag].npcNodes[index2];
            }

            if (dict.ContainsKey("nd_oAct_" + i.ToString()))
            {
                index = -1;
                index = (int)((long)dict["nd_oAct_" + i.ToString()]);
                if (index != -1)
                    diags[currentDiag].npcNodes[i].outAction = diags[currentDiag].actionNodes[index];
            }
        }

        for (int i = 0; i < diags[currentDiag].actionNodes.Count; i++)
        {
            diags[currentDiag].actionNodes[i].paramType = (int)((long)dict["ac_paramT_" + i.ToString()]);

            int index = -1;
            index = (int)((long)dict["ac_oSet_" + i.ToString()]);

            if (index != -1)
                diags[currentDiag].actionNodes[i].outPlayer = diags[currentDiag].playerNodes[index];

            if (dict.ContainsKey("ac_oNPC_" + i.ToString()))
            {
                index = -1;
                index = (int)((long)dict["ac_oNPC_" + i.ToString()]);
                if (index != -1)
                    diags[currentDiag].actionNodes[i].outNPC = diags[currentDiag].npcNodes[index];
            }

            if (dict.ContainsKey("ac_oAct_" + i.ToString()))
            {
                index = -1;
                index = (int)((long)dict["ac_oAct_" + i.ToString()]);
                if (index != -1)
                    diags[currentDiag].actionNodes[i].outAction = diags[currentDiag].actionNodes[index];
            }
        }

        diags[currentDiag].loaded = true;
        return true;
    }

    //Return the default start node
    public int startNode
    {
        get
        {
            return startPoint;
        }
    }

    public string GetFirstTag(bool searchPlayer)
    {
        if (!isLoaded)
        {
            Debug.LogError("No dialogue loaded!");
            return string.Empty;
        }

        string firstTag = string.Empty;
        if (searchPlayer)
        {
            for (int i = 0; i < diags[currentDiag].playerNodes.Count; i++)
            {
                firstTag = diags[currentDiag].playerNodes[i].playerTag;
                if (!string.IsNullOrEmpty(firstTag))
                    break;

            }
        }
        else
        {
            for (int i = 0; i < diags[currentDiag].npcNodes.Count; i++)
            {
                firstTag = diags[currentDiag].npcNodes[i].tag;
                if (!string.IsNullOrEmpty(firstTag))
                    break;

            }
        }
        return firstTag;
    }

    /// <summary>
    /// Loads the desired dialogue(s) to memory. Use both fields for a more specific search.
    /// </summary>
    /// <param name='dialogueName'>
    /// The name of the dialogue file. Leave empty to only search under load tag.
    /// </param>
    /// <param name='loadtag'>
    /// The name of assigned load tag. Leave empty to only search for a specific dialogue. 
    /// </param>
    public void LoadDialogues(string dialogueName, string loadtag)
    {
        bool didLoad = false;

        foreach (Diags d in diags)
        {
            currentDiag = diags.IndexOf(d);

            if (loadtag.Length > 0)
            {
                if (loadtag == d.loadTag)
                {
                    if (dialogueName.Length > 0)
                    {
                        if (d.name == dialogueName)
                        {
                            Load(d.name);
                            didLoad = true;
                            break;
                        }
                    }
                    else
                    {
                        Load(d.name);
                        didLoad = true;
                    }
                }
            }
            else
            {
                if (d.name == dialogueName)
                {
                    Load(d.name);
                    didLoad = true;
                    break;
                }
            }
        }

        if (!didLoad)
            Debug.LogError("Found no dialogue(s) to load!");

        if (OnLoaded != null)
            OnLoaded();

        currentDiag = -1;
    }

    /// <summary>
    /// Loads all of the dialogues to memory.
    /// </summary>
    public void LoadDialogues()
    {
        foreach (Diags d in diags)
        {
            currentDiag = diags.IndexOf(d);
            Load(d.name);
        }

        if (OnLoaded != null)
            OnLoaded();

        currentDiag = -1;
    }


    /// <summary>
    /// Unloads all of the dialogues from memory.
    /// </summary>
    public void UnloadDialogues()
    {
        foreach (Diags d in diags)
        {
            d.playerNodes = new List<CommentSet>();
            d.npcNodes = new List<Answer>();
            d.actionNodes = new List<ActionNode>();
            d.start = -1;
            d.loaded = false;
        }
        if (OnUnloaded != null)
            OnUnloaded();
    }
}




