using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetCodeManager : Singleton<NetCodeManager>
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void StartServer(){
        NetworkManager.Singleton.StartServer();
    }
    public void StartHost(){
        NetworkManager.Singleton.StartHost();
    }
    public void StartClient(){
        NetworkManager.Singleton.StartClient();
    }
}
