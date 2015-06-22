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
	PAL _pal;
	CMP _cmp;
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
		_lev = Asset.LoadCached<LEV>(LEVname + ".LEV");
		_pal = Asset.LoadCached<PAL>(_lev.Palette.ToUpper());
		_cmp = Asset.LoadCached<CMP>(LEVname + ".CMP");

		_game.SolidCMP.SetTexture("_PAL", _pal.Texture);
		_game.SolidCMP.SetTexture("_CMP", _cmp.Texture);
		
		BM.CreateArgs bmCreateArgs = new BM.CreateArgs();
		bmCreateArgs.TextureFormat = TextureFormat.Alpha8;
		bmCreateArgs.AnisoLevel = 0;
		bmCreateArgs.FilterMode = FilterMode.Point;
		bmCreateArgs.bMipmap = false;
			
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

	private void GenerateSectors() {
		for (int i = 0; i < _lev.Sectors.Count; ++i) {
			GenerateSector(i);
		}
		//GenerateSector(172);
		//GenerateSector(36);
		//GenerateSector(5, true);
		//GenerateSector(192, true);
	}

	private bool CheckSector(LEV.Sector sector) {
		// all vertices in sector should be shared by at least 2 walls.
		List<int> count = new List<int>(sector.Vertices.Count);
		for (int i = 0; i < sector.Vertices.Count; ++i) {
			count.Add(0);
		}

		foreach (var wall in sector.Walls) {
			count[wall.V0] = count[wall.V0] + 1;
			count[wall.V1] = count[wall.V1] + 1;
		}

		for (int i = 0; i < sector.Vertices.Count; ++i) {
			if (count[i] < 2) {
				return false;
			}
		}

		return true;
	}

	private void GenerateSector(int sectorIndex, bool debugDraw = false) {
		//Debug.Log("Generating sector " + sectorIndex);

		Sector sector = new Sector();
		sector.LEVSector = _lev.Sectors[sectorIndex];

		if (!CheckSector(sector.LEVSector)) {
			Debug.Log("Sector " + sectorIndex + " is bad, removing. ");
			return;
		}

		_sectors.Add(sector);

		sector.GO = new GameObject("Sector" + sectorIndex, typeof(MeshFilter), typeof(MeshRenderer));
		sector.GO.transform.parent = _sectorsGO.transform;

		sector.Mesh = new Mesh();

		sector.MeshFilter = sector.GO.GetComponent<MeshFilter>();
		sector.MeshRenderer = sector.GO.GetComponent<MeshRenderer>();

		sector.MeshFilter.mesh = sector.Mesh;

		List<List<int>> meshTris = new List<List<int>>();

		int numWalls = 0;
		int numFloors = 0;

		if ((sector.LEVSector.Flags0 & LEV.Sector.EFlags0.NoWalls) == LEV.Sector.EFlags0.None) {
			numWalls = sector.LEVSector.Walls.Count;
		}

		bool hasFloor = ((sector.LEVSector.Flags0 & LEV.Sector.EFlags0.SkyFloor) == LEV.Sector.EFlags0.None) && (sector.LEVSector.FloorTex != -1);
		bool hasCeil = ((sector.LEVSector.Flags0 & LEV.Sector.EFlags0.SkyCeil) == LEV.Sector.EFlags0.None) && (sector.LEVSector.CeilTex != -1);

		//hasFloor = false;
		//hasCeil = false;

		if (hasFloor) {
			++numFloors;
		}

		if (hasCeil) {
			++numFloors;
		}

		if ((numFloors == 0) && (numWalls == 0)) {
			return;
		}

		sector.Vertices = new Vector3[(sector.LEVSector.Vertices.Count*numFloors) + (numWalls * 4 * 3)];
		sector.UVs = new Vector2[sector.Vertices.Length];
		sector.Materials = new Material[numFloors + (numWalls * 3)];

		// add floor / cieling verts
		int ceilVertOfs = 0;

		if (hasFloor) {
			for (int i = 0; i < sector.LEVSector.Vertices.Count; ++i) {
				Vector2 v2 = sector.LEVSector.Vertices[i];
				sector.Vertices[i] = new Vector3(v2.x, sector.LEVSector.FloorAlt, v2.y);
			}
			ceilVertOfs = sector.LEVSector.Vertices.Count;
		}

		if (hasCeil) {
			for (int i = 0; i < sector.LEVSector.Vertices.Count; ++i) {
				Vector2 v2 = sector.LEVSector.Vertices[i];
				sector.Vertices[i + ceilVertOfs] = new Vector3(v2.x, sector.LEVSector.CeilAlt, v2.y);
			}
		}

		if (hasFloor || hasCeil) {
			List<int> floorTris = new List<int>();
			List<int> ceilTris = new List<int>();
			GenerateSectorFloorsAndCeilings(sector.LEVSector, sectorIndex, hasFloor, hasCeil, ref floorTris, ref ceilTris, sector.UVs, sector.Materials, debugDraw);

			if (hasFloor) {
				meshTris.Add(floorTris);
			}

			if (hasCeil) {
				meshTris.Add(ceilTris);
			}
		}

		// assume every wall has an adjoin with top/bottom quads
		for (int i = 0; i < numWalls; ++i) {
			int baseIndex = (sector.LEVSector.Vertices.Count*numFloors) + i * 12;
			List<int> top = new List<int>();
			GenerateQuadTris(baseIndex, top);
			List<int> mid = new List<int>();
			GenerateQuadTris(baseIndex + 4, mid);
			List<int> bottom = new List<int>();
			GenerateQuadTris(baseIndex + 8, bottom);

			meshTris.Add(top);
			meshTris.Add(mid);
			meshTris.Add(bottom);

			MakeSectorWall(sector, i, numFloors + (i*3), baseIndex);
		}

		sector.Mesh.vertices = sector.Vertices;
		sector.Mesh.uv = sector.UVs;
		sector.Mesh.subMeshCount = meshTris.Count;
		sector.MeshRenderer.materials = sector.Materials;

		for (int i = 0; i < meshTris.Count; ++i) {
			var m = meshTris[i];
			sector.Mesh.SetTriangles(m.ToArray(), i);
		}
	}

	private void MakeSectorWall(Sector sector, int wallIndex, int materialIndex, int baseVertexOfs) {
		
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

		Material matTop = new Material(_game.SolidCMP);
		Material matMid = new Material(_game.SolidCMP);
		Material matBottom = new Material(_game.SolidCMP);

		float lightLevel = sector.LEVSector.Ambient + levWall.Light;

		matTop.SetFloat("_LightLevel", lightLevel);
		matMid.SetFloat("_LightLevel", lightLevel);
		matBottom.SetFloat("_LightLevel", lightLevel);

		matTop.mainTexture = wall.Top.Frames[0].Texture;
		matMid.mainTexture = wall.Mid.Frames[0].Texture;
		matBottom.mainTexture = wall.Bottom.Frames[0].Texture;

		sector.Materials[materialIndex + 0] = matTop;
		sector.Materials[materialIndex + 1] = matMid;
		sector.Materials[materialIndex + 2] = matBottom;

		UpdateSectorWall(sector, wallIndex, baseVertexOfs);
	}

	private void UpdateSectorWall(Sector sector, int wallIndex, int baseVertexOfs) {
		LEV.Sector adjoin = null;
		LEV.Wall LEVWall = sector.LEVSector.Walls[wallIndex];
		Wall wall = sector.Walls[wallIndex];

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

	private void GenerateSectorFloorsAndCeilings(LEV.Sector sector, int sectorIndex, bool hasFloor, bool hasCeil, ref List<int> outFloorTris, ref List<int> outCeilTris, Vector2[] outUVs, Material[] outMats, bool debugDraw) {
		List<int> tess = SectorTess.TesselateSector(sector, sectorIndex, debugDraw);

		int vertOfs = 0;
		int matOfs = 0;

		if (hasFloor) {
			outFloorTris = tess;
			BM bm = _textures[sector.FloorTex] ?? _defaultTexture;
			outMats[0] = new Material(_game.SolidCMP);
			outMats[0].mainTexture = bm.Frames[0].Texture;
			outMats[0].SetFloat("_LightLevel", sector.Ambient);
			UpdateFloorUVs(sector, bm, sector.FloorShiftX, sector.FloorShiftZ, vertOfs, outUVs);
			vertOfs += sector.Vertices.Count;
			++matOfs;
		}

		if (hasCeil) {
			outCeilTris = new List<int>(tess.Count);
			for (int i = 0; i < tess.Count; i += 3) {
				outCeilTris.Add(tess[i+2] + vertOfs);
				outCeilTris.Add(tess[i+1] + vertOfs);
				outCeilTris.Add(tess[i] + vertOfs);
			}

			BM bm = _textures[sector.CeilTex] ?? _defaultTexture;
			outMats[matOfs] = new Material(_game.SolidCMP);
			outMats[matOfs].mainTexture = bm.Frames[0].Texture;
			outMats[matOfs].SetFloat("_LightLevel", sector.Ambient);
			UpdateFloorUVs(sector, bm, sector.CeilShiftX, sector.CeilShiftZ, vertOfs, outUVs);
		}
	}

	private void UpdateFloorUVs(LEV.Sector sector, BM bm, float shiftX, float shiftY, int ofs, Vector2[] outUVs) {
		BM.Frame frame = bm.Frames[0];
		float w = frame.Texture.width;
		float h = frame.Texture.height;
		float rw = frame.WRecip;
		float rh = frame.HRecip;

		for (int i = 0; i < sector.Vertices.Count; ++i) {
			Vector2 v = sector.Vertices[i];
			float s = -(v.x-shiftX)*8f;
			float t = -(v.y-shiftY)*8f;

			outUVs[ofs + i] = new Vector2(s*rw, 1f-(t*rh));
		}
	}

	public void Dispose() {
		foreach (var bm in _textures) {
			bm.Dispose();
		}
		_textures = null;

		_lev.Dispose();
		_pal.Dispose();
		_cmp.Dispose();

		GameObject.Destroy(_sectorsGO);

		System.GC.SuppressFinalize(this);
	}

}
