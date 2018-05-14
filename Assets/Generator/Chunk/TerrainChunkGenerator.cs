using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TerrainGenerator
{
    public class TerrainChunkGenerator : MonoBehaviour
    {
        public Material TerrainMaterial;
		public GameObject TreePrefab;

		public Texture2D FlatTexture;
        public Texture2D SteepTexture;

		private Dictionary<int, TerrainChunkSettings> Settings;

		private NoiseProvider NoiseProvider;

		private Dictionary<Vector2i, TerrainChunk> LoadedChunks { get; set; }
		private string CachePath;
		private double r;
		int chunkSz = 100;

		TerrainQuadTree<TerrainChunk> QT;

		public void Set(int seed, double earthFactor, int Depth, bool searchALatLon = false, double atminheight=2000, string cachepath = "") {
			LoadedChunks = new Dictionary<Vector2i, TerrainChunk> ();
			CachePath = cachepath;
			QT = new TerrainQuadTree<TerrainChunk> (Depth, CreateAChunck);
			int Radius = QT.SZ / 2;
			r = 6400000 * earthFactor; //earth
			double m = 10000 * earthFactor; // max mountain height
			double H = 50000 * earthFactor; // max flight height
			double A = Math.Sqrt((r+H)*(r+H) - r*r); //horizon if flight at max height
			double B = Math.Sqrt((r+m)*(r+m) - r*r); //horizon if on upper mountain
			double h = (r*r - H*H - (r+m)*(r+m) + (A+B)*(A+B)) / (2*r + 2*H); //underground needed to see upper mountain at max flight height
			//double v = Math.Sqrt((A+B)*(A+B) - (h+H)*(h+H)); //radius to see upper mountain at max flight height
			double undersee = -10000 * earthFactor;

			//sert pour faire les calculs de bounds
			NoiseProvider np = new NoiseProvider(seed, r, m, undersee, undersee, m);

			//recherche
			bool found = !searchALatLon;
			for (double lon = 0; !found && lon < 360; lon += 10) {
				for (double lat = -90; !found && lat <= 90; lat += 10) {
					np.LatitudeAngle = lat;
					np.LongitudeAngle = lon;
					double hzero = np.HeightAt (0, 0);
					if (hzero - r > atminheight) {
						found = true;
					}
				}
			}
			//found
			if (searchALatLon) {
				Debug.Log ("(°,°):" + (np.HeightAt (0, 0) - r) + " at lat=" + np.LatitudeAngle + " lon=" + np.LongitudeAngle);
			}

			//on va calculer la denivellation sur le radius
			double denivel_max = undersee-1;
			double denivel_min = m+1;
			double denivel_sum = 0;
			double denivel_nb = 0;

			//Check 1024 points to estimate max and min height
			for (int x = 0; x < 32; ++x) {
				for (int z = 0; z < 32; ++z) {
					double rx = (float)chunkSz / 2;
					double rz = (float)chunkSz / 2;
					double ch = np.HeightAt (x*(double)chunkSz + rx, z*(double)chunkSz + rz) - r;
					denivel_sum += ch;
					denivel_nb += 1;
					if (ch > denivel_max) {
						denivel_max = ch;
					}
					if (ch < denivel_min) {
						denivel_min = ch;
					}
				}
			}

			double denivel_moy = denivel_sum / denivel_nb;
			denivel_min = Math.Floor (denivel_min);
			denivel_max = Math.Ceiling (denivel_max);
			Debug.Log ("denivel ["+denivel_min.ToString() + ", " + denivel_max.ToString() + "] moy="+denivel_moy+" Radius="+Radius);
			Debug.Log ("Constructed : r="+r.ToString()+" max="+H.ToString()+" min="+(undersee).ToString()+" h="+h.ToString()+" denivel="+(denivel_max-denivel_min).ToString());

			int maxChunk = 128 + 1;
			//normal
			NoiseProvider = new NoiseProvider (seed, r, m, undersee, denivel_min, denivel_max);
			if (searchALatLon) {
				NoiseProvider.LatitudeAngle = np.LatitudeAngle;
				NoiseProvider.LongitudeAngle = np.LongitudeAngle;
			}

			Settings = new Dictionary<int, TerrainChunkSettings> ();
			for (int res = 0; res <= Depth; ++res) {
				Settings[(int)Math.Pow(2, res)] = new TerrainChunkSettings (maxChunk, maxChunk, chunkSz * (int)Math.Pow(2, res), chunkSz, (int)(denivel_max - denivel_min), FlatTexture, SteepTexture, TerrainMaterial, TreePrefab);
			}
		}

		private void Awake()
        {
        }
		/*
		private void Update()
        {
			if (r > 0) {
				Cache.Update ();
			}
        }
        */
		private TerrainChunk CreateAChunck(TerrainQuadTreeNode<TerrainChunk> node) {
			var chunk = new TerrainChunk (Settings [node.SZ], NoiseProvider, node.X, node.Y, node.SZ, node, CachePath);
			return chunk;
		}

		public bool KeepDisplayed(TerrainQuadTreeNode<TerrainChunk> node) {
			return node.obj.IsDisplayed ();
		}

		public bool KeepAllNotSplitted(TerrainQuadTreeNode<TerrainChunk> node) {
			return ! node.Splitted;
		}

		public bool KeepAllNotCreated(TerrainQuadTreeNode<TerrainChunk> node) {
			return ! node.Splitted && ! node.obj.IsHeightmapReady();
		}

		public bool KeepAllReady(TerrainQuadTreeNode<TerrainChunk> node) {
			if (node.Splitted &&
				node.TL.obj.IsHeightmapReady () &&
				node.TR.obj.IsHeightmapReady () &&
				node.BL.obj.IsHeightmapReady () &&
				node.BR.obj.IsHeightmapReady ()) {
				return false;
			}
			return node.obj.IsHeightmapReady () && !node.obj.IsDisplayed();
		}

        public void UpdateTerrain(Vector3 worldPosition, int radius)
        {
            var chunkPosition = GetChunkPosition(worldPosition);
			bool needUpdateNeighbors = false;
			//if changed whe have to adapt chuck cache content
			if (QT.adapt (chunkPosition.X, chunkPosition.Z)) {
				var newall = new Dictionary<Vector2i, TerrainChunk> ();
				foreach (var node in QT.Enumerate (KeepAllNotSplitted)) {
					newall.Add (node.obj.Position, node.obj);
				}
				foreach (var chunk in LoadedChunks.Except (newall).ToList()) {
					chunk.Value.Remove ();
					//SetChunkNeighborhood (chunk.Value);
					LoadedChunks.Remove (chunk.Key);
					needUpdateNeighbors = true;
				}
			}
			if (r > 0) {
				var NotToConstruct = new List<TerrainChunk>();
				foreach (var node in QT.Enumerate(KeepAllNotCreated)) {
					NotToConstruct.Add (node.obj);
				}
				NotToConstruct.Sort ((a, b) => (chunkPosition.DistanceTo(a.Position).CompareTo(chunkPosition.DistanceTo(b.Position))));
				foreach (var tc in NotToConstruct.Take(SystemInfo.processorCount - 1)) {
					if (!tc.IsConstructing ()) {
						tc.StartGenerateHeightmap ();
					}
				}

				foreach (var node in QT.Enumerate(KeepAllReady)) {
					node.obj.CreateTerrain ();
					//SetChunkNeighborhood (node.obj);
					LoadedChunks.Add (node.obj.Position, node.obj);
					needUpdateNeighbors = true;
				}
			}

			if (needUpdateNeighbors) {
				foreach (var chunk in LoadedChunks) {
					SetChunkNeighborhood (chunk.Value);
				}
				UpdateAllChunkNeighbors ();
			}
        }

		void OnDrawGizmos() {
			if (QT != null) {
				foreach (var node in QT.Enumerate(KeepDisplayed)) {
					Vector3 size = node.obj.Terrain.terrainData.size;
					Vector3 center = node.obj.TerrainGameObject.transform.position + size / 2;
					Gizmos.color = Color.white;
					Gizmos.DrawWireCube(center, size);
				}
			}
		}

		private void SetChunkNeighborhood(TerrainChunk chunk)
		{
			TerrainChunk xUp=null;
			TerrainChunk xDown=null;
			TerrainChunk zUp=null;
			TerrainChunk zDown=null;

			//only connect same Resolution chunck
			//try no neighbors
			LoadedChunks.TryGetValue(new Vector2i(chunk.Position.X + chunk.Position.Res, chunk.Position.Z, chunk.Position.Res), out xUp);
			if (xUp == null) {
				if ((chunk.Position.X % (chunk.Position.Res * 2)) != 0) {
					var NPos = new Vector2i ();
					NPos.X = chunk.Position.X + chunk.Position.Res;
					NPos.Z = (chunk.Position.Z / (chunk.Position.Res * 2)) * (chunk.Position.Res * 2);
					NPos.Res = chunk.Position.Res * 2;
					LoadedChunks.TryGetValue (NPos, out xUp);
				}
			}

			LoadedChunks.TryGetValue(new Vector2i(chunk.Position.X - chunk.Position.Res, chunk.Position.Z, chunk.Position.Res), out xDown);
			if (xDown == null) {
				if ((chunk.Position.X % (chunk.Position.Res * 2)) == 0) {
					var NPos = new Vector2i ();
					NPos.X = chunk.Position.X - chunk.Position.Res * 2;
					NPos.Z = (chunk.Position.Z / (chunk.Position.Res * 2)) * (chunk.Position.Res * 2);
					NPos.Res = chunk.Position.Res * 2;
					LoadedChunks.TryGetValue (NPos, out xDown);
				}
			}

			LoadedChunks.TryGetValue(new Vector2i(chunk.Position.X, chunk.Position.Z + chunk.Position.Res, chunk.Position.Res), out zUp);
			if (zUp == null) {
				if ((chunk.Position.Z % (chunk.Position.Res * 2)) != 0) {
					var NPos = new Vector2i ();
					NPos.X = (chunk.Position.X / (chunk.Position.Res * 2)) * (chunk.Position.Res * 2);
					NPos.Z = chunk.Position.Z + chunk.Position.Res;
					NPos.Res = chunk.Position.Res * 2;
					LoadedChunks.TryGetValue (NPos, out zUp);
				}
			}

			LoadedChunks.TryGetValue(new Vector2i(chunk.Position.X, chunk.Position.Z - chunk.Position.Res, chunk.Position.Res), out zDown);
			if (zDown == null) {
				if ((chunk.Position.Z % (chunk.Position.Res * 2)) == 0) {
					var NPos = new Vector2i ();
					NPos.X = (chunk.Position.X / (chunk.Position.Res * 2)) * (chunk.Position.Res * 2);
					NPos.Z = chunk.Position.Z - chunk.Position.Res * 2;
					NPos.Res = chunk.Position.Res * 2;
					LoadedChunks.TryGetValue (NPos, out zDown);
				}
			}

			if (xUp != null) {
				chunk.SetNeighbors (xUp, TerrainNeighbor.XUp);
				xUp.SetNeighbors (chunk, TerrainNeighbor.XDown);
			}
			if (xDown != null)
			{
				chunk.SetNeighbors(xDown, TerrainNeighbor.XDown);
				xDown.SetNeighbors(chunk, TerrainNeighbor.XUp);
			}
			if (zUp != null)
			{
				chunk.SetNeighbors(zUp, TerrainNeighbor.ZUp);
				zUp.SetNeighbors(chunk, TerrainNeighbor.ZDown);
			}
			if (zDown != null)
			{
				chunk.SetNeighbors(zDown, TerrainNeighbor.ZDown);
				zDown.SetNeighbors(chunk, TerrainNeighbor.ZUp);
			}
		}

		private void UpdateAllChunkNeighbors()
		{
			foreach (var chunkEntry in LoadedChunks)
				chunkEntry.Value.UpdateNeighbors();
		}

        public Vector2i GetChunkPosition(Vector3 worldPosition)
        {
			var x = (int)Mathf.Floor(worldPosition.x / chunkSz);
			var z = (int)Mathf.Floor(worldPosition.z / chunkSz);
			if (QT != null && !QT.empty ()) {
				var node = QT.GetAtPos (x, z, KeepDisplayed);
				return node.Position;
			} else {
				return new Vector2i (x, z, 1);
			}
        }

        public bool IsTerrainAvailable(Vector3 worldPosition)
        {
			if (QT.empty ()) {
				return false;
			} else {
				var x = (int)Mathf.Floor(worldPosition.x / chunkSz);
				var z = (int)Mathf.Floor(worldPosition.z / chunkSz);
				var node = QT.GetAtPos (x, z, KeepDisplayed);
				return node.IsDisplayed ();
			}
		}

        public float GetTerrainHeight(Vector3 worldPosition)
        {
			if (QT.empty ()) {
				return 0;
			} else {
				var x = (int)Mathf.Floor(worldPosition.x / chunkSz);
				var z = (int)Mathf.Floor(worldPosition.z / chunkSz);
				var node = QT.GetAtPos (x, z, KeepDisplayed);
				return node.GetTerrainHeight (worldPosition);
			}
        }
    }
}