using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour {
	public Camera ethan;
	public Camera map;
	public Camera menu;
	public Canvas menuCanvas;


	void Start() {
		ethan.enabled = false;
		map.enabled = false;
		menu.enabled = true;
	}

	void Update () {
		if (Input.GetKey (KeyCode.M)) {
			if (!menu.enabled) {
				ethan.enabled = false;
				map.enabled = false;
				menu.enabled = true;
				menuCanvas.enabled = true;
			}
		}
	}

	public void LoadScene(string name) {
		ethan.enabled = false;
		map.enabled = false;
		menu.enabled = false;
		menuCanvas.enabled = false;
		if (name == "terrain") {
			ethan.enabled = true;
		} else if (name == "carte") {
			map.enabled = true;
		} else {
			menu.enabled = true;
			menuCanvas.enabled = true;
		}
	}

}
