using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Utilities {

	/// <summary>
	/// A base class for command classes that run in the service container and need to be able to consume scoped DI services.
	/// It is intended for finite operations that run in their own application host together with their dependencies and terminate the host when they terminate the host when they are doen.
	/// It thus extends the functionality of <see cref="IScopedBackgroundService"/> by a mechanism to pass a result out of the command and by cleanly shutting down the host after the command has is finished.
	/// Command logic needs to be provided by a deriving class by implementing <see cref="RunAsync(CancellationToken)"/>.
	///
	/// The result is provided through the <see cref="ServiceResultWrapper{TValue}"/> passed to the constructor.
	/// Deriving classes should have a <see cref="ServiceResultWrapper{TService, TValue}"/> constructor-injected by the DI container and pass that to the base class constructor.
	/// </summary>
	/// <typeparam name="TResult">The type of the result of the command to pass back to the surrounding programm.</typeparam>
	/// <remarks>
	/// The surrounding programm should perform the following steps to run the command:
	/// <list type="number">
	/// <item><description>Call <c>Host.CreateDefaultBuilder()</c>.</description></item>
	/// <item><description>Optional: Apply <c>.UseConsoleLifetime()</c>.</description></item>
	/// <item><description>Configure logging, dependency services, etc. in the DI container, including options and parameters for the command, to be injected in its constructor.</description></item>
	/// <item><description>
	/// Instantiate a <see cref="ServiceResultWrapper{TService, TValue}"/> with
	/// <c>TService</c> being the command implementation class derived from this class and with
	/// <c>TValue</c> being the result type to return from the command.
	/// For a typical console application <c>TValue</c> can be <c>int</c> to pass back an exit code.
	/// This instance will be called <c>result</c> below.
	/// </description></item>
	/// <item><description><c>.AddSingleton(result)</c> into the DI container.</description></item>
	/// <item><description><c><![CDATA[.AddScopedBackgroundService<TheCommandClass>()]]></c> the command implementation class into the DI container.</description></item>
	/// <item><description>Call <c>.Build()</c> on the host builder to create an <see cref="IHost"/> instance <c>host</c>.</description></item>
	/// <item><description><c>await host.RunAsync();</c></description></item>
	/// <item><description>Take the command result out of <c>result</c>. In the case of an exit code, return it from <c>Main</c>.</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// Defining the command:
	/// <code><![CDATA[
	/// class MyCommandOptions{
	/// 	public string Text { get; set; }
	/// }
	///
	/// class MyCommand : CommandService<int> {
	/// 	private MyCommandOptions opts;
	/// 	private ILogger<MyCommand> logger;
	///
	/// 	public MyCommand(IHost host, ServiceResultWrapper<MyCommand, int> result, MyCommandOptions opts, ILogger<MyCommand> logger) : base(host, result) {
	/// 		this.opts = opts;
	/// 		this.logger = logger;
	/// 	}
	///
	/// 	protected override async Task<int> RunAsync(CancellationToken ct) {
	/// 		logger.LogInformation("Hello from MyCommand: {Text}", opts.Text);
	/// 		return 0;
	/// 	}
	///
	/// 	protected override int ResultForUncaughtException(Exception ex) => 1;
	/// }
	/// ]]></code>
	/// Calling it in the surounding program:
	/// <code><![CDATA[
	/// static IHostBuilder CreateHostBuilder(MyCommandOptions opts, ServiceResultWrapper<MyCommand, int> result) =>
	///		 Host.CreateDefaultBuilder()
	///				.UseConsoleLifetime(options => options.SuppressStatusMessages = true)
	///				.ConfigureAppConfiguration(config => {
	///					// put application config here.
	///				})
	///				.ConfigureServices((context, services) => {
	///					// add other dependencies of command to services here
	///					services.AddSingleton(opts); // Add options for the command.
	///					services.AddSingleton(result); // Add result.
	///					services.AddScopedBackgroundService<MyCommand>(); // Add command class.
	///				});
	///
	///	async static Task<int> Main(string[] args) {
	///			MyCommandOptions opts;
	///			// Fill opts from args here.
	///			ServiceResultWrapper<MyCommand, int> result = new(0);
	///			using var host = CreateHostBuilder(opts, result).Build();
	///			await host.RunAsync();
	///			return result.Result;
	///	}
	/// ]]></code>
	/// </example>
	public abstract class CommandService<TResult> : IScopedBackgroundService {
		private IHost host;
		private ServiceResultWrapper<TResult> resultWrapper;

		/// <summary>
		/// Accepts the required injected object from the derived class' constructor.
		/// </summary>
		/// <param name="host">A reference to the application host that will run the command.</param>
		/// <param name="resultWrapper">A wrapper to provide the result in.
		/// For better type safety, derived classes should take a <see cref="ServiceResultWrapper{TService, TValue}"/> where <c>TService</c> is the derived class, and pass it here.</param>
		protected CommandService(IHost host, ServiceResultWrapper<TResult> resultWrapper) {
			this.host = host;
			this.resultWrapper = resultWrapper;
		}

		async Task IScopedBackgroundService.ExecuteAsync(CancellationToken stoppingToken) {
			await Task.Yield();
			try {
				var result = await RunAsync(stoppingToken);
				resultWrapper.Result = result;
			}
			catch (Exception ex) {
				resultWrapper.Result = ResultForUncaughtException(ex);
			}
			_ = host.StopAsync();
		}

		/// <summary>
		/// Invokes asynchronous execution of the actual operation of the command.
		/// </summary>
		/// <param name="ct">A cancellation token that is triggered when the application host is performing a graceful shutdown.</param>
		/// <returns>A task representing the command's operation, providing the command's result as its result upon successful completion.</returns>
		protected abstract Task<TResult> RunAsync(CancellationToken ct);

		/// <summary>
		/// Is called if <see cref="RunAsync(CancellationToken)"/> results in an exception, i.e. if an uncaught exception escapes from <see cref="RunAsync(CancellationToken)"/>.
		/// Its purpose is to determine to what value to set the result in this case.
		/// </summary>
		/// <param name="ex">The uncaught exception from <see cref="RunAsync(CancellationToken)"/>.</param>
		/// <returns>The value to set into the result wrapper under the given error condition.</returns>
		protected abstract TResult ResultForUncaughtException(Exception ex);
	}
}
