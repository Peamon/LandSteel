using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TerrainGenerator;

public class WorldTerrain : MonoBehaviour {

	public GameObject Character;
	public int seed = 0;
	public double earthFactor = 1;
	public int Radius = 4;
	public string CachePath = "/Users/popigny/Documents/Developpement/LandSteel/TerrainCache";

	private Vector2i PreviousPlayerChunkPosition;
	public TerrainChunkGenerator Generator;

	public WorldTerrain() {
		//Debug.Log("WorldTerrain()");
	}

	// Use this for initialization
	void Start () {
		//Debug.Log("WorldTerrain::Awake()");
		Generator.Set (seed, earthFactor, Radius, false, 3000, CachePath);
	}

	// Update is called once per frame
	void Update () {
		// Update only if the current scene is rendered
		if (Camera.main.name == "Ethan Camera") {
			Vector3 worldPos = Character.transform.position;
			Generator.UpdateTerrain (worldPos, Radius);
		}
	}
}
