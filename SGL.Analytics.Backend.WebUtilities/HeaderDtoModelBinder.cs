using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {
	public class HeaderDtoModelBinder : IModelBinder {
		public async Task BindModelAsync(ModelBindingContext bindingContext) {
			if (bindingContext == null) throw new ArgumentNullException(nameof(bindingContext));
			var modelName = bindingContext.FieldName;
			var prefix = "";
			if (!string.IsNullOrEmpty(bindingContext.BinderModelName)) {
				modelName = bindingContext.BinderModelName;
				prefix = $"{bindingContext.BinderModelName}.";
			}
			var valid = true;
			var constructor = bindingContext.ModelType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderByDescending(ci => ci.GetParameters().Length).First();
			var ctorArgs = new object?[constructor.GetParameters().Length];
			var ctorParams = constructor.GetParameters();
			for (int i = 0; i < ctorParams.Length; ++i) {
				var headerName = $"{prefix}{ctorParams[i].Name}";
				if (bindingContext.HttpContext.Request.Headers.TryGetValue(headerName, out var values) && values.Count > 0) {
					var strVal = values.First();
					var converter = getConverter(ctorParams[i].ParameterType);
					var convertedVal = converter(strVal);
					if (convertedVal is not null) {
						ctorArgs[i] = convertedVal;
					}
					else {
						valid = false;
						bindingContext.ModelState.TryAddModelError(modelName, $"The header {headerName} could not be parsed as the expected type.");
					}
				}
				else {
					valid = false;
					bindingContext.ModelState.TryAddModelError(modelName, $"The header {headerName} is required.");
				}
			}
			if (valid) {
				bindingContext.Model = constructor.Invoke(ctorArgs);
				bindingContext.Result = ModelBindingResult.Success(bindingContext.Model);
			}
			else {
				bindingContext.Result = ModelBindingResult.Failed();
			}
			await Task.CompletedTask;
		}

		private Func<string, object?> getConverter(Type type) {
			switch (type) {
				case Type _ when type == typeof(DateTime):
					return source => DateTime.TryParse(source, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
				case Type _ when type == typeof(string):
					return source => source;
				case Type _ when type == typeof(Guid):
					return source => Guid.TryParse(source, out var guid) ? guid : null;
				case { IsEnum: true }:
					return source => Enum.TryParse(type, source, out var e) ? e : null;
				case Type _ when type == typeof(Int16):
					return source => Int16.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
				case Type _ when type == typeof(Int32):
					return source => Int32.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
				case Type _ when type == typeof(Int64):
					return source => Int64.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
				case Type _ when type == typeof(Double):
					return source => Double.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out var fp) ? fp : null;
				case Type _ when type == typeof(Decimal):
					return source => Decimal.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out var fp) ? fp : null;
				case Type _ when type == typeof(Boolean):
					return source => Boolean.TryParse(source, out var b) ? b : null;
				default:
					return source => null;
			}
		}
	}

	public class DtoFromHeaderModelBinderAttribute : ModelBinderAttribute {
		public DtoFromHeaderModelBinderAttribute() : base(typeof(HeaderDtoModelBinder)) { }
		public override BindingSource BindingSource => BindingSource.Header;
	}
}
