using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lykke.Cqrs.Routing
{
    public class RoutingKey
    {
        private readonly Dictionary<string, string> m_Hints = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public Type MessageType { get; set; }
        public CommunicationType CommunicationType { get; set; }
        public RouteType RouteType { get; set; }
        public uint Priority { get; set; }
        public string LocalContext { get; set; }
        public string RemoteBoundedContext { get; set; }
        public bool Exclusive { get; set; }

        protected bool Equals(RoutingKey other)
        {
            return MessageType == other.MessageType &&
                   CommunicationType == other.CommunicationType &&
                   RouteType == other.RouteType &&
                   Priority == other.Priority &&
                   Exclusive == other.Exclusive &&
                   string.Equals(RemoteBoundedContext, other.RemoteBoundedContext) &&
                   string.Equals(LocalContext, other.LocalContext) &&
                   m_Hints.Keys.Count == other.m_Hints.Keys.Count &&
                   m_Hints.Keys.All(k => other.m_Hints.ContainsKey(k) && Equals(m_Hints[k], other.m_Hints[k]));
        }

        public string this[string key]
        {
            get
            {
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "priority") == 0)
                    return Priority.ToString(CultureInfo.InvariantCulture);
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "MessageType") == 0)
                    return MessageType.ToString();
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "CommunicationType") == 0)
                    return CommunicationType.ToString();
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "RouteType") == 0)
                    return RouteType.ToString();
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "RemoteBoundContext") == 0)
                    return RemoteBoundedContext;
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "LocalBoundedContext") == 0)
                    return LocalContext;
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "Exclusive") == 0)
                    return Exclusive.ToString();
                string value;
                m_Hints.TryGetValue(key, out value);
                return value;
            }
            set
            {
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "priority") == 0 ||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "MessageType") == 0||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "CommunicationType") == 0 ||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "RouteType") == 0 ||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "RemoteBoundContext") == 0||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "LocalBoundedContext") == 0||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "Exclusive") == 0)
                    throw new ArgumentException(key + " should be set with corresponding RoutingKey property", "key");
                m_Hints[key] = value;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RoutingKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (MessageType != null ? MessageType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ CommunicationType.GetHashCode();
                hashCode = (hashCode * 397) ^ RouteType.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) Priority;
                hashCode = (hashCode * 397) ^ Exclusive.GetHashCode();
                hashCode = (hashCode * 397) ^ (LocalContext != null ? LocalContext.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RemoteBoundedContext != null ? RemoteBoundedContext.GetHashCode() : 0);
                hashCode = m_Hints.Keys.OrderBy(k => k).Aggregate(hashCode, (h, key) => (h * 397) ^ key.GetHashCode());
                hashCode = m_Hints.Values.OrderBy(v => v).Aggregate(hashCode, (h, value) => (h * 397) ^ (value!=null?value.GetHashCode():0));
                return hashCode;
            }
        }

        public static bool operator ==(RoutingKey left, RoutingKey right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(RoutingKey left, RoutingKey right)
        {
            return !Equals(left, right);
        }
    }
}