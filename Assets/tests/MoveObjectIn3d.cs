using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveObjectIn3d : MonoBehaviour {

	Vector3 target;

	// Use this for initialization
	void Start () {
		target = gameObject.transform.position;
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetKey(KeyCode.U)) {
			target.y += 1;
		}
		if (Input.GetKey(KeyCode.J)) {
			target.y -= 1;
		}
		if (Input.GetKey(KeyCode.T)) {
			target.z += 1;
		}
		if (Input.GetKey(KeyCode.G)) {
			target.z -= 1;
		}
		if (Input.GetKey(KeyCode.F)) {
			target.x += 1;
		}
		if (Input.GetKey(KeyCode.H)) {
			target.x -= 1;
		}
		transform.position = Vector3.Lerp (transform.position, target, Time.deltaTime * 1);
	}
}
