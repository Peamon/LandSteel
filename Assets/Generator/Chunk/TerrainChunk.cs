using System;
using UnityEngine;
using System.IO;

namespace TerrainGenerator
{
	public class TerrainChunk : TQTReady
    {
        public Vector2i Position { get; private set; }

        private Terrain Terrain { get; set; }

        private TerrainData Data { get; set; }

        private TerrainChunkSettings Settings { get; set; }

        private NoiseProvider NoiseProvider { get; set; }

        private TerrainChunkNeighborhood Neighborhood { get; set; }

        private float[,] Heightmap { get; set; }

        private object HeightmapThreadLockObject { get; set; }

		private TerrainQuadTreeNode<TerrainChunk> TQTNode;

		private string CachePath;

		public TerrainChunk(TerrainChunkSettings settings, NoiseProvider noiseProvider, int x, int z, int res, TerrainQuadTreeNode<TerrainChunk> node, string cache_path)
        {
			CachePath = cache_path;
			TQTNode = node;
            HeightmapThreadLockObject = new object();

            Settings = settings;
            NoiseProvider = noiseProvider;
            Neighborhood = new TerrainChunkNeighborhood();

            Position = new Vector2i(x, z, res);
        }

        #region Heightmap stuff

		public TerrainChunk GetParent() {
			if (TQTNode != null && TQTNode.parent != null) {
				return TQTNode.parent.obj;
			}
			return null;
		}

		public void TryGenerateHeightmapWithSplitted() {
			if (TQTNode != null &&
				TQTNode.TL.obj.IsHeightmapReady() &&
				TQTNode.TR.obj.IsHeightmapReady() &&
				TQTNode.BL.obj.IsHeightmapReady() &&
				TQTNode.BR.obj.IsHeightmapReady()) {
				Debug.Log ("TryGenerateHeightmapWithSplitted(): " + Position);

				var heightmap = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
				var resdiv2 = Settings.HeightmapResolution / 2;
				for (var zRes = 0; zRes < resdiv2; zRes++) {
					for (var xRes = 0; xRes < resdiv2; xRes++) {
						heightmap [zRes, xRes] = TQTNode.TL.obj.Heightmap [zRes * 2, xRes * 2];
					}
				}
				for (var zRes = 0; zRes < resdiv2; zRes++) {
					for (var xRes = 0; xRes <= resdiv2; xRes++) {
						heightmap [zRes, xRes + resdiv2] = TQTNode.TR.obj.Heightmap [zRes * 2, xRes * 2];
					}
				}
				for (var zRes = 0; zRes <= resdiv2; zRes++) {
					for (var xRes = 0; xRes < resdiv2; xRes++) {
						heightmap [zRes + resdiv2, xRes] = TQTNode.BL.obj.Heightmap [zRes * 2, xRes * 2];
					}
				}
				for (var zRes = 0; zRes <= resdiv2; zRes++) {
					for (var xRes = 0; xRes <= resdiv2; xRes++) {
						heightmap [zRes + resdiv2, xRes + resdiv2] = TQTNode.BR.obj.Heightmap [zRes * 2, xRes * 2];
					}
				}

				saveData (heightmap);

				lock (HeightmapThreadLockObject) {
					Heightmap = heightmap;
				}
			}
		}

		public void saveData(float[,] data, bool force = false) {
			string fpath = CachePath + "/" + NoiseProvider.getFolder ();
			fpath += "/" + Position.Res.ToString ();
			fpath += "/" + Position.X.ToString() + "_" + Position.Z.ToString () + ".raw";
			Debug.Log (fpath);

			if (force || ! File.Exists (fpath)) {
				if (!Directory.Exists (Path.GetDirectoryName (fpath))) {
					Directory.CreateDirectory (Path.GetDirectoryName (fpath));
				}
				var byteArray = new byte[data.Length * 4];
				Buffer.BlockCopy (data, 0, byteArray, 0, byteArray.Length);
				File.WriteAllBytes (fpath, byteArray);
			}
		}

        public void GenerateHeightmap()
        {
			Debug.Log ("GenerateHeightmap(): " + Position);
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

			var heightmap = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
			var loaded = false;
			if (File.Exists (fpath)) {
				var byteArray = File.ReadAllBytes (fpath);
				if (byteArray.Length == heightmap.Length * 4) {
					Buffer.BlockCopy (byteArray, 0, heightmap, 0, byteArray.Length);
					loaded = true;
				}
			}
			if (!loaded) {
				for (var zRes = 0; zRes < Settings.HeightmapResolution; zRes++) {
					for (var xRes = 0; xRes < Settings.HeightmapResolution; xRes++) {
						if (parent != null && parent.Heightmap != null && xRes % 2 == 0 && zRes % 2 == 0) {
							heightmap [zRes, xRes] = parent.Heightmap [zRes / 2 + zDecal, xRes / 2 + xDecal];
						} else {
							double xCoordinate = (double)Position.X / Position.Res + (double)xRes / ((double)Settings.HeightmapResolution - 1);
							double zCoordinate = (double)Position.Z / Position.Res + (double)zRes / ((double)Settings.HeightmapResolution - 1);
							heightmap [zRes, xRes] = lnoise.GetValue ((float)xCoordinate * Settings.Length, (float)zCoordinate * Settings.Length);
						}
					}
				}

				saveData (heightmap, true);
			}

			lock (HeightmapThreadLockObject) {
				Heightmap = heightmap;
			}
			if (parent != null) {
				parent.TryGenerateHeightmapWithSplitted ();
			}
        }

        public bool IsHeightmapReady()
        {
			lock (HeightmapThreadLockObject) {
				return Heightmap != null;
			}
        }

		public bool IsReady() {
			return Terrain != null && Terrain.drawHeightmap;
		}

		public float GetTerrainHeight(Vector3 worldPosition)
        {
            return Terrain.SampleHeight(worldPosition);
        }

        #endregion

        #region Main terrain generation

        public void CreateTerrain()
        {
			Debug.Log ("CreateTerrain(): " + Position);
            Data = new TerrainData();
            Data.heightmapResolution = Settings.HeightmapResolution;
            Data.alphamapResolution = Settings.AlphamapResolution;
			lock (HeightmapThreadLockObject) {
				Data.SetHeights (0, 0, Heightmap);
			}
            ApplyTextures(Data);

            Data.size = new Vector3(Settings.Length, Settings.Height, Settings.Length);
            var newTerrainGameObject = Terrain.CreateTerrainGameObject(Data);
			newTerrainGameObject.transform.position = new Vector3(Position.X * Settings.Decal, 0, Position.Z * Settings.Decal);

			Terrain = newTerrainGameObject.GetComponent<Terrain>();
            Terrain.heightmapPixelError = 8;
            Terrain.materialType = UnityEngine.Terrain.MaterialType.Custom;
            Terrain.materialTemplate = Settings.TerrainMaterial;
            Terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
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

		public bool CanBeRemoved() {
			if (TQTNode != null && TQTNode.TL != null) {
				if (TQTNode.TL.obj.IsHeightmapReady() &&
					TQTNode.TR.obj.IsHeightmapReady() &&
					TQTNode.BL.obj.IsHeightmapReady() &&
					TQTNode.BR.obj.IsHeightmapReady()) {
					return true;
				}
				return false;
			}
			return true;
		}

        public void Remove()
        {
			Debug.Log ("Remove(): " + Position);
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
				//Terrain.drawHeightmap = false;
				GameObject.Destroy(Terrain.gameObject);
				Terrain = null;
			}
        }

		public void Delete()
		{
			Debug.Log ("Delete(): " + Position);
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
                var xDown = Neighborhood.XDown == null ? null : Neighborhood.XDown.Terrain;
                var xUp = Neighborhood.XUp == null ? null : Neighborhood.XUp.Terrain;
                var zDown = Neighborhood.ZDown == null ? null : Neighborhood.ZDown.Terrain;
                var zUp = Neighborhood.ZUp == null ? null : Neighborhood.ZUp.Terrain;

				//Todo add QuadTree terrain Neighbors capacity instead of this !
				// *--*-----*
				// |  |     |
				// *--*     |
				// |  |     |
				// *--*-----*

				//Update HeightMap Data for part without neighbors.
				//reset to default
				var decal = -0.0f;
				lock (HeightmapThreadLockObject) {
					Terrain.terrainData.SetHeights (0, 0, Heightmap);
				}

				//Debug.Log ("UpdateNeighbors" + Position + ": xd=" + xDown + " xu=" + xUp + " zd=" + zDown + " zu=" + zUp);

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
                Terrain.SetNeighbors(xDown, zUp, xUp, zDown);
                Terrain.Flush();
            }
        }

        #endregion
    }
}