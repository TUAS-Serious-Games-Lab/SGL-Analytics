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

		public ICollection<ApplicationUserPropertyInstance> AppSpecificProperties { get; set; } = null!;

		public UserRegistration(Guid id, Guid appId, string username) {
			Id = id;
			AppId = appId;
			Username = username;
		}

		public static UserRegistration Create(Guid id, ApplicationWithUserProperties app, string username) {
			var userReg = new UserRegistration(id, app.Id, username);
			userReg.App = app;
			userReg.AppSpecificProperties = new List<ApplicationUserPropertyInstance>();
			return userReg;
		}

		public static UserRegistration Create(ApplicationWithUserProperties app, string username) {
			return Create(Guid.NewGuid(), app, username);
		}

		public void ValidateProperties() {
			Debug.Assert(AppSpecificProperties is not null, "AppSpecificProperties navigation property is not present.");
			Debug.Assert(App is not null, "App navigation property is not present.");
			Debug.Assert(App.UserProperties is not null, "UserProperties navigation property of associated App is not present.");
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
			}
		}
	}
}
