using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ligofff.RuntimeExceptionsHandler
{
    public class ErrorNotification : MonoBehaviour
    {
        [SerializeField]
        private float disappearTime = 5f;
        
        [SerializeField]
        private TextMeshProUGUI errorTitle;
        
        [SerializeField]
        private Button customButton;

        private Coroutine _disappearCoroutine;

        public virtual void Setup(string title, string condition, string stacktrace, Action customAction)
        {
            errorTitle.text = title;

            if (customAction is { })
            {
                customButton.onClick.AddListener(customAction.Invoke);
            }
            else customButton.gameObject.SetActive(false);

            _disappearCoroutine = StartCoroutine(Disappear(disappearTime));
        }

        private IEnumerator Disappear(float time)
        {
            yield return new WaitForSeconds(time);
            Close();
        }
        
        public virtual void Close()
        {
            if (_disappearCoroutine != null)
            {
                StopCoroutine(_disappearCoroutine);
                _disappearCoroutine = null;
            }
            
            Destroy(gameObject);
        }
    }
}