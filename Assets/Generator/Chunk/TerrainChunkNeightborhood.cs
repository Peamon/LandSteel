namespace TerrainGenerator
{
    public class TerrainChunkNeighborhood
    {
        public TerrainChunk XUp { get; set; }

        public TerrainChunk XDown { get; set; }

        public TerrainChunk ZUp { get; set; }

        public TerrainChunk ZDown { get; set; }

		public override string ToString()
		{
			return "[TerrainChunkNeighborhood"
				+ " xU" + (XUp != null ? XUp.Position.ToString() : "[null]")
				+ " xD" + (XDown != null ? XDown.Position.ToString() : "[null]")
				+ " zU" + (ZUp != null ? ZUp.Position.ToString() : "[null]")
				+ " zD" + (ZDown != null ? ZDown.Position.ToString() : "[null]");
		}
    }

    public enum TerrainNeighbor
    {
        XUp,
        XDown,
        ZUp,
        ZDown
    }
}