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
		public LEV.Sector levSector;
		public GameObject go;
		public Mesh mesh;
		public MeshFilter meshFilter;
		public MeshRenderer meshRenderer;
		public Vector3[] vertices;
		public Vector2[] uvs;
		public Material[] materials;
		public List<Wall> walls = new List<Wall>();

		public void Destroy() {
			if (materials != null) {
				for (int i = 0; i < materials.Length; ++i) {
					GameObject.Destroy(materials[i]);
				}
			}
			if (go != null) {
				GameObject.Destroy(go);
			}
		}
	};
	
	public World(Game game) {
		_game = game;
		_sectorsGO = new GameObject("Sectors");
	}

	public void Load(string LEVname) {
		_lev = Asset.LoadCached<LEV>(LEVname + ".LEV");
		_pal = Asset.LoadCached<PAL>(_lev.Palette.ToUpper());
		_cmp = Asset.LoadCached<CMP>(LEVname + ".CMP");

		_game.SolidCMP.SetTexture("_PAL", _pal.texture);
		_game.SolidCMP.SetTexture("_CMP", _cmp.texture);
		
		BM.CreateArgs bmCreateArgs = new BM.CreateArgs();
		bmCreateArgs.pal = _pal;

		if (_game.EmulateCMPShading) {
			bmCreateArgs.textureFormat = TextureFormat.Alpha8;
			bmCreateArgs.anisoLevel = 0;
			bmCreateArgs.filterMode = FilterMode.Point;
			bmCreateArgs.bMipmap = false;
		} else {
			bmCreateArgs.textureFormat = TextureFormat.RGBA32;
			bmCreateArgs.anisoLevel = 9;
			bmCreateArgs.filterMode = FilterMode.Trilinear;
			bmCreateArgs.bMipmap = true;
		}

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
		List<int> count = new List<int>(sector.vertices.Count);
		for (int i = 0; i < sector.vertices.Count; ++i) {
			count.Add(0);
		}

		foreach (var wall in sector.walls) {
			count[wall.v0] = count[wall.v0] + 1;
			count[wall.v1] = count[wall.v1] + 1;
		}

		for (int i = 0; i < sector.vertices.Count; ++i) {
			if (count[i] < 2) {
				return false;
			}
		}

		return true;
	}

	private void GenerateSector(int sectorIndex, bool debugDraw = false) {
		//Debug.Log("Generating sector " + sectorIndex);

		Sector sector = new Sector();
		sector.levSector = _lev.Sectors[sectorIndex];

		if (!CheckSector(sector.levSector)) {
			Debug.Log("Sector " + sectorIndex + " is bad, removing. ");
			return;
		}

		_sectors.Add(sector);

		sector.go = new GameObject("Sector" + sectorIndex, typeof(MeshFilter), typeof(MeshRenderer));
		sector.go.transform.parent = _sectorsGO.transform;

		sector.mesh = new Mesh();

		sector.meshFilter = sector.go.GetComponent<MeshFilter>();
		sector.meshRenderer = sector.go.GetComponent<MeshRenderer>();

		sector.meshFilter.mesh = sector.mesh;

		List<List<int>> meshTris = new List<List<int>>();

		int numWalls = 0;
		int numFloors = 0;

		if ((sector.levSector.flags0 & LEV.Sector.EFlags0.NoWalls) == 0) {
			numWalls = sector.levSector.walls.Count;
		}

		bool hasFloor = ((sector.levSector.flags0 & LEV.Sector.EFlags0.SkyFloor) == 0) && (sector.levSector.floorTex != -1);
		bool hasCeil = ((sector.levSector.flags0 & LEV.Sector.EFlags0.SkyCeil) == 0) && (sector.levSector.ceilTex != -1);

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

		sector.vertices = new Vector3[(sector.levSector.vertices.Count*numFloors) + (numWalls * 4 * 3)];
		sector.uvs = new Vector2[sector.vertices.Length];
		sector.materials = new Material[numFloors + (numWalls * 3)];

		// add floor / ceiling verts
		int ceilVertOfs = 0;

		if (hasFloor) {
			for (int i = 0; i < sector.levSector.vertices.Count; ++i) {
				Vector2 v2 = sector.levSector.vertices[i];
				sector.vertices[i] = new Vector3(v2.x, sector.levSector.floorAlt, v2.y);
			}
			ceilVertOfs = sector.levSector.vertices.Count;
		}

		if (hasCeil) {
			for (int i = 0; i < sector.levSector.vertices.Count; ++i) {
				Vector2 v2 = sector.levSector.vertices[i];
				sector.vertices[i + ceilVertOfs] = new Vector3(v2.x, sector.levSector.ceilAlt, v2.y);
			}
		}

		if (hasFloor || hasCeil) {
			List<int> floorTris = new List<int>();
			List<int> ceilTris = new List<int>();
			GenerateSectorFloorsAndCeilings(sector.levSector, sectorIndex, hasFloor, hasCeil, ref floorTris, ref ceilTris, sector.uvs, sector.materials, debugDraw);

			if (hasFloor) {
				meshTris.Add(floorTris);
			}

			if (hasCeil) {
				meshTris.Add(ceilTris);
			}
		}

		// assume every wall has an adjoin with top/bottom quads
		for (int i = 0; i < numWalls; ++i) {
			int baseIndex = (sector.levSector.vertices.Count*numFloors) + i * 12;
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

		sector.mesh.vertices = sector.vertices;
		sector.mesh.uv = sector.uvs;
		sector.mesh.subMeshCount = meshTris.Count;
		sector.meshRenderer.materials = sector.materials;

		for (int i = 0; i < meshTris.Count; ++i) {
			var m = meshTris[i];
			sector.mesh.SetTriangles(m.ToArray(), i);
		}
	}

	private void MakeSectorWall(Sector sector, int wallIndex, int materialIndex, int baseVertexOfs) {
		
		LEV.Wall levWall = sector.levSector.walls[wallIndex];
		Wall wall = new Wall();
		wall.LEVWall = levWall;

		if (levWall.texTop.texture != -1) {
			wall.Top = _textures[levWall.texTop.texture];
		}

		if (levWall.texMid.texture != -1) {
			if ((levWall.adjoin == -1) || ((levWall.flags0 & LEV.Wall.EFlags0.AlwaysDrawMid) != 0)) {
				wall.Mid = _textures[levWall.texMid.texture];
			}
		}

		if (levWall.texBottom.texture != -1) {
			wall.Bottom = _textures[levWall.texBottom.texture];
		}

		wall.Top = wall.Top ?? _defaultTexture;
		wall.Mid = wall.Mid ?? _defaultTexture;
		wall.Bottom = wall.Bottom ?? _defaultTexture;

		sector.walls.Add(wall);

		Material matTop = new Material(_game.EmulateCMPShading ? _game.SolidCMP : _game.Solid);
		Material matMid = new Material(_game.EmulateCMPShading ? _game.SolidCMP : _game.Solid);
		Material matBottom = new Material(_game.EmulateCMPShading ? _game.SolidCMP : _game.Solid);

		float lightLevel = sector.levSector.ambient + levWall.light;

		if (_game.EmulateCMPShading) {
			matTop.SetFloat("_LightLevel", lightLevel);
			matMid.SetFloat("_LightLevel", lightLevel);
			matBottom.SetFloat("_LightLevel", lightLevel);
		}

		matTop.mainTexture = wall.Top.Frames[0].Texture;
		matMid.mainTexture = wall.Mid.Frames[0].Texture;
		matBottom.mainTexture = wall.Bottom.Frames[0].Texture;

		sector.materials[materialIndex + 0] = matTop;
		sector.materials[materialIndex + 1] = matMid;
		sector.materials[materialIndex + 2] = matBottom;

		UpdateSectorWall(sector, wallIndex, baseVertexOfs);
	}

	private void UpdateSectorWall(Sector sector, int wallIndex, int baseVertexOfs) {
		LEV.Sector adjoin = null;
		LEV.Wall levWall = sector.levSector.walls[wallIndex];
		Wall wall = sector.walls[wallIndex];

		if (levWall.adjoin != -1) {
			adjoin = _lev.Sectors[levWall.adjoin];
		}

		float secondAlt = sector.levSector.ceilAlt;

		// top

		if ((adjoin != null) && ((sector.levSector.ceilAlt > adjoin.ceilAlt) && ((adjoin.flags0 & LEV.Sector.EFlags0.SkyCeil) == 0))) {
			secondAlt = adjoin.ceilAlt;
		}

		UpdateWallQuad(
			sector,
			wall.Top,
			baseVertexOfs,
			sector.levSector.vertices[levWall.v0],
			sector.levSector.vertices[levWall.v1],
			sector.levSector.ceilAlt,
			secondAlt,
			false,
			levWall.texTop.shiftX*8,
			-levWall.texTop.shiftY*8,
			false
		);

		// mid

		if ((adjoin == null) || ((levWall.flags0 & LEV.Wall.EFlags0.AlwaysDrawMid) != 0)) {
			secondAlt = sector.levSector.floorAlt;
		} else if ((adjoin.flags0 & LEV.Sector.EFlags0.SkyFloor) == 0) {
			secondAlt = sector.levSector.ceilAlt;
		}

		UpdateWallQuad(
			sector,
			wall.Mid,
			baseVertexOfs + 4,
			sector.levSector.vertices[levWall.v0],
			sector.levSector.vertices[levWall.v1],
			sector.levSector.ceilAlt,
			secondAlt,
			!((levWall.flags0 & LEV.Wall.EFlags0.TexAnchor) != 0),
			levWall.texMid.shiftX*8,
			-levWall.texMid.shiftY*8,
			(levWall.flags0 & LEV.Wall.EFlags0.FlipHorz) != 0
		);

		// bottom

		secondAlt = sector.levSector.floorAlt;

		if ((adjoin != null) && (sector.levSector.floorAlt < adjoin.floorAlt)) {
			secondAlt = adjoin.floorAlt;
		}

		UpdateWallQuad(
			sector,
			wall.Bottom,
			baseVertexOfs + 8, 
			sector.levSector.vertices[levWall.v0],
			sector.levSector.vertices[levWall.v1],
			secondAlt,
			sector.levSector.floorAlt,
			true,
			levWall.texBottom.shiftX*8,
		    -levWall.texBottom.shiftY*8,
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

		sector.vertices[vertexOfs + 0] = v0;
		sector.vertices[vertexOfs + 1] = v1;
		sector.vertices[vertexOfs + 2] = v2;
		sector.vertices[vertexOfs + 3] = v3;

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

		sector.uvs[vertexOfs + 0] = uv0;
		sector.uvs[vertexOfs + 1] = uv1;
		sector.uvs[vertexOfs + 2] = uv2;
		sector.uvs[vertexOfs + 3] = uv3;
	}

	private void GenerateSectorFloorsAndCeilings(LEV.Sector sector, int sectorIndex, bool hasFloor, bool hasCeil, ref List<int> outFloorTris, ref List<int> outCeilTris, Vector2[] outUVs, Material[] outMats, bool debugDraw) {
		List<int> tess = SectorTess.TesselateSector(sector, sectorIndex, debugDraw);

		int vertOfs = 0;
		int matOfs = 0;

		if (hasFloor) {
			outFloorTris = tess;
			BM bm = _textures[sector.floorTex] ?? _defaultTexture;
			outMats[0] = new Material(_game.EmulateCMPShading ? _game.SolidCMP : _game.Solid);
			outMats[0].mainTexture = bm.Frames[0].Texture;
			if (_game.EmulateCMPShading) {
				outMats[0].SetFloat("_LightLevel", sector.ambient);
			}
			UpdateFloorUVs(sector, bm, sector.floorShiftX, sector.floorShiftZ, vertOfs, outUVs);
			vertOfs += sector.vertices.Count;
			++matOfs;
		}

		if (hasCeil) {
			outCeilTris = new List<int>(tess.Count);
			for (int i = 0; i < tess.Count; i += 3) {
				outCeilTris.Add(tess[i+2] + vertOfs);
				outCeilTris.Add(tess[i+1] + vertOfs);
				outCeilTris.Add(tess[i] + vertOfs);
			}

			BM bm = _textures[sector.ceilTex] ?? _defaultTexture;
			outMats[matOfs] = new Material(_game.EmulateCMPShading ? _game.SolidCMP : _game.Solid);
			outMats[matOfs].mainTexture = bm.Frames[0].Texture;
			if (_game.EmulateCMPShading) {
				outMats[matOfs].SetFloat("_LightLevel", sector.ambient);
			}
			UpdateFloorUVs(sector, bm, sector.ceilShiftX, sector.ceilShiftZ, vertOfs, outUVs);
		}
	}

	private void UpdateFloorUVs(LEV.Sector sector, BM bm, float shiftX, float shiftY, int ofs, Vector2[] outUVs) {
		BM.Frame frame = bm.Frames[0];
		float w = frame.Texture.width;
		float h = frame.Texture.height;
		float rw = frame.WRecip;
		float rh = frame.HRecip;

		for (int i = 0; i < sector.vertices.Count; ++i) {
			Vector2 v = sector.vertices[i];
			float s = -(v.x-shiftX)*8f;
			float t = -(v.y-shiftY)*8f;

			outUVs[ofs + i] = new Vector2(s*rw, 1f-(t*rh));
		}
	}

	public void Dispose() {
		foreach (var bm in _textures) {
			bm.Dispose();
		}
		foreach (var s in _sectors) {
			s.Destroy();
		}

		_textures = null;

		_lev.Dispose();
		_pal.Dispose();
		_cmp.Dispose();

		GameObject.Destroy(_sectorsGO);

		System.GC.SuppressFinalize(this);
	}

}
