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
	List<BM> _textures = new List<BM>();
	
	public World(Game game) {
		_game = game;
		_sectorsGO = new GameObject("Sectors");
	}

	public void Load(string LEVname) {
		using (LEV lev = Asset.LoadCached<LEV>(LEVname)) {
			using (PAL pal = Asset.LoadCached<PAL>(lev.Palette.ToUpper())) {

				BM.CreateArgs bmCreateArgs = new BM.CreateArgs();
				bmCreateArgs.Pal = pal;

				foreach (var texName in lev.Textures) {
					try {
						_textures.Add(Asset.LoadCached<BM>(texName.ToUpper(), bmCreateArgs));
					} catch (System.IO.FileNotFoundException e) {
						_textures.Add(null);
					}
				}

				GenerateSectors(lev);
			}
		}
	}

	private void GenerateSectors(LEV lev) {
		for (int i = 0; i < lev.Sectors.Count; ++i) {
			GenerateSector(lev, i);
		}
	}

	private void GenerateSector(LEV lev, int sectorIndex) {
		LEV.Sector sector = lev.Sectors[sectorIndex];

		Vector3[] workVerts = new Vector3[sector.Vertices.Count*2];

		for (int i = 0; i < sector.Vertices.Count; ++i) {
			Vector2 sectorVertex = sector.Vertices[i];

			Vector3 v;
			v.x = sectorVertex.x;
			v.z = sectorVertex.y;
			v.y = sector.FloorAlt;

			workVerts[i] = v;
			v.y = sector.CeilAlt;

			workVerts[i + sector.Vertices.Count] = v;
		}

		GameObject sectorGO = new GameObject("Sector" + sectorIndex, typeof(MeshFilter), typeof(MeshRenderer));
		sectorGO.transform.parent = _sectorsGO.transform;

		Mesh mesh = new Mesh();

		MeshFilter mf = sectorGO.GetComponent<MeshFilter>();
		MeshRenderer mr = sectorGO.GetComponent<MeshRenderer>();

		mf.mesh = mesh;

		List<Vector3> verts = new List<Vector3>();
		List<Vector2> uvs = new List<Vector2>();
		List<Material> mats = new List<Material>();

		List<List<int>> wallTris = new List<List<int>>();

		for (int i = 0; i < sector.Walls.Count; ++i) {
			List<int> tris = new List<int>();
			if (GenerateSectorWalls(lev, sector, i, workVerts, tris, verts, uvs, mats)) {
				wallTris.Add(tris);
			}
		}

		mesh.vertices = verts.ToArray();
		mesh.uv = uvs.ToArray();
		mesh.subMeshCount = wallTris.Count;

		for (int i = 0; i < wallTris.Count; ++i) {
			var wall = wallTris[i];
			mesh.SetTriangles(wall.ToArray(), i);
		}

		mr.materials = mats.ToArray();

		//GenerateSectorFloors(lev, sector, ref subMeshIndex, mesh, verts, tris, uvs, mats);
	}

	private bool GenerateSectorWalls(LEV lev, LEV.Sector sector, int index, Vector3[] workVerts, List<int> tris, List<Vector3> verts, List<Vector2> uvs, List<Material> mats) {

		LEV.Wall wall = sector.Walls[index];

		if ((wall.Adjoin != -1) && (wall.Mirror != -1)) {
			LEV.Sector adjoin = lev.Sectors[wall.Adjoin];
			LEV.Wall mirror = adjoin.Walls[wall.Mirror];
			return false;
		}

		int baseIndex = verts.Count;

		verts.Add(workVerts[wall.V0 + sector.Vertices.Count]);
		verts.Add(workVerts[wall.V1 + sector.Vertices.Count]);
		verts.Add(workVerts[wall.V1]);
		verts.Add(workVerts[wall.V0]);

		uvs.Add(new Vector2(0, 0));
		uvs.Add(new Vector2(1, 0));
		uvs.Add(new Vector2(1, 1));
		uvs.Add(new Vector2(0, 1));

		tris.Add(baseIndex + 3);
		tris.Add(baseIndex + 1);
		tris.Add(baseIndex + 0);
		tris.Add(baseIndex + 1);
		tris.Add(baseIndex + 3);
		tris.Add(baseIndex + 2);

		Material mat = new Material(_game.WallMaterial);
		mat.mainTexture = _textures[wall.TexMid.Texture].Frames[0].Texture;
		mats.Add(mat);

		return true;
	}

	private void GenerateSectorFloors(LEV lev, LEV.Sector sector, ref int subMeshIndex, Mesh mesh, Vector3[] verts, List<Vector3> tris, List<Vector2> uvs, List<Material> mats) {
	}

	public void Dispose() {
		foreach (var bm in _textures) {
			bm.Dispose();
		}
		_textures = null;

		GameObject.Destroy(_sectorsGO);

		System.GC.SuppressFinalize(this);
	}

}
