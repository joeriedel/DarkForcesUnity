/* SectorTess.cs
 *
 * The MIT License (MIT)
 *
 * Copyright (c) 2015 Joseph Riedel
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
*/

using UnityEngine;
using System.Collections.Generic;

public class SectorTess {
	class Seg {
		public Vector2 a;
		public Vector2 b;
		public Vector2 n;
		public Vector2 ln;
		public float da;
		public float db;
		public float dbmda; // DB - DA
		public float d;
		public int ia;
		public int ib;
		public int wallIdx;
		
		public Seg(LEV.Sector sector, int ia, int ib, int wallIdx) {
			this.ia = ia;
			this.ib = ib;
			this.wallIdx = wallIdx;

			a = sector.vertices[ia];
			b = sector.vertices[ib];

			ln = b-a;
			ln.Normalize();

			da = Vector2.Dot(a, ln);
			db = Vector2.Dot(b, ln);

			dbmda = db - da;

			n = -ln;

			float t = n.x;
			n.x = -n.y;
			n.y = t;

			d = Vector2.Dot(a, n);
		}

		public void Flip() {
			Vector2 tv = a;
			a = b;
			b = a;
			n = -n;
			d = -d;
		}

		public float Distance(Vector2 p) {
			return Vector2.Dot(n, p) - d;
		}

		public bool InFront(Vector2 p) {
			return Distance(p) > 0.01f;
		}

		public bool InBack(Vector2 p) {
			return Distance(p) < -0.01f;
		}

		public bool Intersects(Seg other) {
			float d = Vector2.Dot(n, other.n);
			if (Mathf.Abs(d) > 0.9999) {
				return false; // parallel
			}

			float sa = Distance(other.a);
			float sb = Distance(other.b);

			if ((sa <= 0.01f) && (sb <= 0.01f)) {
				return false;
			}

			if ((sa >= -0.01f) && (sb >= -0.01f)) {
				return false;
			}

			Vector2 p = Vector2.Lerp(other.a, other.b, Mathf.Abs(sa) / ((Mathf.Abs(sa)+Mathf.Abs(sb))));

			return IsPointOnLine(p) && other.IsPointOnLine(p);
		}

		public bool IsPointOnLine(Vector2 p) {
			float pD = Vector2.Dot(ln, p);
			float y = pD - da;

			if (dbmda < 0f) {
				if ((y > -0.01f) || (y < dbmda+0.01f)) {
					return false;
				}
			} else {
				if ((y < 0.01f) || (y > dbmda-0.01f)) {
					return false;
				}
			}

			return true;
		}

		public bool SharesVertex(Seg other) {
			return (ia == other.ia) || (ia == other.ib) ||
				(ib == other.ia) || (ib == other.ia);
		}
	}

	static  bool CheckSegCounts(LinkedList<Seg> segs, List<int> counts) {
		foreach (var seg in segs) {
			if ((counts[seg.ia] < 2) || (counts[seg.ia] < 2)) {
				return false;
			}
		}
		return true;
	}
	
	static List<Seg> CopySegList(LEV.Sector sector, IEnumerable<Seg> segs) {
		List<Seg> newSegs = new List<Seg>();

		foreach (var seg in segs) {
			newSegs.Add(new Seg(sector, seg.ia, seg.ib, seg.wallIdx));
		}

		return newSegs;
	}
	
	static float _debugRenderOffset = 0f;
	static List<List<Seg>> _segSteps;

	static void DrawSeg(Seg seg, Color color) {
		Vector3 a = new Vector3(seg.a.x, _debugRenderOffset, seg.a.y);
		Vector3 b = new Vector3(seg.b.x, _debugRenderOffset, seg.b.y);
		Debug.DrawLine(a, b, color, float.MaxValue);

		Vector2 mid = Vector2.Lerp(seg.a, seg.b, 0.5f);
		Vector2 nml = mid + seg.n*0.15f;

		a = new Vector3(mid.x, _debugRenderOffset, mid.y);
		b = new Vector3(nml.x, _debugRenderOffset, nml.y);
		Debug.DrawLine(a, b, color, float.MaxValue);
	}

	static void DebugDrawStep(LEV.Sector sector, IEnumerable<Seg> newSegs, Seg newSeg0, Seg newSeg1, List<int> outTris) {
		foreach (var list in _segSteps) {
			foreach (var seg in list) {
				DrawSeg(seg, Color.green);
			}
		}

		foreach (var seg in newSegs) {
			if ((seg != newSeg0) && (seg != newSeg1)) {
				DrawSeg(seg, Color.red);
			}
		}

		if (outTris.Count >= 3) {
			int idx = outTris.Count - 3;

			Vector2 x = sector.vertices[outTris[idx]];
			Vector2 y = sector.vertices[outTris[idx+1]];
			Vector2 z = sector.vertices[outTris[idx+2]];

			Vector3 a = new Vector3(x.x, _debugRenderOffset, x.y);
			Vector3 b = new Vector3(y.x, _debugRenderOffset, y.y);
			Vector3 c = new Vector3(z.x, _debugRenderOffset, z.y);

			Debug.DrawLine(a, b, Color.yellow, float.MaxValue);
			Debug.DrawLine(b, c, Color.yellow, float.MaxValue);
			Debug.DrawLine(c, a, Color.yellow, float.MaxValue);
		}

		if (newSeg0 != null) {
			Vector3 a = new Vector3(newSeg0.a.x, _debugRenderOffset, newSeg0.a.y);
			Vector3 b = new Vector3(newSeg0.b.x, _debugRenderOffset, newSeg0.b.y);
			Debug.DrawLine(a, b, Color.magenta, float.MaxValue);
		}

		if (newSeg1 != null) {
			Vector3 a = new Vector3(newSeg1.a.x, _debugRenderOffset, newSeg1.a.y);
			Vector3 b = new Vector3(newSeg1.b.x, _debugRenderOffset, newSeg1.b.y);
			Debug.DrawLine(a, b, Color.magenta, float.MaxValue);
		}

		_debugRenderOffset += 16f;
	}

	// Returns list of triangle indices
	public static List<int> TesselateSector(LEV.Sector sector, int sectorIndex, bool debugDraw = false) {

		LinkedList<Seg> segs = new LinkedList<Seg>();
		List<int> verts = new List<int>();
		List<int> counts = new List<int>();

		for (int i = 0; i < sector.vertices.Count; ++i) {
			verts.Add(i);
			counts.Add(0);
		}

		for (int i = 0; i < sector.walls.Count; ++i) {
			LEV.Wall wall = sector.walls[i];

			// is there a reversed seg?
			LinkedListNode<Seg> revSeg = FindSeg(segs, wall.v1, wall.v0);

			if (revSeg != null) {
				// if this wall is an adjoin it overrides the solid wall
				if (wall.adjoin != -1) {
					segs.Remove(revSeg);
				} else {
					continue; // this is a duplicate of either a solid or an existing adjoin.
				}
			}
			
			Incr(counts, wall.v0, 1);
			Incr(counts, wall.v1, 1);
			segs.AddLast(new Seg(sector, wall.v0, wall.v1, i));
		}

		List<int> tris = new List<int>();

		if (debugDraw) {
			_debugRenderOffset = sector.ceilAlt + 8f;
			_segSteps = new List<List<Seg>>();
			DebugDrawStep(sector, segs, null, null, tris);
			_segSteps.Add(CopySegList(sector, segs));
		}

		for (var node = segs.First; node != null;) {

			List<int> validVerts = new List<int>();
			for (int i = 0; i < counts.Count; ++i) {
				if (counts[i] > 0) {
					DebugCheck.Assert(counts[i] > 1);
					validVerts.Add(i);
				}
			}

			verts = validVerts;

			node = MakeSegTri(sector, node, verts, counts, tris, debugDraw);
		}

		if (segs.Count > 0) {
			Debug.LogWarning("Bad floor in sector " + sectorIndex);
		}

		_segSteps = null;

		return tris;
	}

	static void Incr(List<int> counts, int index, int add) {
		counts[index] = counts[index] + add;
	}

	static LinkedListNode<Seg> MakeSegTri(LEV.Sector sector, LinkedListNode<Seg> node, List<int> verts, List<int> counts, List<int> outTris, bool debugDraw) {

		LinkedList<Seg> segs = node.List;

		// can we trivially make a triangle from our connected seg?
		List<LinkedListNode<Seg>> connectedSegs = FindConnectedSegs(segs, node.Value, node.Value.ib);
		if (connectedSegs == null) {
			// this is a bad seg
			segs.Remove(node);
			return segs.First;
		}

		foreach (var connectedSeg in connectedSegs) {
			if (TrySegToSeg(sector, segs, node.Value, connectedSeg.Value, counts, outTris, debugDraw)) {
				return segs.First;
			}
		}

		connectedSegs = FindConnectedSegs(segs, node.Value, node.Value.ia);
		if (connectedSegs == null) {
			// this is a bad seg
			segs.Remove(node);
			return segs.First;
		}
		foreach (var connectedSeg in connectedSegs) {
			if (TrySegToSeg(sector, segs, connectedSeg.Value, node.Value, counts, outTris, debugDraw)) {
				return segs.First;
			}
		}

		int i;
		while ((i=FindVertex(sector, node.Value, verts)) != -1) {
			if (TrySegToVertex(sector, segs, node.Value, i, counts, outTris, debugDraw)) {
				return segs.First;
			} else {
				verts.Remove(i);
			}
		}

		return null;
	}

	static List<LinkedListNode<Seg>> FindConnectedSegs(LinkedList<Seg> segs, Seg seg, int vert) {
		List<LinkedListNode<Seg>> list = null;

		for (var node = segs.First; node != null; node = node.Next) {
			Seg testSeg = node.Value;
			if ((testSeg != seg) && ((testSeg.ia == vert) || (testSeg.ib == vert))) {
				if (list == null) {
					list = new List<LinkedListNode<Seg>>();
				}
				list.Add(node);
			}
		}

		return list;
	}

	static LinkedListNode<Seg> FindSeg(LinkedList<Seg> segs, int v0, int v1) {
		for (var node = segs.First; node != null; node = node.Next) {
			Seg testSeg = node.Value;
			if ((testSeg.ia == v0) && (testSeg.ib == v1)) {
				return node;
			}
		}

		return null;
	}

	static bool TrySegToSeg(LEV.Sector sector, LinkedList<Seg> segs, Seg seg0, Seg seg1, List<int> counts, List<int> outTris, bool debugDraw) {

		if (!seg0.InFront(seg1.b)) {
			// concave angle
			return false;
		}
				
		// does this vertex cross any segs?
		LinkedListNode<Seg> existingSeg = FindSeg(segs, seg1.ib, seg0.ia);
		Seg addSeg0;

		if (existingSeg == null) {
			if (FindSeg(segs, seg0.ia, seg1.ib) != null) {
				 // bad-sector: inward facing wall.
				return false;
			}
			addSeg0 = new Seg(sector, seg0.ia, seg1.ib, -1);
		} else {
			addSeg0 = existingSeg.Value;
		}

		if (existingSeg == null) {
			foreach (var testSeg in segs) {
				if ((seg0 != testSeg) && (seg1 != testSeg)) {
					if (addSeg0.Intersects(testSeg)) {
						return false;
					}
				}
			}
		}

		if (ContainsAnyVertices(sector, seg0, seg1, addSeg0)) {
			return false;
		}

		// seg + seg create valid triangle
		outTris.Add(seg1.ia);
		outTris.Add(seg1.ib);
		outTris.Add(seg0.ia);

		segs.Remove(seg0);
		segs.Remove(seg1);

		if (existingSeg != null) {
			Incr(counts, seg0.ia, -2);
			Incr(counts, seg0.ib, -2);
			Incr(counts, seg1.ib, -2);
			segs.Remove(existingSeg);
			if (debugDraw) {
				DebugDrawStep(sector, segs, null, null, outTris);
				_segSteps.Add(CopySegList(sector, segs));
			}
		} else {
			segs.AddFirst(addSeg0);
			Incr(counts, seg0.ib, -2);
			if (debugDraw) {
				DebugDrawStep(sector, segs, addSeg0, null, outTris);
				_segSteps.Add(CopySegList(sector, segs));
			}
		}

		return true;
	}

	public static bool PointInTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2) {
		var s = p0.y * p2.x - p0.x * p2.y + (p2.y - p0.y) * p.x + (p0.x - p2.x) * p.y;
		var t = p0.x * p1.y - p0.y * p1.x + (p0.y - p1.y) * p.x + (p1.x - p0.x) * p.y;

		if ((s < 0.01f) != (t < 0.01f))
			return false;

		var A = -p1.y * p2.x + p0.y * (p2.x - p1.x) + p0.x * (p1.y - p2.y) + p1.x * p2.y;
		if (A < 0.0) {
			s = -s;
			t = -t;
			A = -A;
		}
		return s > -0.01f && t > -0.01f && (s + t) < A+0.01f;
	}

	static bool ContainsAnyVertices(LEV.Sector sector, Seg seg0, Seg seg1, Seg seg2) {

		Vector2 a = sector.vertices[seg0.ia];
		Vector2 b = sector.vertices[seg0.ib];
		Vector2 c;
		
		if ((seg1.ia != seg0.ia) && (seg1.ia != seg0.ib)) {
			c = sector.vertices[seg1.ia];
		} else {
			c = sector.vertices[seg1.ib];
		}

		for (int i = 0; i < sector.vertices.Count; ++i) {
			if ((seg0.ia != i) && (seg0.ib != i) &&
				(seg1.ia != i) && (seg1.ib != i) &&
				(seg2.ia != i) && (seg2.ib != i)) {

				Vector2 p = sector.vertices[i];
				if (PointInTriangle(p, a, b, c)) {
					return true;
				}
			}
		}

		return false;
	}

	static bool TrySegToVertex(LEV.Sector sector, LinkedList<Seg> segs, Seg seg, int index, List<int> counts, List<int> outTris, bool debugDraw) {

		// bad sector: inward facing wall
		if ((FindSeg(segs, seg.ia, index) != null) ||
			(FindSeg(segs, index, seg.ib) != null)) {
			return false;
		}

		// does this vertex cross any segs?
		Seg addSeg0 = new Seg(sector, seg.ia, index, -1);

		foreach (var testSeg in segs) {
			if (seg != testSeg) {
				if (addSeg0.Intersects(testSeg)) {
					return false;
				}
			}
		}

		Seg addSeg1 = new Seg(sector, index, seg.ib, -1);

		foreach (var testSeg in segs) {
			if (seg != testSeg) {
				if (addSeg1.Intersects(testSeg)) {
					return false;
				}
			}
		}

		if (ContainsAnyVertices(sector, seg, addSeg0, addSeg1)) {
			return false;
		}
		
		// seg + seg + vertex creates valid triangle
		outTris.Add(seg.ia);
		outTris.Add(seg.ib);
		outTris.Add(index);

		segs.AddFirst(addSeg0);
		segs.AddFirst(addSeg1);
		segs.Remove(seg);

		Incr(counts, index, 2);

		if (debugDraw) {
			DebugDrawStep(sector, segs, addSeg0, addSeg1, outTris);
			_segSteps.Add(CopySegList(sector, segs));
		}

		return true;
	}

	static int FindVertex(LEV.Sector sector, Seg seg, List<int> verts) {
		// find closest vertex in front of line
		float bestDist = float.MaxValue;
		int bestIndex = -1;

		foreach (var i in verts) {
			Vector2 p = sector.vertices[i];

			float d = seg.Distance(p);
			if (d > 0f) {

				float d0 = (p-seg.a).sqrMagnitude;
				float d1 = (p-seg.b).sqrMagnitude;

				d = Mathf.Min(d0, d1);

				if (d < bestDist) {
					bestIndex = i;
					bestDist = d;
				}
			}
		}

		return bestIndex;
	}
}
