// Copyright 2012 Henrik Feldt

using System;
using System.Collections.Generic;

namespace NLog
{
	public static class LoggerExtensions
	{
		public static void FatalTag(this Logger logger, string message, params string[] tags)
		{
			Log(logger, LogLevel.Fatal, message, tags);
		}
		public static void ErrorTag(this Logger logger, string message, params string[] tags)
		{
			Log(logger, LogLevel.Error, message, tags);
		}
		public static void WarnTag(this Logger logger, string message, params string[] tags)
		{
			Log(logger, LogLevel.Warn, message, tags);
		}
		public static void InfoTag(this Logger logger, string message, params string[] tags)
		{
			Log(logger, LogLevel.Info, message, tags);
		}
		public static void DebugTag(this Logger logger, string message, params string[] tags)
		{
			Log(logger, LogLevel.Debug, message, tags);
		}
		public static void TraceTag(this Logger logger, string message, params string[] tags)
		{
			Log(logger, LogLevel.Trace, message, tags);
		}

		static void Log(Logger logger, LogLevel level, string message, string[] tags)
		{
			Log(logger, level, message, null, null, tags);
		}

		/// <summary>Log a message with a single field attached.</summary>
		/// <exception cref="ArgumentNullException">key == null || see <see cref="Logger.Log(NLog.LogEventInfo)"/></exception>
		public static void LogField(this Logger logger, LogLevel level, string message, string key, object value)
		{
			if (key == null) throw new ArgumentNullException("key");

			Log(logger, level, message, fields: new Dictionary<string, object>
				{
					{ key, value }
				});
		}

		/// <summary>Log a message with a single field attached.</summary>
		/// <exception cref="ArgumentNullException">key == null || see <see cref="Logger.Log(NLog.LogEventInfo)"/></exception>
		public static void LogFields<T>(this Logger logger, LogLevel level, string message, params Tuple<string, T>[] fields)
		{
			var dictionary = new Dictionary<string, object>(fields.Length);

			foreach (var tuple in fields)
			{
				if (tuple.Item1 == null)
					throw new ArgumentNullException("fields", string.Format("LogFields contains tuple with null key, for message '{0}'", message));

				dictionary.Add(tuple.Item1, tuple.Item2);
			}

			Log(logger, level, message, fields: dictionary);
		}

		public static void Log(this Logger logger,
			LogLevel level,
			string message,
			object[] parameters = null,
			Exception exception = null,
			string[] tags = null,
			IDictionary<string,object> fields = null)
		{
			if (message == null) return;

			var info = new LogEventInfo(level, logger.Name, null, message, parameters, exception);

			if (fields != null)
				info.Properties.Add("fields", fields);

			if (tags != null) 
				info.Properties.Add("tags", tags);
			
			logger.Log(typeof(LoggerExtensions), info);
		}
	}
}