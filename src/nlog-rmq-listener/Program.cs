using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace NLog.RabbitMQ.Listener
{
	// SRP violated, I know, I know... But someone please think of the cohesion!!
	public class Program
	{
		public class ParsedOptions : CommandLineOptionsBase
		{
			[Option("h", "hostname", DefaultValue = "localhost", HelpText = "RabbitMQ server IP or DNS name. Default 'localhost'.")]
			public string HostName { get; set; }

			[Option("e", "exchange", DefaultValue = "app-logging", HelpText = "What exchange to bind the listener queue to. Default 'app-logging'.")]
			public string Exchange { get; set; }

			[Option("p", "password", DefaultValue = "guest", HelpText = "Password for authenticating. Default 'guest'.")]
			public string Password { get; set; }

			[Option("u", "username", DefaultValue = "guest", HelpText = "Username for authenticating. Default 'guest'.")]
			public string UserName { get; set; }

			[Option("v", "virtual-host", DefaultValue = "/", HelpText = "VHost to listen to. Default '/'.")]
			public string VHost { get; set; }

			[Option("j", "pretty-json", DefaultValue = false, HelpText = "Expect log output to be JSON and try to output it pretty. Default 'false'.")]
			public bool PrettyJSON { get; set; }

			[Option("b", "basic-props", DefaultValue = false, HelpText = "Also output the IBasicProperties data from the RMQ envelope. Default 'false'.")]
			public bool BasicProps { get; set; }

			[Option("d", "durable", DefaultValue = null, HelpText = 
				"Whether to create a durable RMQ queue; it will be there permanently " +
				"until you *close the listener*. Sample: " +
				"'-d mylistener'", MutuallyExclusiveSet = "Durabilities")]
			public string Durable { get; set; }

			[Option("D", "permanently-durable", DefaultValue = null, HelpText =
				"Whether to create a durable RMQ queue; it will be there permanently " +
				"until you **delete it manually** - WARNING - if you have a busy " +
				"broker you can sink your broker if you don't drain the queue. Sample: " +
				"'-D mylistener'", MutuallyExclusiveSet = "Durabilities")]
			public string PermDurable { get; set; }

			[Option("k", "routing-key", DefaultValue = "#", HelpText = "The routing key " +
				"which you wish to target your listener to. Default '#'.")]
			public string RoutingKey { get; set; }

			[Option("l", "level", DefaultValue = "Trace", HelpText = "The level with which you " +
				"wish to watch events as they come in. Default 'Trace', requires flag '-j'/'--pretty-json'.")]
			public string Level { get; set; }

			[Option(null, "file-out", DefaultValue = true, HelpText = "Whether to write to a " +
				"file as well as write to console. Default 'true'.")]
			public bool WriteToFile { get; set; }

			[HelpOption]
			public string GetUsage()
			{
				return @"
Usage: nlog-rmq-listener [options]
Description: Will create an adjacent file 'log.txt' and also output all log data to the console.

" + HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
			}

			public ConnectionFactory Factory
			{
				get
				{
					return new ConnectionFactory
						{
							HostName = HostName,
							UserName = UserName,
							Password = Password,
							VirtualHost = VHost,
							Protocol = Protocols.DefaultProtocol
						};
				}
			}

			/// <summary>
			/// Calls the callback with the selected queue-name (or an
			/// empty string, and a boolean specifying whether to make
			/// the queue a permanent queue [or otherwise to delete it afterwards]). 
			/// Eager evaluation of the callback, if it is not null.
			/// </summary>
			/// <returns>The name of the queue</returns>
			public string WithQueue(
				Func<string> transient,
				Action<string> duringProgram,
				Action<string> permanent)
			{
				var durable = Durable ?? PermDurable;
				var q = durable ?? "";

				if (durable == null)
					return transient();
				
				if (!string.IsNullOrEmpty(Durable))
					duringProgram(q);
				else
					permanent(q);

				return q;
			}
		}

		public static ParsedOptions ParseArgsToFactory(params string[] args)
		{
			var opts = new ParsedOptions();
			var parser = new CommandLineParser(new CommandLineParserSettings(true, true));

			if (!parser.ParseArguments(args, opts))
			{
				Console.WriteLine(opts.GetUsage());
				return null;
			}

			return opts;
		}

		private static readonly object _locker = new object();
		static volatile bool _stopping;

		private static void Main(string[] args)
		{
			var opts = ParseArgsToFactory(args);
			if (opts == null) return;
			Console.WriteLine("Hello.");
			Console.CancelKeyPress += Console_CancelKeyPress;
			var p = new Program();
			p.ReceiveLoop(opts);
		}

		static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			Console.WriteLine("CTRL+C received - exiting.");
			_stopping = true;
			e.Cancel = true; // let me kill myself instead of you killing me
		}

		void ReceiveLoop(ParsedOptions opts)
		{
			while (!_stopping)
			{
				try
				{
					while (!_stopping)
					{
						try
						{
							Console.WriteLine("Setting up connection...");
							using (var c = opts.Factory.CreateConnection())
							using (var m = c.CreateModel())
							{
								m.BasicQos(0u, 100, false); // http://www.rabbitmq.com/amqp-0-9-1-reference.html#basic.qos.prefetch-size

								var consumer = new QueueingBasicConsumer(m);

								m.ExchangeDeclarePassive(opts.Exchange);

								var turnOff = true;
								var q = opts.WithQueue(
									() =>
										{
											turnOff = false; // does itself after - below - seconds
											return m.QueueDeclare("", false, false, false, new Dictionary<string, object>
												{
													{"x-expires", 10*60000} // expire queue after 30 minutes, see http://www.rabbitmq.com/extensions.html
												});
										},
									qn =>
										{
											Console.WriteLine("Declaring queue for this program's life-time. If this computer crashes, you need to manually delete it.");
											m.QueueDeclare(qn, true, false, false, null);
										},
									qn =>
										{
											Console.WriteLine("Declaring permanent queue. (WARNING - read the help unless you know what you do)");
											m.QueueDeclare(qn, true, false, false, null);
											turnOff = false;
										});

								m.QueueBind(q, opts.Exchange, opts.RoutingKey);
								m.BasicConsume(q, true, consumer);

								Console.WriteLine("Entering print loop...");
								PrintLoop(consumer, opts);

								if (turnOff)
								{
									Console.WriteLine("Deleting queue...");
									m.QueueDelete(q);
								}

								Console.WriteLine("Shutting down connection...");
							}
						}
						catch (BrokerUnreachableException)
						{
							Console.WriteLine("Could not connect, retrying...");
							Thread.Sleep(1000);
						}
					}
				}
				// sometimes this happens if rabbit is shut down
				catch (EndOfStreamException)
				{
					Console.WriteLine("RabbitMQ went down, re-connecting...");
				}
				// this happens most of the time
				catch (IOException)
				{
					Console.WriteLine("RabbitMQ went down ungracefully, re-connecting...");
				}
				// this happens when one kills erlang (killall -9 erl)
				catch (OperationInterruptedException)
				{
					Console.WriteLine(String.Format("Yet another one of RabbitMQ's failure modes - re-connecting... (you might not have the {0} exchange in your broker)",
						opts.Exchange));
				}
			}
		}

		void PrintLoop(QueueingBasicConsumer consumer, ParsedOptions opts)
		{
			while (!_stopping)
			{
				BasicDeliverEventArgs tmp;

				if (!consumer.Queue.Dequeue(200, out tmp))
					continue;

			    if (opts.BasicProps)
					DebugUtil.DumpProperties(tmp.BasicProperties, Console.Out, 1);

				string output,
					   formatted,
					   input = tmp.Body.AsUtf8String();

				if (opts.PrettyJSON)
				{
					if (!LevelFilter.ShouldPrint(input, LogLevel.FromString(opts.Level), out formatted))
						continue;

					output = formatted;
				}
					
				else output = input;

				Console.WriteLine(output);

				if (opts.WriteToFile) WriteToFile(output);
			}
		}

		private static void WriteToFile(string body)
		{
			lock (_locker)
				File.AppendAllLines("log.txt", new[] {body});
		}
	}

	static class Extensions
	{
		public static string AsUtf8String(this byte[] args)
		{
			return Encoding.UTF8.GetString(args);
		}
	}
}
