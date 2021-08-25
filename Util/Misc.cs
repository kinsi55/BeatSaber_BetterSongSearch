namespace BetterSongSearch.Util {
	// One of either, Mono or Unity, is stupid and does not allow one to use ?? or ??= as "NULL" is not *really* "null", sigh
	static class XD {
		public static T FunnyMono<T>(T a) where T: UnityEngine.Object {
			if(a == null)
				return default(T);

			return a;
		}
	}
}
