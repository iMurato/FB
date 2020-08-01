using System.Linq;
using Firebase;
using Firebase.Database;
using Firebase.Unity.Editor;
using UnityEngine;

public class FirebaseController : MonoBehaviour
{
    [SerializeField] private string Link;
    [SerializeField] private string _PrettyTextPlayer;
    [SerializeField] private string _PrettyTextRoom;
    [SerializeField] private int ReConnectTime;
    [SerializeField] private int _KeyNumberCount;
    [SerializeField] private int _RoomCapacity;
    
    private FirebaseApp BaseApp;
    
    private static DatabaseReference BaseReference;
    private static DatabaseReference BaseTracking;

    private static System.Random Random = new System.Random();

    public static string MyName = "";
    public static string MyRoom = "";

    private static string PrettyTextPlayer;
    private static string PrettyTextRoom;
    private static int KeyNumberCount;
    private static int RoomCapacity;

    private static int OnLobbyChangeCallCount = 0;
    private static int OnRoomChangeCallCount = 0;
    
    private int OnPauseCallCount = 0;

    private void Awake()
    {
        PrettyTextPlayer = _PrettyTextPlayer;
        PrettyTextRoom = _PrettyTextRoom;
        KeyNumberCount = _KeyNumberCount;
        RoomCapacity = _RoomCapacity;

        Check();
    }

    private void Check()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => 
        {
            if (task.Result != DependencyStatus.Available){ Invoke("Check", ReConnectTime); return; }
            
            BaseApp = FirebaseApp.DefaultInstance;
            BaseApp.SetEditorDatabaseUrl(Link);
            BaseReference = FirebaseDatabase.DefaultInstance.RootReference;
            
            Connect();
        });
    }
    
    private static string GenerateKey(string PrettyText)
    {
        string Key = "";

        if (PrettyText != "Room")
        {
            Key = PrettyTextPlayer;
        }
        else
        {
            Key = PrettyTextRoom;
        }
        
        for (int i = 0; i < KeyNumberCount; i++)
        {
            Key = Key + Random.Next(0, 10);
        }

        return Key;
    }

    private static bool Free(DataSnapshot Snapshot, string Key)
    {
        foreach (DataSnapshot Child in Snapshot.Children)
        {
            if (Child.Key != Key)
            {
                continue;
            }
                        
            return false;
        }
                    
        return true;
    }

    public static void Connect()
    {
        BaseReference.GetValueAsync().ContinueWith(Task =>
        {
            if (!Task.IsCompleted) return;
                
            DataSnapshot ActivePlayer = Task.Result.Child("ActivePlayer");
            DataSnapshot ActiveRoom = Task.Result.Child("ActivePlayer");
            DataSnapshot Lobby = Task.Result.Child("Lobby");

            if (!(MyName != ""))
            {
                MyName = GenerateKey("Player");

                while (!Free(ActivePlayer, MyName))
                {
                    MyName = GenerateKey("Player");
                }
                
                Debug.Log(MyName);
                
                BaseReference.Child("ActivePlayer").Child(MyName).SetValueAsync(0);
            }

            if (Lobby.Children.Count() + 1 >= RoomCapacity)
            {
                MyRoom = GenerateKey("Room");

                while (!Free(ActiveRoom, MyRoom))
                {
                    MyRoom = GenerateKey("Room");
                }
                
                Debug.Log(MyRoom);

                BaseTracking = BaseReference.Child("ActiveRoom").Child(MyRoom);
                BaseTracking.ValueChanged += OnRoomChange;
            }
            else
            {
                BaseTracking = BaseReference.Child("Lobby").Child(MyName);
                BaseTracking.ValueChanged += OnLobbyChange;
            }
        });
    }

    private static void OnLobbyChange(object Sender, ValueChangedEventArgs Argument)
    {
        if (OnLobbyChangeCallCount > 0)
        {
            if (Argument.Snapshot.Value != null)
            {
                return;
            }
            else
            {
                BaseTracking.ValueChanged -= OnLobbyChange;
                BaseTracking = BaseReference.Child("ActiveRoom");
                BaseTracking.ValueChanged += OnRoomChange;
                
                OnLobbyChangeCallCount = 0;
            }
        }
        else
        {
            BaseTracking.SetValueAsync(0);
            
            OnLobbyChangeCallCount++;
        }
    }
    
    private static void OnRoomChange(object Sender, ValueChangedEventArgs Argument)
    {
        if (MyRoom != "")
        {
            if (OnRoomChangeCallCount > 0)
            {
                if (Argument.Snapshot.Children.Count() < RoomCapacity)
                {
                    Disconnect();
                }
                else
                {
                    Debug.Log("React.");
                }
            }
            else
            {
                if (!(Argument.Snapshot.Children.Count() != RoomCapacity))
                {
                    OnRoomChangeCallCount++;
                }
                else if (!Argument.Snapshot.Children.Any())
                {
                    BaseReference.Child("Lobby").GetValueAsync().ContinueWith(Task =>
                    {
                        if (!Task.IsCompleted) return;
                    
                        BaseReference.Child("ActiveRoom").Child(MyRoom).Child(MyName).SetValueAsync(0);
                    
                        for (int i = 1; i < RoomCapacity; i++)
                        {
                            string LastPlayer = Task.Result.Children.ElementAt(Task.Result.Children.Count() - i).Key;
                        
                            BaseReference.Child("Lobby").Child(LastPlayer).SetValueAsync(null);
                            BaseReference.Child("ActiveRoom").Child(MyRoom).Child(LastPlayer).SetValueAsync(0);
                        }
                    });
                }
            }
        }
        else
        {
            string Search(DataSnapshot Snapshot, string Key)
            {
                string Room = "";
                        
                foreach (DataSnapshot Child in Snapshot.Children)
                {
                    if (Child.Key != Key)
                    {
                        Room = Search(Child, Key);

                        if (Room != "")
                        {
                            break;
                        }
                    }
                    else
                    {
                        Room = Snapshot.Key;
                    
                        break;
                    }
                }

                return Room;
            }

            DataSnapshot ActiveRoom = Argument.Snapshot;
            MyRoom = Search(ActiveRoom, MyName);

            if (MyRoom != "")
            {
                Debug.Log(MyRoom);

                BaseTracking.ValueChanged -= OnRoomChange;
                BaseTracking = BaseReference.Child("ActiveRoom").Child(MyRoom);
                BaseTracking.ValueChanged += OnRoomChange;
            }
        }
    }

    public static void Disconnect()
    {
        if (MyName != "")
        {
            if (MyRoom != "")
            {
                if (BaseTracking != null)
                {
                    BaseTracking.ValueChanged -= OnRoomChange;
                }
                
                BaseReference.Child("ActiveRoom").Child(MyRoom).Child(MyName).SetValueAsync(null);

                OnRoomChangeCallCount = 0;
                
                MyRoom = "";
            }
            else
            {
                if (BaseTracking != null)
                {
                    BaseTracking.ValueChanged -= OnLobbyChange;
                }

                BaseReference.Child("Lobby").Child(MyName).SetValueAsync(null);

                OnLobbyChangeCallCount = 0;
            }
            
            BaseReference.Child("ActivePlayer").Child(MyName).SetValueAsync(null);

            MyName = "";
        }
    }
    
    #if UNITY_EDITOR

        private void OnApplicationQuit()
        {
            Disconnect();
        }

    #else
    
        private void OnApplicationPause(bool OnPause)
        {
            if (OnPauseCallCount > 0 && OnPause)
            {
                Disconnect();
            }
            else
            {
                OnPauseCallCount++;
            }
        }
    
    #endif
}
