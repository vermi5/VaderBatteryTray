using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VaderBatteryTray
{
    internal sealed class SharedBatteryState
    {
        private readonly object sync = new object();
        private BatterySnapshot snapshot;
        private DateTime publishedUtc;
        private string dockState = "unknown";
        private DateTime dockStateObservedUtc;
        private long dockStateSequence;
        private string dockStateSource;
        private byte? dockStateRawField9;
        private bool dockStateActiveSessionObserved;
        private bool dockControllerConnected;
        private DateTime dockControllerConnectedObservedUtc;
        private long dockControllerConnectedSequence;
        private string dockControllerConnectedSource;

        public void Publish(BatterySnapshot value)
        {
            lock (sync)
            {
                snapshot = value;
                publishedUtc = DateTime.UtcNow;
            }
        }

        // This is deliberately independent from the battery snapshot. The Dock
        // watcher can therefore publish physical-presence evidence immediately,
        // without starting a controller GET_INFO refresh.
        public void ObserveDockState(
            string state,
            string source,
            DateTime observedUtc,
            byte? rawField9,
            bool activeSessionObserved,
            bool controllerConnected)
        {
            if (!String.Equals(state, "docked", StringComparison.Ordinal) &&
                !String.Equals(state, "undocked", StringComparison.Ordinal) &&
                !String.Equals(state, "unknown", StringComparison.Ordinal))
            {
                state = "unknown";
            }

            DateTime utc = observedUtc == DateTime.MinValue
                ? DateTime.UtcNow
                : observedUtc.ToUniversalTime();
            lock (sync)
            {
                if (!String.Equals(dockState, state, StringComparison.Ordinal))
                {
                    dockState = state;
                    unchecked
                    {
                        dockStateSequence++;
                    }
                }

                dockStateObservedUtc = utc;
                dockStateSource = source;
                dockStateRawField9 = rawField9;
                dockStateActiveSessionObserved = activeSessionObserved;

                if (dockControllerConnected != controllerConnected)
                {
                    dockControllerConnected = controllerConnected;
                    unchecked
                    {
                        dockControllerConnectedSequence++;
                    }
                }
                dockControllerConnectedObservedUtc = utc;
                dockControllerConnectedSource =
                    "dock-ef-activity-is-controller-connected";
            }
        }

        public string GetJson()
        {
            lock (sync)
            {
                return BatterySnapshotJson.Serialize(
                    snapshot,
                    publishedUtc,
                    dockState,
                    dockStateObservedUtc,
                    dockStateSequence,
                    dockStateSource,
                    dockStateRawField9,
                    dockStateActiveSessionObserved,
                    dockControllerConnected,
                    dockControllerConnectedObservedUtc,
                    dockControllerConnectedSequence,
                    dockControllerConnectedSource);
            }
        }
    }

    internal static class BatterySnapshotJson
    {
        public static string Serialize(
            BatterySnapshot snapshot,
            DateTime publishedUtc,
            string dockState,
            DateTime dockStateObservedUtc,
            long dockStateSequence,
            string dockStateSource,
            byte? dockStateRawField9,
            bool dockStateActiveSessionObserved,
            bool dockControllerConnected,
            DateTime dockControllerConnectedObservedUtc,
            long dockControllerConnectedSequence,
            string dockControllerConnectedSource)
        {
            StringBuilder json = new StringBuilder();
            json.Append("{");
            AppendNumber(json, "schemaVersion", 5, false);
            AppendString(json, "controller", "Flydigi Vader 5 Pro", true);

            if (snapshot == null)
            {
                AppendString(json, "status", "starting", true);
                AppendBoolean(json, "connected", false, true);
                AppendBoolean(json, "controllerPresent", false, true);
                AppendBoolean(json, "dockPresent", false, true);
                AppendBoolean(json, "batteryAvailable", false, true);
                AppendNumber(json, "bandLevel", 0, true);
                AppendString(json, "band", null, true);
                AppendBoolean(json, "charging", false, true);
                AppendBoolean(json, "dockControllerStateInferred", false, true);
                AppendString(json, "dockControllerState", null, true);
                AppendString(json, "power", null, true);
                AppendString(json, "connection", null, true);
                AppendString(json, "source", null, true);
                AppendString(json, "firmware", null, true);
                AppendString(json, "observedUtc", null, true);
                AppendString(json, "publishedUtc", null, true);
                AppendDockState(json, dockState, dockStateObservedUtc, dockStateSequence, dockStateSource, dockStateRawField9, dockStateActiveSessionObserved, dockControllerConnected, dockControllerConnectedObservedUtc, dockControllerConnectedSequence, dockControllerConnectedSource);
                AppendString(json, "error", null, true);
                json.Append("}");
                return json.ToString();
            }

            string status = snapshot.HasBattery || snapshot.HasBatteryBand
                ? "ok"
                : (!snapshot.ControllerInterfacePresent && snapshot.DockInterfacePresent
                    ? "controller-unavailable"
                    : (!snapshot.ControllerInterfacePresent && !snapshot.DockInterfacePresent
                        ? "receiver-disconnected"
                        : "unavailable"));
            DateTime observedUtc = snapshot.UtcObservationTimestamp;
            string observedText = observedUtc == DateTime.MinValue
                ? null
                : observedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            string publishedText = publishedUtc == DateTime.MinValue
                ? null
                : publishedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

            AppendString(json, "status", status, true);
            AppendBoolean(json, "connected", snapshot.InterfacePresent, true);
            AppendBoolean(json, "controllerPresent", snapshot.ControllerInterfacePresent, true);
            AppendBoolean(json, "dockPresent", snapshot.DockInterfacePresent, true);
            AppendBoolean(json, "batteryAvailable", snapshot.HasBattery || snapshot.HasBatteryBand, true);
            AppendNumber(json, "bandLevel", snapshot.BandLevel, true);
            AppendString(json, "band", EmptyToNull(snapshot.BandText), true);
            AppendBoolean(json, "charging", snapshot.IsCharging, true);
            AppendBoolean(
                json,
                "dockControllerStateInferred",
                snapshot.IsDockChargeSleep,
                true);
            AppendString(
                json,
                "dockControllerState",
                snapshot.IsDockChargeSleep ? "charge-sleep" : "normal",
                true);
            AppendString(json, "power", EmptyToNull(snapshot.PowerText), true);
            AppendString(json, "connection", EmptyToNull(snapshot.ConnectionText), true);
            AppendString(json, "source", snapshot.DataSource == BatteryDataSource.Unknown ? null : snapshot.DataSource.ToString(), true);
            AppendString(json, "firmware", EmptyToNull(snapshot.Firmware), true);
            AppendString(json, "observedUtc", observedText, true);
            AppendString(json, "publishedUtc", publishedText, true);
            AppendDockState(json, dockState, dockStateObservedUtc, dockStateSequence, dockStateSource, dockStateRawField9, dockStateActiveSessionObserved, dockControllerConnected, dockControllerConnectedObservedUtc, dockControllerConnectedSequence, dockControllerConnectedSource);
            AppendString(json, "error", EmptyToNull(snapshot.Error), true);
            json.Append("}");
            return json.ToString();
        }

        private static void AppendDockState(
            StringBuilder json,
            string dockState,
            DateTime dockStateObservedUtc,
            long dockStateSequence,
            string dockStateSource,
            byte? dockStateRawField9,
            bool dockStateActiveSessionObserved,
            bool dockControllerConnected,
            DateTime dockControllerConnectedObservedUtc,
            long dockControllerConnectedSequence,
            string dockControllerConnectedSource)
        {
            string observedText = dockStateObservedUtc == DateTime.MinValue
                ? null
                : dockStateObservedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            AppendString(json, "dockState", EmptyToNull(dockState) ?? "unknown", true);
            AppendString(json, "dockStateObservedUtc", observedText, true);
            AppendNumber(json, "dockStateSequence", dockStateSequence, true);
            AppendString(json, "dockStateSource", EmptyToNull(dockStateSource), true);
            AppendNullableNumber(json, "dockStateRawField9", dockStateRawField9, true);
            AppendBoolean(
                json,
                "dockStateActiveSessionObserved",
                dockStateActiveSessionObserved,
                true);
            string controllerConnectedObservedText =
                dockControllerConnectedObservedUtc == DateTime.MinValue
                    ? null
                    : dockControllerConnectedObservedUtc.ToUniversalTime().ToString(
                        "o",
                        CultureInfo.InvariantCulture);
            AppendBoolean(json, "dockControllerConnected", dockControllerConnected, true);
            AppendString(
                json,
                "dockControllerConnectedObservedUtc",
                controllerConnectedObservedText,
                true);
            AppendNumber(
                json,
                "dockControllerConnectedSequence",
                dockControllerConnectedSequence,
                true);
            AppendString(
                json,
                "dockControllerConnectedSource",
                EmptyToNull(dockControllerConnectedSource),
                true);
        }

        private static string EmptyToNull(string value)
        {
            return String.IsNullOrEmpty(value) ? null : value;
        }

        private static void AppendString(StringBuilder json, string name, string value, bool comma)
        {
            AppendName(json, name, comma);
            if (value == null)
            {
                json.Append("null");
                return;
            }

            json.Append('"');
            AppendEscaped(json, value);
            json.Append('"');
        }

        private static void AppendBoolean(StringBuilder json, string name, bool value, bool comma)
        {
            AppendName(json, name, comma);
            json.Append(value ? "true" : "false");
        }

        private static void AppendNumber(StringBuilder json, string name, int value, bool comma)
        {
            AppendName(json, name, comma);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendNumber(StringBuilder json, string name, long value, bool comma)
        {
            AppendName(json, name, comma);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendNullableNumber(
            StringBuilder json,
            string name,
            byte? value,
            bool comma)
        {
            AppendName(json, name, comma);
            if (!value.HasValue)
            {
                json.Append("null");
                return;
            }
            json.Append(value.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendName(StringBuilder json, string name, bool comma)
        {
            if (comma)
            {
                json.Append(',');
            }
            json.Append('"');
            json.Append(name);
            json.Append("\":");
        }

        private static void AppendEscaped(StringBuilder json, string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '"':
                        json.Append("\\\"");
                        break;
                    case '\\':
                        json.Append("\\\\");
                        break;
                    case '\b':
                        json.Append("\\b");
                        break;
                    case '\f':
                        json.Append("\\f");
                        break;
                    case '\n':
                        json.Append("\\n");
                        break;
                    case '\r':
                        json.Append("\\r");
                        break;
                    case '\t':
                        json.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            json.Append("\\u");
                            json.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            json.Append(character);
                        }
                        break;
                }
            }
        }
    }

    internal sealed class RainmeterBridge : IDisposable
    {
        public const int Port = 42115;
        public const string StateUrl = "http://127.0.0.1:42115/api/v1/state";

        private readonly object sync = new object();
        private readonly SharedBatteryState state;
        private readonly Action requestRefresh;
        private TcpListener listener;
        private Thread thread;
        private bool disposed;
        private string startError;
        private DateTime lastRefreshRequestUtc = DateTime.MinValue;

        public RainmeterBridge(SharedBatteryState state, Action requestRefresh)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
            this.state = state;
            this.requestRefresh = requestRefresh;
        }

        public bool IsRunning
        {
            get
            {
                lock (sync)
                {
                    return listener != null && startError == null && !disposed;
                }
            }
        }

        public string StartError
        {
            get
            {
                lock (sync)
                {
                    return startError;
                }
            }
        }

        public void Start()
        {
            lock (sync)
            {
                if (disposed || listener != null || startError != null)
                {
                    return;
                }

                try
                {
                    listener = new TcpListener(IPAddress.Loopback, Port);
                    listener.Start();
                    thread = new Thread(Run);
                    thread.IsBackground = true;
                    thread.Name = "Vader Rainmeter bridge";
                    thread.Start();
                }
                catch (Exception ex)
                {
                    startError = ex.Message;
                    if (listener != null)
                    {
                        listener.Stop();
                        listener = null;
                    }
                }
            }
        }

        private void Run()
        {
            while (true)
            {
                TcpListener activeListener;
                lock (sync)
                {
                    if (disposed || listener == null)
                    {
                        return;
                    }
                    activeListener = listener;
                }

                try
                {
                    using (TcpClient client = activeListener.AcceptTcpClient())
                    {
                        HandleClient(client);
                    }
                }
                catch (SocketException)
                {
                    lock (sync)
                    {
                        if (disposed || listener == null)
                        {
                            return;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch
                {
                    // A malformed local request must not terminate the bridge.
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            client.ReceiveTimeout = 2000;
            client.SendTimeout = 2000;

            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
            {
                string requestLine = reader.ReadLine();
                if (String.IsNullOrEmpty(requestLine))
                {
                    return;
                }

                for (int headerIndex = 0; headerIndex < 32; headerIndex++)
                {
                    string header = reader.ReadLine();
                    if (String.IsNullOrEmpty(header))
                    {
                        break;
                    }
                }

                string[] parts = requestLine.Split(' ');
                if (parts.Length < 2)
                {
                    WriteResponse(stream, 400, "Bad Request", "text/plain; charset=utf-8", "Bad request");
                    return;
                }

                string method = parts[0];
                string path = parts[1];
                int queryIndex = path.IndexOf('?');
                if (queryIndex >= 0)
                {
                    path = path.Substring(0, queryIndex);
                }

                if (!String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(stream, 405, "Method Not Allowed", "text/plain; charset=utf-8", "Only GET is supported");
                    return;
                }

                if (String.Equals(path, "/api/v1/state", StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(stream, 200, "OK", "application/json; charset=utf-8", state.GetJson());
                    return;
                }

                if (String.Equals(path, "/api/v1/command/refresh", StringComparison.OrdinalIgnoreCase))
                {
                    bool accepted = TryRequestRefresh();
                    WriteResponse(
                        stream,
                        accepted ? 202 : 429,
                        accepted ? "Accepted" : "Too Many Requests",
                        "application/json; charset=utf-8",
                        accepted ? "{\"accepted\":true}" : "{\"accepted\":false}");
                    return;
                }

                if (String.Equals(path, "/api/v1/health", StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(stream, 200, "OK", "application/json; charset=utf-8", "{\"status\":\"ok\"}");
                    return;
                }

                WriteResponse(stream, 404, "Not Found", "text/plain; charset=utf-8", "Not found");
            }
        }

        private bool TryRequestRefresh()
        {
            Action callback;
            lock (sync)
            {
                DateTime now = DateTime.UtcNow;
                if (requestRefresh == null ||
                    now - lastRefreshRequestUtc < TimeSpan.FromSeconds(2))
                {
                    return false;
                }

                lastRefreshRequestUtc = now;
                callback = requestRefresh;
            }

            try
            {
                callback();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteResponse(
            NetworkStream stream,
            int statusCode,
            string statusText,
            string contentType,
            string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string headers =
                "HTTP/1.1 " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + statusText + "\r\n" +
                "Content-Type: " + contentType + "\r\n" +
                "Content-Length: " + bodyBytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "Cache-Control: no-store\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }

        public void Dispose()
        {
            TcpListener activeListener;
            Thread activeThread;
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                activeListener = listener;
                activeThread = thread;
                listener = null;
                thread = null;
            }

            if (activeListener != null)
            {
                activeListener.Stop();
            }
            if (activeThread != null && activeThread != Thread.CurrentThread)
            {
                activeThread.Join(1000);
            }
        }
    }
}
