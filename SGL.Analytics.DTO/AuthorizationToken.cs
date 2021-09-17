using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SGL.Analytics.DTO {

	public enum AuthorizationTokenScheme {
		Bearer
	}

	public struct AuthorizationToken {
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public AuthorizationTokenScheme Scheme { get; }
		public string Value { get; }

		[JsonConstructor]
		public AuthorizationToken(AuthorizationTokenScheme Scheme, string Value) {
			this.Scheme = Scheme;
			this.Value = Value;
		}
		public AuthorizationToken(string Value) : this(AuthorizationTokenScheme.Bearer, Value) { }

		public AuthenticationHeaderValue ToHttpHeaderValue() => new AuthenticationHeaderValue(Scheme.ToString(), Value);
		public override string? ToString() => $"{Scheme.ToString()} {Value}";
	}
}
