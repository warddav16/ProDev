using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Conversation : ScriptableObject
{
    [System.Serializable]
    public class ResponseNode
    {
        [TextArea]
        public string AnswerText;
    }

    [System.Serializable]
    public class ConversationNode
    {
        [TextArea]
        public string ConvoText;

        public List< ResponseNode > responses;

    }

    public List<ConversationNode> nodes;
}
