using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;
using TerrainGenerator;

public class WorldTerrain : MonoBehaviour {

	public GameObject Character;
	public int seed = 0;
	public double earthFactor = 1;
	public int Radius = 4;

	private Vector2i PreviousPlayerChunkPosition;
	public TerrainChunkGenerator Generator;

	public WorldTerrain() {
		Debug.Log("WorldTerrain()");
	}

	// Use this for initialization
	void Start () {
		Debug.Log("WorldTerrain::Awake()");
		Generator.Set (seed, earthFactor, Radius, true, 3000);
	}

	// Update is called once per frame
	void Update () {
		Vector3 worldPos = Character.transform.position;
		Vector2i playerChunkPosition = Generator.GetChunkPosition(worldPos);
		if (!playerChunkPosition.Equals(PreviousPlayerChunkPosition))
		{
			Generator.UpdateTerrain(worldPos, Radius);
			PreviousPlayerChunkPosition = playerChunkPosition;
		}
	}
}
