using UnityEngine;

public class Player : Entity
{
    void Awake()
    {
        Attributes = new Attributes(100);
        PlayerReference.SetPlayer(this);
    }
}
