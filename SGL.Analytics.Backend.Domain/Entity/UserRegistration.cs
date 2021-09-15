using SGL.Analytics.Backend.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;

namespace SGL.Analytics.Backend.Domain.Entity {

	public class UserRegistration {
		public Guid Id { get; set; }
		public Guid AppId { get; set; }
		public ApplicationWithUserProperties App { get; set; } = null!;
		public string Username { get; set; }
		public string HashedSecret { get; set; }

		public ICollection<ApplicationUserPropertyInstance> AppSpecificProperties { get; set; } = null!;

		public UserRegistration(Guid id, Guid appId, string username, string hashedSecret) {
			Id = id;
			AppId = appId;
			Username = username;
			HashedSecret = hashedSecret;
		}

		public static UserRegistration Create(Guid id, ApplicationWithUserProperties app, string username, string hashedSecret) {
			var userReg = new UserRegistration(id, app.Id, username, hashedSecret);
			userReg.App = app;
			userReg.AppSpecificProperties = new List<ApplicationUserPropertyInstance>();
			return userReg;
		}

		public static UserRegistration Create(ApplicationWithUserProperties app, string username, string hashedSecret) {
			return Create(Guid.NewGuid(), app, username, hashedSecret);
		}

		private ApplicationUserPropertyInstance setAppSpecificPropertyImpl(string name, object? value, Func<string, ApplicationUserPropertyDefinition> getPropDef) {
			var propInst = AppSpecificProperties.Where(p => p.Definition.Name == name).SingleOrDefault();
			if (propInst is null) {
				propInst = ApplicationUserPropertyInstance.Create(getPropDef(name), this);
				AppSpecificProperties.Add(propInst);
			}
			propInst.Value = value;
			return propInst;
		}

		public ApplicationUserPropertyInstance SetAppSpecificProperty(string name, object? value) {
			return setAppSpecificPropertyImpl(name, value, name => {
				var propDef = App.UserProperties.Where(p => p.Name == name).SingleOrDefault();
				if (propDef is null) {
					throw new UndefinedPropertyException(name);
				}
				return propDef;
			});
		}

		public object? GetAppSpecificProperty(string name) {
			var propInst = AppSpecificProperties.Where(p => p.Definition.Name == name).SingleOrDefault();
			if (propInst is null) {
				throw new PropertyNotFoundException(name);
			}
			return propInst.Value;
		}

		public ApplicationUserPropertyInstance SetAppSpecificProperty(ApplicationUserPropertyDefinition propDef, object? value) {
			return setAppSpecificPropertyImpl(propDef.Name, value, name => propDef);
		}

		public object? GetAppSpecificProperty(ApplicationUserPropertyDefinition propDef) {
			return GetAppSpecificProperty(propDef.Name);
		}

		public void ValidateProperties() {
			Debug.Assert(AppSpecificProperties is not null, "AppSpecificProperties navigation property is not present.");
			Debug.Assert(App is not null, "App navigation property is not present.");
			Debug.Assert(App.UserProperties is not null, "UserProperties navigation property of associated App is not present.");
			// TODO: If this method becomes a bottleneck, maybe use temporary Dictionaries / Sets to avoid O(n^2) runtime.
			// However, the involved 'n's should be quite low and this happens in-memory, just before we access the database, which should dwarf this overhead.
			foreach (var propDef in App.UserProperties.Where(p => p.Required)) {
				var propInst = AppSpecificProperties.Where(pi => pi.DefinitionId == propDef.Id).ToList();
				if (propInst.Count == 0) {
					throw new RequiredPropertyMissingException(propDef.Name);
				}
				else if (propInst.Single().IsNull()) {
					throw new RequiredPropertyNullException(propDef.Name);
				}
			}
			foreach (var propInst in AppSpecificProperties) {
				if (!App.UserProperties.Any(pd => pd.Id == propInst.DefinitionId)) {
					throw new UndefinedPropertyException(propInst.Definition.Name);
				}
				if (AppSpecificProperties.Count(p => p.Definition.Name == propInst.Definition.Name) > 1) {
					throw new ConflictingPropertyInstanceException(propInst.Definition.Name);
				}
			}
		}
	}
}
