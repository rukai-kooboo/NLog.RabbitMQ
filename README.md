# RabbitMQ target for NLog
 
The RabbitMQ target writes asynchronously to a RabbitMQ instance. Either use this repo, ur get the nuget:

`Install-Package NLog.RabbitMQ`

You will find the listener tool in the nuget `tools` folder.

**For more future-proof logging I recommend
[Logary](https://github.com/logary/logary) which encapsulates many of the
lessons building this taught me and also has support for many more targets**

##Configuration:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
    xmlns:haf="https://github.com/haf/NLog.RabbitMQ/raw/master/src/schemas/NLog.RabbitMQ.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    internalLogToConsole="true">

  <extensions>
    <add assembly="NLog.Targets.RabbitMQ" />
  </extensions>

  <targets async="true">
    <!-- when http://nlog.codeplex.com/workitem/6491 is fixed, then xsi:type="haf:RabbitMQ" instead;
       these are the defaults (except 'topic', 'appid', and 'useJSON'): 
    -->
    <target name="RabbitMQTarget"
        xsi:type="RabbitMQ"
        username="guest" 
        password="guest" 
        hostname="localhost" 
        exchange="app-logging"
        port="5672"
        topic="DemoApp.Logging.{0}"
        vhost="/"
        durable="true"
        appid="NLog.RabbitMQ.DemoApp"
        maxBuffer="10240"
        heartBeatSeconds="3"
        useJSON="true"
        layout="${message}"
        compression="none"
        />
  </targets>

  <rules>
    <logger name="*" minlevel="Trace" writeTo="RabbitMQTarget"/>
  </rules>

</nlog>
```

Minimum target recommended:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
    xmlns:haf="https://github.com/haf/NLog.RabbitMQ/raw/master/src/schemas/NLog.RabbitMQ.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    internalLogToConsole="true">

  <extensions>
    <add assembly="NLog.Targets.RabbitMQ" />
  </extensions>

  <targets async="true">
    <!-- when http://nlog.codeplex.com/workitem/6491 is fixed, then xsi:type="haf:RabbitMQ" instead;
       these are the defaults (except 'topic' and 'appid'): 
    -->
    <target name="RabbitMQTarget"
        xsi:type="RabbitMQ"
        useJSON="true"
        layout="${message}"
        />
  </targets>

  <rules>
    <logger name="*" minlevel="Trace" writeTo="RabbitMQTarget"/>
  </rules>

</nlog>
```

Remember to mark your `NLog.config` file to be copied to the output directory!

**Recommendation - async wrapper target**

Make the targets tag look like this: `<targets async="true"> ... </targets>` so that
a failure of communication with RabbitMQ doesn't slow the application down. With this configuration
an overloaded message broker will have 10000 messages buffered in the logging application
before messages start being discarded. A downed message broker will have its messages
in the *inner* target (i.e. RabbitMQ-target), not in the async buffer (as the RabbitMQ-target
will not block which is what AsyncWrapperTarget buffers upon).

##Important - shutting it down!

Because NLog doesn't expose a single method for shutting everything down (but loads automatically by static properties - the loggers' first invocation to the framework) - you need to add this code to the exit of your application!

```csharp
var allTargets = LogManager.Configuration.AllTargets;

foreach (var target in allTargets)
  target.Dispose();
```

For an example of how to do this with WPF see the demo.

##Configuration schema

See https://github.com/haf/NLog.RabbitMQ/blob/master/src/schemas/NLog.RabbitMQ.xsd

## Value-Add - How to use with LogStash?

Make sure you are using the flag `useJSON='true'` in your configuration, then you [download logstash](http://logstash.net/)! Place it in a folder, and add a file that you call 'logstash.conf' next to it:

```
input {
  amqp {
    durable => true
    exchange => "app-logging"
    exclusive => false
    format => "json_event"
    host => "localhost"
    key => "#"
    name => ""
    passive => false
    password => "guest"
    port => 5672
    prefetch_count => 10
    ssl => false
    # tags => ... # array (optional)
    type => "nlog"
    user => "guest"
    verify_ssl => false
    vhost => "/"
  }
}

output {
  # Emit events to stdout for easy debugging of what is going through
  # logstash.
  stdout { }

  # This will use elasticsearch to store your logs.
  # The 'embedded' option will cause logstash to run the elasticsearch
  # server in the same process, so you don't have to worry about
  # how to download, configure, or run elasticsearch!
  elasticsearch { embedded => true }
}
```

You then start the monolithic logstash: `java -jar logstash-1.1.0-monolithic.jar agent -f logstash.conf -- web`.
Now you can surf to http://127.0.0.1:9292 and search your logs that you're generating using the DemoApp in this project.

You can now log from e.g. your web site like this:

```
var request = ((HttpApplication)sender).Request; // or HttpContext.Current.Request;
_logger.Log(LogLevel.Debug,
    string.Format("{0} {1}", request.HttpMethod, request.Path),
    tags: new[] {"requests"},
    fields: new Dictionary<string, object>
      {
        {"REMOTE_ADDR", request.ServerVariables["REMOTE_ADDR"] ?? ""},
        {"HTTP_USER_AGENT", request.ServerVariables["HTTP_USER_AGENT"] ?? ""}
      });
```

Added overloads to `NLog.Logger`: 

```
Logger.Log(level : LogLevel, message : String, parameters : object[], exception : Exception, tags : string[], fields : IDictionary<string, object>
Logger.{Fatal|Error|Warn|Info|Debug|Trace}Tag(message : String, tags : params string[])
```
