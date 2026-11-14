using UnityEngine;
using System.Collections;

public abstract class Entity : MonoBehaviour
{
    public Attributes Attributes;
    public float knockbackForce = 15f;
    public float knockbackDuration = 0.5f;
    public float flashDuration = 0.05f;
    private Coroutine knockbackCoroutine;
    private Coroutine flashCoroutine;
    private Material[][] flashMaterials;
    private Color[][] flashOriginalColors;

    public void TakeDamage(int damage, Vector3 origin)
    {
        // Damage
        Attributes.health -= damage;

        // Knockback
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            Vector3 direction = transform.position - origin;
            if (direction.sqrMagnitude > 0f)
            {
                knockbackCoroutine = StartCoroutine(ApplyKnockback(characterController, direction.normalized));
            }
        }

        StartFlash();

        // Death check
        if (Attributes.health <= 0)
        {
            Die();
        }
    }

    private void StartFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            RestoreFlashColors();
        }

        flashCoroutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            flashCoroutine = null;
            yield break;
        }

        flashMaterials = new Material[renderers.Length][];
        flashOriginalColors = new Color[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            flashMaterials[i] = renderers[i].materials;
            flashOriginalColors[i] = new Color[flashMaterials[i].Length];

            for (int j = 0; j < flashMaterials[i].Length; j++)
            {
                flashOriginalColors[i][j] = flashMaterials[i][j].color;
                flashMaterials[i][j].color = Color.white;
            }
        }

        yield return new WaitForSeconds(flashDuration);

        RestoreFlashColors();
        flashCoroutine = null;
    }

    private void RestoreFlashColors()
    {
        if (flashMaterials == null || flashOriginalColors == null)
        {
            return;
        }

        for (int i = 0; i < flashMaterials.Length; i++)
        {
            for (int j = 0; j < flashMaterials[i].Length; j++)
            {
                if (flashMaterials[i][j] != null)
                {
                    flashMaterials[i][j].color = flashOriginalColors[i][j];
                }
            }
        }

        flashMaterials = null;
        flashOriginalColors = null;
    }

    private IEnumerator ApplyKnockback(CharacterController characterController, Vector3 direction)
    {
        float elapsed = 0f;

        while (elapsed < knockbackDuration)
        {
            float deltaTime = Time.deltaTime;
            float decay = Mathf.Exp(-5f * (elapsed / knockbackDuration));
            characterController.Move(direction * (knockbackForce / knockbackDuration) * decay * deltaTime);
            elapsed += deltaTime;
            yield return null;
        }
    }

    private void Die()
    {
        Debug.Log("Entity has died.");
        Destroy(gameObject);
    }
}
