using System.Runtime.InteropServices;
using System.Text;

using CoreServices;
using ObjCRuntime;

using Marille.Workers;
using Mono.Options;
using Serilog;
using UuidExtensions;

namespace Marille;

static class MainClass {
	#region Fields

	public static int Verbosity { get; set; } = 1;
	public static bool ShowHelp { get; set; } = false;
	
	public static List<string> Paths { get; } = new ();
	
	#endregion

	#region Helpers
	
	static void PrintHelp (OptionSet options)
	{
		Console.WriteLine ("Usage: fsevents [OPTIONS]+ message");
		Console.WriteLine ("Example of a file system watcher using Marille.");
		Console.WriteLine ();
		Console.WriteLine ("Options:");
		options.WriteOptionDescriptions (Console.Out);
	}
	
	static void InitializeLog ()
	{
		LoggerConfiguration logConfiguration = new LoggerConfiguration ()
			.Enrich.WithThreadId ()
			.Enrich.WithThreadName ();
		
		// cast verbosity to a log level and set it as the minimum level
		var minLevel = (LogLevel) Verbosity;
		
		switch (minLevel) {
		case LogLevel.Fatal:
			logConfiguration.MinimumLevel.Fatal ();
			break;
		case LogLevel.Error:
			logConfiguration.MinimumLevel.Error ();
			break;
		case LogLevel.Information:
			logConfiguration.MinimumLevel.Information ();
			break;
		case LogLevel.Debug:
			logConfiguration.MinimumLevel.Debug ();
			break;
		default:
			logConfiguration.MinimumLevel.Information ();
			break;
		}
		
		logConfiguration.MinimumLevel.Debug ();

		// thread id == min level of log
		Log.Logger = logConfiguration
			.WriteTo.Console (outputTemplate: "{Timestamp:HH:mm:ss} [{Level}] ({ThreadId}) {Message}{NewLine}{Exception}")
			.CreateLogger ();
	}
	
	#endregion

	static int Main (string [] args)
	{
		try {
			Console.OutputEncoding = new UTF8Encoding (false, false);
			NSApplication.Init ();
			Environment.CurrentDirectory = Runtime.OriginalWorkingDirectory!;
			return Main2 (args);
		} catch (Exception e) {
			Console.WriteLine (e);
			Exit (1);
		}
		return 1;
	}

	[DllImport ("/usr/lib/libSystem.dylib")]
	static extern void exit (int exitcode);
	public static void Exit (int exitCode = 0)
	{
		// This is ugly. *Very* ugly. The problem is that:
		//
		// * The Apple API we use will create background threads to process messages.
		// * Those messages are delivered to managed code, which means the mono runtime
		//   starts tracking those threads.
		// * The mono runtime will not terminate those threads (in fact the mono runtime
		//   will do nothing at all to those threads, since they're not running managed
		//   code) upon shutdown. But the mono runtime will wait for those threads to
		//   exit before exiting the process. This means mtouch will never exit.
		// 
		// So just go the nuclear route.
		exit (exitCode);
	}

	static int Main2 (string [] args)
	{
		// Use mono.options to parse the args, this is a sample so we won't do too complex things
		var os = new OptionSet () {
			{ "h|?|help", "Displays the help", v => ShowHelp = v != null },
			{ "v", "Verbose", v => Verbosity++ },
			{ "q", "Quiet", v => Verbosity = 0 },
			{ "p|path=", "Add a path to monitor", v => Paths.Add (v) },
		};
		
		try {
			var extra = os.Parse (args);
		} catch (Exception e) {
			// We could not parse the argumets, print the error and suggest to call help
			Console.WriteLine("fsevents:");
			Console.WriteLine (e.Message);
			Console.WriteLine ("Try `fsevents --help' for more information.");
			return 1;
		}

		if (ShowHelp) {
			PrintHelp (os);
			return 0;
		}

		// Add logging so that we can see what is going on
		InitializeLog ();
		
		// create the app data dir in which we will store the diffs
		var baseDir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "Marille", "Diffs");
		Directory.CreateDirectory (baseDir);
		Log.Information ("Created base directory {BaseDir}", baseDir);
		
		// we are going to create a uuid7 for the new snapshot. uuid7 is time shortable and unique so that way
		// we will be able to read see the snapshot and the diff in the future.
		var snapshot = SnapshotManager.Create (baseDir, Paths);

		Log.Information ("Starting fsevents with {Paths}", Paths);

		// we need to create several things to be able to get the events:
		// 1. Hub: will be used to deliver the events to the consumers.
		// 2. Worker: will be used to process the events.
		// 3. FSMonitor: will be used to monitor the file system and deliver the events to the hub.
		var hub = new Hub ();
		
		// because we are going to start a main loop in the main thread, we need the channel initialization to be
		// done in a background thread else we will be blocked.
		Task.Run (async () => {
			// workers will be disposed by the hub
			//var worker = new LogEventToConsole ();
			var eventFilter = new EventFilterer (hub);
			var diffGenerator = new DiffGenerator (snapshot);
			
			var fsEventsErrorHandler = new FSEventsErrorHandler ();
			var textFileChangedErrorHandler = new TextFileChangedErrorHandler ();
			
			var fsEventsConfig = new TopicConfiguration {
				Mode = ChannelDeliveryMode.AtLeastOnceSync
			};
			var txtEventsConfig = new TopicConfiguration {
				Mode = ChannelDeliveryMode.AtMostOnceAsync
			};
			await hub.CreateAsync (nameof (FSMonitor), txtEventsConfig, textFileChangedErrorHandler, diffGenerator);
			await hub.CreateAsync (nameof (FSMonitor), fsEventsConfig, fsEventsErrorHandler, eventFilter);
			Log.Information ("Channels created");
			Console.WriteLine ("Feel free to edit your files, we will keep a history of the edits in the session!!!");
			Console.WriteLine ("Press Ctrl+C to stop the watcher.");
		});

		var monitor = new FSMonitor (Paths, hub, FSEventStreamCreateFlags.FileEvents | FSEventStreamCreateFlags.WatchRoot);
		monitor.ScheduleWithRunLoop (NSRunLoop.Current);
		Log.Information ("FSEvent monitor scheduled");
		monitor.Start ();
		Console.WriteLine($"Starting watch on {string.Join (',', Paths)}");
		NSRunLoop.Main.Run ();

		return 0;
	}
}
