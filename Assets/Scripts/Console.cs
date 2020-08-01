using UnityEngine;
using UnityEngine.UI;

public class Console : MonoBehaviour
{
    private Text ConsoleMessage;

    private string ConsoleText;

    private void Awake()
    {
        ConsoleMessage = GetComponent<Text>();
        ConsoleText = ConsoleMessage.text;
    }

    private void Update()
    {
        ConsoleMessage.text = ConsoleText;
    }

    private void OnEnable()
    {
        Application.logMessageReceivedThreaded += OnLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceivedThreaded -= OnLog;
    }

    private void OnLog(string Message, string StackTrace, LogType Type)
    {
        if (Type != LogType.Error)
        {
            Message = "[LOG] " + Message;
        }
        else
        {
            Message = "[ERROR] " + Message;
        }
        
        ConsoleText = ConsoleText + "\n" + Message;
    }
}
