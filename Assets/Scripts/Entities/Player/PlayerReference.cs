using UnityEngine;

public static class PlayerReference
{
    public static Player Player { get; private set; }

    public static void SetPlayer(Player player)
    {
        Player = player;
    }

    public static Vector3 GetPlayerPosition()
    {
        Vector3 position = Vector3.zero;
        if (Player != null)
        {
            position = Player.transform.position;
        }
        else
        {
            Debug.LogWarning("Player reference is not set.");
        }
        return position;
    }
}
