using UnityEngine;

public class Attributes
{
    public int maxHealth;
    public int health;

    public Attributes(int maxHealth)
    {
        this.maxHealth = maxHealth;
        this.health = maxHealth;
    }

    public bool TakeDamage(int damage)
    // Returns true if the entity is dead after taking damage, false otherwise
    {
        health -= damage;
        if (health <= 0)
        {
            return true; // Entity is dead
        }
        return false; // Entity is still alive
    }
}
