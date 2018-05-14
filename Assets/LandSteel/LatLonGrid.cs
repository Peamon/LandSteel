using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class LatLonGrid : MonoBehaviour {
	public float radius = 5.05f;
	LineRenderer lineRenderer;
	public Color color = Color.white;
	public float width = 0.01f;
	public Shader shader = null;
	public Material mat = null;

	// Use this for initialization
	void Start () {
		if (mat == null) {
			mat = new Material (shader);
		}
		lineRenderer = gameObject.AddComponent<LineRenderer>();
		lineRenderer.material = mat;
		lineRenderer.startWidth = width;
		lineRenderer.endWidth = width;
		lineRenderer.positionCount = 180 * 360;
		lineRenderer.startColor = color;
		lineRenderer.endColor = color;

		int i = 0;
		for (int lon = 0; lon < 360; ++lon) {
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
	}
	
	// Update is called once per frame
	void Update () {
	}

	void OnDrawGizmos() {
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere (transform.position, radius);
	}

	void OnDrawGizmosSelected() {
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere (transform.position, radius);
	}
}
