using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private GameObject Panel;
    private void Awake()
    {
        Panel = GameObject.Find("SpeechBox");
        Panel.SetActive(false);
    }
}
