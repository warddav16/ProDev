using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnswerButton : MonoBehaviour 
{
    public uint AnswerIndex = 0;

    public void Answer()
    {
	ConversationUIManager.Instance().AnswerQuestion(AnswerIndex);
    }
}
