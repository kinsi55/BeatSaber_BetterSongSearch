using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BetterSongSearch.Util {
	class RatelimitCoroutine {
		Action exitfn;
		float limit;
		public RatelimitCoroutine(Action exitfn, float limit = 0.5f) {
			this.exitfn = exitfn;
			this.limit = limit;
		}

		bool isLimited = false;
		bool queuedFallingEdge = false;

		public IEnumerator Call() => Call(false);

		public IEnumerator CallNextFrame() {
			if(!isLimited) {
				isLimited = true;
				yield return 0;
				isLimited = false;
			}
			yield return Call(false);
		}

		IEnumerator Call(bool isFallingEdge = false) {
			if(isLimited && !isFallingEdge) {
				queuedFallingEdge = true;
				yield break;
			}

			isLimited = true;

			exitfn();

			yield return new WaitForSeconds(limit);

			if(queuedFallingEdge) {
				queuedFallingEdge = false;
				yield return Call(true);
			} else {
				isLimited = false;
			}
		}
	}
}
