using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckPointManager : MonoBehaviour
{
    public static CheckPointManager Instance;

    [System.Serializable]
    public class CheckpointData
    {
        public string checkpointName;
        public string sceneName;
        public Vector3 position;
        public int checkpointOrder;
    }

    public CheckpointData currentCheckpoint = new CheckpointData();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //Debug.Log($"Escena cargada: {scene.name}");
        InitializeCheckpointForScene(scene.name);
    }

    private void InitializeCheckpointForScene(string sceneName)
    {
        // Si es una escena nueva o diferente, resetear al checkpoint inicial
        if (currentCheckpoint.sceneName != sceneName)
        {
            ResetToSceneInitial(sceneName);
        }
    }

    private void ResetToSceneInitial(string sceneName)
    {
        currentCheckpoint.sceneName = sceneName;
        currentCheckpoint.checkpointName = "Initial";
        currentCheckpoint.checkpointOrder = 0;
        currentCheckpoint.position = FindInitialSpawnInScene();

        //Debug.Log($"Checkpoint reset para escena: {sceneName}");
    }

    private Vector3 FindInitialSpawnInScene()
    {
        // Buscar checkpoint inicial en la escena
        CheckPoint[] checkpoints = FindObjectsByType<CheckPoint>(FindObjectsSortMode.None);
        foreach (CheckPoint cp in checkpoints)
        {
            if (cp.isInitialCheckpoint)
                return cp.GetRespawnPosition();
        }

        // Fallback: buscar objeto llamado "SpawnPoint" o usar (0,0,0)
        GameObject spawnObj = GameObject.Find("SpawnPoint");
        return spawnObj != null ? spawnObj.transform.position : Vector3.zero;
    }

    public void RegisterCheckpoint(CheckPoint checkpoint)
    {
        if (checkpoint == null) return;

        // Solo registrar si es un checkpoint más avanzado
        if (checkpoint.checkpointOrder > currentCheckpoint.checkpointOrder ||
            currentCheckpoint.sceneName != SceneManager.GetActiveScene().name)
        {
            currentCheckpoint.checkpointName = checkpoint.gameObject.name;
            currentCheckpoint.sceneName = SceneManager.GetActiveScene().name;
            currentCheckpoint.position = checkpoint.GetRespawnPosition();
            currentCheckpoint.checkpointOrder = checkpoint.checkpointOrder;

            //Debug.Log($"Checkpoint registrado: {checkpoint.gameObject.name} (Orden: {checkpoint.checkpointOrder})");
        }
    }

    public Vector3 GetRespawnPosition()
    {
        // Verificar que estamos en la escena correcta
        if (SceneManager.GetActiveScene().name == currentCheckpoint.sceneName)
        {
            return currentCheckpoint.position;
        }

        return FindInitialSpawnInScene();
    }

    public void OnLevelComplete()
    {
        Debug.Log("Nivel completado - resetando checkpoints");
        ResetToSceneInitial(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
