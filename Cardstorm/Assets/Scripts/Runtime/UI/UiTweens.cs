using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Cardstorm.UI
{
    // Efectos cortos de la interfaz.
    public static class UiTweens
    {
        // Tambaleo: gira a un lado y a otro y vuelve a su sitio.
        public static void Wobble(Transform target, ref Tween tween, float degrees = 7f, float duration = 0.4f)
        {
            tween?.Kill();
            target.localRotation = Quaternion.identity;
            tween = target.DOPunchRotation(new Vector3(0f, 0f, degrees), duration, 8, 0.6f)
                .SetUpdate(true)
                .SetLink(target.gameObject);
        }

        // «Pop»: crece un poco y vuelve a su tamaño.
        public static void Pop(Transform target, ref Tween tween, float amount = 0.12f, float duration = 0.2f)
        {
            tween?.Kill();
            target.localScale = Vector3.one;
            tween = target.DOPunchScale(new Vector3(amount, amount, 0f), duration, 6, 0.5f)
                .SetUpdate(true)
                .SetLink(target.gameObject);
        }

        // El texto pasa a flash y vuelve a su color.
        public static void Flash(TMP_Text text, Color restColor, Color flash, ref Tween tween, float duration = 0.3f)
        {
            tween?.Kill();
            text.color = flash;
            tween = DOTween.To(() => text.color, c => text.color = c, restColor, duration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetLink(text.gameObject);
        }

        // Fundido del alfa de un CanvasGroup. Con duración 0 cambia en el acto.
        public static void Fade(CanvasGroup group, bool visible, float duration, ref Tween tween)
        {
            tween?.Kill();
            float target = visible ? 1f : 0f;
            if (duration <= 0f)
            {
                group.alpha = target;
                return;
            }
            tween = DOTween.To(() => group.alpha, a => group.alpha = a, target, duration)
                .SetEase(visible ? Ease.OutQuad : Ease.InQuad)
                .SetUpdate(true)
                .SetLink(group.gameObject);
        }

        // Cuenta de un número a otro en el texto. format lleva un {0}, como SetText.
        public static void Count(TMP_Text text, string format, int from, int to, ref Tween tween, float duration = 0.2f)
        {
            tween?.Kill();
            if (from == to || duration <= 0f)
            {
                text.SetText(format, to);
                return;
            }
            int shown = from;
            tween = DOTween.To(() => shown, v => { shown = v; text.SetText(format, v); }, to, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(text.gameObject);
        }
    }
}
