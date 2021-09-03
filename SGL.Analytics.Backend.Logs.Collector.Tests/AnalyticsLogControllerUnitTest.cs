using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.LogCollector.Controllers;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Services;
using SGL.Analytics.DTO;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Collector.Tests {
	public class AnalyticsLogControllerUnitTest {
		private readonly ITestOutputHelper output;
		private DummyLogManager logManager = new DummyLogManager();
		private ILoggerFactory loggerFactory;
		private AnalyticsLogController controller;
		private string apiToken = StringGenerator.GenerateRandomWord(32);

		public AnalyticsLogControllerUnitTest(ITestOutputHelper output) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			logManager.Apps.Add(nameof(AnalyticsLogControllerUnitTest), new Domain.Entity.Application(Guid.NewGuid(), nameof(AnalyticsLogControllerUnitTest), apiToken));
			controller = new AnalyticsLogController(logManager, logManager, loggerFactory.CreateLogger<AnalyticsLogController>());
		}

	}
}
