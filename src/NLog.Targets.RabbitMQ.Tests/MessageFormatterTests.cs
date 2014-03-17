// Copyright 2012 Henrik Feldt

using System;
using System.Collections.Generic;
using System.Globalization;
using NLog.Layouts;
using NLog.Targets;
using NUnit.Framework;
using Newtonsoft.Json;

namespace NLog.RabbitMQ.Tests
{
	public class MessageFormatterTests
	{
		Layout l;
		LogEventInfo evt;
		IList<Field> fields = null;

		[SetUp]
		public void given()
		{
			l = "${message}";
			evt = new LogEventInfo(
				LogLevel.Debug,
				"MessageFormatterTests",
				CultureInfo.InvariantCulture,
				"Hello World",
				null,
				GenerateException());

			evt.Properties.Add("tags", new[] { "skurk:rånarligan" });
		}

		Exception GenerateException()
		{
			try
			{
				var a = 0;
				var c = 1/a;
			}
			catch (Exception e)
			{
				return e;
			}
			return null;
		}

		/// <summary><see cref="LogLine"/></summary>
		[Test]
		public void contains_iso8601_timestamp()
		{
			var res = MessageFormatter.GetMessageInner(true, l, evt, fields);
			Assert.That(res, Is.StringContaining(
				evt.TimeStamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)));
		}

		[Test]
		public void contains_message()
		{
			var json = LogLine();
			Assert.That(json.Message, Is.EqualTo("Hello World"));
		}

		LogLine LogLine()
		{
			var res = MessageFormatter.GetMessageInner(true, l, evt, fields);
			Console.WriteLine(res);
			var json = JsonConvert.DeserializeObject<LogLine>(res);
			return json;
		}

		[Test]
		public void contains_exception()
		{
			var res = MessageFormatter.GetMessageInner(true, l, evt, fields);
			Assert.That(res, Is.StringContaining(@"""exception"":"));
		}

		[Test]
		public void level_is_debug()
		{
			var json = LogLine();
			Assert.That(json.Level, Is.EqualTo("Debug"));
		}

		[Test]
		public void source_scheme_is_nlog()
		{
			var json = LogLine();
			Assert.That(json.Source.Scheme, Is.EqualTo("nlog"));
		}

		[Test]
		public void contains_tags()
		{
			var json = LogLine();
			CollectionAssert.AreEqual(new[]{"skurk:rånarligan"}, json.Tags);
		}
		
		[Test]
		public void contains_custom_properties()
		{
			evt.Properties.Add("requestId", "This request");
			var json = LogLine();
			Assert.AreEqual("This request", json.Fields["requestId"]);
		}
		
		[Test]
		public void ignores_custom_properties_with_non_string_keys()
		{
			evt.Properties.Add(42, "Some value");
			var json = LogLine();
			CollectionAssert.DoesNotContain(json.Fields.Values, "Some value");
		}

		[Test]
		public void custom_property_named_tags_is_ignored()
		{
			var json = LogLine();
			Assert.False(json.Fields.ContainsKey("tags"));
		} 

		[Test]
		public void custom_property_named_fields_is_ignored()
		{
			evt.Properties.Add("fields", new Dictionary<string, object>());
			var json = LogLine();
			Assert.False(json.Fields.ContainsKey("fields"));
		}

		[Test]
		public void config_defined_fields_is_present_in_json_output()
		{
			fields = new List<Field>() { new Field("fieldname", new SimpleLayout("hard coded value")) };

			var json = LogLine();
			Assert.True(json.Fields.ContainsKey("fieldname"));
		}

		[Test]
		public void config_defined_fields_is_layout_expanded()
		{
			fields = new List<Field>() { new Field("fieldname", new SimpleLayout("${message}")) };

			var json = LogLine();

			Assert.AreEqual(json.Message, json.Fields["fieldname"]);
		}

		[Test]
		public void config_defined_fields_is_overridden_with_runtime_properties()
		{
			string expected = "runtime value";

			fields = new List<Field>() { new Field("fieldname", new SimpleLayout("default value")) };
			evt.Properties.Add("fieldname", expected);

			var json = LogLine();

			Assert.AreEqual(expected, json.Fields["fieldname"]);
		}

	}
}