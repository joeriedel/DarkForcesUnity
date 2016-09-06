/* Game.cs
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
using System.Collections;
using System.Collections.Generic;

/*! Main game class. */
public class Game : MonoBehaviour {

	public Material SolidCMP;
	public Material Solid;
	public bool EmulateCMPShading;

	private iMUSE _iMuse;
	private World _world;

	void Awake() {
		Asset.StaticInit(this);

		_files = new Files();
		_files.Initialize();
	}

	void Start() {
		_world = new World(this);
		_world.Load("SECBASE");

		_iMuse = GetComponent<iMUSE>();

		using (GMD stalk = Asset.LoadCached<GMD>("STALK-01.GMD")) {
			using (GMD fight = Asset.LoadCached<GMD>("FIGHT-01.GMD")) {
				_iMuse.PlayLevelMusic(stalk, fight);
			}
		}

		Asset.PurgeCache();
	}

	static void CreateQuad(Mesh mesh, float W, float H) {
		Vector3[] verts = new Vector3[4];
		Vector2[] uvs = new Vector2[4];

		verts[0] = new Vector3(-W/2, H/2, 0);
		verts[1] = new Vector3(W/2, H/2, 0);
		verts[2] = new Vector3(W/2, -H/2, 0);
		verts[3] = new Vector3(-W/2, -H/2, 0);

		uvs[0] = new Vector2(0, 1);
		uvs[1] = new Vector2(1, 1);
		uvs[2] = new Vector3(1, 0);
		uvs[3] = new Vector3(0, 0);

		int[] triangles = new int[6];
		triangles[0] = 0;
		triangles[1] = 1;
		triangles[2] = 3;
		triangles[3] = 3;
		triangles[4] = 1;
		triangles[5] = 2;

		mesh.Clear();
		mesh.vertices = verts;
		mesh.uv = uvs;
		mesh.triangles = triangles;

	}

	void Update() {
	}

	public Files Files { get { return _files; } }

	private Files _files;
}
