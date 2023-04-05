using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ligofff.RuntimeExceptionsHandler
{
    public class ExceptionWindow : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI windowTitle;
        
        [SerializeField]
        private TextMeshProUGUI exception;
        
        [SerializeField]
        private Button buttonPrefab;

        [SerializeField]
        private Transform buttonsParent;

        public virtual void Setup(string title, string mainText, params (string, Action)[] additionalButtons)
        {
            Clear();
            
            windowTitle.text = title;
            exception.text = mainText;

            foreach (var tuple in additionalButtons)
            {
                var button = Instantiate(buttonPrefab, buttonsParent);
                button.onClick.AddListener(() => tuple.Item2?.Invoke());
                if (button.gameObject.GetComponentInChildren<TextMeshProUGUI>() is {} buttonTitle)
                {
                    buttonTitle.text = tuple.Item1;
                }
            }
        }

        private void Clear()
        {
            windowTitle.text = "";
            exception.text = "";

            foreach (Transform ch in buttonsParent)
            {
                Destroy(ch.gameObject);
            }
        }

        public virtual void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }
    }
}