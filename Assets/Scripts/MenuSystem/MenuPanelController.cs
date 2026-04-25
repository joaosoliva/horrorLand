using UnityEngine;

namespace HorrorLand.MenuSystem
{
    public class MenuPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;

        public void SetVisible(bool isVisible)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(isVisible);
            }
        }

        public void Toggle()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(!panelRoot.activeSelf);
            }
        }
    }
}
