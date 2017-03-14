using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CreateConversation
{
    [MenuItem("Assets/Create/Conversation")]
    public static void CreateAsset()
    {
        ScriptableObjectUtility.CreateAsset<Conversation>();
    }
}
