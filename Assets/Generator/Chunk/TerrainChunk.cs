using System;
using UnityEngine;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace TerrainGenerator
{
	public class TerrainChunk
    {
        public Vector2i Position { get; private set; }

		public Terrain Terrain { get; set; }

        private TerrainData Data { get; set; }

        private TerrainChunkSettings Settings { get; set; }

        private NoiseProvider NoiseProvider { get; set; }

        private TerrainChunkNeighborhood Neighborhood { get; set; }

		private double[,] PreHeightmap { get; set; }
        private float[,] Heightmap { get; set; }
		private float WorldHeightMin { get; set; }
		private float WorldHeightMax { get; set; }

        private object HeightmapThreadLockObject { get; set; }

		private TerrainQuadTreeNode<TerrainChunk> TQTNode;

		private string CachePath;

		private Thread thread;

		public GameObject TerrainGameObject;

		public TerrainChunk(TerrainChunkSettings settings, NoiseProvider noiseProvider, int x, int z, int res, TerrainQuadTreeNode<TerrainChunk> node, string cache_path)
        {
			CachePath = cache_path;
			TQTNode = node;
            HeightmapThreadLockObject = new object();

            Settings = settings;
            NoiseProvider = noiseProvider;
            Neighborhood = new TerrainChunkNeighborhood();

            Position = new Vector2i(x, z, res);

			thread = null;
			TerrainGameObject = null;
        }

        #region Heightmap stuff

		public TerrainChunk GetParent() {
			if (TQTNode != null && TQTNode.parent != null) {
				return TQTNode.parent.obj;
			}
			return null;
		}

		public void TryGenerateHeightmapWithSplitted() {
			if (! IsHeightmapReady() && TQTNode != null &&
				TQTNode.TL.obj.IsPreHeightmapReady() &&
				TQTNode.TR.obj.IsPreHeightmapReady() &&
				TQTNode.BL.obj.IsPreHeightmapReady() &&
				TQTNode.BR.obj.IsPreHeightmapReady()) {
				//Debug.Log ("TryGenerateHeightmapWithSplitted(): generate " + Position);

				var heightmap = new double[Settings.HeightmapResolution, Settings.HeightmapResolution];
				var resdiv2 = Settings.HeightmapResolution / 2;
				for (var zRes = 0; zRes < resdiv2; zRes++) {
					for (var xRes = 0; xRes < resdiv2; xRes++) {
						heightmap [zRes, xRes] = TQTNode.TL.obj.PreHeightmap [zRes * 2, xRes * 2];
					}
				}
				for (var zRes = 0; zRes < resdiv2; zRes++) {
					for (var xRes = 0; xRes <= resdiv2; xRes++) {
						heightmap [zRes, xRes + resdiv2] = TQTNode.TR.obj.PreHeightmap [zRes * 2, xRes * 2];
					}
				}
				for (var zRes = 0; zRes <= resdiv2; zRes++) {
					for (var xRes = 0; xRes < resdiv2; xRes++) {
						heightmap [zRes + resdiv2, xRes] = TQTNode.BL.obj.PreHeightmap [zRes * 2, xRes * 2];
					}
				}
				for (var zRes = 0; zRes <= resdiv2; zRes++) {
					for (var xRes = 0; xRes <= resdiv2; xRes++) {
						heightmap [zRes + resdiv2, xRes + resdiv2] = TQTNode.BR.obj.PreHeightmap [zRes * 2, xRes * 2];
					}
				}

				saveData (heightmap);

				lock (HeightmapThreadLockObject) {
					PreHeightmap = heightmap;
				}
				var parent = GetParent ();
				if (parent != null) {
					parent.TryGenerateHeightmapWithSplitted ();
				}
				CalculateHeightmap ();
			}
		}

		public void saveData(double[,] data, bool force = false) {
			string fpath = CachePath + "/" + NoiseProvider.getFolder ();
			fpath += "/" + Position.Res.ToString ();
			fpath += "/" + Position.X.ToString() + "_" + Position.Z.ToString () + ".raw";
			//Debug.Log (fpath);

			if (force || ! File.Exists (fpath)) {
				if (!Directory.Exists (Path.GetDirectoryName (fpath))) {
					Directory.CreateDirectory (Path.GetDirectoryName (fpath));
				}
				var byteArray = new byte[data.Length * sizeof(double)];
				Buffer.BlockCopy (data, 0, byteArray, 0, byteArray.Length);
				File.WriteAllBytes (fpath, byteArray);
			}
		}

		public bool HaveACacheOnDisk() {
			string fpath = CachePath + "/" + NoiseProvider.getFolder ();
			fpath += "/" + Position.Res.ToString ();
			fpath += "/" + Position.X.ToString() + "_" + Position.Z.ToString () + ".raw";
			return File.Exists (fpath);
		}

		public void StartGenerateHeightmap() {
			if (thread == null) {
				thread = new Thread (GenerateHeightmap);
				thread.Start ();
			}
		}

        public void GenerateHeightmap()
        {
			//Debug.Log ("GenerateHeightmap(): " + Position);
			//copy NoiseProvider it's because it's not thread safety
			//but 2 NoiseProvider with the same parameters give the same results
			var lnoise = new NoiseProvider (NoiseProvider);
			var parent = GetParent ();
			var xDecal = 0;
			var zDecal = 0;
			if (parent != null) {
				if (TQTNode == parent.TQTNode.TL) {
					xDecal = 0;
					zDecal = 0;
				} else if (TQTNode == parent.TQTNode.TR) {
					xDecal = Settings.HeightmapResolution / 2;
					zDecal = 0;
				} else if (TQTNode == parent.TQTNode.BL) {
					xDecal = 0;
					zDecal = Settings.HeightmapResolution / 2;
				} else if (TQTNode == parent.TQTNode.BR) {
					xDecal = Settings.HeightmapResolution / 2;
					zDecal = Settings.HeightmapResolution / 2;
				}
			}

			string fpath = CachePath + "/" + NoiseProvider.getFolder ();
			fpath += "/" + Position.Res.ToString ();
			fpath += "/" + Position.X.ToString() + "_" + Position.Z.ToString () + ".raw";

			var heightmap = new double[Settings.HeightmapResolution, Settings.HeightmapResolution];
			var loaded = false;
			if (File.Exists (fpath)) {
				var byteArray = File.ReadAllBytes (fpath);
				if (byteArray.Length == heightmap.Length * sizeof(double)) {
					Buffer.BlockCopy (byteArray, 0, heightmap, 0, byteArray.Length);
					loaded = true;
				}
			}
			if (!loaded) {
				for (var zRes = 0; zRes < Settings.HeightmapResolution; zRes++) {
					for (var xRes = 0; xRes < Settings.HeightmapResolution; xRes++) {
						if (false && parent != null && parent.PreHeightmap != null && xRes % 2 == 0 && zRes % 2 == 0) {
							heightmap [zRes, xRes] = parent.PreHeightmap [zRes / 2 + zDecal, xRes / 2 + xDecal];
						} else {
							double xCoordinate = (double)Position.X / Position.Res + (double)xRes / ((double)Settings.HeightmapResolution - 1);
							double zCoordinate = (double)Position.Z / Position.Res + (double)zRes / ((double)Settings.HeightmapResolution - 1);
							double yCoordinate = lnoise.HeightAt ((float)xCoordinate * Settings.Length, (float)zCoordinate * Settings.Length);
							if (yCoordinate < 100.0 ) {
								Debug.Log("coucoue");
								yCoordinate = lnoise.HeightAt ((float)xCoordinate * Settings.Length, (float)zCoordinate * Settings.Length);
							}
							heightmap [zRes, xRes] = yCoordinate; //lnoise.GetValue ((float)xCoordinate * Settings.Length, (float)zCoordinate * Settings.Length);
						}
					}
				}

				saveData (heightmap, true);
			}

			lock (HeightmapThreadLockObject) {
				PreHeightmap = heightmap;
			}

			if (parent != null) {
				parent.TryGenerateHeightmapWithSplitted ();
			}

			CalculateHeightmap ();
        }

		public void CalculateHeightmap() {
			Debug.Log ("CalculateHeightmap: " + Position);
			double min = double.MaxValue;
			double max = double.MinValue;
			for (var zRes = 0; zRes < Settings.HeightmapResolution; zRes++) {
				for (var xRes = 0; xRes < Settings.HeightmapResolution; xRes++) {
					if (PreHeightmap [zRes, xRes] < min) {
						min = PreHeightmap [zRes, xRes];
					}
					if (PreHeightmap [zRes, xRes] > max) {
						max = PreHeightmap [zRes, xRes];
					}
				}
			}
			min = Math.Floor (min);
			max = Math.Ceiling (max);
			double height = max - min;
			var heightmap = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
			for (var zRes = 0; zRes < Settings.HeightmapResolution; zRes++) {
				for (var xRes = 0; xRes < Settings.HeightmapResolution; xRes++) {
					double val = (PreHeightmap [zRes, xRes] - min) / height;
					heightmap[zRes, xRes] = (float)val;
				}
			}
			lock (HeightmapThreadLockObject) {
				WorldHeightMin = (float)(min - NoiseProvider.r);
				WorldHeightMax = (float)(max - NoiseProvider.r);
				Heightmap = heightmap;
			}
		}


		public bool IsPreHeightmapReady()
		{
			lock (HeightmapThreadLockObject) {
				return PreHeightmap != null;
			}
		}

        public bool IsHeightmapReady()
        {
			lock (HeightmapThreadLockObject) {
				return Heightmap != null;
			}
        }

		public bool IsConstructing()
		{
			return thread != null && ! IsHeightmapReady();
		}

		public bool IsDisplayed() {
			return Terrain != null;
		}

		public float GetTerrainHeight(Vector3 worldPosition)
        {
			return Terrain.SampleHeight(worldPosition) + WorldHeightMin;
        }

        #endregion

        #region Main terrain generation

        public void CreateTerrain()
        {
			Debug.Log ("CreateTerrain(): " + Position + " " + WorldHeightMin + " " + WorldHeightMax);
            Data = new TerrainData();
            Data.heightmapResolution = Settings.HeightmapResolution;
            Data.alphamapResolution = Settings.AlphamapResolution;
			lock (HeightmapThreadLockObject) {
				Data.SetHeights (0, 0, Heightmap);
			}
            ApplyTextures(Data);

			if (Settings.TreePrefab != null) {
				var tp = new TreePrototype ();
				tp.prefab = Settings.TreePrefab;
				var prototypes = new List<TreePrototype> ();
				prototypes.Add (tp);
				Data.treePrototypes = prototypes.ToArray ();
				Data.RefreshPrototypes ();
			}

            //Data.size = new Vector3(Settings.Length, Settings.Height, Settings.Length);
			Data.size = new Vector3(Settings.Length, WorldHeightMax - WorldHeightMin, Settings.Length);
			TerrainGameObject = Terrain.CreateTerrainGameObject(Data);
			//TerrainGameObject.transform.position = new Vector3(Position.X * Settings.Decal, 0, Position.Z * Settings.Decal);
			TerrainGameObject.transform.position = new Vector3(Position.X * Settings.Decal, WorldHeightMin, Position.Z * Settings.Decal);


			Terrain = TerrainGameObject.GetComponent<Terrain>();
            Terrain.heightmapPixelError = 8;
            Terrain.materialType = UnityEngine.Terrain.MaterialType.Custom;
            Terrain.materialTemplate = Settings.TerrainMaterial;
            Terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

			if (Settings.TreePrefab != null) {
				GenerateTrees ();
			}

			Terrain.Flush();
		}

        private void ApplyTextures(TerrainData terrainData)
        {
            var flatSplat = new SplatPrototype();
            var steepSplat = new SplatPrototype();

            flatSplat.texture = Settings.FlatTexture;
            steepSplat.texture = Settings.SteepTexture;

            terrainData.splatPrototypes = new SplatPrototype[]
            {
                flatSplat,
                steepSplat
            };

            terrainData.RefreshPrototypes();

            var splatMap = new float[terrainData.alphamapResolution, terrainData.alphamapResolution, 2];

            for (var zRes = 0; zRes < terrainData.alphamapHeight; zRes++)
            {
                for (var xRes = 0; xRes < terrainData.alphamapWidth; xRes++)
                {
                    var normalizedX = (float)xRes / (terrainData.alphamapWidth - 1);
                    var normalizedZ = (float)zRes / (terrainData.alphamapHeight - 1);

                    var steepness = terrainData.GetSteepness(normalizedX, normalizedZ);
                    var steepnessNormalized = Mathf.Clamp(steepness / 1.5f, 0, 1f);

                    splatMap[zRes, xRes, 0] = 1f - steepnessNormalized;
                    splatMap[zRes, xRes, 1] = steepnessNormalized;
					/*
					var heightness = terrainData.GetInterpolatedHeight (normalizedX, normalizedZ);
					splatMap[zRes, xRes, 0] = heightness;
					splatMap[zRes, xRes, 1] = 1f - heightness;
					*/

				}
            }

            terrainData.SetAlphamaps(0, 0, splatMap);
        }


		private void GenerateTrees () {

			var treeList= new List<TreeInstance>();

			for (int i = 0; i < 100; ++i) {
				var treeposition = TerrainGameObject.transform.position;
				var treeDisplacement = Vector3.zero;

				// 1 - Create Tree from Prototype Tree 0

				var newtree = new TreeInstance ();
				newtree.prototypeIndex = 0;
				newtree.color = new Color (1, 1, 1);
				newtree.lightmapColor = new Color (1, 1, 1); 
				newtree.heightScale = 1;
				newtree.widthScale = 1;

				// 3 - Displace tree position randomly and height adjustment

				treeDisplacement = new Vector3 (UnityEngine.Random.Range (0, Settings.Length), 0, UnityEngine.Random.Range (0, Settings.Length)); // World Space Coords Random around player transform
				newtree.position = new Vector3 (treeposition.x + treeDisplacement.x, treeposition.y + treeDisplacement.y, treeposition.z + treeDisplacement.z);
				newtree.position.y = Terrain.terrainData.GetInterpolatedHeight (newtree.position.x, newtree.position.z);  // Dont sure if I have to normalize coords 
				var newtreeterrainLocalPos = newtree.position - Terrain.transform.position;
				var newtreenormalizedPos = new Vector2 (Mathf.InverseLerp (0.0f, Terrain.terrainData.size.x, newtreeterrainLocalPos.x), Mathf.InverseLerp (0.0f, Terrain.terrainData.size.z, newtreeterrainLocalPos.z));
				newtree.position = new Vector3 (newtreenormalizedPos.x, 0, newtreenormalizedPos.y);

				// 4 - Add tree to the terrain

				treeList.Add (newtree);

			}

			Terrain.terrainData.treeInstances = treeList.ToArray ();
		}

        #endregion

        #region Distinction
        
		public override int GetHashCode()
        {
            return Position.GetHashCode();
        }

		public override bool Equals(object obj)
        {
            var other = obj as TerrainChunk;
            if (other == null)
                return false;

            return this.Position.Equals(other.Position);
        }

		public override string ToString()
		{
			return "<TerrainChunck pos="+Position+">";
		}

        #endregion

        #region Chunk removal

        public void Remove()
        {
			if (thread != null) {
				thread.Abort ();
				thread = null;
			}
			//Debug.Log ("Remove(): " + Position);
			if (Neighborhood.XDown != null) {
				Neighborhood.XDown.RemoveFromNeighborhood (this);
				Neighborhood.XDown = null;
			}
			if (Neighborhood.XUp != null) {
				Neighborhood.XUp.RemoveFromNeighborhood (this);
				Neighborhood.XUp = null;
			}
			if (Neighborhood.ZDown != null) {
				Neighborhood.ZDown.RemoveFromNeighborhood (this);
				Neighborhood.ZDown = null;
			}
			if (Neighborhood.ZUp != null) {
				Neighborhood.ZUp.RemoveFromNeighborhood (this);
				Neighborhood.ZUp = null;
			}

			if (Terrain != null) {
				//Terrain.drawHeightmap = false;
				GameObject.Destroy (Terrain.gameObject);
				Terrain = null;
			}
        }

		public void Delete()
		{
			
			// If heihtmap is generating we must stop
			if (thread != null) {
				thread.Abort ();
				thread = null;
			}

			//Debug.Log ("Delete(): " + Position);
			lock (HeightmapThreadLockObject) {
				Heightmap = null;
			}
			Settings = null;

			if (Neighborhood.XDown != null)
			{
				Neighborhood.XDown.RemoveFromNeighborhood(this);
				Neighborhood.XDown = null;
			}
			if (Neighborhood.XUp != null)
			{
				Neighborhood.XUp.RemoveFromNeighborhood(this);
				Neighborhood.XUp = null;
			}
			if (Neighborhood.ZDown != null)
			{
				Neighborhood.ZDown.RemoveFromNeighborhood(this);
				Neighborhood.ZDown = null;
			}
			if (Neighborhood.ZUp != null)
			{
				Neighborhood.ZUp.RemoveFromNeighborhood(this);
				Neighborhood.ZUp = null;
			}

			if (Terrain != null) {
				GameObject.Destroy(Terrain.gameObject);
				Terrain = null;
			}
		}
        public void RemoveFromNeighborhood(TerrainChunk chunk)
        {
            if (Neighborhood.XDown == chunk)
                Neighborhood.XDown = null;
            if (Neighborhood.XUp == chunk)
                Neighborhood.XUp = null;
            if (Neighborhood.ZDown == chunk)
                Neighborhood.ZDown = null;
            if (Neighborhood.ZUp == chunk)
                Neighborhood.ZUp = null;
        }

        #endregion

        #region Neighborhood

        public void SetNeighbors(TerrainChunk chunk, TerrainNeighbor direction)
        {
            if (chunk != null)
            {
                switch (direction)
                {
                    case TerrainNeighbor.XUp:
                        Neighborhood.XUp = chunk;
                        break;

                    case TerrainNeighbor.XDown:
                        Neighborhood.XDown = chunk;
                        break;

                    case TerrainNeighbor.ZUp:
                        Neighborhood.ZUp = chunk;
                        break;

                    case TerrainNeighbor.ZDown:
                        Neighborhood.ZDown = chunk;
                        break;
                }
            }
        }

        public void UpdateNeighbors()
        {
            if (Terrain != null)
            {
				var xDown = Neighborhood.XDown == null || Neighborhood.XDown.Position.Res != Position.Res ? null : Neighborhood.XDown.Terrain;
				var xUp = Neighborhood.XUp == null || Neighborhood.XUp.Position.Res != Position.Res ? null : Neighborhood.XUp.Terrain;
				var zDown = Neighborhood.ZDown == null || Neighborhood.ZDown.Position.Res != Position.Res ? null : Neighborhood.ZDown.Terrain;
				var zUp = Neighborhood.ZUp == null || Neighborhood.ZUp.Position.Res != Position.Res ? null : Neighborhood.ZUp.Terrain;

				Debug.Log ("UpdateNeighbors() " + Position + " " + Neighborhood);

				//Todo add QuadTree terrain Neighbors capacity instead of this !
				// *--*-----*
				// |  |     |
				// *--*     |
				// |  |     |
				// *--*-----*

				//Update HeightMap Data for part without neighbors.
				//reset to default
				bool debug = false;

				/*
				lock (HeightmapThreadLockObject) {
					if (! debug) {
						Terrain.terrainData.SetHeights (0, 0, Heightmap);
					} else {
						float[,] heightmap = (float[,])Heightmap.Clone ();
						heightmap [0, 0] = 1.0f; //Heightmap [0, 0] + 0.1f;
						heightmap [0, Settings.HeightmapResolution - 1] = 1.0f; //Heightmap [0, Settings.HeightmapResolution - 1] + 0.1f;
						heightmap [Settings.HeightmapResolution - 1, 0] = 1.0f; //Heightmap [Settings.HeightmapResolution - 1, 0] + 0.1f;
						heightmap [Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1] = 1.0f; //Heightmap [Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1] + 0.1f;
						Terrain.terrainData.SetHeights (0, 0, heightmap);
					}
				}
				*/

				//Debug.Log ("UpdateNeighbors" + Position + ": xd=" + xDown + " xu=" + xUp + " zd=" + zDown + " zu=" + zUp);
				/*
				var decal = -0.0f;
				if (zDown == null) {
					var heightmap = new float[1, Settings.HeightmapResolution];
					lock (HeightmapThreadLockObject) {
						for (int i = 0; i < Settings.HeightmapResolution; ++i) {
							heightmap [0, i] = Heightmap [0, i] + decal;
						}
					}
					Terrain.terrainData.SetHeights (0, 0, heightmap);
				}
				if (zUp == null) {
					var heightmap = new float[1, Settings.HeightmapResolution];
					lock (HeightmapThreadLockObject) {
						for (int i = 0; i < Settings.HeightmapResolution; ++i) {
							heightmap [0, i] = Heightmap [Settings.HeightmapResolution - 1, i] + decal;
						}
					}
					Terrain.terrainData.SetHeights (0, Settings.HeightmapResolution-1, heightmap);
				}
				if (xDown == null) {
					var heightmap = new float[Settings.HeightmapResolution, 1];
					lock (HeightmapThreadLockObject) {
						for (int i = 0; i < Settings.HeightmapResolution; ++i) {
							heightmap [i, 0] = Heightmap [i, 0] + decal;
						}
					}
					Terrain.terrainData.SetHeights (0, 0, heightmap);
				}
				if (xUp == null) {
					var heightmap = new float[Settings.HeightmapResolution, 1];
					lock (HeightmapThreadLockObject) {
						for (int i = 0; i < Settings.HeightmapResolution; ++i) {
							heightmap [i, 0] = Heightmap [i, Settings.HeightmapResolution - 1] + decal;
						}
					}
					Terrain.terrainData.SetHeights (Settings.HeightmapResolution-1, 0, heightmap);
				}
				*/
                Terrain.SetNeighbors(xDown, zUp, xUp, zDown);
                Terrain.Flush();
            }
        }

        #endregion
    }
}