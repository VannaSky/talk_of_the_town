using UnityEngine;

    public class GlobalLogSettings : MonoBehaviour
    {
        [SerializeField] private LogLevel logLevel = LogLevel.Warning;
        [SerializeField] private bool dontDestroyOnLoad = true;

        void Awake()
        {
            GameLog.GlobalLevel = logLevel;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }
    }
