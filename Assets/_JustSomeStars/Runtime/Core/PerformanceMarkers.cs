using System;
using System.Threading;
using Unity.Profiling;

namespace JustSomeStars.Runtime.Core
{
    public sealed class RuntimePerformanceMarker
    {
        private readonly ProfilerMarker m_Marker;
        private long m_Samples;

        internal RuntimePerformanceMarker(string name)
        {
            m_Marker = new ProfilerMarker(name);
        }

        public long Samples => Interlocked.Read(ref m_Samples);

        public Scope Auto()
        {
            Interlocked.Increment(ref m_Samples);
            m_Marker.Begin();
            return new Scope(m_Marker);
        }

        internal void Reset()
        {
            Interlocked.Exchange(ref m_Samples, 0L);
        }

        public readonly struct Scope : IDisposable
        {
            private readonly ProfilerMarker m_Marker;

            internal Scope(ProfilerMarker marker)
            {
                m_Marker = marker;
            }

            public void Dispose()
            {
                m_Marker.End();
            }
        }
    }

    public static class PerformanceMarkers
    {
        public static readonly RuntimePerformanceMarker Player =
            new RuntimePerformanceMarker("JSS.Player");
        public static readonly RuntimePerformanceMarker Crew =
            new RuntimePerformanceMarker("JSS.Crew");
        public static readonly RuntimePerformanceMarker Flight =
            new RuntimePerformanceMarker("JSS.Flight");
        public static readonly RuntimePerformanceMarker Lens =
            new RuntimePerformanceMarker("JSS.Lens");
        public static readonly RuntimePerformanceMarker UI =
            new RuntimePerformanceMarker("JSS.UI");
        public static readonly RuntimePerformanceMarker Streaming =
            new RuntimePerformanceMarker("JSS.Streaming");

        public static long PlayerSamples => Player.Samples;
        public static long CrewSamples => Crew.Samples;
        public static long FlightSamples => Flight.Samples;
        public static long LensSamples => Lens.Samples;
        public static long UISamples => UI.Samples;
        public static long StreamingSamples => Streaming.Samples;

        public static void ResetForTests()
        {
            Player.Reset();
            Crew.Reset();
            Flight.Reset();
            Lens.Reset();
            UI.Reset();
            Streaming.Reset();
        }
    }
}
