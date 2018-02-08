namespace TerrainGenerator
{
    public class Vector2i
    {
        public int X { get; set; }

        public int Z { get; set; }

		public int Res { get; set; }

		public Vector2i()
        {
            X = 0;
            Z = 0;
			Res = 1;
        }

		public Vector2i(int x, int z, int res)
        {
            X = x;
            Z = z;
			Res = res;
        }

        public override int GetHashCode()
        {
			return X.GetHashCode() ^ Z.GetHashCode() ^ Res.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as Vector2i;
            if (other == null)
                return false;

			return this.X == other.X && this.Z == other.Z && this.Res == other.Res;
        }

		public int DistanceTo(Vector2i v) {
			int dX = (X + Res / 2) - (v.X + v.Res / 2);
			int dZ = (Z + Res / 2) - (v.Z + v.Res / 2);
			return dX*dX+dZ*dZ;
		}

        public override string ToString()
        {
			return "[" + X + "," + Z + "," + Res + "]";
        }
    }
}