﻿using System;
using TerrainGenerator;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
	public class TerrainQuadTreeNode<T>
	{
		public int X { get; set; }
		public int Y { get; set; }
		public int SZ { get; set; }
		private TerrainQuadTree<T>.CreateTerrainQuadTreeNodeElement creator;
		public T obj { get; set; }

		public TerrainQuadTreeNode<T> TL { get; set; }
		public TerrainQuadTreeNode<T> TR { get; set; }
		public TerrainQuadTreeNode<T> BL { get; set; }
		public TerrainQuadTreeNode<T> BR { get; set; }
		public TerrainQuadTreeNode<T> parent { get; set; }
		public bool Splitted { get; set; }

		public TerrainQuadTreeNode (int x, int y, int sz, TerrainQuadTreeNode<T> p, TerrainQuadTree<T>.CreateTerrainQuadTreeNodeElement c)
		{
			X = x;
			Y = y;
			SZ = sz;
			Splitted = false;
			creator = c;
			obj = creator (this);
			parent = p;
		}

		private bool split() {
			if (!Splitted && SZ > 1) {
				TL = new TerrainQuadTreeNode<T> (X, Y, SZ / 2, this, creator);
				TR = new TerrainQuadTreeNode<T> (X + SZ / 2, Y, SZ / 2, this, creator);
				BL = new TerrainQuadTreeNode<T> (X, Y + SZ / 2, SZ / 2, this, creator);
				BR = new TerrainQuadTreeNode<T> (X + SZ / 2, Y + SZ / 2, SZ / 2, this, creator);
				Splitted = true;
				return true;
			}
			return false;
		}

		private bool fusion() {
			if (Splitted) {
				TL = null;
				TR = null;
				BL = null;
				BR = null;
				Splitted = false;
				return true;
			}
			return false;
		}

		public T GetAtPos(int x, int y, TerrainQuadTree<T>.Keep c) {
			if (c(this)) {
				return obj;
			}
			if (Splitted) {
				if (x < X + SZ / 2) {
					if (y < Y + SZ / 2) {
						return TL.GetAtPos (x, y, c);
					} else {
						return BL.GetAtPos (x, y, c);
					}
				} else {
					if (y < Y + SZ / 2) {
						return TR.GetAtPos (x, y, c);
					} else {
						return BR.GetAtPos (x, y, c);
					}
				}
			}
			return obj;
		}

		public bool adapt(int x, int y) {
			bool ret = false;
			if (SZ > 1) {
				int centerX = X + SZ / 2;
				int centerY = Y + SZ / 2;
				int distCarre = (centerX - x) * (centerX - x) + (centerY - y) * (centerY - y);
				if (distCarre < (1.5*SZ)*(1.5*SZ)) {
					ret |= split ();
					ret |= TL.adapt (x, y);
					ret |= TR.adapt (x, y);
					ret |= BL.adapt (x, y);
					ret |= BR.adapt (x, y);
				} else {
					ret |= fusion ();
				}
			}
			return ret;
		}

		public System.Collections.Generic.IEnumerable<TerrainQuadTreeNode<T>> Enumerate(TerrainQuadTree<T>.Keep c) {
			if (c (this)) {
				yield return this;
			}
			if (Splitted && SZ > 1) {
				foreach (TerrainQuadTreeNode<T> tqn in TL.Enumerate(c)) {
					yield return tqn;
				}
				foreach (TerrainQuadTreeNode<T> tqn in TR.Enumerate(c)) {
					yield return tqn;
				}
				foreach (TerrainQuadTreeNode<T> tqn in BL.Enumerate(c)) {
					yield return tqn;
				}
				foreach (TerrainQuadTreeNode<T> tqn in BR.Enumerate(c)) {
					yield return tqn;
				}
			}
		}
	}

	public class TerrainQuadTree<T>
	{
		public int SZ { get; }
		public delegate T CreateTerrainQuadTreeNodeElement(TerrainQuadTreeNode<T> node);
		public delegate bool Keep(TerrainQuadTreeNode<T> node);
		CreateTerrainQuadTreeNodeElement creator;
		public TerrainQuadTreeNode<T> root;

		public TerrainQuadTree (int depth, CreateTerrainQuadTreeNodeElement c)
		{
			SZ = (int)Math.Pow (2, depth);
			creator = c;
			root = null;
		}

		public bool empty() {
			return root == null;
		}

		public T GetAtPos(int x, int y, Keep c) {
			if (x < - root.SZ / 2 || x > root.SZ / 2 || y < - root.SZ / 2 || y > root.SZ / 2) {
				throw new System.IndexOutOfRangeException();
			}
			return root.GetAtPos(x, y, c);
		}

		public bool adapt(int x, int y) {
			bool ret = false;
			if (root == null) {
				root = new TerrainQuadTreeNode<T> (-SZ / 2, -SZ / 2, SZ, null, creator);
				ret = true;
			}
			if (x < - root.SZ / 2 || x > root.SZ / 2 || y < - root.SZ / 2 || y > root.SZ / 2) {
				throw new System.IndexOutOfRangeException();
			}
			ret |= root.adapt (x, y);
			return ret;
		}

		public System.Collections.Generic.IEnumerable<TerrainQuadTreeNode<T>> Enumerate(Keep c) {
			foreach (TerrainQuadTreeNode<T> tqn in root.Enumerate(c)) {
				yield return tqn;
			}
		}
	}
}

