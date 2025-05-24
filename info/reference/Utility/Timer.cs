using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    [SerializeField] private Text secondText;
    
    public float Speed {get; private set;} = 1f;
    public float CurrentTime {get; private set;} = 0;
    public int StartTime {get; private set;} = 0;
    public bool IsPaused {get; private set;} = true;
    public bool IsDone {get; private set;} = false;

    public event Action onStartEvent;
    public event Action<float> onDoneEvent;


    // Update is called once per frame
    void Update()
    {
        if (!IsPaused && !IsDone) {
            CurrentTime = Mathf.Max(CurrentTime - Speed * Time.deltaTime, 0);
            secondText?.SetText(GetTime().ToString());

            if (CurrentTime == 0) {
                Done();
            }
        }
    }

    public void SetSpeed(float spd) {
        Speed = spd;
    }

    public void SetTimer(int sec, float spd = 1, bool start = true) {
        Speed = spd;
        CurrentTime = sec;
        StartTime = sec;
        IsPaused = true;
        IsDone = false;
        if (!start)
            return;
        
        StartTimer();
    }

    public int GetTime() {
        return Mathf.CeilToInt(CurrentTime);
    }

    public void StartTimer() {
        IsPaused = false;
        IsDone = false;
        onStartEvent?.Invoke();
    }   

    public void Pause() {
        IsPaused = true;
    }

    public void Unpause() {
        IsPaused = false;
    }

    public float Done() {
        float leftTime = CurrentTime;
        CurrentTime = 0;
        IsPaused = true;
        IsDone = true;
        onDoneEvent?.Invoke(leftTime);
        return leftTime;
    }

    public void Restart() {
        SetTimer(StartTime, Speed);
    }
}
