using System.Collections.Generic;
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
    [SerializeField] private int _KeyNumberCount;
    [SerializeField] private int _RoomCapacity;
    
    private FirebaseApp BaseApp;
    
    private static DatabaseReference BaseReference;
    private static DatabaseReference BaseTracking;

    private static System.Random Random = new System.Random();

    public static string MyName = "";
    public static string MyRoom = "";
    
    public static Dictionary<string, string> MyData = new Dictionary<string, string>(); 

    private static string PrettyTextPlayer;
    private static string PrettyTextRoom;
    private static int KeyNumberCount;
    private static int RoomCapacity;

    public static bool OnCheck;
    public static bool OnConnect;
    public static bool OnReady;
    
    private static bool OnLobbySkip;

    private void Awake()
    {
        PrettyTextPlayer = _PrettyTextPlayer;
        PrettyTextRoom = _PrettyTextRoom;
        KeyNumberCount = _KeyNumberCount;
        RoomCapacity = _RoomCapacity;
        
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(Task => 
        {
            if (Task.Result != DependencyStatus.Available) return;
            
            BaseApp = FirebaseApp.DefaultInstance;
            BaseApp.SetEditorDatabaseUrl(Link);
            
            FirebaseDatabase.DefaultInstance.GetReference(".info/connected").ValueChanged += CheckConnect;

            void CheckConnect(object Sender, ValueChangedEventArgs Argument)
            {
                if (Argument.Snapshot.Value.ToString() != "True")
                {
                    OnConnect = false;
                }
                else
                {
                    OnConnect = true;
                }
            }

            BaseReference = FirebaseDatabase.DefaultInstance.RootReference;

            OnCheck = true;
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
            if (Task.IsFaulted) return;
                
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

                foreach (KeyValuePair<string, string> Data in MyData)
                {
                    BaseReference.Child("ActivePlayer").Child(MyName).Child(Data.Key).SetValueAsync(Data.Value);
                }
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
        if (OnLobbySkip && !(Argument.Snapshot.Value != null))
        {
            BaseTracking.ValueChanged -= OnLobbyChange;
            BaseTracking = BaseReference.Child("ActiveRoom");
            BaseTracking.ValueChanged += OnActiveRoomChange;

            OnLobbySkip = false;
        }
        else if (!OnLobbySkip)
        {
            foreach (KeyValuePair<string, string> Data in MyData)
            {
                BaseTracking.Child(Data.Key).SetValueAsync(Data.Value);
            }
            
            OnLobbySkip = true;
        }
    }

    private static void OnActiveRoomChange(object Sender, ValueChangedEventArgs Argument)
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

            BaseTracking.ValueChanged -= OnActiveRoomChange;
            BaseTracking = BaseReference.Child("ActiveRoom").Child(MyRoom);
            BaseTracking.ValueChanged += OnRoomChange;
        }
    }

    private static void OnRoomChange(object Sender, ValueChangedEventArgs Argument)
    {
        if (Argument.Snapshot.Children.Any())
        {
            if (OnReady)
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
            else if (!(Argument.Snapshot.Children.Count() != RoomCapacity))
            {
                OnReady = true;
            }
        }
        else
        {
            BaseReference.Child("Lobby").GetValueAsync().ContinueWith(Task =>
            {
                if (Task.IsFaulted) return;
                
                DataSnapshot Lobby = Task.Result;
                
                foreach (KeyValuePair<string, string> Data in MyData)
                {
                    BaseTracking.Child(MyName).Child(Data.Key).SetValueAsync(Data.Value);
                }

                for (int i = 1; i < RoomCapacity; i++)
                {
                    string LastPlayer = Lobby.Children.ElementAt(Lobby.Children.Count() - i).Key;
                    
                    foreach (DataSnapshot Data in Lobby.Child(LastPlayer).Children)
                    {
                        BaseTracking.Child(LastPlayer).Child(Data.Key).SetValueAsync(Data.Value);
                    }
                        
                    BaseReference.Child("Lobby").Child(LastPlayer).SetValueAsync(null);
                }
            });
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
                
                MyRoom = "";
            }
            else
            {
                if (BaseTracking != null)
                {
                    BaseTracking.ValueChanged -= OnLobbyChange;
                    BaseTracking.ValueChanged -= OnActiveRoomChange;
                }

                BaseReference.Child("Lobby").Child(MyName).SetValueAsync(null);
            }
            
            OnReady = false;
            
            BaseReference.Child("ActivePlayer").Child(MyName).SetValueAsync(null);

            MyName = "";
        }
    }
}
