using System;
using TerrainGenerator;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
	public interface Ready {
		bool IsReady();
	}

	class DescComparer<T> : IComparer<T>
	{
		public int Compare(T x, T y)
		{
			return Comparer<T>.Default.Compare(y, x);
		}
	}

	public class TerrainQuadTreeNode<T> where T : Ready
	{
		public int X { get; set; }
		public int Y { get; set; }
		public int SZ { get; set; }
		private int nbfils { get; set; }
		private TerrainQuadTree<T>.CreateTerrainQuadTreeNodeElement creator;
		public T obj { get; set; }

		private TerrainQuadTreeNode<T> TL { get; set; }
		private TerrainQuadTreeNode<T> TR { get; set; }
		private TerrainQuadTreeNode<T> BL { get; set; }
		private TerrainQuadTreeNode<T> BR { get; set; }
		private bool Splitted { get; set; }

		public TerrainQuadTreeNode (int x, int y, int sz, TerrainQuadTree<T>.CreateTerrainQuadTreeNodeElement c)
		{
			X = x;
			Y = y;
			SZ = sz;
			Splitted = false;
			creator = c;
			obj = creator (this);
			nbfils = 0;
		}

		private void split() {
			Debug.Log ("split() " + X + " " + Y + " " + SZ);
			if (!Splitted && SZ > 1) {
				TL = new TerrainQuadTreeNode<T> (X, Y, SZ / 2, creator);
				TR = new TerrainQuadTreeNode<T> (X + SZ / 2, Y, SZ / 2, creator);
				BL = new TerrainQuadTreeNode<T> (X, Y + SZ / 2, SZ / 2, creator);
				BR = new TerrainQuadTreeNode<T> (X + SZ / 2, Y + SZ / 2, SZ / 2, creator);
				Splitted = true;
				nbfils = 4;
			}
		}

		private void fusion() {
			if (Splitted) {
				TL = null;
				TR = null;
				BL = null;
				BR = null;
				Splitted = false;
				nbfils = 0;
			}
		}

		public int adapt(int x, int y) {
			Debug.Log ("adapt() " + X + " " + Y + " " + SZ + " " + obj.IsReady());
			if (SZ > 1) { // && obj.IsReady()) {
				int centerX = X + SZ / 2;
				int centerY = Y + SZ / 2;
				int distCarre = (centerX - x) * (centerX - x) + (centerY - y) * (centerY - y);
				if (distCarre < (1.5*SZ)*(1.5*SZ)) {
					split ();
					nbfils += TL.adapt (x, y);
					nbfils += TR.adapt (x, y);
					nbfils += BL.adapt (x, y);
					nbfils += BR.adapt (x, y);
				} else {
					fusion ();
				}
			}
			return nbfils;
		}

		public System.Collections.Generic.IEnumerable<TerrainQuadTreeNode<T>> Enumerator {
			get {
				if (Splitted && SZ > 1) {
					foreach (TerrainQuadTreeNode<T> tqn in TL.Enumerator) {
						yield return tqn;
					}
					foreach (TerrainQuadTreeNode<T> tqn in TR.Enumerator) {
						yield return tqn;
					}
					foreach (TerrainQuadTreeNode<T> tqn in BL.Enumerator) {
						yield return tqn;
					}
					foreach (TerrainQuadTreeNode<T> tqn in BR.Enumerator) {
						yield return tqn;
					}
				} else {
					yield return this;
				}
			}
		}
	}

	public class TerrainQuadTree<T> where T : Ready
	{
		public int SZ { get; }
		public delegate T CreateTerrainQuadTreeNodeElement(TerrainQuadTreeNode<T> node);
		CreateTerrainQuadTreeNodeElement creator;
		public TerrainQuadTreeNode<T> root;

		public TerrainQuadTree (int depth, CreateTerrainQuadTreeNodeElement c)
		{
			SZ = (int)Math.Pow (2, depth);
			creator = c;
			root = null;
		}

		public void adapt(int x, int y) {
			if (root == null) {
				root = new TerrainQuadTreeNode<T> (-SZ / 2, -SZ / 2, SZ, creator);
			}
			if (x < - root.SZ / 2 || x > root.SZ / 2 || y < - root.SZ / 2 || y > root.SZ / 2) {
				throw new System.IndexOutOfRangeException();
			}
			root.adapt (x, y);
		}

		public System.Collections.Generic.IEnumerable<TerrainQuadTreeNode<T>> Enumerator {
			get {
				foreach (TerrainQuadTreeNode<T> tqn in root.Enumerator) {
					yield return tqn;
				}
			}
		}
	}
}

