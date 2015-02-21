using UnityEngine;
using System.Collections.Generic;

public class AssertServices {
	public class Exception : System.Exception {
		public Exception() { }
		public Exception(string msg) : base(msg) { }
	}

	public void Assert(bool expression) {
		Assert(expression, null);
	}

	public void Assert(bool expression, string msg) {
		if (!expression) {
			if (msg != null) {
				throw new Exception(msg);
			} else {
				throw new Exception();
			}
		}
	}
}

public sealed class DebugCheck {

	private static AssertServices Instance;

	static DebugCheck() {
		Instance = new AssertServices();
	}

	public static void Assert(bool expression) {
		if (Instance != null) {
			Instance.Assert(expression);
		}
	}

	public static void Assert(bool expression, string msg) {
		if (Instance != null) {
			Instance.Assert(expression, msg);
		}
	}

}

public sealed class RuntimeCheck {

	private static AssertServices Instance;

	static RuntimeCheck() {
		Instance = new AssertServices();
	}

	public static void Assert(bool expression) {
		if (Instance != null) {
			Instance.Assert(expression);
		}
	}

	public static void Assert(bool expression, string msg) {
		if (Instance != null) {
			Instance.Assert(expression, msg);
		}
	}

}