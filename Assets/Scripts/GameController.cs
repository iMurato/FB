using UnityEngine;

public class GameController : MonoBehaviour
{
    [SerializeField] private Animator Connect;
    
    private bool OnPauseSkip;
    
    private void Awake()
    {
        FirebaseController.MyData.Add("Sign", "0");
    }

    private void Update()
    {
        
    }

    public void OnConnectClick()
    {
        FirebaseController.Connect();
        
        Connect.Play("Ui-Push-Disable");
    }

    #if UNITY_EDITOR

        private void OnApplicationQuit()
        {
            FirebaseController.Disconnect();
        }

    #else
    
        private void OnApplicationPause(bool OnPause)
        {
            if (OnPause && OnPauseSkip)
            {
                FirebaseController.Disconnect();
            }
            else
            {
                OnPauseSkip = true;
            }
        }
        
    #endif
}
