/* GOBEditor.cs
 *
 * The MIT License (MIT)
 *
 * Copyright (c) 2013 Joseph Riedel
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
using System.Collections;

namespace EditorTools {
	
public class GOBEditorWindow : EditorWindow {
	
	[MenuItem("DarkForces/Extract GOB...")]
	static void Open() {
		string gobPath = EditorUtility.OpenFilePanel("Choose GOB File", "", "gob");
		if (gobPath.Length > 0) {
			string folderPath = EditorUtility.OpenFolderPanel("Choose Destination Folder", "", "");
			if (folderPath.Length > 0) {
				if (!ExtractGOB(gobPath, folderPath)) {
					EditorUtility.DisplayDialog("Error", "Unable to extract GOB.", "OK");
				}
			}
		}
	}

	static bool ExtractGOB(string gobPath, string folderPath) {
		using (GOBFile gob = GOBFile.Open(gobPath)) {
			if (gob == null)
				return false;

			foreach (GOBFile.File file in gob.Files) {
				try {
					using (FileStream sysFile = File.Create(folderPath + "/" + file.Name)) {
						byte[] data = file.Load();
						sysFile.Write(data, 0, data.Length);
					}
				} catch (System.Exception e) {
					Debug.Log("Error extracting GOB '" + gobPath + "', file '" + file.Name + "'");
					return false;
				}
			}
		}

		return true;
	}
}

}