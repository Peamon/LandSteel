using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class PlaneMap : MonoBehaviour {
	public Camera cameraMap;
	public GameObject sphereMap;
	public float distance = 5.05f;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		Vector3 angles = cameraMap.transform.eulerAngles;
		float lon = - angles.y - 90;
		float lat = angles.x;

		double phi = Math.PI * (90 - lat) / 180; //coordonnée sphérique et radian
		double teta = Math.PI * lon / 180;
		double sinphi = Math.Sin (phi);
		double dx = sinphi * Math.Cos (teta);
		double dy = sinphi * Math.Sin (teta);
		double dz = Math.Cos (phi);
		Vector3 pos = new Vector3((float)dx, (float)dz, (float)dy) * distance;

		transform.rotation = Quaternion.Euler(-lat+90, -lon+90, 0);
		transform.position = sphereMap.transform.position + pos;
	}
}
