using TMPro;
using UnityEngine;

namespace JustSomeStars.Runtime.Accessibility
{
    [DisallowMultipleComponent]
    public sealed class AccessibleStatusSymbol : MonoBehaviour
    {
        [SerializeField] private TMP_Text symbolLabel;
        [SerializeField] private string standardSymbol = "●";
        [SerializeField] private string alternateSymbol = "◆";

        public void Apply(ColorVisionMode mode)
        {
            if (symbolLabel != null)
            {
                symbolLabel.text = mode == ColorVisionMode.Standard
                    ? standardSymbol
                    : alternateSymbol;
            }
        }
    }
}
