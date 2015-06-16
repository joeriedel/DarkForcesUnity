/* Tokenizer.cs
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

public sealed class Tokenizer {

	public class Exception : System.Exception {
		public Exception() { }
		public Exception(string msg) : base(msg) { }
	}

	public enum ECommentStyle {
		LEV,
		INF
	}

	public Tokenizer(string name, ECommentStyle commentStyle, ByteStream stream) {
		_name = name;
		_commentStyle = commentStyle;
		_stream = stream;
	}
  
	public int Line {
		get { return _lineNum; }
	}
  
	public int CharPos {
		get { return _charNum; }
	}
  
	public void UngetToken() {
		_unToken = _token;
	}

	public string GetNextToken() {
		if (_unToken != null) {
			string tempToken = _unToken;
			_unToken = null;
			return tempToken;
		}

		if (_eof) {
			return null;
		}

		_token = ReadToken();
		return _token;
	}

	public string RequireNextToken() {
		string token = GetNextToken();
		CheckThrow(token != null, "Expected token!");
		return token;
	}

	public float? GetNextFloat() {
		string token = GetNextToken();
		if (token != null) {
			return float.Parse(token);
		}
		return null;
	}

	public int? GetNextInt() {
		string token = GetNextToken();
		if (token != null) {
			return int.Parse(token);
		}
		return null;
	}

	public float RequireNextFloat() {
		string token = GetNextToken();
		CheckThrow(token != null, "Expected float!");
		return float.Parse(token);
	}

	public int RequireNextInt() {
		string token = GetNextToken();
		CheckThrow(token != null, "Expected int!");
		return int.Parse(token);
	}

	public bool IsNextToken(string nextToken) {
		string token = GetNextToken();
		return token == nextToken;
	}

	public void EnsureNextToken(string nextToken) {
		CheckThrow(IsNextToken(nextToken), "Expected '" + nextToken + "'");
	}

	public bool IsWhitespace(int c) {
		return (c < 33) || (c > 126);
	}

	public string ReadToken() {

		char? c = GetNextChar(true, true);
		if (c == null) {
			_eof = true;
			return null;
		}

		bool quote = c == '"';
		System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();

		if (!quote) {
			stringBuilder.Append(c.Value);
		}

		while ((c = GetNextChar(false, true)) != null) {
			if (!quote && char.IsWhiteSpace(c.Value)) {
				break;
			}

			if (c == '"') {
				CheckThrow(quote, "Quote in middle of token!");
				break;				
			}

			stringBuilder.Append(c.Value);
		}

		CheckThrow(!quote, "EOS before closing quote!");

		return stringBuilder.ToString();
	}

	private char? SafeReadChar() {
		if (_stream.EOS) {
			return null;
		}
		return _stream.ReadChar();
	}

	private char? GetNextChar(bool skipWhitespace, bool skipComments) {

		while (true) {
			char? c = null;

			// skip white space.
			if (skipWhitespace) {
				while (true) {
					c = SafeReadChar();
					if (c == null) {
						return null;
					}

					++_charNum;

					if (!char.IsWhiteSpace(c.Value)) {
						break;
					}

					if (c == CR) {
						if (PeekNextChar() == LF) {
							_stream.Skip(1);
						}
						++_lineNum;
						_charNum = 0;
					} else if (c == LF) {
						++_lineNum;
						_charNum = 0;
					}
				}
			} else {
				c = SafeReadChar();
				if (c != null) {
					++_charNum;

					if (c == CR) {
						++_lineNum;
						_charNum = 0;

						if (PeekNextChar() == LF) {
							_stream.Skip(1);
						}
					} else if (c == LF) {
						++_lineNum;
						_charNum = 0;
					}
				}
			}

			if (skipComments) {
				if ((c == '#') && (_commentStyle == ECommentStyle.LEV)) {
					// single line comment, skip line
					while ((c = SafeReadChar()) != null) {
						if (c == CR) {
							++_lineNum;
							_charNum = 0;

							if (PeekNextChar() == LF) {
								_stream.Skip(1);
							}
							break;
						} else if (c == LF) {
							++_lineNum;
							_charNum = 0;
							break;
						}
					}

					if (c == null) {
						return null;
					}

					continue;
				} else if ((c == '/') && (_commentStyle == ECommentStyle.INF)) {
					// might be a comment.
					char? nc = PeekNextChar();
					if (nc == '*') {
						_stream.Skip(1);

						/* multiple line comment, read until closing */
						while ((c = SafeReadChar()) != null) {
							if (c == '*') {
								if (PeekNextChar() == '/') {
									_stream.Skip(1);
									break;
								}
							} else if (c == CR) {
								if (PeekNextChar() == LF) {
									_stream.Skip(1);
								}
								++_lineNum;
								_charNum = 0;
							} else if (c == LF) {
								++_lineNum;
								_charNum = 0;
							}
						}

						if (c == null) {
							return null;
						}

						continue;
					}
				}
			}

			return c;
		}

	}
  
	char? PeekNextChar()  {
		if (_stream.EOS) {
			return null;
		}
		long position = _stream.Position;
		int c = _stream.ReadByte();
		_stream.SeekSet(position);
		return (char?)c;
	}

	public void CheckThrow(bool expression, string msg) {
		if (!expression) {
			ThrowError(msg);
		}
	}

	public void ThrowError(string msg) {
		RuntimeCheck.Assert(false, string.Format("Parse error '{0}' file: '{1}', line '{2}', char '{3}'", msg, _name, _lineNum, _charNum));
	}

	const char CR = '\r';
	const char LF = '\n';
	
	private ECommentStyle _commentStyle;
	private string _token;
	private string _unToken;
	private ByteStream _stream;
	private bool _eof = false;
	private int _lineNum = 1;
	private int _charNum = 1;
	private readonly string _name;
}
