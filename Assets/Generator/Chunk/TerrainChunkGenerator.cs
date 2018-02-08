using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TerrainGenerator
{
    public class TerrainChunkGenerator : MonoBehaviour
    {
        public Material TerrainMaterial;

        public Texture2D FlatTexture;
        public Texture2D SteepTexture;

		private Dictionary<int, TerrainChunkSettings> Settings;

		private NoiseProvider NoiseProvider;

        private ChunkCache Cache;
		private double r;
		int chunkSz = 100;

		TerrainQuadTree<TerrainChunk> QT;

		public void Set(int seed, double earthFactor, int Depth, bool searchALatLon = false, double atminheight=2000) {
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

			//Check nth point random to estimate max and min height
			for (int nb = 0; nb < 1000; ++nb) {
				int x = UnityEngine.Random.Range(-Radius, Radius);
				int z = UnityEngine.Random.Range(-Radius, Radius);
				double rx = UnityEngine.Random.Range(0, (float)chunkSz);
				double rz = UnityEngine.Random.Range(0, (float)chunkSz);
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
				Settings[(int)Math.Pow(2, res)] = new TerrainChunkSettings (maxChunk, maxChunk, chunkSz * (int)Math.Pow(2, res), chunkSz, (int)(denivel_max - denivel_min), FlatTexture, SteepTexture, TerrainMaterial);
			}
			Cache = new ChunkCache();
		}

		private void Awake()
        {
        }

		private void Update()
        {
			if (r > 0) {
				Cache.Update ();
			}
        }

		private TerrainChunk CreateAChunck(TerrainQuadTreeNode<TerrainChunk> node) {
			return new TerrainChunk(Settings[node.SZ], NoiseProvider, node.X, node.Y, node.SZ);
		}

		private List<TerrainChunk> GetChunkPositionsInRadius(Vector2i chunkPosition, int radius)
        {
			var result = new List<TerrainChunk>();
			QT.adapt (chunkPosition.X, chunkPosition.Z);
			foreach (TerrainQuadTreeNode<TerrainChunk> node in QT.Enumerator) {
				result.Add (node.obj);
			}
			result.Sort ((a, b) => (chunkPosition.DistanceTo(a.Position).CompareTo(chunkPosition.DistanceTo(b.Position))));

            return result;
        }

        public void UpdateTerrain(Vector3 worldPosition, int radius)
        {
            var chunkPosition = GetChunkPosition(worldPosition);
            var newPositions = GetChunkPositionsInRadius(chunkPosition, radius);

            var loadedChunks = Cache.GetGeneratedChunks();
            var chunksToRemove = loadedChunks.Except(newPositions).ToList();

            var positionsToGenerate = newPositions.Except(chunksToRemove).ToList();
			foreach (var chunck in positionsToGenerate) {
				if (Cache.ChunkCanBeAdded (chunck.Position)) {
					Cache.AddNewChunk (chunck);
				}
			}

			foreach (var chunck in chunksToRemove) {
				if (Cache.ChunkCanBeRemoved (chunck.Position))
					Cache.RemoveChunk (chunck.Position);
			}
        }

        public Vector2i GetChunkPosition(Vector3 worldPosition)
        {
			var x = (int)Mathf.Floor(worldPosition.x / chunkSz);
			var z = (int)Mathf.Floor(worldPosition.z / chunkSz);

            return new Vector2i(x, z, 1);
        }

        public bool IsTerrainAvailable(Vector3 worldPosition)
        {
            var chunkPosition = GetChunkPosition(worldPosition);
            return Cache.IsChunkGenerated(chunkPosition);
        }

        public float GetTerrainHeight(Vector3 worldPosition)
        {
            var chunkPosition = GetChunkPosition(worldPosition);
            var chunk = Cache.GetGeneratedChunk(chunkPosition);
            if (chunkPosition != null)
                return chunk.GetTerrainHeight(worldPosition);

            return 0;
        }
    }
}