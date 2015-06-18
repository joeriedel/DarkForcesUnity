/* World.cs
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

public class World : System.IDisposable {

	Game _game;
	GameObject _sectorsGO;
	BM _defaultTexture;
	List<BM> _textures = new List<BM>();
	LEV _lev;
	List<Sector> _sectors = new List<Sector>();

	class Wall {
		public BM Top, Mid, Bottom, Sign;
		public LEV.Wall LEVWall;
	};

	class Sector {
		public LEV.Sector LEVSector;
		public GameObject GO;
		public Mesh Mesh;
		public MeshFilter MeshFilter;
		public MeshRenderer MeshRenderer;
		public Vector3[] Vertices;
		public Vector2[] UVs;
		public Material[] Materials;
		public List<Wall> Walls = new List<Wall>();
	};
	
	public World(Game game) {
		_game = game;
		_sectorsGO = new GameObject("Sectors");
	}

	public void Load(string LEVname) {
		_lev = Asset.LoadCached<LEV>(LEVname);
		using (PAL pal = Asset.LoadCached<PAL>(_lev.Palette.ToUpper())) {

			BM.CreateArgs bmCreateArgs = new BM.CreateArgs();
			bmCreateArgs.Pal = pal;
			
			foreach (var texName in _lev.Textures) {
				try {
					BM bm = Asset.LoadCached<BM>(texName.ToUpper(), bmCreateArgs);
					_textures.Add(bm);
					if (_defaultTexture == null) {
						_defaultTexture = bm;
					}
				} catch (System.IO.FileNotFoundException) {
					_textures.Add(null);
				}
			}

			GenerateSectors();
		}
	}

	private void GenerateSectors() {
		for (int i = 0; i < _lev.Sectors.Count; ++i) {
			GenerateSector(i);
		}
	}

	private void GenerateSector(int sectorIndex) {
		Sector sector = new Sector();
		_sectors.Add(sector);

		sector.LEVSector = _lev.Sectors[sectorIndex];

		sector.GO = new GameObject("Sector" + sectorIndex, typeof(MeshFilter), typeof(MeshRenderer));
		sector.GO.transform.parent = _sectorsGO.transform;

		sector.Mesh = new Mesh();

		sector.MeshFilter = sector.GO.GetComponent<MeshFilter>();
		sector.MeshRenderer = sector.GO.GetComponent<MeshRenderer>();

		sector.MeshFilter.mesh = sector.Mesh;

		List<List<int>> meshTris = new List<List<int>>();

		sector.Vertices = new Vector3[sector.LEVSector.Walls.Count * 4 * 3];
		sector.UVs = new Vector2[sector.Vertices.Length];
		sector.Materials = new Material[sector.LEVSector.Walls.Count * 3];

		// assume every wall has an adjoin with top/bottom quads
		for (int i = 0; i < sector.LEVSector.Walls.Count; ++i) {
			int baseIndex = i * 12;
			List<int> top = new List<int>();
			GenerateQuadTris(baseIndex, top);
			List<int> mid = new List<int>();
			GenerateQuadTris(baseIndex + 4, mid);
			List<int> bottom = new List<int>();
			GenerateQuadTris(baseIndex + 8, bottom);

			meshTris.Add(top);
			meshTris.Add(mid);
			meshTris.Add(bottom);

			MakeSectorWall(sector, i);
		}

		sector.Mesh.vertices = sector.Vertices;
		sector.Mesh.uv = sector.UVs;
		sector.Mesh.subMeshCount = meshTris.Count;
		sector.MeshRenderer.materials = sector.Materials;

		for (int i = 0; i < meshTris.Count; ++i) {
			var m = meshTris[i];
			sector.Mesh.SetTriangles(m.ToArray(), i);
		}

		//GenerateSectorFloorsAndCeilings(lev, sector, );
	}

	private void MakeSectorWall(Sector sector, int wallIndex) {
		
		LEV.Wall levWall = sector.LEVSector.Walls[wallIndex];
		Wall wall = new Wall();
		wall.LEVWall = levWall;

		if (levWall.TexTop.Texture != -1) {
			wall.Top = _textures[levWall.TexTop.Texture];
		}

		if (levWall.TexMid.Texture != -1) {
			if ((levWall.Adjoin == -1) || ((levWall.Flags0 & LEV.Wall.EFlags0.AlwaysDrawMid) != LEV.Wall.EFlags0.None)) {
				wall.Mid = _textures[levWall.TexMid.Texture];
			}
		}

		if (levWall.TexBottom.Texture != -1) {
			wall.Bottom = _textures[levWall.TexBottom.Texture];
		}

		wall.Top = wall.Top ?? _defaultTexture;
		wall.Mid = wall.Mid ?? _defaultTexture;
		wall.Bottom = wall.Bottom ?? _defaultTexture;

		sector.Walls.Add(wall);

		int materialIndex = wallIndex * 3;

		Material matTop = new Material(_game.WallSolid);
		Material matMid = new Material(_game.WallSolid);
		Material matBottom = new Material(_game.WallSolid);

		matTop.mainTexture = wall.Top.Frames[0].Texture;
		matMid.mainTexture = wall.Mid.Frames[0].Texture;
		matBottom.mainTexture = wall.Bottom.Frames[0].Texture;

		sector.Materials[materialIndex + 0] = matTop;
		sector.Materials[materialIndex + 1] = matMid;
		sector.Materials[materialIndex + 2] = matBottom;

		UpdateSectorWall(sector, wallIndex);
	}

	private void UpdateSectorWall(Sector sector, int wallIndex) {
		LEV.Sector adjoin = null;
		LEV.Wall LEVWall = sector.LEVSector.Walls[wallIndex];
		Wall wall = sector.Walls[wallIndex];

		int baseVertexOfs = wallIndex*12;

		if (LEVWall.Adjoin != -1) {
			adjoin = _lev.Sectors[LEVWall.Adjoin];
		}

		float secondAlt = sector.LEVSector.CeilAlt;

		// top

		if ((adjoin != null) && (sector.LEVSector.CeilAlt > adjoin.CeilAlt)) {
			secondAlt = adjoin.CeilAlt;
		}

		UpdateWallQuad(
			sector,
			wall.Top,
			baseVertexOfs,
			sector.LEVSector.Vertices[LEVWall.V0],
			sector.LEVSector.Vertices[LEVWall.V1],
			sector.LEVSector.CeilAlt,
			secondAlt,
			false,
			LEVWall.TexTop.ShiftX*8,
			-LEVWall.TexTop.ShiftY*8,
			false
		);

		// mid

		if ((adjoin == null) || ((LEVWall.Flags0 & LEV.Wall.EFlags0.AlwaysDrawMid) != LEV.Wall.EFlags0.None)) {
			secondAlt = sector.LEVSector.FloorAlt;
		} else {
			secondAlt = sector.LEVSector.CeilAlt;
		}

		UpdateWallQuad(
			sector,
			wall.Mid,
			baseVertexOfs + 4,
			sector.LEVSector.Vertices[LEVWall.V0],
			sector.LEVSector.Vertices[LEVWall.V1],
			sector.LEVSector.CeilAlt,
			secondAlt,
			!((LEVWall.Flags0 & LEV.Wall.EFlags0.TexAnchor) != LEV.Wall.EFlags0.None),
			LEVWall.TexMid.ShiftX*8,
			-LEVWall.TexMid.ShiftY*8,
			(LEVWall.Flags0 & LEV.Wall.EFlags0.FlipHorz) != LEV.Wall.EFlags0.None
		);

		// bottom

		secondAlt = sector.LEVSector.FloorAlt;

		if ((adjoin != null) && (sector.LEVSector.FloorAlt < adjoin.FloorAlt)) {
			secondAlt = adjoin.FloorAlt;
		}

		UpdateWallQuad(
			sector,
			wall.Bottom,
			baseVertexOfs + 8,
			sector.LEVSector.Vertices[LEVWall.V0],
			sector.LEVSector.Vertices[LEVWall.V1],
			secondAlt,
			sector.LEVSector.FloorAlt,
			true,
			LEVWall.TexBottom.ShiftX*8,
		    -LEVWall.TexBottom.ShiftY*8,
			false
		);
	}

	private void GenerateQuadTris(int baseIndex, List<int> outTris) {
		outTris.Add(baseIndex + 0);
		outTris.Add(baseIndex + 1);
		outTris.Add(baseIndex + 3);
		outTris.Add(baseIndex + 2);
		outTris.Add(baseIndex + 3);
		outTris.Add(baseIndex + 1);
	}

	private void UpdateWallQuad(Sector sector, BM bm, int vertexOfs, Vector2 sv0, Vector2 sv1, float top, float bottom, bool pegBottom, float txShiftX, float txShiftY, bool flip) {
		Vector3 v0 = new Vector3(sv0.x, top, sv0.y);
		Vector3 v1 = new Vector3(sv1.x, top, sv1.y);
		Vector3 v2 = new Vector3(sv1.x, bottom, sv1.y);
		Vector3 v3 = new Vector3(sv0.x, bottom, sv0.y);

		sector.Vertices[vertexOfs + 0] = v0;
		sector.Vertices[vertexOfs + 1] = v1;
		sector.Vertices[vertexOfs + 2] = v2;
		sector.Vertices[vertexOfs + 3] = v3;

		float length = ((sv1 != sv0) ? (sv1 - sv0).magnitude : 0f) * 8f;
		float height = Mathf.Abs(bottom-top)*8;

		//txShiftX += sv0.x % (float)(bm.Frames[0].Texture.width);

		float uvLeft = txShiftX*bm.Frames[0].WRecip;
		float uvRight = (txShiftX + length)*bm.Frames[0].WRecip;

		if (flip) {
			uvLeft = -uvLeft;
			uvRight = -uvRight;
		}

		float txTop = (txShiftY * bm.Frames[0].HRecip);

		if (pegBottom) {
			txTop += (bm.Frames[0].Texture.height - height) * bm.Frames[0].HRecip;
		}

		float uvTop = txTop;
		float uvBottom = txTop + (height * bm.Frames[0].HRecip);

		Vector2 uv0 = new Vector2(uvLeft, 1f-uvTop);
		Vector2 uv1 = new Vector2(uvRight, 1f-uvTop);
		Vector2 uv2 = new Vector2(uvRight, 1f-uvBottom);
		Vector2 uv3 = new Vector2(uvLeft, 1f-uvBottom);

		sector.UVs[vertexOfs + 0] = uv0;
		sector.UVs[vertexOfs + 1] = uv1;
		sector.UVs[vertexOfs + 2] = uv2;
		sector.UVs[vertexOfs + 3] = uv3;
	}

	private void GenerateSectorFloorsAndCeilings(LEV.Sector sector, Vector3[] verts, List<int> outTris, List<Vector3> outVerts, List<Vector2> outUVs, List<Material> outMats) {
		/*
		List<Poly2Tri.TriangulationPoint> tessVerts = new List<Poly2Tri.TriangulationPoint>(sector.Vertices.Count);
		for (int i = 0; i < sector.Vertices.Count; ++i) {
			tessVerts[i].X = sector.Vertices[i].x;
			tessVerts[i].Y = sector.Vertices[i].y;
		}

		List<Poly2Tri.TriangulationConstraint> tessConstraint = new List<Poly2Tri.TriangulationConstraint>(sector.Walls.Count);
		for (int i = 0; i < sector.Walls.Count; ++i) {
			tessConstraint[i] = new Poly2Tri.TriangulationConstraint(
				tessVerts[sector.Walls[i].V0],
				tessVerts[sector.Walls[i].V1]
			);
		}

		Poly2Tri.P2T.Triangulate(new Poly2Tri.ConstrainedPointSet(tessVerts, tessConstraint));
		*/

	}

	public void Dispose() {
		foreach (var bm in _textures) {
			bm.Dispose();
		}
		_textures = null;

		_lev.Dispose();

		GameObject.Destroy(_sectorsGO);

		System.GC.SuppressFinalize(this);
	}

}
