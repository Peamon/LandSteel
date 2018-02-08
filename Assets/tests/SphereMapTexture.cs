using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using TerrainGenerator;

public class SphereMapTexture : MonoBehaviour {

	public int seed = 0;
	private Color[] TextureData;
	private object TextureDataLockObject;

	private Color[] TextureDataReady;
	private int ResReady;
	private object TextureDataReadyLockObject;

	private Dictionary<double, Color> gradients;

	NoiseProvider np;

	// Use this for initialization
	void Start () {
		TextureDataLockObject = new object ();
		TextureData = null;
		TextureDataReadyLockObject = new object ();
		TextureDataReady = null; 
		ResReady = 0;

		double earthFactor = 1;
		double r = 6400000 * earthFactor; //earth
		double m = 10000 * earthFactor; // max flight height
		double undersee = -10000 * earthFactor;

		//on va calculer la denivellation sur le radius
		np = new NoiseProvider(seed, r, m, undersee, undersee, m);

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

		var thread = new Thread(GenerateTextureData);
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
					for (int x = 0; x < texl; ++x) {
						double lon = 360 * (double)x / (double)texl;
						double height = np.HeightAtLatLon (lat, lon);
						TextureData[x + y * texl] = GetColor(height);
					}
					lock (TextureDataReadyLockObject) {
						ResReady = Res;
						TextureDataReady = TextureData;
					}
				}
			}
		}
		Debug.Log ("Finished.");
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
	}
}
