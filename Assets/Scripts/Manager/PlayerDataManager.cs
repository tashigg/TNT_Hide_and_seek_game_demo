using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }
    [SerializeField] public PlayerData _playerData = new PlayerData();
    void Awake(){
        DontDestroyOnLoad(this);
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }
    public PlayerData playerData
    {
        get { return _playerData; }
        set { _playerData = value; }
    }


    public void SetName(string value)
    {
        _playerData.name = (value);
        PlayerPrefs.SetString(Constants.NAME_PREF, value);
    }
    public void SetId(string value){
        _playerData.id = value;
    }

    public void SetHp(int value)
    {
        _playerData.hp = value;
    }
    public void SetType(PlayerTypeInGame value)
    {
        _playerData.typePlayer = value;
    }
    public void SetStatus(PlayerStatus value)
    {
        _playerData.status = value;
    }

}