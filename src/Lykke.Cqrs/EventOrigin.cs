using System;

namespace Lykke.Cqrs
{
    internal class EventOrigin
    {
        public Type EventType { get; private set; }
        public string BoundedContext { get; private set; }

        public EventOrigin(string boundedContext, Type eventType)
        {
            BoundedContext = boundedContext;
            EventType = eventType;
        }

        protected bool Equals(EventOrigin other)
        {
            return Equals(EventType, other.EventType) && string.Equals(BoundedContext, other.BoundedContext);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EventOrigin) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((EventType != null ? EventType.GetHashCode() : 0)*397) ^ (BoundedContext != null ? BoundedContext.GetHashCode() : 0);
            }
        }

        public static bool operator ==(EventOrigin left, EventOrigin right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EventOrigin left, EventOrigin right)
        {
            return !Equals(left, right);
        }
    }
}