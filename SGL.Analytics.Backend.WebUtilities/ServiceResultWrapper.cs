using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {

	public class ServiceResultWrapper<T> {
		public T Result { get; set; }

		public ServiceResultWrapper(T result) {
			Result = result;
		}
	}
}
