using System.Collections.Generic;
using UnityEngine;  // using for PlayerPrefs
using System;  // using for Serializable

[Serializable]
public class PlayerData {
    public string name = "NO_NAME";
    public string id = "";
    public int hp = 100;
    public PlayerTypeInGame typePlayer = PlayerTypeInGame.Police;
    public PlayerStatus status = PlayerStatus.Offline;
}