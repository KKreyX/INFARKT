using System;
using System.Collections.Generic;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher instance;
    private readonly Queue<Action> actionQueue = new Queue<Action>();
    private readonly object queueLock = new object();

    public static MainThreadDispatcher Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<MainThreadDispatcher>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("MainThreadDispatcher");
                    instance = obj.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(obj);
                }
            }
            return instance;
        }
    }

    public static void EnsureCreated()
    {
        // Этот метод должен вызываться только из главного потока
        if (instance == null)
        {
            // Создаем экземпляр, обращаясь к Instance
            var temp = Instance;
        }
    }

    public void Enqueue(Action action)
    {
        if (action == null || instance == null) return;
        
        lock (queueLock)
        {
            actionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        if (actionQueue.Count == 0) return;

        List<Action> actionsToProcess;
        
        lock (queueLock)
        {
            if (actionQueue.Count == 0) return;
            actionsToProcess = new List<Action>(actionQueue);
            actionQueue.Clear();
        }

        foreach (Action action in actionsToProcess)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in MainThreadDispatcher action: {e.Message}");
            }
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}