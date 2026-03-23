using UnityEngine;

public static class SnakeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSnakeGame()
    {
        if (Object.FindObjectOfType<SnakeGameController>() != null)
        {
            return;
        }

        var gameObject = new GameObject("SnakeGame");
        gameObject.AddComponent<SnakeGameController>();
    }
}