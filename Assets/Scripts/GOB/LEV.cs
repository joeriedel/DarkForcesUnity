/* LEV.cs
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
using System.IO;
using System.Collections.Generic;

/*! Dark Forces level data. */
public sealed class LEV : Asset {

	public struct WallTex {
		public int Texture;
		public float ShiftX;
		public float ShiftY;
	}

	public class Wall {
		[System.Flags]
		public enum EFlags0 {
			AlwaysDrawMid = 0x1,
			IlluminatedSign = 0x2,
			FlipHorz = 0x4,
			EvelLight = 0x8,
			TexAnchor = 0x10,
			ElevMorph = 0x20,
			ScrollTop = 0x40,
			ScrollMid = 0x80,
			ScrollBot = 0x100,
			MapHidden = 0x200,
			MapNormal = 0x400,
			SignAnchored = 0x800,
			DamagesPlayer = 0x1000,
			MapDoor = 0x2000
		}

		[System.Flags]
		public enum EFlags2 {
			AlwaysWalk = 0x1,
			BlocksAll = 0x2,
			BlocksEnemy = 0x4,
			BlocksWeapon = 0x8
		}

		public int V0, V1;
		public int Adjoin;
		public int Mirror;
		public int Walk;
		public EFlags0 Flags0;
		public int Flags1;
		public EFlags2 Flags2;
		public int Light;
		public WallTex TexMid;
		public WallTex TexTop;
		public WallTex TexBottom;
		public WallTex TexSign;
	}

	public class Sector {
		[System.Flags]
		public enum EFlags0 {
			Sky = 0x1,
			Door = 0x2,
			Bounce = 0x4,
			SkyJoin = 0x8,
			IceFloor = 0x10,
			SnowFloor = 0x20,
			ExplodingDoor = 0x40,
			FloorJoin = 0x80,
			Crush = 0x100,
			NoWalls = 0x200,
			DamageSmall = 0x400,
			DamageLarge = 0x800,
			NoSmartObject = 0x1000,
			SmartObject = 0x2000,
			SubSector = 0x4000,
			Rendered = 0x10000,
			Player = 0x20000,
			Secret = 0x40000
		}

		public string Name;
		public EFlags0 Flags0;
		public int Flags1;
		public int Flags2;
		public int FloorTex;
		public int CeilTex;
		public int Layer;
		public int Ambient;
		public float FloorAlt;
		public float CeilAlt;
		public float SecondAlt;
		public float FloorShiftX;
		public float FloorShiftZ;
		public float CeilShiftX;
		public float CeilShiftZ;
		public List<Vector2> Vertices = new List<Vector2>();
		public List<Wall> Walls = new List<Wall>();
	}

	public LEV(string name, byte[] data, object createArgs) : base(name, Type.LEV) {
		using (MemoryStream backing = new MemoryStream(data, false))
		using (ByteStream stream = new ByteStream(backing)) {
			ParseLevel(new Tokenizer(name, Tokenizer.ECommentStyle.LEV, stream));
		}
	}

	private void ParseLevel(Tokenizer levelTokens) {
		ParseHeader(levelTokens);
		ParseTextures(levelTokens);
		ParseSectors(levelTokens);
	}

	private void ParseHeader(Tokenizer levelTokens) {
		levelTokens.EnsureNextToken("LEV");
		levelTokens.RequireNextToken(); // skip version.
		
		levelTokens.EnsureNextToken("LEVELNAME");
		_name = levelTokens.RequireNextToken();

		levelTokens.EnsureNextToken("PALETTE");
		_pal = levelTokens.RequireNextToken();

		levelTokens.EnsureNextToken("MUSIC");
		_music = levelTokens.RequireNextToken();

		levelTokens.EnsureNextToken("PARALLAX");
		_parallax.x = levelTokens.RequireNextFloat();
		_parallax.y = levelTokens.RequireNextFloat();
	}

	private void ParseTextures(Tokenizer levelTokens) {
		levelTokens.EnsureNextToken("TEXTURES");
		levelTokens.RequireNextInt();

		while (true) {
			if (levelTokens.IsNextToken("NUMSECTORS")) {
				levelTokens.RequireNextInt(); // eat sector count.
				break;
			}

			string texture = levelTokens.GetNextToken();
			_textures.Add(texture);
		}
	}

	private void ParseSectors(Tokenizer levelTokens) {
		string token;
		while ((token = levelTokens.GetNextToken()) != null) {
			levelTokens.CheckThrow(token == "SECTOR", "Expected SECTOR!");
			levelTokens.RequireNextInt();

			Sector sector = ParseSectorHeader(levelTokens);
			ParseSectorVertices(sector, levelTokens);
			ParseSectorWalls(sector, levelTokens);
			Sectors.Add(sector);
		}
	}

	private Sector ParseSectorHeader(Tokenizer levelTokens) {
		Sector sector = new Sector();

		levelTokens.EnsureNextToken("NAME");
		string sectorName = levelTokens.GetNextToken();

		if (sectorName != "AMBIENT") {
			sector.Name = sectorName;
		} else { // unnamed sector
			levelTokens.UngetToken();
		}

		levelTokens.EnsureNextToken("AMBIENT");
		sector.Ambient = levelTokens.RequireNextInt();

		levelTokens.EnsureNextToken("FLOOR");
		levelTokens.EnsureNextToken("TEXTURE");
		sector.FloorTex = levelTokens.RequireNextInt();
		sector.FloorShiftX = levelTokens.RequireNextFloat();
		sector.FloorShiftZ = levelTokens.RequireNextFloat();
		levelTokens.RequireNextInt();

		levelTokens.EnsureNextToken("FLOOR");
		levelTokens.EnsureNextToken("ALTITUDE");
		sector.FloorAlt = levelTokens.RequireNextFloat();

		levelTokens.EnsureNextToken("CEILING");
		levelTokens.EnsureNextToken("TEXTURE");
		sector.CeilTex = levelTokens.RequireNextInt();
		sector.CeilShiftX = levelTokens.RequireNextFloat();
		sector.CeilShiftZ = levelTokens.RequireNextFloat();
		levelTokens.RequireNextInt();

		levelTokens.EnsureNextToken("CEILING");
		levelTokens.EnsureNextToken("ALTITUDE");
		sector.CeilAlt = levelTokens.RequireNextFloat();

		levelTokens.EnsureNextToken("SECOND");
		levelTokens.EnsureNextToken("ALTITUDE");
		sector.SecondAlt = levelTokens.RequireNextFloat();

		levelTokens.EnsureNextToken("FLAGS");
		sector.Flags0 = (Sector.EFlags0)levelTokens.RequireNextInt();
		sector.Flags1 = levelTokens.RequireNextInt();
		sector.Flags2 = levelTokens.RequireNextInt();

		levelTokens.EnsureNextToken("LAYER");
		sector.Layer = levelTokens.RequireNextInt();

		return sector;
	}

	private void ParseSectorVertices(Sector sector, Tokenizer levelTokens) {
		levelTokens.EnsureNextToken("VERTICES");

		int numVerts = levelTokens.RequireNextInt();

		for (int i = 0; i < numVerts; ++i) {
			Vector2 v;

			levelTokens.EnsureNextToken("X:");
			v.x = levelTokens.RequireNextFloat();
			levelTokens.EnsureNextToken("Z:");
			v.y = levelTokens.RequireNextFloat();

			sector.Vertices.Add(v);
		}
	}

	private void ParseSectorWalls(Sector sector, Tokenizer levelTokens) {
		levelTokens.EnsureNextToken("WALLS");

		int numWalls = levelTokens.RequireNextInt();

		for (int i = 0; i < numWalls; ++i) {
			Wall wall = ParseWall(levelTokens);
			sector.Walls.Add(wall);
		}
	}

	private Wall ParseWall(Tokenizer levelTokens) {
		levelTokens.EnsureNextToken("WALL");

		Wall wall = new Wall();

		levelTokens.EnsureNextToken("LEFT:");
		wall.V0 = levelTokens.RequireNextInt();
		levelTokens.EnsureNextToken("RIGHT:");
		wall.V1 = levelTokens.RequireNextInt();

		wall.TexMid = ParseWallTex("MID:", levelTokens, true);
		wall.TexTop = ParseWallTex("TOP:", levelTokens, true);
		wall.TexBottom = ParseWallTex("BOT:", levelTokens, true);
		wall.TexSign = ParseWallTex("SIGN:", levelTokens, false);

		levelTokens.EnsureNextToken("ADJOIN:");
		wall.Adjoin = levelTokens.RequireNextInt();

		levelTokens.EnsureNextToken("MIRROR:");
		wall.Mirror = levelTokens.RequireNextInt();

		levelTokens.EnsureNextToken("WALK:");
		wall.Walk = levelTokens.RequireNextInt();

		levelTokens.EnsureNextToken("FLAGS:");
		wall.Flags0 = (Wall.EFlags0)levelTokens.RequireNextInt();
		wall.Flags1 = levelTokens.RequireNextInt();
		wall.Flags2 = (Wall.EFlags2)levelTokens.RequireNextInt();

		levelTokens.EnsureNextToken("LIGHT:");
		wall.Light = levelTokens.RequireNextInt();

		return wall;
	}

	private WallTex ParseWallTex(string name, Tokenizer levelTokens, bool trailing) {
		levelTokens.EnsureNextToken(name);

		WallTex tex = new WallTex();

		tex.Texture = levelTokens.RequireNextInt();
		tex.ShiftX = levelTokens.RequireNextFloat();
		tex.ShiftY = levelTokens.RequireNextFloat();

		if (trailing) {
			levelTokens.RequireNextInt();
		}
		
		return tex;
	}

	public string LevelName { get { return _name; } }
	public string Music { get { return _music; } }
	public string Palette { get { return _pal; } }
	public Vector2 Parallax { get { return _parallax; } }
	public List<string> Textures { get { return _textures; } }
	public List<Sector> Sectors { get { return _sectors; } }

	private string _name;
	private string _music;
	private string _pal;
	private Vector2 _parallax;
	private List<string> _textures = new List<string>();
	private List<Sector> _sectors = new List<Sector>();
}
