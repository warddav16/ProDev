using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    float MaxConversatioNDistance = 5.0f;
    void Update()
    {
        if( Input.GetMouseButtonUp(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit outInfo;
            if( Physics.Raycast(ray, out outInfo, MaxConversatioNDistance))
            {
                VIDE_Assign assign = outInfo.collider.gameObject.GetComponent<VIDE_Assign>();
                if( assign != null)
                {
                    ConversationUIManager.Instance().StartConversation(assign);
                }
            }
        }
    }
}
