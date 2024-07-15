using Newtonsoft.Json;

namespace WalletConnectSharp.Core.Models.Expirer
{
    /// <summary>
    /// A class that converts a <see cref="Expiration.Target"/> >string to either a ID (long) or a Topic (string). Either the ID or Topic
    /// will be non-null while the other will be null.
    /// The format for the <see cref="Expiration.Target"/> can be either
    /// * id:123
    /// * topic:my_topic_string
    /// </summary>
    public class ExpirerTarget
    {
        /// <summary>
        /// The resulting ID from the given <see cref="Expiration.Target"/>. If the <see cref="Expiration.Target"/> did not include an Id, then
        /// this field will be null
        /// </summary>
        [JsonProperty("id")]
        public long? Id;
        
        /// <summary>
        /// The resulting Topic from the given <see cref="Expiration.Target"/>. If the <see cref="Expiration.Target"/> did not include a Topic, then
        /// this field will be null
        /// </summary>
        [JsonProperty("topic")]
        public string Topic;

        /// <summary>
        /// Create a new instance of this class with a given <see cref="Expiration.Target"/>. The given <see cref="Expiration.Target"/> will
        /// be converted and stored to either the ID field or Topic field
        /// </summary>
        /// <param name="target">The <see cref="Expiration.Target"/> to convert</param>
        /// <exception cref="FormatException">If the format for the given <see cref="Expiration.Target"/> is invalid</exception>
        public ExpirerTarget(string target)
        {
            var values = target.Split(':');
            if (values.Length != 2)
            {
                throw new FormatException($"Invalid target format: {target}. Expected format: 'type:value'.");
            }

            var (type, value) = (values[0], values[1]);

            switch (type)
            {
                case "topic":
                    Topic = value;
                    break;
                case "id" when long.TryParse(value, out var id):
                    Id = id;
                    break;
                case "id":
                    throw new FormatException($"Cannot parse id {value} as a long.");
                default:
                    throw new FormatException($"Invalid target type: {type}. Expected 'id' or 'topic'.");
            }
        }
    }
}
