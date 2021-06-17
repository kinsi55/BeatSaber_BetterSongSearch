using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterSongSearch.Util {
	// One of either, Mono or Unity, is stupid and does not allow one to use ?? or ??= as "NULL" is not *really* "null", sigh
	static class XD {
		public static T FunnyMono<T>(T a) {
			if(a == null)
				return default(T);

			return a;
		}
	}
}
