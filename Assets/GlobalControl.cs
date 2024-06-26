using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalControl : MonoBehaviour
{
    public static bool isPaused = false;

    public static GameObject? player;
    GameObject pauseScreen;

    void Start()
    {
        player = GameObject.Find("Player");
        pauseScreen = GameObject.Find("PauseScreen");
        SetPause(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    void TogglePause()
    {
        SetPause(!isPaused);
    }

    void SetPause(bool shouldPause)
    {
        isPaused = shouldPause;
        pauseScreen.SetActive(isPaused);
        if (isPaused)
        {
            Time.timeScale = 0;
        }
        else
        {
            Time.timeScale = 1;
        }
    }
}
