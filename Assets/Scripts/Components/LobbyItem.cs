using UnityEngine.UI;
using System;
using UnityEngine;
using TMPro;

public class LobbyItem : MonoBehaviour {
    [SerializeField] public TextMeshProUGUI lobbySttText;
    [SerializeField] public TextMeshProUGUI lobbyCodeText;
    [SerializeField] public TextMeshProUGUI lobbyNameText;
    [SerializeField] public Button joinButton;
    public string lobbyId;

    Action<string> onJoinClickCallback;

    void Start(){
        joinButton.onClick.AddListener(JoinClick);
    }
    public void SetData(string stt,string id, string code, string name = "default"){
        lobbySttText.text = stt;
        lobbyCodeText.text = code;
        lobbyNameText.text = name;
        lobbyId = id;
        this.gameObject.SetActive(true);
    }
    public void SetOnClickJoin(Action<string> callback){
        onJoinClickCallback = callback;
    }
    public void JoinClick(){
        Debug.Log("= Join Clicked  ID: " + lobbyId);
        onJoinClickCallback?.Invoke(lobbyId);
    }
}