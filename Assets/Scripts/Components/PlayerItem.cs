using UnityEngine.UI;
using System;
using UnityEngine;
using TMPro;

public class PlayerItem : MonoBehaviour {
    [SerializeField] public TextMeshProUGUI sttText;
    [SerializeField] public TextMeshProUGUI nameText;
    [SerializeField] public TextMeshProUGUI roleText;
    [SerializeField] public Image readyImage;
    [SerializeField] public Sprite isReadyImage;
    [SerializeField] public Sprite isNotReadyImage;

    void Start(){
        
    }
    public void SetData(string stt,string name, string role, bool isReady = false){
        sttText.text = stt;
        nameText.text = name;
        roleText.text = role;
        SetReadyImage(isReady);
        this.gameObject.SetActive(true);
    }
    public void SetReadyImage(bool isReady = false){
        if(isReady){
            readyImage.sprite = isReadyImage;
        }else{
            readyImage.sprite = isNotReadyImage;
        }
    }
}