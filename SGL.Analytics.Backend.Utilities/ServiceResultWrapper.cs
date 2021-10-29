using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Utilities {

	public class ServiceResultWrapper<TValue> {
		public TValue Result { get; set; }

		protected ServiceResultWrapper(TValue result) {
			Result = result;
		}
	}

	public class ServiceResultWrapper<TService, TValue> : ServiceResultWrapper<TValue> {
		public ServiceResultWrapper(TValue result) : base(result) { }
	}
}
