using Cardstorm.Localization;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Primera pantalla de cada subida de nivel.
    public class LevelUpChoiceScreen : ModalScreen
    {
        [SerializeField] TMP_Text titleText;
        [Tooltip("«1 de 3» cuando hay varias subidas seguidas; oculto si solo hay una.")]
        [SerializeField] TMP_Text queueText;
        [SerializeField] TMP_Text goldText;
        [SerializeField] Button shopButton;
        [SerializeField] Button gemsButton;
        [Tooltip("Aviso de que después se podrá eliminar una carta; solo en los niveles que lo ofrecen.")]
        [SerializeField] TMP_Text removalHint;

        public event Action ShopChosen;
        public event Action GemsChosen;

        protected override void Awake()
        {
            base.Awake();
            shopButton.onClick.AddListener(OnShopClicked);
            gemsButton.onClick.AddListener(OnGemsClicked);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            shopButton.onClick.RemoveListener(OnShopClicked);
            gemsButton.onClick.RemoveListener(OnGemsClicked);
        }

        // Sin gemas en el catálogo, Mejorar carta no se puede pulsar.
        public void Show(int level, int position, int total, int gold, bool gemsAvailable, bool offersRemoval, bool lockInput)
        {
            if (titleText != null) titleText.SetText(L10n.Get(L10nKeys.Level), level);
            UpdateQueue(position, total);
            if (goldText != null) goldText.SetText(L10n.Get(L10nKeys.Gold), gold);
            gemsButton.interactable = gemsAvailable;
            if (removalHint != null) removalHint.gameObject.SetActive(offersRemoval);
            Open(lockInput);
        }

        public void UpdateQueue(int position, int total)
        {
            if (queueText == null) return;
            queueText.gameObject.SetActive(total > 1);
            queueText.SetText(L10n.Get(L10nKeys.Queue), position, total);
        }

        protected override Selectable DefaultSelection() => shopButton;

        void OnShopClicked() { if (AcceptsInput) ShopChosen?.Invoke(); }

        void OnGemsClicked() { if (AcceptsInput) GemsChosen?.Invoke(); }
    }
}
