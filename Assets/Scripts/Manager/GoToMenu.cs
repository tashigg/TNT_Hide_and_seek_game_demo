using System;
using System.Collections;
using Unity.Services.Core;
using UnityEngine;

/*
    Script to go to the Menu scene after the loading manager is load.
*/
public class GoToMenu : MonoBehaviour
{
    public void Awake()
    {
        UnityServicesInit();
    }
    private async void UnityServicesInit()
    {
        await UnityServices.InitializeAsync();
    }

    IEnumerator Start()
    {
        // Wait for the loading scene manager to start
        yield return new WaitUntil(() => LoadingSceneManager.Instance != null);

        // Load the menu
        LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
    }
}