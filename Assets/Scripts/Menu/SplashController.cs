using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Fades the logo in, holds, fades out, then loads PressToStartScene.
    /// Any button press after fade-in skips the hold.
    /// </summary>
    public class SplashController : MonoBehaviour
    {
        [SerializeField] CanvasGroup logoGroup;
        [SerializeField] float       fadeInDuration  = 0.8f;
        [SerializeField] float       holdDuration    = 1.5f;
        [SerializeField] float       fadeOutDuration = 0.6f;
        [SerializeField] string      nextScene       = "2_PressToStartScene";

        bool _skipRequested;

        void OnEnable()  => InputSystem.onAnyButtonPress.CallOnce(_ => _skipRequested = true);
        void OnDisable() { }

        IEnumerator Start()
        {
            logoGroup.alpha = 0f;
            yield return Fade(0f, 1f, fadeInDuration);

            _skipRequested = false; // reset — only skip during hold
            float elapsed = 0f;
            while (elapsed < holdDuration && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(1f, 0f, fadeOutDuration);
            SceneManager.LoadScene(nextScene);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed         += Time.unscaledDeltaTime;
                logoGroup.alpha  = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            logoGroup.alpha = to;
        }
    }
}
