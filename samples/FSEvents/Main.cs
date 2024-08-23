using System.Runtime.InteropServices;
using System.Text;

using CoreServices;
using ObjCRuntime;

using Marille.Workers;
using Serilog;

namespace Marille;

static class MainClass {

	#region Helpers
	static void InitializeLog ()
	{
		LoggerConfiguration logConfiguration = new LoggerConfiguration ()
			.Enrich.WithThreadId ()
			.Enrich.WithThreadName ();

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
		// Add logging so that we can see what is going on
		InitializeLog ();

		// we need to create several things to be able to get the events:
		// 1. Hub: will be used to deliver the events to the consumers.
		// 2. Worker: will be used to process the events.
		// 3. FSMonitor: will be used to monitor the file system and deliver the events to the hub.
		using var _hub = new Hub ();
		
		// because we are going to start a main loop in the main thread, we need the channel initialization to be
		// done in a background thread else we will be blocked.
		Task.Run (async () => {
			// workers will be disposed by the hub
			//var worker = new LogEventToConsole ();
			var eventFilter = new EventFilterer (_hub);
			var diffGenerator = new DiffGenerator ();
			
			var fsEventsErrorHandler = new FSEventsErrorHandler ();
			var textfileErrorHandler = new TextFileChangedErrorHandler ();
			
			var fsEventsConfig = new TopicConfiguration {
				Mode = ChannelDeliveryMode.AtLeastOnceSync
			};
			var txtEventsConfig = new TopicConfiguration {
				Mode = ChannelDeliveryMode.AtMostOnceAsync
			};
			await _hub.CreateAsync (nameof (FSMonitor), txtEventsConfig, textfileErrorHandler, diffGenerator);
			await _hub.CreateAsync (nameof (FSMonitor), fsEventsConfig, fsEventsErrorHandler, eventFilter);
			Console.WriteLine ("Channel created");
		});

		var monitor = new FSMonitor ("/Users/mandel/Xamarin", _hub, FSEventStreamCreateFlags.FileEvents | FSEventStreamCreateFlags.WatchRoot);
		monitor.ScheduleWithRunLoop (NSRunLoop.Current);
		Console.WriteLine ("FSEvent monitor scheduled");
		monitor.Start ();
		NSRunLoop.Main.Run ();

		return 0;
	}
}
