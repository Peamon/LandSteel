using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using TerrainGenerator;

public class SphereMapTexture : MonoBehaviour {

	public WorldTerrain world;

	public Color color = Color.magenta;
	public float width = 0.05f;
	public float radius = 5.001f;
	public Shader shader = null;
	public Material mat = null;

	private float currentLat = 0.0f;
	private LineRenderer lineRenderer;
	private int nbthreads = 1;
	private bool isOnScreen = false;
	private Color[] TextureData;
	private object TextureDataLockObject;

	private Color[] TextureDataReady;
	private int ResReady;
	private object TextureDataReadyLockObject;

	private Dictionary<double, Color> gradients;

	private Thread thread;
	private List<Thread> subthreads;

	NoiseProvider np;

	private class ThreadTextureParam {
		public int xstart;
		public int nb;
		public int texl;
		public int y;
		public double lat;

		public ThreadTextureParam(int pxstart, int pnb, int ptexl, int py, double plat) {
			xstart = pxstart;
			nb = pnb;
			texl = ptexl;
			y = py;
			lat = plat;
		}
	}

	// Use this for initialization
	void Start () {
		nbthreads = SystemInfo.processorCount;
		subthreads = new List<Thread> ();
		TextureDataLockObject = new object ();
		TextureData = null;
		TextureDataReadyLockObject = new object ();
		TextureDataReady = null; 
		ResReady = 0;

		gradients = new Dictionary<double, Color> ();
		gradients.Add (-16384.0, new Color (  3.0f/255.0f,  29.0f/255.0f,  63.0f/255.0f));
		gradients.Add (-256.0,   new Color (  3.0f/255.0f,  29.0f/255.0f,  63.0f/255.0f));
		gradients.Add (-1.0,     new Color (  7.0f/255.0f, 106.0f/255.0f, 127.0f/255.0f));
		gradients.Add (0.0,      new Color ( 62.0f/255.0f,  86.0f/255.0f,  30.0f/255.0f));
		gradients.Add (1024.0,   new Color ( 84.0f/255.0f,  96.0f/255.0f,  50.0f/255.0f));
		gradients.Add (2048.0,   new Color (130.0f/255.0f, 127.0f/255.0f,  97.0f/255.0f));
		gradients.Add (3072.0,   new Color (184.0f/255.0f, 163.0f/255.0f, 141.0f/255.0f));
		gradients.Add (4096.0,   new Color (255.0f/255.0f, 255.0f/255.0f, 255.0f/255.0f));
		gradients.Add (6144.0,   new Color (128.0f/255.0f, 255.0f/255.0f, 255.0f/255.0f));
		gradients.Add (16384.0,  new Color (  0.0f/255.0f,   0.0f/255.0f, 255.0f/255.0f));

		if (mat == null) {
			if (shader == null) {
				shader = Shader.Find ("Particles/Additive");
			}
			mat = new Material (shader);
		}
		lineRenderer = gameObject.AddComponent<LineRenderer>();
		lineRenderer.material = mat;
		lineRenderer.startWidth = width;
		lineRenderer.endWidth = width;
		lineRenderer.positionCount = 360;
		lineRenderer.startColor = color;
		lineRenderer.endColor = color;
		lineRenderer.loop = true;

		StartGenerateTexture ();
	}

	void StartGenerateTexture() {
		//on va calculer la denivellation sur le radius
		double earthFactor = world.earthFactor;
		double r = 6400000 * earthFactor;
		double m = 10000 * earthFactor;
		double undersee = -10000 * earthFactor;

		np = new NoiseProvider(world.seed, r, m, undersee, undersee, m);

		thread = new Thread(GenerateTextureData);
		thread.Start();
	}

	void GenerateTextureData() {
		for (int Res = 2; Res <= 10; ++Res) {
			Debug.Log ("Res=" + Res);
			lock (TextureDataLockObject) {
				int texh = (int)Math.Pow (2, Res);
				int texl = texh * 2;
				lock (TextureDataReadyLockObject) {
					Color[] TextureDataTemp = TextureData;
					TextureData = new Color[texl * texh];
					for (int y = 0; y < texh/2; ++y) {
						for (int x = 0; x < texl/2; ++x) {
							if (TextureDataTemp != null) {
								TextureData [(x * 2) + (y * 2) * texl] = TextureDataTemp [x + y * texl / 2];
								TextureData [(x * 2 + 1) + (y * 2) * texl] = TextureDataTemp [x + y * texl / 2];
								TextureData [(x * 2) + (y * 2 + 1) * texl] = TextureDataTemp [x + y * texl / 2];
								TextureData [(x * 2 + 1) + (y * 2 + 1) * texl] = TextureDataTemp [x + y * texl / 2];
							} else {
								TextureData [(x * 2) + (y * 2) * texl / 2] = Color.black;
								TextureData [(x * 2 + 1) + (y * 2) * texl / 2] = Color.black;
								TextureData [(x * 2) + (y * 2 + 1) * texl / 2] = Color.black;
								TextureData [(x * 2 + 1) + (y * 2 + 1) * texl / 2] = Color.black;
							}
						}
					}
				}
				for (int y = 0; y < texh; ++y) {
					double lat = 90 - 180 * (double)y / (double)texh;
					currentLat = - (float)lat;
					var nnbthreads = nbthreads;
					if (texl < nbthreads * 50) {
						nnbthreads = 1;
					} else {
						while (! isOnScreen) {
							Thread.Sleep (1000);
						}
					}
					for (int i = 0; i < nnbthreads; ++i) {
						//TODO: An optimized way is to create a pool and use it with signals, it will avoid time consuming Thread creation/deletion.
						var thread = new Thread (ColorizeTextureData);
						var param = new ThreadTextureParam (i * texl / nnbthreads, texl / nnbthreads, texl, y, lat);
						thread.Start (param);
						subthreads.Add (thread);
					}

					foreach (var thread in subthreads) {
						thread.Join ();
					}

					subthreads.Clear ();

					lock (TextureDataReadyLockObject) {
						ResReady = Res;
						TextureDataReady = TextureData;
					}
				}
			}
		}
		Debug.Log ("Finished.");
	}

	private void ColorizeTextureData(object threadparam) {
		var param = (ThreadTextureParam)threadparam;
		NoiseProvider nnp = new NoiseProvider (np);
		for (int x = param.xstart; x < param.xstart + param.nb && x < param.texl; ++x) {
			double lon = 360 * (double)x / (double)param.texl;
			double height = nnp.HeightAtLatLon (param.lat, lon);
			TextureData[x + param.y * param.texl] = GetColor(height);
		}
	}

	Color GetColor(double height) {
		KeyValuePair<double, Color> start = new KeyValuePair<double, Color>(height, Color.black);
		KeyValuePair<double, Color> end = start;
		Dictionary<double, Color>.Enumerator it = gradients.GetEnumerator ();
		while (it.MoveNext ()) {
			if (height < it.Current.Key) {
				end = it.Current;
				break;
			}
			start = it.Current;
		}
		double len = end.Key - start.Key;
		double pos = height - start.Key;
		float t = (float)(pos / len);
		Color lerped = Color.Lerp (start.Value, end.Value, t);
		return lerped;
	}

	// Update is called once per frame
	void Update () {
		isOnScreen = (Camera.main.name == "Map Camera");
		lock (TextureDataReadyLockObject) {
			if (TextureDataReady != null) {
				int texh = (int)Math.Pow (2, ResReady);
				int texl = texh * 2;
				Texture2D texture = new Texture2D (texl, texh);
				texture.SetPixels (TextureDataReady);
				texture.Apply ();
				Renderer renderer = gameObject.GetComponent<Renderer> ();
				renderer.material.mainTexture = texture;
				TextureDataReady = null;
			}
		}

		int i = 0;
		float lat = currentLat;
		for (int lon = 0; lon < 360; ++lon) {
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

	void OnDestroy() {
		foreach (var thread in subthreads) {
			thread.Interrupt ();
		}
		thread.Interrupt ();
	}
}
