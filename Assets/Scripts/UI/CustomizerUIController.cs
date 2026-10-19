using UnityEngine;
using UnityEngine.UI;
using CapsuleCharacter;

namespace CapsuleCharacter.Runtime
{
    public class CustomizerUIController : MonoBehaviour
    {
        [SerializeField] private MascotCustomizer customizer;

        [Header("Body / Face / Hat Buttons")]
        [SerializeField] private Button btnNextBody;
        [SerializeField] private Button btnPrevBody;
        [SerializeField] private Button btnNextFace;
        [SerializeField] private Button btnPrevFace;
        [SerializeField] private Button btnNextHat;
        [SerializeField] private Button btnPrevHat;
        [SerializeField] private Button btnRandomize;
        [SerializeField] private Button btnReset;

        [Header("Size Buttons")]
        [SerializeField] private Button btnNextSize;
        [SerializeField] private Button btnPrevSize;

        [Header("Animation Buttons")]
        [SerializeField] private Button btnNextAnimation;
        [SerializeField] private Button btnPrevAnimation;

        private void Start()
        {
            if (customizer == null)
            {
                Debug.LogError("CustomizerUIController: Please assign a MascotCustomizer reference in the Inspector.", this);
                return;
            }

            btnNextBody?.onClick.AddListener(customizer.NextBody);
            btnPrevBody?.onClick.AddListener(customizer.PrevBody);
            btnNextFace?.onClick.AddListener(customizer.NextFace);
            btnPrevFace?.onClick.AddListener(customizer.PrevFace);
            btnNextHat?.onClick.AddListener(customizer.NextHat);
            btnPrevHat?.onClick.AddListener(customizer.PrevHat);
            btnRandomize?.onClick.AddListener(customizer.Randomize);
            btnReset?.onClick.AddListener(customizer.ResetCustomization);

            btnNextSize?.onClick.AddListener(customizer.NextSize);
            btnPrevSize?.onClick.AddListener(customizer.PrevSize);
            btnNextAnimation?.onClick.AddListener(customizer.NextAnimation);
            btnPrevAnimation?.onClick.AddListener(customizer.PrevAnimation);
        }

        private void OnDestroy()
        {
            if (customizer == null)
                return;

            btnNextBody?.onClick.RemoveListener(customizer.NextBody);
            btnPrevBody?.onClick.RemoveListener(customizer.PrevBody);
            btnNextFace?.onClick.RemoveListener(customizer.NextFace);
            btnPrevFace?.onClick.RemoveListener(customizer.PrevFace);
            btnNextHat?.onClick.RemoveListener(customizer.NextHat);
            btnPrevHat?.onClick.RemoveListener(customizer.PrevHat);
            btnRandomize?.onClick.RemoveListener(customizer.Randomize);
            btnReset?.onClick.RemoveListener(customizer.ResetCustomization);

            btnNextSize?.onClick.RemoveListener(customizer.NextSize);
            btnPrevSize?.onClick.RemoveListener(customizer.PrevSize);
            btnNextAnimation?.onClick.RemoveListener(customizer.NextAnimation);
            btnPrevAnimation?.onClick.RemoveListener(customizer.PrevAnimation);
        }
    }
}
