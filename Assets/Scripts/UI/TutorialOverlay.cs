using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class TutorialOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button closeButton;

        private GameManager gameManager;

        public bool IsVisible => root != null && root.activeInHierarchy;

        private void Awake()
        {
            EnsureRoot();
            MakeOverlayClickThrough();
        }

        public void Initialize(GameManager owner)
        {
            gameManager = owner;
            EnsureRoot();

            if (titleText != null)
            {
                titleText.text = "CUM JOCI";
            }

            if (bodyText != null)
            {
                bodyText.text = "Trage piesele luminoase pe tabla.\nPiesele stinse nu incap momentan.\nFa randuri sau coloane complete.\nCand vezi POP!, apasa culoarea ca sa o detonezi.";
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => gameManager.CloseTutorial());
            }
        }

        public void Show()
        {
            EnsureRoot();
            if (root != null)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
            }
        }

        public void Hide()
        {
            EnsureRoot();
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void EnsureRoot()
        {
            if (root == null)
            {
                root = gameObject;
            }
        }

        private void MakeOverlayClickThrough()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (closeButton != null && graphics[i].transform.IsChildOf(closeButton.transform))
                {
                    continue;
                }

                graphics[i].raycastTarget = false;
            }
        }
    }
}
