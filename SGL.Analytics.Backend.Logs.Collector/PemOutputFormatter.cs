using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector {
	public class PemOutputFormatter : TextOutputFormatter {

		public PemOutputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x-pem-file"));
			SupportedEncodings.Add(Encoding.UTF8);
		}

		public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding) {
			if (context.Object is IEnumerable<string> values) {
				foreach (var value in values) {
					await context.HttpContext.Response.WriteAsync(value, selectedEncoding);
					await context.HttpContext.Response.WriteAsync("\n", selectedEncoding);
				}
			}
			else if (context.Object is string value) {
				await context.HttpContext.Response.WriteAsync(value, selectedEncoding);
			}
			else {
				throw new ArgumentException("Unsupported Object type", nameof(context));
			}
		}

		protected override bool CanWriteType(Type type) {
			return typeof(string).IsAssignableFrom(type) || typeof(IEnumerable<string>).IsAssignableFrom(type);
		}
	}
}
