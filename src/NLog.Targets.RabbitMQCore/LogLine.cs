// Copyright 2012 Henrik Feldt

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NLog.Targets
{
	public class LogLine
	{
		[JsonProperty("@source")]
		public Uri Source { get; set; }

		[JsonProperty("@timestamp")]
		public string TimeStampISO8601 { get; set; }

		[JsonProperty("@message")]
		public string Message { get; set; }

		[JsonProperty("@fields")]
		public IDictionary<string, object> Fields { get; set; }

		[JsonProperty("@tags")]
		public HashSet<string> Tags { get; set; }

		[JsonProperty("@type")]
		public string Type { get; set; }

		[JsonProperty("level")]
		public string Level { get; set; }
	}

	public static class LogLineEx
	{
		/// <summary>Makes sure the defaults are there</summary>
		public static void EnsureADT(this LogLine line)
		{
			if (line.Fields == null)
				line.Fields = new Dictionary<string, object>();

			if (line.Tags == null)
				line.Tags = new HashSet<string>();
		}

		public static void AddField(
			this LogLine line,
			string name, object value)
		{
			if (value == null)
				return;

			if (line.Fields == null)
				line.Fields = new Dictionary<string, object>();

			line.Fields.Add(name, value);
		}

		public static void AddTag(this LogLine line, string tag)
		{
			if (tag == null)
				return;

			if (line.Tags == null)
				line.Tags = new HashSet<string> { tag };
			else
				line.Tags.Add(tag);
		}
	}
}