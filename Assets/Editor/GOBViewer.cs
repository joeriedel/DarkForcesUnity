/* GOBViewer.cs
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

using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace EditorTools {

	public abstract class GOBResourceViewer : System.IDisposable {
		public GOBResourceViewer(Asset asset) {
			_asset = asset;
		}
		
		public abstract void OnGUI();

		public abstract bool Visibile { get; }

		public void Dispose() {
			_asset.Dispose();
			_asset = null;
			System.GC.SuppressFinalize(this);
		}

		protected Asset _asset;
	}

	public class GOBViewerWindow : EditorWindow {

		[MenuItem("DarkForces/View GOB Files")]
		static void OpenWindow() {
			EditorWindow.GetWindow(typeof(GOBViewerWindow), true, "DARK.GOB, SOUNDS.GOB, TEXTURES.GOB, SPRITES.GOB");
		}

		Files _files;
		List<GOBResourceViewer> _views;

		void OnEnable() {
			_files = new Files();
			_views = new List<GOBResourceViewer>();

			_files.Initialize();

			// PAL to use
			using (PAL pal = Asset.New("RAMSHED.PAL", _files.Load("RAMSHED.PAL"), Asset.Type.PAL, null) as PAL) {

				BM.CreateArgs bmCreateArgs = new BM.CreateArgs();
				bmCreateArgs.Pal = pal;

				foreach (var gob in _files.GOBs) {
					foreach (var file in gob.Files) {
						switch (Asset.TypeForName(file.Name)) {
							case Asset.Type.BM:
							_views.Add(new GOBBMViewer(Asset.Load(file, bmCreateArgs) as BM));
							break;
						}
					}
				}
			}
		}

		void OnDisable() {
			foreach (var viewer in _views) {
				viewer.Dispose();
			}
			_views = null;
			_files.Dispose();
			_files = null;
		}

		Vector2 _scrollOfs = Vector2.zero;
		
		void OnGUI() {

			_scrollOfs = GUILayout.BeginScrollView(_scrollOfs, false, true);
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();

			GOBBMViewer.Show = GUILayout.Toggle(GOBBMViewer.Show, "BM");
			GUILayout.EndHorizontal();

			foreach (var viewer in _views) {
				if (viewer.Visibile) {
					GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
					viewer.OnGUI();
				}
			}
			
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
		}

	}

	public class GOBBMViewer : GOBResourceViewer {

		public static bool Show = true;
		float scale;
		float frame;
		float FPS;

		public GOBBMViewer(BM bm) : base(bm) {
			_bm = bm;

			float edge = Mathf.Max(bm.Frames[0].Texture.width, bm.Frames[0].Texture.height);
			scale = Mathf.Clamp(64f / edge, 0.1f, 1f);

			frame = 0f;

			FPS = bm.FPS;

			if ((bm.Frames.Count > 1) && (FPS < 1)) {
				FPS = 1;
			}
		}

		public override bool Visibile {
			get { return Show; }
		}

		public override void OnGUI() {
			frame += Time.deltaTime * FPS;

			int iframe = Mathf.FloorToInt(frame);
			if (iframe >= _bm.Frames.Count) {
				iframe = 0;
			}

			GUILayout.Label(_bm.Name + " - " + _bm.Frames[0].Texture.width + "x" + _bm.Frames[0].Texture.height, EditorStyles.boldLabel);

			scale = GUILayout.HorizontalSlider(scale, 0.1f, 1f, GUILayout.Width(200));
			float w = _bm.Frames[iframe].Texture.width * scale;
			float h = _bm.Frames[iframe].Texture.height * scale;

			var r = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
			GUI.DrawTexture(r, _bm.Frames[iframe].Texture);

		}

		private BM _bm;
	}
}
