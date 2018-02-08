using System;
using UnityEngine;

namespace TerrainGenerator
{
	public class TerrainChunk : Ready
    {
        public Vector2i Position { get; private set; }

        private Terrain Terrain { get; set; }

        private TerrainData Data { get; set; }

        private TerrainChunkSettings Settings { get; set; }

        private NoiseProvider NoiseProvider { get; set; }

        private TerrainChunkNeighborhood Neighborhood { get; set; }

        private float[,] Heightmap { get; set; }

        private object HeightmapThreadLockObject { get; set; }

		public TerrainChunk(TerrainChunkSettings settings, NoiseProvider noiseProvider, int x, int z, int res)
        {
            HeightmapThreadLockObject = new object();

            Settings = settings;
            NoiseProvider = noiseProvider;
            Neighborhood = new TerrainChunkNeighborhood();

            Position = new Vector2i(x, z, res);
        }

        #region Heightmap stuff

        public void GenerateHeightmap()
        {
			//copy NoiseProvider it's because it's not thread safety
			//but 2 NoiseProvider with the same parameters give the same results
			var lnoise = new NoiseProvider (NoiseProvider);
            lock (HeightmapThreadLockObject)
            {
                var heightmap = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];

                for (var zRes = 0; zRes < Settings.HeightmapResolution; zRes++)
                {
                    for (var xRes = 0; xRes < Settings.HeightmapResolution; xRes++)
                    {
						double xCoordinate = (double)Position.X/Position.Res + (double)xRes / ((double)Settings.HeightmapResolution - 1);
						double zCoordinate = (double)Position.Z/Position.Res + (double)zRes / ((double)Settings.HeightmapResolution - 1);

						heightmap[zRes, xRes] = lnoise.GetValue((float)xCoordinate * Settings.Length, (float)zCoordinate * Settings.Length);
                    }
                }

                Heightmap = heightmap;
				//Debug.Log ("pos=("+Position.X+", "+Position.Z+", "+Position.Res+") "+" minx=" + lnoise.minx + " maxx=" + lnoise.maxx + " length="+Settings.Length);
            }
        }

        public bool IsHeightmapReady()
        {
            return Terrain == null && Heightmap != null;
        }

		public bool IsReady() {
			return IsHeightmapReady ();
		}

        public float GetTerrainHeight(Vector3 worldPosition)
        {
            return Terrain.SampleHeight(worldPosition);
        }

        #endregion

        #region Main terrain generation

        public void CreateTerrain()
        {
            Data = new TerrainData();
            Data.heightmapResolution = Settings.HeightmapResolution;
            Data.alphamapResolution = Settings.AlphamapResolution;
            Data.SetHeights(0, 0, Heightmap);
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

        #endregion

        #region Chunk removal

        public void Remove()
        {
            Heightmap = null;
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

            if (Terrain != null)
                GameObject.Destroy(Terrain.gameObject);
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
				var decal = -0.1f;
				Terrain.terrainData.SetHeights (0, 0, Heightmap);

				//Debug.Log ("UpdateNeighbors" + Position + ": xd=" + xDown + " xu=" + xUp + " zd=" + zDown + " zu=" + zUp);

				if (zDown == null) {
					var heightmap = new float[1, Settings.HeightmapResolution];
					for (int i = 0; i < Settings.HeightmapResolution; ++i) {
						heightmap[0, i] = Heightmap [0, i]+decal;
					}
					Terrain.terrainData.SetHeights (0, 0, heightmap);
				}
				if (zUp == null) {
					var heightmap = new float[1, Settings.HeightmapResolution];
					for (int i = 0; i < Settings.HeightmapResolution; ++i) {
						heightmap[0, i] = Heightmap [Settings.HeightmapResolution-1, i]+decal;
					}
					Terrain.terrainData.SetHeights (0, Settings.HeightmapResolution-1, heightmap);
				}
				if (xDown == null) {
					var heightmap = new float[Settings.HeightmapResolution, 1];
					for (int i = 0; i < Settings.HeightmapResolution; ++i) {
						heightmap[i, 0] = Heightmap [i, 0]+decal;
					}
					Terrain.terrainData.SetHeights (0, 0, heightmap);
				}
				if (xUp == null) {
					var heightmap = new float[Settings.HeightmapResolution, 1];
					for (int i = 0; i < Settings.HeightmapResolution; ++i) {
						heightmap[i, 0] = Heightmap [i, Settings.HeightmapResolution-1]+decal;
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