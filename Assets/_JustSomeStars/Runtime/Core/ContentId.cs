using System;

namespace JustSomeStars.Runtime.Core
{
    public readonly struct ContentId : IEquatable<ContentId>
    {
        private readonly string m_Value;

        public ContentId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A content ID must be non-empty and already trimmed.",
                    nameof(value));
            }

            m_Value = value;
        }

        public string Value => m_Value;

        public bool IsValid => !string.IsNullOrEmpty(m_Value);

        public bool Equals(ContentId other)
        {
            return string.Equals(m_Value, other.m_Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ContentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(m_Value ?? string.Empty);
        }

        public override string ToString()
        {
            return m_Value ?? string.Empty;
        }

        public static bool operator ==(ContentId left, ContentId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContentId left, ContentId right)
        {
            return !left.Equals(right);
        }
    }
}
