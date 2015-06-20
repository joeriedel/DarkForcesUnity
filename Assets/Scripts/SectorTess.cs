using UnityEngine;
using System.Collections.Generic;

public class SectorTess {
	class Seg {
		public Vector2 A;
		public Vector2 B;
		public Vector2 N;
		public Vector2 LN;
		public float DA;
		public float DB;
		public float DBmDA; // DB - DA
		public float D;
		public int IdxA;
		public int IdxB;
		public int WallIdx;
		
		public Seg(LEV.Sector sector, int a, int b, int wallIdx) {
			IdxA = a;
			IdxB = b;
			WallIdx = wallIdx;

			A = sector.Vertices[a];
			B = sector.Vertices[b];

			LN = B-A;
			LN.Normalize();

			DA = Vector2.Dot(A, LN);
			DB = Vector2.Dot(B, LN);

			DBmDA = DB - DA;

			N = -LN;

			float t = N.x;
			N.x = -N.y;
			N.y = t;

			D = Vector2.Dot(this.A, N);
		}

		public void Flip() {
			Vector2 tv = A;
			A = B;
			B = A;
			N = -N;
			D = -D;
		}

		public float Distance(Vector2 p) {
			return Vector2.Dot(N, p) - D;
		}

		public bool InFront(Vector2 p) {
			return Distance(p) > 0.01f;
		}

		public bool InBack(Vector2 p) {
			return Distance(p) < -0.01f;
		}

		public bool Intersects(Seg other) {
			float d = Vector2.Dot(N, other.N);
			if (Mathf.Abs(d) > 0.9999) {
				return false; // parallel
			}

			float SA = Distance(other.A);
			float SB = Distance(other.B);

			if ((SA <= 0.01f) && (SB <= 0.01f)) {
				return false;
			}

			if ((SA >= -0.01f) && (SB >= -0.01f)) {
				return false;
			}

			Vector2 p = Vector2.Lerp(other.A, other.B, Mathf.Abs(SA) / ((Mathf.Abs(SA)+Mathf.Abs(SB))));

			return IsPointOnLine(p) && other.IsPointOnLine(p);
		}

		public bool IsPointOnLine(Vector2 p) {
			float pD = Vector2.Dot(LN, p);
			float y = pD - DA;

			if (DBmDA < 0f) {
				if ((y > -0.01f) || (y < DBmDA+0.01f)) {
					return false;
				}
			} else {
				if ((y < 0.01f) || (y > DBmDA-0.01f)) {
					return false;
				}
			}

			return true;
		}

		public bool SharesVertex(Seg other) {
			return (IdxA == other.IdxA) || (IdxB == other.IdxA) ||
				(IdxA == other.IdxB) || (IdxB == other.IdxB);
		}
	}

	static  bool CheckSegCounts(LinkedList<Seg> segs, List<int> counts) {
		foreach (var seg in segs) {
			if ((counts[seg.IdxA] < 2) || (counts[seg.IdxB] < 2)) {
				return false;
			}
		}
		return true;
	}

	// Returns list of triangle indices
	public static List<int> TesselateSector(LEV.Sector sector) {

		LinkedList<Seg> segs = new LinkedList<Seg>();
		List<int> verts = new List<int>();
		List<int> counts = new List<int>();

		for (int i = 0; i < sector.Vertices.Count; ++i) {
			verts.Add(i);
			counts.Add(0);
		}

		for (int i = 0; i < sector.Walls.Count; ++i) {
			LEV.Wall wall = sector.Walls[i];
			Incr(counts, wall.V0, 1);
			Incr(counts, wall.V1, 1);
			segs.AddLast(new Seg(sector, wall.V0, wall.V1, i));
		}

		int br = 0;

		List<int> tris = new List<int>();
		for (var node = segs.First; node != null;) {

			if (br == 45) {
				int b = 0;
			}

			List<int> validVerts = new List<int>();
			for (int i = 0; i < counts.Count; ++i) {
				if (counts[i] > 0) {
					DebugCheck.Assert(counts[i] > 1);
					validVerts.Add(i);
				}
			}

			verts = validVerts;

			node = MakeSegTri(sector, node, verts, counts, tris);
			++br;
		}

		if (segs.Count > 0) {
			Debug.LogWarning("Bad floor!");
		}

		//DebugCheck.Assert(segs.Count == 0);

		return tris;
	}

	static void Incr(List<int> counts, int index, int add) {
		counts[index] = counts[index] + add;
	}

	static LinkedListNode<Seg> MakeSegTri(LEV.Sector sector, LinkedListNode<Seg> node, List<int> verts, List<int> counts, List<int> outTris) {

		LinkedList<Seg> segs = node.List;

		if (node.Value.WallIdx == 3) {
			int b = 0;
		}

		// can we trivially make a triangle from our connected seg?
		LinkedListNode<Seg> connectedSeg = FindConnectedSeg(segs, node.Value, node.Value.IdxB);
		if (TrySegToSeg(sector, segs, node.Value, connectedSeg.Value, counts, outTris)) {
			return segs.First;
		}

		connectedSeg = FindConnectedSeg(segs, node.Value, node.Value.IdxA);
		if (TrySegToSeg(sector, segs, connectedSeg.Value, node.Value, counts, outTris)) {
			return segs.First;
		}

		int i;
		while ((i=FindVertex(sector, node.Value, verts)) != -1) {
			if (TrySegToVertex(sector, segs, node.Value, i, counts, outTris)) {
				return segs.First;
			} else {
				verts.Remove(i);
			}
		}

		Debug.LogWarning("MakeSegTri: bad floor");
		return null;
	}

	static LinkedListNode<Seg> FindConnectedSeg(LinkedList<Seg> segs, Seg seg, int vert) {
		for (var node = segs.First; node != null; node = node.Next) {
			Seg testSeg = node.Value;
			if ((testSeg != seg) && ((testSeg.IdxA == vert) || (testSeg.IdxB == vert))) {
				return node;
			}
		}

		return null;
	}

	static LinkedListNode<Seg> FindSeg(LinkedList<Seg> segs, int v0, int v1) {
		for (var node = segs.First; node != null; node = node.Next) {
			Seg testSeg = node.Value;
			if ((testSeg.IdxA == v0) && (testSeg.IdxB == v1)) {
				return node;
			}
		}

		return null;
	}

	static bool TrySegToSeg(LEV.Sector sector, LinkedList<Seg> segs, Seg seg0, Seg seg1, List<int> counts, List<int> outTris) {

		if (!seg0.InFront(seg1.B)) {
			// concave angle
			return false;
		}
				
		// does this vertex cross any segs?
		LinkedListNode<Seg> existingSeg = FindSeg(segs, seg1.IdxB, seg0.IdxA);
		Seg addSeg0;

		if (existingSeg == null) {
			if (FindSeg(segs, seg0.IdxA, seg1.IdxB) != null) {
				int b = 0;
			}
			addSeg0 = new Seg(sector, seg0.IdxA, seg1.IdxB, -1);
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
		outTris.Add(seg1.IdxA);
		outTris.Add(seg1.IdxB);
		outTris.Add(seg0.IdxA);

		segs.Remove(seg0);
		segs.Remove(seg1);

		if (existingSeg != null) {
			Incr(counts, seg0.IdxA, -2);
			Incr(counts, seg0.IdxB, -2);
			Incr(counts, seg1.IdxB, -2);
			segs.Remove(existingSeg);
			if (!CheckSegCounts(segs, counts)) {
				int b = 0;
			}
		} else {
			segs.AddLast(addSeg0);
			Incr(counts, seg0.IdxB, -2);
			if (!CheckSegCounts(segs, counts)) {
				int b = 0;
			}
		}

		return true;
	}

	public static bool PointInTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2) {
		var s = p0.y * p2.x - p0.x * p2.y + (p2.y - p0.y) * p.x + (p0.x - p2.x) * p.y;
		var t = p0.x * p1.y - p0.y * p1.x + (p0.y - p1.y) * p.x + (p1.x - p0.x) * p.y;

		if ((s < 0) != (t < 0))
			return false;

		var A = -p1.y * p2.x + p0.y * (p2.x - p1.x) + p0.x * (p1.y - p2.y) + p1.x * p2.y;
		if (A < 0.0) {
			s = -s;
			t = -t;
			A = -A;
		}
		return s > 0 && t > 0 && (s + t) < A;
	}

	static bool ContainsAnyVertices(LEV.Sector sector, Seg seg0, Seg seg1, Seg seg2) {

		Vector2 a = sector.Vertices[seg0.IdxA];
		Vector2 b = sector.Vertices[seg0.IdxB];
		Vector2 c;
		
		if ((seg1.IdxA != seg0.IdxA) && (seg1.IdxA != seg0.IdxB)) {
			c = sector.Vertices[seg1.IdxA];
		} else {
			c = sector.Vertices[seg1.IdxB];
		}

		for (int i = 0; i < sector.Vertices.Count; ++i) {
			if ((seg0.IdxA != i) && (seg0.IdxB != i) &&
				(seg1.IdxA != i) && (seg1.IdxB != i) &&
				(seg2.IdxA != i) && (seg2.IdxB != i)) {

				Vector2 p = sector.Vertices[i];
				if (PointInTriangle(p, a, b, c)) {
					return true;
				}
			}
		}

		return false;
	}

	static bool TrySegToVertex(LEV.Sector sector, LinkedList<Seg> segs, Seg seg, int index, List<int> counts, List<int> outTris) {
		// does this vertex cross any segs?
		Seg addSeg0 = new Seg(sector, seg.IdxA, index, -1);

		foreach (var testSeg in segs) {
			if (seg != testSeg) {
				if (addSeg0.Intersects(testSeg)) {
					return false;
				}
			}
		}

		Seg addSeg1 = new Seg(sector, index, seg.IdxB, -1);

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
		outTris.Add(seg.IdxA);
		outTris.Add(seg.IdxB);
		outTris.Add(index);
		
		segs.AddLast(addSeg0);
		segs.AddLast(addSeg1);
		segs.Remove(seg);

		Incr(counts, index, 2);

		if (!CheckSegCounts(segs, counts)) {
			int b = 0;
		}

		return true;
	}

	static int FindVertex(LEV.Sector sector, Seg seg, List<int> verts) {
		// find closest vertex in front of line
		float bestDist = float.MaxValue;
		int bestIndex = -1;

		foreach (var i in verts) {
			Vector2 p = sector.Vertices[i];

			float d = seg.Distance(p);
			if (d > 0f) {

				float d0 = (p-seg.A).sqrMagnitude;
				float d1 = (p-seg.B).sqrMagnitude;

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
