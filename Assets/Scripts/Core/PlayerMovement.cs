using UnityEngine;
using UnityEngine.InputSystem; // 1. The Input System "using" statement

public class PlayerMovement : MonoBehaviour
{
	public Transform trnsfrm;
	public float m, n, L, speed;
	InputAction moveAction;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		moveAction = InputSystem.actions.FindAction("Move");
	}

	// Update is called once per frame
	void Update()
	{
		Vector2 moveValue = moveAction.ReadValue<Vector2>() * Time.deltaTime * speed;
		Vector3 pos = trnsfrm.position + new Vector3(moveValue.x, 0, moveValue.y);

		/*if (Input.GetKey("left")) 
			pos.x -= Time.deltaTime;

		if (Input.GetKey("right")) 
			pos.x += Time.deltaTime;

		if (Input.GetKey("down")) 
			pos.z -= Time.deltaTime;

		if (Input.GetKey("up")) 
			pos.z += Time.deltaTime;*/

		float chladni = Mathf.Abs(Mathf.Cos(n * Mathf.PI * pos.x / L) * Mathf.Cos(m * Mathf.PI * pos.z / L) - Mathf.Cos(m * Mathf.PI * pos.x / L) * Mathf.Cos(n * Mathf.PI * pos.z / L)) / 2;
		Debug.Log("x:" + pos.x + " z:" + pos.z + " " + chladni);
		gameObject.GetComponent<Renderer>().material.color = new Color(chladni, 0, 0);
		trnsfrm.position = pos;
	}
}
