using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Utilities.Backend;
using SGL.Utilities.Backend.TestUtilities.Applications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Tests.Dummies {
	public class DummyApplicationRepository : DummyApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> {
		private readonly Dictionary<string, ApplicationWithUserProperties> apps = new();
		private int nextPropertyDefinitionId = 1;

		protected override void OnAdd(ApplicationWithUserProperties app) {
			assignPropertyDefinitionIds(app);
		}

		protected override void OnUpdate(ApplicationWithUserProperties app) {
			assignPropertyDefinitionIds(app);
		}

		private void assignPropertyDefinitionIds(ApplicationWithUserProperties app) {
			foreach (var propDef in app.UserProperties) {
				if (propDef.Id == 0) propDef.Id = nextPropertyDefinitionId++;
			}
		}
	}
}
