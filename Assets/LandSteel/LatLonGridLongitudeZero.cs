using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class LatLonGridLatitudeZero : MonoBehaviour {
	private LineRenderer lineRenderer;
	public Color color = Color.red;
	public float width = 0.05f;

	// Use this for initialization
	void Start () {
		LatLonGrid parent = this.GetComponentInParent<LatLonGrid>();
		Shader shader = parent.shader;
		Material mat = parent.mat;
		if (mat == null) {
			mat = new Material (shader);
		}
		lineRenderer = gameObject.AddComponent<LineRenderer>();
		lineRenderer.material = mat;
		lineRenderer.startWidth = width;
		lineRenderer.endWidth = width;
		lineRenderer.positionCount = 180; 
		lineRenderer.startColor = color;
		lineRenderer.endColor = color;

		float radius = parent.radius;
		int i = 0;
		int lon = 0;
		for(int lat = -90; lat < 90; lat++) {
			//lat = phi (-90 -> 90)
			//lon = teta (0 -> 360)
			double phi = Math.PI * (90 - lat) / 180; //coordonnée sphérique et radian
			double teta = Math.PI * lon / 180;
			double sinphi = Math.Sin (phi);
			double dx = sinphi * Math.Cos (teta);
			double dy = sinphi * Math.Sin (teta);
			double dz = Math.Cos (phi);
			Vector3 pos = new Vector3((float)dx, (float)dz, (float)dy) * radius;
			lineRenderer.SetPosition (i, pos + gameObject.transform.position);
			++i;
		}
	}
	
	// Update is called once per frame
	void Update () {
	}
}
