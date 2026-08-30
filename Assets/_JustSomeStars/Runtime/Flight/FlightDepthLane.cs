using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    public sealed class FlightDepthLane : MonoBehaviour
    {
        [SerializeField] private int laneIndex;
        [SerializeField] private float presentationScale = 1f;
        [SerializeField] private int sortingOrder;
        [SerializeField] private int[] declaredDestinations = System.Array.Empty<int>();

        public int LaneIndex => laneIndex;
        public float PresentationScale => presentationScale;
        public int SortingOrder => sortingOrder;
        public int[] DeclaredDestinations => declaredDestinations;
    }
}
