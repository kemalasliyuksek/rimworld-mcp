namespace RimWorldMcp.Bridge.Core
{
    public sealed class BridgeResponse
    {
        public int StatusCode { get; }

        public string Body { get; }

        public BridgeResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body;
        }

        public static BridgeResponse Error(int statusCode, string code, string message)
        {
            var json = new JsonWriter();
            json.BeginObject();
            json.Key("error").BeginObject();
            json.Prop("code", code);
            json.Prop("message", message);
            json.EndObject();
            json.EndObject();
            return new BridgeResponse(statusCode, json.ToString());
        }
    }
}
