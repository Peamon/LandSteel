using UnityEngine;

namespace TerrainGenerator
{
    public class TerrainChunkSettings
    {
        public int HeightmapResolution { get; private set; }

        public int AlphamapResolution { get; private set; }

        public int Length { get; private set; }

		public int Decal { get; private set; }

		public int Height { get; private set; }

        public Texture2D FlatTexture { get; private set; }

        public Texture2D SteepTexture { get; private set; }

        public Material TerrainMaterial { get; private set; }

		public GameObject TreePrefab { get; private set; }

		public TerrainChunkSettings(int heightmapResolution, int alphamapResolution, int length, int decal, int height, Texture2D flatTexture, Texture2D steepTexture, Material terrainMaterial, GameObject treePrefab)
        {
            HeightmapResolution = heightmapResolution;
            AlphamapResolution = alphamapResolution;
            Length = length;
			Decal = decal;
            Height = height;
            FlatTexture = flatTexture;
            SteepTexture = steepTexture;
            TerrainMaterial = terrainMaterial;
			TreePrefab = treePrefab;
        }
    }
}