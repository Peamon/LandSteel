using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;
using TerrainGenerator;

public class waitTerrain : MonoBehaviour {

	public TerrainChunkGenerator Generator;
	public bool isOnTerrain = false;

	// Use this for initialization
	void Start () {
		isOnTerrain = false;
	}
	
	// Update is called once per frame
	void Update () {
		if (!isOnTerrain) {
			Vector3 worldPos = transform.position;
			if (Generator.IsTerrainAvailable (worldPos)) {
				Debug.Log (transform.position.ToString());
				worldPos.y = Generator.GetTerrainHeight (worldPos);
				transform.position = worldPos;
				Debug.Log (transform.position.ToString());
				isOnTerrain = true;
			} else {
				/*
				worldPos.x = 20;
				worldPos.y = 500;
				worldPos.z = 20;
				transform.position = worldPos;
				*/
			}
		}
	}
}
